using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    using SpiderSurge.Logging;
    public abstract class BaseAbility : MonoBehaviour
    {
        public abstract string PerkName { get; }
        protected PlayerInput playerInput;
        protected PlayerController playerController;
        protected SpiderHealthSystem spiderHealthSystem;
        protected InputInterceptor inputInterceptor;

        public abstract AbilityKind Kind { get; }

        // A proxy for a player another machine controls: it takes no input; the host runs its effects, clients draw them
        protected bool IsRemote => playerController != null && !playerController.IsOwner;
        protected bool IsAuthority => Net.IsAuthority;
        public int PlayerId => playerController != null ? playerController.playerID.Value : -1;

        protected bool isActive = false;
        protected bool onCooldown = false;
        protected bool skipNextCooldown = false;
        protected Coroutine durationCoroutine;
        protected Coroutine cooldownCoroutine;
        protected float cooldownEndTime = 0f;

        // Rate limit for ability not ready sound (once per second, per player)
        private static readonly Dictionary<int, float> lastAbilityNotReadySoundTimeByPlayer = new Dictionary<int, float>();
        private const float ABILITY_NOT_READY_SOUND_COOLDOWN = 1f;

        // Ability indicator settings
        [Header("Ability Indicator")]
        [SerializeField]
        protected bool showIndicator = true;

        protected AbilityIndicator abilityIndicator;

        protected bool isUltimateActive = false;

        private InputAction leftStickPressAction;
        private InputAction rightStickPressAction;
        private InputAction ultimateButtonAction;
        private InputAction dpadActivationAction;
        private readonly HashSet<InputControl> heldDpadControls = new HashSet<InputControl>();
        private bool? ultimateOnDpad;
        private static int dpadUltimates;
        private static int stickUltimates;
        private bool leftStickPressed = false;
        private bool rightStickPressed = false;
        private float lastLeftStickPressTime = 0f;
        private float lastRightStickPressTime = 0f;
        private const float COMBO_WINDOW = Consts.Values.Inputs.ComboWindow;

        // Override these in derived classes to enable Ultimate
        public virtual bool HasUltimate => false;
        public virtual string UltimatePerkName => $"{PerkName}Ultimate";
        public virtual string UltimatePerkDisplayName => Consts.Values.UI.UltimateDisplayName;
        public virtual string UltimatePerkDescription => Consts.Values.UI.UltimateDefaultDescription;

        // Cached buttons for unregistering when they change
        private string[] _cachedActivationButtons;

        public virtual string[] ActivationButtons
        {
            get
            {
                // Get the player-specific gamepad button, fallback to default L1
                string gamepadButton = Consts.Values.Inputs.GamepadLeftShoulder;
                if (playerInput != null)
                {
                    gamepadButton = PlayerControlSettings.GetPlayerAbilityButton(playerInput.playerIndex);
                }
                return new string[] { Consts.Values.Inputs.KeyboardQ, gamepadButton };
            }
        }

        // Ultimate activation: C key (dual stick combo handled separately)
        public virtual string UltimateActivationButton => Consts.Values.Inputs.KeyboardC;

        public virtual float AbilityBaseDuration => 5f;
        public virtual float AbilityBaseCooldown => 30f;
        public virtual float UltimateBaseDuration => 10f;
        public virtual float UltimateBaseCooldown => 60f;

        public virtual float AbilityDurationPerPerkLevel => 0f;
        public virtual float AbilityCooldownPerPerkLevel => 0f;
        public virtual float UltimateDurationPerPerkLevel => 0f;
        public virtual float UltimateCooldownPerPerkLevel => 0f;

        private float _cachedAbilityDuration;
        private float _cachedAbilityCooldownTime;
        private float _cachedUltimateDuration;
        private float _cachedUltimateCooldownTime;

        public virtual void UpdatePerkModifiers()
        {
            int durationLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0;
            int cooldownLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityCooldown) ?? 0;
            int shortTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.ShortTermInvestment) ?? 0;
            int longTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.LongTermInvestment) ?? 0;

            float duration = AbilityBaseDuration;
            if (durationLevel >= 1) duration += AbilityDurationPerPerkLevel;
            if (shortTermLevel > 0) duration += AbilityDurationPerPerkLevel;
            if (longTermLevel > 0) duration -= AbilityDurationPerPerkLevel;
            _cachedAbilityDuration = duration;

            float cooldown = AbilityBaseCooldown;
            if (cooldownLevel >= 1) cooldown -= AbilityCooldownPerPerkLevel;
            if (shortTermLevel > 0) cooldown -= AbilityCooldownPerPerkLevel;
            if (longTermLevel > 0) cooldown += AbilityCooldownPerPerkLevel;
            _cachedAbilityCooldownTime = cooldown;

            float uDuration = UltimateBaseDuration;
            if (durationLevel == 2) uDuration += UltimateDurationPerPerkLevel;
            if (shortTermLevel > 0) uDuration -= UltimateDurationPerPerkLevel;
            if (longTermLevel > 0) uDuration += UltimateDurationPerPerkLevel;
            _cachedUltimateDuration = uDuration;

            float uCooldown = UltimateBaseCooldown;
            if (cooldownLevel == 2) uCooldown -= UltimateCooldownPerPerkLevel;
            if (shortTermLevel > 0) uCooldown += UltimateCooldownPerPerkLevel;
            if (longTermLevel > 0) uCooldown -= UltimateCooldownPerPerkLevel;
            _cachedUltimateCooldownTime = uCooldown;
        }

        public virtual float AbilityDuration => _cachedAbilityDuration;

        public virtual float AbilityCooldownTime => _cachedAbilityCooldownTime;

        public virtual float UltimateDuration => _cachedUltimateDuration;

        public virtual float UltimateCooldownTime => _cachedUltimateCooldownTime;

        protected virtual void Awake()
        {
            playerInput = GetComponentInParent<PlayerInput>();
            UpdatePerkModifiers();
        }

        protected virtual void OnEnable()
        {
            UpdatePerkModifiers();
        }

        protected virtual void Start()
        {
            playerController = GetComponentInParent<PlayerController>();
            if (playerController == null)
            {
                Logger.LogError($"PlayerController not found for {GetType().Name} on player {playerInput?.playerIndex}");
            }

            // Start initialization coroutine to wait for dependencies
            StartCoroutine(WaitForInit());

            if (HasUltimate && playerInput != null)
            {
                SetupUltimateInputs();
            }
        }

        private IEnumerator WaitForInit()
        {
            while (spiderHealthSystem == null || (playerInput != null && (inputInterceptor == null || !inputInterceptor.IsReady)))
            {
                if (playerController != null && spiderHealthSystem == null)
                {
                    spiderHealthSystem = playerController.spiderHealthSystem;
                }

                if (inputInterceptor == null && playerInput != null)
                {
                    inputInterceptor = GetComponentInParent<InputInterceptor>();
                }

                yield return null;
            }

            // Perform delayed initialization
            if (abilityIndicator == null && showIndicator && IsUnlocked())
            {
                CreateAbilityIndicator();
            }

            if (inputInterceptor != null && ShouldRegister() && ActivationButtons != null)
            {
                RegisterWithInputInterceptor();
            }
            else if (inputInterceptor == null && playerInput != null)
            {
                Logger.LogWarning($"InputInterceptor not found for {GetType().Name} on player {playerInput?.playerIndex} after waiting.");
            }
        }

        public static bool? DpadUltimateBound => dpadUltimates + stickUltimates == 0 ? (bool?)null : dpadUltimates > 0;

        private static void CountUltimateInput(bool dpad, int change)
        {
            if (dpad)
            {
                dpadUltimates += change;
            }
            else
            {
                stickUltimates += change;
            }
        }

        // The gamepad actions are pass-through: a Button action bound to every pad ignores one pad's press while another pad holds the same button.
        private void SetupUltimateInputs()
        {
            try
            {
                bool useDpad = ModConfig.UltimateUseDpadActivation;

                if (useDpad)
                {
                    // D-pad activation: any D-pad direction activates the Ultimate
                    dpadActivationAction = new InputAction(
                        name: $"{GetType().Name}_DpadActivation",
                        type: InputActionType.PassThrough
                    );
                    dpadActivationAction.AddBinding(Consts.Values.Inputs.GamepadDpadUp);
                    dpadActivationAction.AddBinding(Consts.Values.Inputs.GamepadDpadDown);
                    dpadActivationAction.AddBinding(Consts.Values.Inputs.GamepadDpadLeft);
                    dpadActivationAction.AddBinding(Consts.Values.Inputs.GamepadDpadRight);
                    dpadActivationAction.performed += OnDpadChanged;
                    dpadActivationAction.canceled += OnDpadChanged;
                    dpadActivationAction.Enable();
                }
                else
                {
                    // Dual stick combo: left stick press
                    leftStickPressAction = new InputAction(
                        name: $"{GetType().Name}_LeftStickPress",
                        type: InputActionType.PassThrough,
                        binding: Consts.Values.Inputs.GamepadLeftStickPress
                    );
                    leftStickPressAction.performed += OnLeftStickChanged;
                    leftStickPressAction.canceled += OnLeftStickChanged;
                    leftStickPressAction.Enable();

                    // Dual stick combo: right stick press
                    rightStickPressAction = new InputAction(
                        name: $"{GetType().Name}_RightStickPress",
                        type: InputActionType.PassThrough,
                        binding: Consts.Values.Inputs.GamepadRightStickPress
                    );
                    rightStickPressAction.performed += OnRightStickChanged;
                    rightStickPressAction.canceled += OnRightStickChanged;
                    rightStickPressAction.Enable();
                }
                CountUltimateInput(useDpad, 1);
                ultimateOnDpad = useDpad;

                // Keyboard Ultimate button (always available)
                if (!string.IsNullOrEmpty(UltimateActivationButton))
                {
                    ultimateButtonAction = new InputAction(
                        name: $"{GetType().Name}_UltimateButton",
                        type: InputActionType.Button,
                        binding: UltimateActivationButton
                    );
                    ultimateButtonAction.performed += OnUltimateButtonPressed;
                    ultimateButtonAction.Enable();
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error setting up Ultimate inputs for {GetType().Name}: {ex.Message}");
            }
        }

        // Fires on the first D-pad direction pressed, like a button would.
        private void OnDpadChanged(InputAction.CallbackContext context)
        {
            if (context.control == null || !IsDeviceAssigned(context.control.device)) return;
            bool wasHeld = heldDpadControls.Count > 0;
            if (context.ReadValueAsButton())
            {
                heldDpadControls.Add(context.control);
            }
            else
            {
                heldDpadControls.Remove(context.control);
            }

            if (!wasHeld && heldDpadControls.Count > 0 && !PlayerControlSettings.IsMenuHeld(context.control.device))
            {
                ActivateUltimate();
            }
        }

        private void OnLeftStickChanged(InputAction.CallbackContext context)
        {
            if (context.control == null || !IsDeviceAssigned(context.control.device)) return;
            bool pressed = context.ReadValueAsButton();
            if (pressed == leftStickPressed) return;

            leftStickPressed = pressed;
            if (pressed)
            {
                lastLeftStickPressTime = Time.time;
                CheckStickCombo(context.control.device);
            }
        }

        private void OnRightStickChanged(InputAction.CallbackContext context)
        {
            if (context.control == null || !IsDeviceAssigned(context.control.device)) return;
            bool pressed = context.ReadValueAsButton();
            if (pressed == rightStickPressed) return;

            rightStickPressed = pressed;
            if (pressed)
            {
                lastRightStickPressTime = Time.time;
                CheckStickCombo(context.control.device);
            }
        }

        private void CheckStickCombo(InputDevice device)
        {
            if (leftStickPressed && rightStickPressed && !PlayerControlSettings.IsMenuHeld(device))
            {
                float timeDiff = Mathf.Abs(lastLeftStickPressTime - lastRightStickPressTime);
                if (timeDiff <= COMBO_WINDOW)
                {
                    ActivateUltimate();
                }
            }
        }

        private void OnUltimateButtonPressed(InputAction.CallbackContext context)
        {
            if (!IsDeviceAssigned(context.control.device)) return;
            ActivateUltimate();
        }

        private bool IsDeviceAssigned(InputDevice device)
        {
            if (playerInput == null) return false;
            foreach (var assignedDevice in playerInput.devices)
            {
                if (assignedDevice == device)
                {
                    return true;
                }
            }
            return false;
        }

        protected virtual void CreateAbilityIndicator()
        {
            if (!showIndicator || !IsUnlocked() || abilityIndicator != null || spiderHealthSystem == null)
            {
                return;
            }

            GameObject indicatorObj = new GameObject($"{GetType().Name}_Indicator");
            abilityIndicator = indicatorObj.AddComponent<AbilityIndicator>();
            abilityIndicator.Initialize(this, spiderHealthSystem.transform);
        }

        // The game turns player input off while paused, on the perk choice and while a level loads or ends
        public static bool GameAcceptsInput()
        {
            if (GameController.instance != null && !GameController.instance.acceptingInputs)
            {
                return false;
            }
            return HudController.instance == null || !HudController.instance.paused;
        }

        public virtual void Activate()
        {
            UpdatePerkModifiers();

            if (!IsUnlocked() || !GameAcceptsInput())
            {
                return;
            }

            if (!IsPlayerAlive())
            {
                return;
            }

            if (onCooldown)
            {
                PlayAbilityNotReadySound();
                return;
            }

            if (isActive)
            {
                return;
            }

            isActive = true;
            OnActivate();

            if (IsAuthority && SpiderSurgeStatsManager.Instance != null)
            {
                SpiderSurgeStatsManager.Instance.LogAbilityActivation(PlayerId);
            }
            SurgeNet.SendAbility(PlayerId, Kind, false);

            if (AbilityDuration > 0)
            {
                if (durationCoroutine != null)
                {
                    StopCoroutine(durationCoroutine);
                }
                durationCoroutine = StartCoroutine(AbilityDurationCoroutine());
            }
        }

        public virtual void Deactivate()
        {
            if (isActive)
            {
                isActive = false;
                if (isUltimateActive)
                {
                    isUltimateActive = false;
                    OnDeactivateUltimate();
                }
                OnDeactivate();
            }
        }

        public virtual void ActivateUltimate()
        {
            UpdatePerkModifiers();

            if (!IsUnlocked() || !GameAcceptsInput())
            {
                return;
            }

            if (!IsPlayerAlive())
            {
                return;
            }

            if (!IsUltimateUnlocked())
            {
                return;
            }

            if (onCooldown)
            {
                PlayAbilityNotReadySound();
                return;
            }

            if (isActive)
            {
                return;
            }

            isActive = true;
            isUltimateActive = true;
            OnActivateUltimate();

            if (IsAuthority && SpiderSurgeStatsManager.Instance != null)
            {
                SpiderSurgeStatsManager.Instance.LogUltimateActivation(PlayerId);
            }
            SurgeNet.SendAbility(PlayerId, Kind, true);

            if (UltimateDuration > 0)
            {
                if (durationCoroutine != null)
                {
                    StopCoroutine(durationCoroutine);
                }
                durationCoroutine = StartCoroutine(UltimateDurationCoroutine());
            }
        }

        // The host runs a client's request through the same checks its own press would pass
        public void ActivateFromNetwork(bool ultimate)
        {
            if (ultimate)
            {
                ActivateUltimate();
            }
            else
            {
                Activate();
            }
        }

        public virtual bool IsPlayerAlive()
        {
            if (spiderHealthSystem == null) return false;
            return !spiderHealthSystem.dead && !spiderHealthSystem.astralDead;
        }

        public virtual bool IsActive()
        {
            return isActive;
        }

        public virtual bool IsOnCooldown()
        {
            return onCooldown;
        }

        public void SetCooldownToZero()
        {
            if (isActive)
            {
                skipNextCooldown = true;
            }

            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
                cooldownCoroutine = null;
            }
            onCooldown = false;
            cooldownEndTime = 0f;
        }

        public void ReduceCooldown(float amount)
        {
            if (!onCooldown || amount <= 0f)
            {
                return;
            }

            float remaining = cooldownEndTime - Time.time;
            if (remaining <= 0f)
            {
                SetCooldownToZero();
                return;
            }

            remaining -= amount;
            if (remaining <= 0f)
            {
                SetCooldownToZero();
                return;
            }

            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
            }

            cooldownEndTime = Time.time + remaining;
            cooldownCoroutine = StartCoroutine(CooldownCoroutine(remaining));
        }

        public void ForceStartCooldown(bool wasUltimate = false)
        {
            UpdatePerkModifiers();
            skipNextCooldown = false;

            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
                cooldownCoroutine = null;
            }

            float cooldown = wasUltimate ? UltimateCooldownTime : AbilityCooldownTime;
            if (cooldown <= 0f)
            {
                onCooldown = false;
                cooldownEndTime = 0f;
                return;
            }

            onCooldown = true;
            cooldownEndTime = Time.time + cooldown;
            cooldownCoroutine = StartCoroutine(CooldownCoroutine(cooldown));
        }

        public bool IsUnlocked()
        {
            return PerksManager.Instance != null && PerksManager.Instance.GetPerkLevel(PerkName) > 0;
        }

        public bool IsUltimateUnlocked()
        {
            return HasUltimate && PerksManager.Instance != null && PerksManager.Instance.GetPerkLevel(UltimatePerkName) > 0;
        }

        protected virtual bool ShouldRegister()
        {
            return IsUnlocked();
        }

        public void RegisterWithInputInterceptor()
        {
            if (inputInterceptor != null && ShouldRegister() && ActivationButtons != null)
            {
                _cachedActivationButtons = ActivationButtons;
                Logger.LogDebug($"[{GetType().Name}] Registering buttons: {string.Join(", ", _cachedActivationButtons)}");
                foreach (string button in _cachedActivationButtons)
                {
                    if (!string.IsNullOrEmpty(button))
                    {
                        inputInterceptor.RegisterAbility(this, button);
                    }
                }
            }
            else
            {
                if (inputInterceptor == null && playerInput != null) Logger.LogWarning($"[{GetType().Name}] InputInterceptor is null!");
            }
        }

        // Refreshes input bindings when a player changes their control scheme.
        public void RefreshInputBindings()
        {
            Logger.LogDebug($"[{GetType().Name}] Refreshing input bindings...");
            if (inputInterceptor == null)
            {
                Logger.LogWarning($"[{this.GetType().Name}] RefreshInputBindings aborted: InputInterceptor is null");
                return;
            }

            // Unregister old bindings using cached buttons
            if (_cachedActivationButtons != null)
            {
                foreach (string button in _cachedActivationButtons)
                {
                    if (!string.IsNullOrEmpty(button))
                    {
                        inputInterceptor.UnregisterAbility(this, button);
                    }
                }
            }

            // Re-register with new bindings if ability is still unlocked
            if (ShouldRegister())
            {
                RegisterWithInputInterceptor();
            }
            else
            {
                Logger.LogInfo($"[{GetType().Name}] ShouldRegister returned false - skipping registration");
            }
        }

        protected abstract void OnActivate();
        protected virtual void OnDeactivate() { }
        protected virtual void OnActivateUltimate() { OnActivate(); }
        protected virtual void OnDeactivateUltimate() { }
        protected virtual bool ShouldPlayAbilityEndedSound(bool wasUltimate) { return true; }

        private void PlayAbilityNotReadySound()
        {
            if (IsRemote) return;
            int playerId = PlayerId;

            // Get last sound time for this player (default to 0 if not found)
            float lastSoundTime = 0f;
            if (lastAbilityNotReadySoundTimeByPlayer.TryGetValue(playerId, out float storedTime))
            {
                lastSoundTime = storedTime;
            }

            if (Time.time - lastSoundTime >= ABILITY_NOT_READY_SOUND_COOLDOWN)
            {
                lastAbilityNotReadySoundTimeByPlayer[playerId] = Time.time;
                SoundManager.Instance?.PlaySound(
                        Consts.SoundNames.AbilityNotReady,
                        Consts.SoundVolumes.AbilityNotReady * Consts.SoundVolumes.MasterVolume
                    );
            }
        }

        protected virtual void PlayAbilityEndedSound(bool wasUltimate)
        {
            SoundManager.Instance?.PlaySound(
                Consts.SoundNames.AbilityEnded,
                Consts.SoundVolumes.AbilityEnded * Consts.SoundVolumes.MasterVolume
            );
        }

        private IEnumerator AbilityDurationCoroutine()
        {
            bool wasUltimate = isUltimateActive;
            yield return new WaitForSeconds(AbilityDuration);

            if (isActive)
            {
                Deactivate();
                if (!IsRemote && ShouldPlayAbilityEndedSound(wasUltimate))
                {
                    PlayAbilityEndedSound(wasUltimate);
                }
            }

            StartCooldown(wasUltimate);
        }

        private IEnumerator UltimateDurationCoroutine()
        {
            bool wasUltimate = isUltimateActive;
            yield return new WaitForSeconds(UltimateDuration);

            if (isActive)
            {
                Deactivate();
                if (!IsRemote && ShouldPlayAbilityEndedSound(wasUltimate))
                {
                    PlayAbilityEndedSound(wasUltimate);
                }
            }

            StartCooldown(wasUltimate);
        }

        protected void StartCooldown(bool wasUltimate = false)
        {
            UpdatePerkModifiers();

            if (skipNextCooldown)
            {
                skipNextCooldown = false;
                onCooldown = false;
                return;
            }

            float cooldown = wasUltimate ? UltimateCooldownTime : AbilityCooldownTime;
            if (cooldown <= 0) return;

            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
            }
            cooldownEndTime = Time.time + cooldown;
            cooldownCoroutine = StartCoroutine(CooldownCoroutine(cooldown));
        }

        private IEnumerator CooldownCoroutine(float cooldownTime)
        {
            onCooldown = true;

            yield return new WaitForSeconds(cooldownTime);

            onCooldown = false;
            cooldownEndTime = 0f;

            if (SoundManager.Instance != null && IsUnlocked() && !IsRemote)
            {
                SoundManager.Instance.PlaySound(
                    Consts.SoundNames.AbilityReady,
                    Consts.SoundVolumes.AbilityReady * Consts.SoundVolumes.MasterVolume
                );
            }
        }

        protected virtual void OnDestroy()
        {
            if (ultimateOnDpad.HasValue)
            {
                CountUltimateInput(ultimateOnDpad.Value, -1);
                ultimateOnDpad = null;
            }

            if (inputInterceptor != null && _cachedActivationButtons != null)
            {
                foreach (string button in _cachedActivationButtons)
                {
                    if (!string.IsNullOrEmpty(button))
                    {
                        inputInterceptor.UnregisterAbility(this, button);
                    }
                }
            }

            // Clean up Ultimate input actions
            if (leftStickPressAction != null)
            {
                leftStickPressAction.performed -= OnLeftStickChanged;
                leftStickPressAction.canceled -= OnLeftStickChanged;
                leftStickPressAction.Disable();
                leftStickPressAction.Dispose();
            }
            if (rightStickPressAction != null)
            {
                rightStickPressAction.performed -= OnRightStickChanged;
                rightStickPressAction.canceled -= OnRightStickChanged;
                rightStickPressAction.Disable();
                rightStickPressAction.Dispose();
            }
            if (ultimateButtonAction != null)
            {
                ultimateButtonAction.performed -= OnUltimateButtonPressed;
                ultimateButtonAction.Disable();
                ultimateButtonAction.Dispose();
            }
            if (dpadActivationAction != null)
            {
                dpadActivationAction.performed -= OnDpadChanged;
                dpadActivationAction.canceled -= OnDpadChanged;
                dpadActivationAction.Disable();
                dpadActivationAction.Dispose();
            }

            if (durationCoroutine != null)
            {
                StopCoroutine(durationCoroutine);
            }
            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
            }

            // Clean up ability indicator
            if (abilityIndicator != null)
            {
                Destroy(abilityIndicator.gameObject);
                abilityIndicator = null;
            }
        }
    }
}
