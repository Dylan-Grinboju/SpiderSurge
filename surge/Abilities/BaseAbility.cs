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
        public int PlayerId => playerController != null ? playerController.playerID.Value : Players.IdOf(this);

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
        public float CooldownRemaining => onCooldown ? Mathf.Max(0f, cooldownEndTime - Time.time) : 0f;
        public float ActiveRemaining => isActive ? Mathf.Max(0f, activeEndTime - Time.time) : 0f;
        public float ActiveProgress => activeDuration > 0f ? Mathf.Clamp01(ActiveRemaining / activeDuration) : 1f;

        // Rate limit for ability not ready sound (once per second, per player)
        private static readonly Dictionary<int, float> lastAbilityNotReadySoundTimeByPlayer = new Dictionary<int, float>();
        private const float ABILITY_NOT_READY_SOUND_COOLDOWN = 1f;

        protected AbilityIndicator abilityIndicator;

        protected bool isUltimateActive = false;

        private InputAction comboFirstAction;
        private InputAction comboSecondAction;
        private InputAction ultimateButtonAction;
        private bool comboFirstPressed = false;
        private bool comboSecondPressed = false;
        private float lastComboFirstPressTime = 0f;
        private float lastComboSecondPressTime = 0f;
        private const float COMBO_WINDOW = Consts.Values.Inputs.ComboWindow;

        // Override these in derived classes to enable Ultimate
        public virtual bool HasUltimate => false;
        public virtual string UltimatePerkName => $"{PerkName}Ultimate";

        // Cached buttons for unregistering when they change
        private string[] _cachedActivationButtons;

        public string[] ActivationButtons
        {
            get
            {
                string gamepad = playerInput != null ? PlayerControlSettings.GetPlayerAbilityButton(playerInput.playerIndex) : Controls.AbilityController;
                return new[] { Controls.AbilityKeyboard, gamepad };
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

        public virtual float AbilityDuration => PerkScaled(AbilityBaseDuration, AbilityDurationPerPerkLevel, false);

        public float AbilityCooldownTime => PerkScaled(AbilityBaseCooldown, -AbilityCooldownPerPerkLevel, false, Consts.PerkNames.AbilityCooldown) * ChallengeController.CooldownMultiplier;

        public virtual float UltimateDuration => PerkScaled(UltimateBaseDuration, UltimateDurationPerPerkLevel, true);

        public float UltimateCooldownTime => PerkScaled(UltimateBaseCooldown, -UltimateCooldownPerPerkLevel, true, Consts.PerkNames.AbilityCooldown) * ChallengeController.CooldownMultiplier;

        // The level perk adds step to an ability from level 1 and to an ultimate at level 2; Short Term Investment adds it to abilities and takes it from ultimates, Long Term the reverse
        protected static float PerkScaled(float value, float step, bool ultimate, string levelPerk = Consts.PerkNames.AbilityDuration)
        {
            PerksManager perks = PerksManager.Instance;
            int level = perks?.GetPerkLevel(levelPerk) ?? 0;
            int shortTermLevel = perks?.GetPerkLevel(Consts.PerkNames.ShortTermInvestment) ?? 0;
            int longTermLevel = perks?.GetPerkLevel(Consts.PerkNames.LongTermInvestment) ?? 0;

            float investment = ultimate ? -step : step;
            if (ultimate ? level == 2 : level >= 1) value += step;
            if (shortTermLevel > 0) value += investment;
            if (longTermLevel > 0) value -= investment;
            return value;
        }

        protected virtual void Awake()
        {
            playerInput = GetComponentInParent<PlayerInput>();
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
            CreateAbilityIndicator();

            if (inputInterceptor != null && IsRegularUnlocked())
            {
                RegisterWithInputInterceptor();
            }
        }

        // The gamepad actions are pass-through: a Button action bound to every pad ignores one pad's press while another pad holds the same button.
        private void SetupUltimateInputs()
        {
            try
            {
                string[] combo = Controls.Combo(Controls.UltimateController);
                comboFirstAction = CreatePassThrough("ComboFirst", OnComboFirstChanged, combo[0]);
                comboSecondAction = CreatePassThrough("ComboSecond", OnComboSecondChanged, combo[1]);

                ultimateButtonAction = new InputAction(name: $"{GetType().Name}_UltimateButton", type: InputActionType.Button, binding: Controls.UltimateKeyboard);
                ultimateButtonAction.performed += OnUltimateButtonPressed;
                ultimateButtonAction.Enable();
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
            DisposeAction(ref comboFirstAction, OnComboFirstChanged);
            DisposeAction(ref comboSecondAction, OnComboSecondChanged);
            if (ultimateButtonAction != null)
            {
                ultimateButtonAction.performed -= OnUltimateButtonPressed;
                ultimateButtonAction.Disable();
                ultimateButtonAction.Dispose();
                ultimateButtonAction = null;
            }
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

        private void OnComboFirstChanged(InputAction.CallbackContext context) => OnComboChanged(context, ref comboFirstPressed, ref lastComboFirstPressTime);

        private void OnComboSecondChanged(InputAction.CallbackContext context) => OnComboChanged(context, ref comboSecondPressed, ref lastComboSecondPressTime);

        private void OnComboChanged(InputAction.CallbackContext context, ref bool comboPressed, ref float lastPressTime)
        {
            if (context.control == null || !IsDeviceAssigned(context.control.device)) return;
            bool pressed = context.ReadValueAsButton();
            if (pressed == comboPressed) return;

            comboPressed = pressed;
            if (pressed)
            {
                lastPressTime = Time.time;
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

        protected void CreateAbilityIndicator()
        {
            if (!IsRegularUnlocked() || abilityIndicator != null || spiderHealthSystem == null)
            {
                return;
            }

            GameObject indicatorObj = new GameObject($"{GetType().Name}_Indicator");
            abilityIndicator = indicatorObj.AddComponent<AbilityIndicator>();
            abilityIndicator.Initialize(this, spiderHealthSystem.transform);
        }

        public void Activate() => TryActivate(false);

        public void ActivateUltimate() => TryActivate(true);

        private void TryActivate(bool ultimate)
        {
            if (!(ultimate ? IsUltimateUnlocked() : IsRegularUnlocked()) || !AcceptsInput)
            {
                return;
            }

            if (!IsPlayerAlive())
            {
                return;
            }

            if (ultimate && !IsUltimateUnlocked())
            {
                return;
            }

            if (!ChallengeController.CanActivate(ultimate))
            {
                PlayAbilityNotReadySound();
                return;
            }

            if (StatusSource.onCooldown)
            {
                PlayAbilityNotReadySound();
                return;
            }

            if (StatusSource.isActive)
            {
                return;
            }

            isActive = true;
            if (ultimate) isUltimateActive = true;
            activeDuration = ultimate ? UltimateDuration : AbilityDuration;
            activeEndTime = Time.time + activeDuration;
            if (ultimate) OnActivateUltimate();
            else OnActivate();

            if (IsAuthority && SpiderSurgeStatsManager.Instance != null)
            {
                if (ultimate) SpiderSurgeStatsManager.Instance.LogUltimateActivation(PlayerId);
                else SpiderSurgeStatsManager.Instance.LogAbilityActivation(PlayerId);
            }
            SurgeNet.SendAbility(PlayerId, Kind, ultimate);

            if (activeDuration > 0)
            {
                if (durationCoroutine != null)
                {
                    StopCoroutine(durationCoroutine);
                }
                durationCoroutine = StartCoroutine(DurationCoroutine(activeDuration));
            }
        }

        public void Deactivate()
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

        private bool AcceptsInput => !FrozenSpider.IsFrozen(spiderHealthSystem)
            && (fromNetwork ? GameMenus.AcceptRemotePlayerInput : GameMenus.AcceptPlayerInput);

        // The host runs a client's request through the same checks its own press would pass, except for the host's own menus
        public void ActivateFromNetwork(bool ultimate)
        {
            fromNetwork = true;
            try
            {
                TryActivate(ultimate);
            }
            finally
            {
                fromNetwork = false;
            }
        }

        public bool IsPlayerAlive()
        {
            if (spiderHealthSystem == null) return false;
            return !spiderHealthSystem.dead && !spiderHealthSystem.astralDead;
        }

        public bool IsActive()
        {
            return isActive;
        }

        public bool IsOnCooldown()
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

        internal void RescaleChallengeCooldown(float factor)
        {
            if (!onCooldown) return;
            cooldownEndTime = Time.time + CooldownRemaining * factor;
            cooldownDuration *= factor;
        }

        public void ForceStartCooldown()
        {
            skipNextCooldown = false;

            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
                cooldownCoroutine = null;
            }

            float cooldown = AbilityCooldownTime;
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
            return IsRegularUnlocked() || IsUltimateUnlocked();
        }

        public bool IsUltimateUnlocked()
        {
            return HasUltimate && PerksManager.Instance != null && PerksManager.Instance.GetPerkLevel(UltimatePerkName, PlayerId) > 0;
        }

        public bool IsRegularUnlocked() => PerksManager.Instance != null && PerksManager.Instance.GetPerkLevel(PerkName, PlayerId) > 0;

        // The two actions retain one shared cooldown, even when different components implement them.
        public BaseAbility StatusSource
        {
            get
            {
                BaseAbility result = this;
                foreach (var action in GetComponents<BaseAbility>())
                {
                    if (!action.IsUnlocked()) continue;
                    if (action.isActive) return action;
                    if (action.onCooldown && (!result.onCooldown || action.cooldownEndTime > result.cooldownEndTime)) result = action;
                }
                return result;
            }
        }

        public bool HasAvailableAction => GetComponents<BaseAbility>().Any(action =>
            action.IsRegularUnlocked() && ChallengeController.CanActivate(false) || action.IsUltimateUnlocked() && ChallengeController.CanActivate(true));

        public string ActionRestriction => ChallengeController.Pending ? "Choose a challenge" :
            !ChallengeController.CanActivate(false) ? (HasAvailableAction ? "Ultimate only" : "Disabled by challenge") :
            !ChallengeController.CanActivate(true) ? "Regular ability only" : "";

        public void RegisterWithInputInterceptor()
        {
            CreateAbilityIndicator();
            if (!IsRegularUnlocked()) { UnregisterButtons(); return; }
            if (inputInterceptor != null && IsRegularUnlocked())
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
            else if (inputInterceptor == null && playerInput != null)
            {
                Logger.LogWarning($"[{GetType().Name}] InputInterceptor is null!");
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

            UnregisterButtons();

            // Re-register only the selected regular action.
            if (IsRegularUnlocked())
            {
                RegisterWithInputInterceptor();
            }
            else
            {
                Logger.LogInfo($"[{GetType().Name}] ShouldRegister returned false - skipping registration");
            }
        }

        // Unregister old bindings using cached buttons
        private void UnregisterButtons()
        {
            if (inputInterceptor == null || _cachedActivationButtons == null) return;
            foreach (string button in _cachedActivationButtons)
            {
                if (!string.IsNullOrEmpty(button))
                {
                    inputInterceptor.UnregisterAbility(this, button);
                }
            }
            _cachedActivationButtons = null;
        }

        protected abstract void OnActivate();
        protected virtual void OnDeactivate() { }
        protected virtual void OnActivateUltimate() { OnActivate(); }
        protected virtual void OnDeactivateUltimate() { }
        protected virtual bool ShouldPlayAbilityEndedSound(bool wasUltimate) { return true; }

        // Only the caster's own machine plays the sound
        protected void PlayCasterSound(string soundName, float volume)
        {
            if (!IsRemote) SoundManager.Play(soundName, volume);
        }

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
                SoundManager.Play(Consts.SoundNames.AbilityNotReady, Consts.SoundVolumes.AbilityNotReady);
            }
        }

        protected virtual void PlayAbilityEndedSound(bool wasUltimate)
        {
            SoundManager.Play(Consts.SoundNames.AbilityEnded, Consts.SoundVolumes.AbilityEnded);
        }

        private IEnumerator DurationCoroutine(float duration)
        {
            bool wasUltimate = isUltimateActive;
            yield return new WaitForSeconds(duration);

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
                if (ModConfig.IndicatorCompactHud) AbilityHud.Ready(this);
                else if (abilityIndicator != null)
                    ReadyVfx.Pulse(abilityIndicator.transform.position, abilityIndicator.availableColor);
            }

            if (IsUnlocked()) PlayCasterSound(Consts.SoundNames.AbilityReady, Consts.SoundVolumes.AbilityReady);
        }

        protected virtual void OnDestroy()
        {
            UnregisterButtons();

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
