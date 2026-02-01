using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
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

        protected bool isActive = false;
        protected bool onCooldown = false;
        protected bool skipNextCooldown = false;
        protected Coroutine durationCoroutine;
        protected Coroutine cooldownCoroutine;

        // Ability indicator settings
        [Header("Ability Indicator")]
        [SerializeField]
        protected bool showIndicator = true;

        protected AbilityIndicator abilityIndicator;

        // Resulting Ultimate activation settings
        protected bool isUltimateActive = false;
        protected float lastUltimateCooldownMultiplier = 1f;

        // Ultimate input tracking
        private InputAction leftStickPressAction;
        private InputAction rightStickPressAction;
        private InputAction ultimateButtonAction;
        private InputAction dpadActivationAction;
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
        public virtual float UltimateCooldownMultiplier => 3f;

        public virtual string[] ActivationButtons => new string[] { Consts.Values.Inputs.KeyboardQ, Consts.Values.Inputs.GamepadLeftShoulder };

        // Ultimate activation: F key (dual stick combo handled separately)
        public virtual string UltimateActivationButton => Consts.Values.Inputs.KeyboardF;

        // Base values that abilities should override
        public virtual float BaseDuration => 5f;
        public virtual float BaseCooldown => 30f;

        // How much each perk level affects the values
        public virtual float DurationPerPerkLevel => 0f;
        public virtual float CooldownPerPerkLevel => 0f;

        // Computed values based on perk levels
        public virtual float Duration => BaseDuration +
            ((PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0) * DurationPerPerkLevel) +
            ((PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.ShortTermInvestment) ?? 0) * 2 * DurationPerPerkLevel) -
            ((PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.LongTermInvestment) ?? 0) * 1 * DurationPerPerkLevel);
        public virtual float CooldownTime => BaseCooldown -
            ((PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityCooldown) ?? 0) * CooldownPerPerkLevel) +
            ((PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.ShortTermInvestment) ?? 0) * 1 * CooldownPerPerkLevel) -
            ((PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.LongTermInvestment) ?? 0) * 2 * CooldownPerPerkLevel);

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

            // Set up Ultimate activation inputs if this ability has Ultimate
            if (HasUltimate)
            {
                SetupUltimateInputs();
            }
        }

        private IEnumerator WaitForInit()
        {
            // Wait for components to be available
            while (spiderHealthSystem == null || inputInterceptor == null)
            {
                if (playerController != null && spiderHealthSystem == null)
                {
                    spiderHealthSystem = playerController.spiderHealthSystem;
                }

                if (inputInterceptor == null)
                {
                    inputInterceptor = GetComponentInParent<InputInterceptor>();
                }

                // If we have everything we need, we can stop waiting
                if (spiderHealthSystem != null && inputInterceptor != null)
                {
                    break;
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
            else if (inputInterceptor == null)
            {
                Logger.LogWarning($"InputInterceptor not found for {GetType().Name} on player {playerInput?.playerIndex} after waiting.");
            }
        }

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
                        type: InputActionType.Button
                    );
                    dpadActivationAction.AddBinding(Consts.Values.Inputs.GamepadDpadUp);
                    dpadActivationAction.AddBinding(Consts.Values.Inputs.GamepadDpadDown);
                    dpadActivationAction.AddBinding(Consts.Values.Inputs.GamepadDpadLeft);
                    dpadActivationAction.AddBinding(Consts.Values.Inputs.GamepadDpadRight);
                    dpadActivationAction.performed += OnDpadPressed;
                    dpadActivationAction.Enable();
                }
                else
                {
                    // Dual stick combo: left stick press
                    leftStickPressAction = new InputAction(
                        name: $"{GetType().Name}_LeftStickPress",
                        type: InputActionType.Button,
                        binding: Consts.Values.Inputs.GamepadLeftStickPress
                    );
                    leftStickPressAction.performed += OnLeftStickPressed;
                    leftStickPressAction.canceled += OnLeftStickReleased;
                    leftStickPressAction.Enable();

                    // Dual stick combo: right stick press
                    rightStickPressAction = new InputAction(
                        name: $"{GetType().Name}_RightStickPress",
                        type: InputActionType.Button,
                        binding: Consts.Values.Inputs.GamepadRightStickPress
                    );
                    rightStickPressAction.performed += OnRightStickPressed;
                    rightStickPressAction.canceled += OnRightStickReleased;
                    rightStickPressAction.Enable();
                }

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

        private void OnDpadPressed(InputAction.CallbackContext context)
        {
            if (!IsDeviceAssigned(context.control.device)) return;
            ActivateUltimate();
        }

        private void OnLeftStickPressed(InputAction.CallbackContext context)
        {
            if (!IsDeviceAssigned(context.control.device)) return;
            leftStickPressed = true;
            lastLeftStickPressTime = Time.time;
            CheckStickCombo();
        }

        private void OnLeftStickReleased(InputAction.CallbackContext context)
        {
            if (!IsDeviceAssigned(context.control.device)) return;
            leftStickPressed = false;
        }

        private void OnRightStickPressed(InputAction.CallbackContext context)
        {
            if (!IsDeviceAssigned(context.control.device)) return;
            rightStickPressed = true;
            lastRightStickPressTime = Time.time;
            CheckStickCombo();
        }

        private void OnRightStickReleased(InputAction.CallbackContext context)
        {
            if (!IsDeviceAssigned(context.control.device)) return;
            rightStickPressed = false;
        }

        private void CheckStickCombo()
        {
            if (leftStickPressed && rightStickPressed)
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

            try
            {
                abilityIndicator.SetRadius(ModConfig.IndicatorRadius);
                abilityIndicator.SetOffset(new Vector3(ModConfig.IndicatorOffsetX, ModConfig.IndicatorOffsetY, 0f));
                abilityIndicator.SetAvailableColor(ModConfig.IndicatorAvailableColor);
                abilityIndicator.SetCooldownColor(ModConfig.IndicatorCooldownColor);
                abilityIndicator.SetActiveColor(ModConfig.IndicatorActiveColor);
                abilityIndicator.SetShowOnlyWhenReady(ModConfig.IndicatorShowOnlyWhenReady);
            }
            catch (System.Exception)
            {
                // If ModConfig isn't available for some reason, silently continue with defaults
            }

        }

        public virtual void Activate()
        {
            if (!IsUnlocked())
            {
                return;
            }

            if (onCooldown)
            {
                return;
            }

            if (isActive)
            {
                return;
            }

            isActive = true;
            OnActivate();

            if (playerInput != null && SpiderSurgeStatsManager.Instance != null)
            {
                SpiderSurgeStatsManager.Instance.LogActivation(playerInput.playerIndex);
            }

            if (Duration > 0)
            {
                if (durationCoroutine != null)
                {
                    StopCoroutine(durationCoroutine);
                }
                durationCoroutine = StartCoroutine(DurationCoroutine());
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
            if (!IsUnlocked())
            {
                return;
            }

            if (!IsUltimateUnlocked())
            {
                return;
            }

            if (onCooldown)
            {
                return;
            }

            if (isActive)
            {
                return;
            }

            isActive = true;
            isUltimateActive = true;
            lastUltimateCooldownMultiplier = UltimateCooldownMultiplier;
            OnActivateUltimate();

            if (playerInput != null && SpiderSurgeStatsManager.Instance != null)
            {
                SpiderSurgeStatsManager.Instance.LogActivation(playerInput.playerIndex);
            }

            if (Duration > 0)
            {
                if (durationCoroutine != null)
                {
                    StopCoroutine(durationCoroutine);
                }
                durationCoroutine = StartCoroutine(DurationCoroutine());
            }
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
                foreach (string button in ActivationButtons)
                {
                    if (!string.IsNullOrEmpty(button))
                    {
                        inputInterceptor.RegisterAbility(this, button);
                    }
                }
            }
        }

        protected abstract void OnActivate();
        protected virtual void OnDeactivate() { }
        protected virtual void OnActivateUltimate() { OnActivate(); }
        protected virtual void OnDeactivateUltimate() { }

        private IEnumerator DurationCoroutine()
        {
            yield return new WaitForSeconds(Duration);

            if (isActive)
            {
                Deactivate();
            }

            StartCooldown();
        }

        protected void StartCooldown()
        {
            if (skipNextCooldown)
            {
                skipNextCooldown = false;
                onCooldown = false;
                return;
            }

            if (CooldownTime <= 0) return;

            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
            }
            cooldownCoroutine = StartCoroutine(CooldownCoroutine());
        }

        private IEnumerator CooldownCoroutine()
        {
            onCooldown = true;

            float cooldown = CooldownTime * lastUltimateCooldownMultiplier;
            lastUltimateCooldownMultiplier = 1f; // Reset for next activation

            yield return new WaitForSeconds(cooldown);

            onCooldown = false;
        }

        protected virtual void OnDestroy()
        {
            // Unregister from InputInterceptor
            if (inputInterceptor != null && ActivationButtons != null)
            {
                foreach (string button in ActivationButtons)
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
                leftStickPressAction.performed -= OnLeftStickPressed;
                leftStickPressAction.canceled -= OnLeftStickReleased;
                leftStickPressAction.Disable();
                leftStickPressAction.Dispose();
            }
            if (rightStickPressAction != null)
            {
                rightStickPressAction.performed -= OnRightStickPressed;
                rightStickPressAction.canceled -= OnRightStickReleased;
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
                dpadActivationAction.performed -= OnDpadPressed;
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