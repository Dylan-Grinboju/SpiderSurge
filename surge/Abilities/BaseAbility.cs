using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
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
        private bool fromNetwork;
        public int PlayerId => playerController != null ? playerController.playerID.Value : -1;

        protected bool isActive = false;
        protected bool onCooldown = false;
        protected bool skipNextCooldown = false;
        protected Coroutine durationCoroutine;
        protected Coroutine cooldownCoroutine;
        protected float cooldownEndTime = 0f;
        private float cooldownDuration;
        public float CooldownProgress => !onCooldown ? 1f : cooldownDuration <= 0f ? 0f : Mathf.Clamp01(1f - (cooldownEndTime - Time.time) / cooldownDuration);
        private float activeEndTime;
        private float activeDuration;
        private float abilityActiveFrom = -1f;
        private float abilityActiveTo = -1f;
        public float CooldownRemaining => onCooldown ? Mathf.Max(0f, cooldownEndTime - Time.time) : 0f;
        public float ActiveRemaining => isActive ? Mathf.Max(0f, activeEndTime - Time.time) : 0f;
        public float ActiveProgress => activeDuration > 0f ? Mathf.Clamp01(ActiveRemaining / activeDuration) : 1f;

        // Rate limit for ability not ready sound (once per second, per player)
        private static readonly Dictionary<int, float> lastAbilityNotReadySoundTimeByPlayer = new Dictionary<int, float>();
        private const float ABILITY_NOT_READY_SOUND_COOLDOWN = 1f;

        // Ability indicator settings
        [Header("Ability Indicator")]
        [SerializeField]
        protected bool showIndicator = true;

        protected AbilityIndicator abilityIndicator;

        protected bool isUltimateActive = false;

        private InputAction comboFirstAction;
        private InputAction comboSecondAction;
        private InputAction ultimateButtonAction;
        private InputAction ultimatePressAction;
        private readonly HashSet<InputControl> heldUltimateControls = new HashSet<InputControl>();
        private bool ultimateBound;
        private static int boundUltimates;
        private bool comboFirstPressed = false;
        private bool comboSecondPressed = false;
        private float lastComboFirstPressTime = 0f;
        private float lastComboSecondPressTime = 0f;
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
                string keyboard = Controls.Get(Controls.AbilityKeyboard);
                string gamepad = playerInput != null ? PlayerControlSettings.GetPlayerAbilityButton(playerInput.playerIndex) : PlayerControlSettings.DefaultGamepadButton;
                var buttons = new List<string>(2);
                if (keyboard != Controls.None) buttons.Add(keyboard);
                if (gamepad != Controls.None) buttons.Add(gamepad);
                return buttons.ToArray();
            }
        }

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

        // Every change to the ultimate controls rebinds them at once, so a bound ultimate always uses the current binding
        public static bool UltimateBound => boundUltimates > 0;

        // The gamepad actions are pass-through: a Button action bound to every pad ignores one pad's press while another pad holds the same button.
        private void SetupUltimateInputs()
        {
            try
            {
                string controller = Controls.Get(Controls.UltimateController);
                string[] combo = Controls.Combo(controller);
                if (combo != null)
                {
                    comboFirstAction = CreatePassThrough("ComboFirst", OnComboFirstChanged, combo[0]);
                    comboSecondAction = CreatePassThrough("ComboSecond", OnComboSecondChanged, combo[1]);
                }
                else if (controller != Controls.None)
                {
                    ultimatePressAction = CreatePassThrough("Press", OnUltimatePressChanged, Controls.PressPaths(controller).ToArray());
                }
                ultimateBound = true;
                boundUltimates++;

                string keyboard = Controls.Get(Controls.UltimateKeyboard);
                if (keyboard != Controls.None)
                {
                    ultimateButtonAction = new InputAction(name: $"{GetType().Name}_UltimateButton", type: InputActionType.Button);
                    foreach (string path in Controls.PressPaths(keyboard))
                    {
                        ultimateButtonAction.AddBinding(path);
                    }
                    ultimateButtonAction.performed += OnUltimateButtonPressed;
                    ultimateButtonAction.Enable();
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error setting up Ultimate inputs for {GetType().Name}: {ex.Message}");
            }
        }

        private InputAction CreatePassThrough(string name, System.Action<InputAction.CallbackContext> onChange, params string[] paths)
        {
            var action = new InputAction(name: $"{GetType().Name}_Ultimate{name}", type: InputActionType.PassThrough);
            foreach (string path in paths)
            {
                action.AddBinding(path);
            }
            action.performed += onChange;
            action.canceled += onChange;
            action.Enable();
            return action;
        }

        private void TearDownUltimateInputs()
        {
            if (ultimateBound)
            {
                boundUltimates--;
                ultimateBound = false;
            }
            DisposeAction(ref comboFirstAction, OnComboFirstChanged);
            DisposeAction(ref comboSecondAction, OnComboSecondChanged);
            DisposeAction(ref ultimatePressAction, OnUltimatePressChanged);
            if (ultimateButtonAction != null)
            {
                ultimateButtonAction.performed -= OnUltimateButtonPressed;
                ultimateButtonAction.Disable();
                ultimateButtonAction.Dispose();
                ultimateButtonAction = null;
            }
            heldUltimateControls.Clear();
            comboFirstPressed = false;
            comboSecondPressed = false;
        }

        private static void DisposeAction(ref InputAction action, System.Action<InputAction.CallbackContext> onChange)
        {
            if (action == null) return;
            action.performed -= onChange;
            action.canceled -= onChange;
            action.Disable();
            action.Dispose();
            action = null;
        }

        public void RefreshControls()
        {
            if (inputInterceptor != null && _cachedActivationButtons != null)
            {
                RefreshInputBindings();
            }
            if (HasUltimate && playerInput != null && ultimateBound)
            {
                TearDownUltimateInputs();
                SetupUltimateInputs();
            }
        }

        // Fires on the first bound control pressed, so the whole D-pad works like one button
        private void OnUltimatePressChanged(InputAction.CallbackContext context)
        {
            if (context.control == null || !IsDeviceAssigned(context.control.device)) return;
            bool wasHeld = heldUltimateControls.Count > 0;
            if (context.ReadValueAsButton())
            {
                heldUltimateControls.Add(context.control);
            }
            else
            {
                heldUltimateControls.Remove(context.control);
            }

            if (!wasHeld && heldUltimateControls.Count > 0 && !Controls.Ignored(context.control.device))
            {
                ActivateUltimate();
            }
        }

        private void OnComboFirstChanged(InputAction.CallbackContext context)
        {
            if (context.control == null || !IsDeviceAssigned(context.control.device)) return;
            bool pressed = context.ReadValueAsButton();
            if (pressed == comboFirstPressed) return;

            comboFirstPressed = pressed;
            if (pressed)
            {
                lastComboFirstPressTime = Time.time;
                CheckCombo(context.control.device);
            }
        }

        private void OnComboSecondChanged(InputAction.CallbackContext context)
        {
            if (context.control == null || !IsDeviceAssigned(context.control.device)) return;
            bool pressed = context.ReadValueAsButton();
            if (pressed == comboSecondPressed) return;

            comboSecondPressed = pressed;
            if (pressed)
            {
                lastComboSecondPressTime = Time.time;
                CheckCombo(context.control.device);
            }
        }

        private void CheckCombo(InputDevice device)
        {
            if (comboFirstPressed && comboSecondPressed && !Controls.Ignored(device))
            {
                float timeDiff = Mathf.Abs(lastComboFirstPressTime - lastComboSecondPressTime);
                if (timeDiff <= COMBO_WINDOW)
                {
                    ActivateUltimate();
                }
            }
        }

        private void OnUltimateButtonPressed(InputAction.CallbackContext context)
        {
            if (!IsDeviceAssigned(context.control.device) || Controls.Ignored(context.control.device)) return;
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

        public virtual void Activate()
        {
            UpdatePerkModifiers();

            if (!IsUnlocked() || !AcceptsInput)
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
            activeDuration = AbilityDuration;
            activeEndTime = Time.time + activeDuration;
            abilityActiveFrom = Time.time;
            abilityActiveTo = float.MaxValue;
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
                else
                {
                    abilityActiveTo = Time.time;
                }
                OnDeactivate();
            }
        }

        // The ability itself, not its ultimate, was running at that game time
        public bool AbilityActiveAt(float time) => time >= abilityActiveFrom && time <= abilityActiveTo;

        public virtual void ActivateUltimate()
        {
            UpdatePerkModifiers();

            if (!IsUnlocked() || !AcceptsInput)
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
            activeDuration = UltimateDuration;
            activeEndTime = Time.time + activeDuration;
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

        private bool AcceptsInput => fromNetwork ? GameMenus.AcceptRemotePlayerInput : GameMenus.AcceptPlayerInput;

        // The host runs a client's request through the same checks its own press would pass, except for the host's own menus
        public void ActivateFromNetwork(bool ultimate)
        {
            fromNetwork = true;
            try
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
            finally
            {
                fromNetwork = false;
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

            cooldownEndTime = Mathf.Max(Time.time, cooldownEndTime - amount);
        }

        internal void ApplyCooldownRemaining(float remaining)
        {
            if (isActive || float.IsNaN(remaining) || float.IsInfinity(remaining)) return;
            cooldownEndTime = Time.time + Mathf.Max(0f, remaining);
            if (!onCooldown && remaining > 0f)
            {
                cooldownDuration = remaining;
                cooldownCoroutine = StartCoroutine(CooldownCoroutine());
            }
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
            cooldownDuration = cooldown;
            cooldownEndTime = Time.time + cooldown;
            cooldownCoroutine = StartCoroutine(CooldownCoroutine());
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
            CreateAbilityIndicator();
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
            cooldownDuration = cooldown;
            cooldownEndTime = Time.time + cooldown;
            cooldownCoroutine = StartCoroutine(CooldownCoroutine());
        }

        private IEnumerator CooldownCoroutine()
        {
            onCooldown = true;

            while (Time.time < cooldownEndTime) yield return null;

            onCooldown = false;
            cooldownEndTime = 0f;
            cooldownCoroutine = null;

            if (IsUnlocked() && spiderHealthSystem != null && IsPlayerAlive())
            {
                ReadyVfx.Pulse(spiderHealthSystem.transform.position, abilityIndicator != null ? abilityIndicator.availableColor : Color.white);
            }

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

            TearDownUltimateInputs();

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
