using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public abstract class BaseAbility : MonoBehaviour
    {
        public abstract string PerkName { get; }
        protected PlayerInput playerInput;
        protected PlayerController playerController;
        protected SpiderHealthSystem spiderHealthSystem;
        protected InputInterceptor inputInterceptor;

        protected bool isActive = false;
        protected bool onCooldown = false;
        protected Coroutine durationCoroutine;
        protected Coroutine cooldownCoroutine;

        // Ability indicator settings
        [Header("Ability Indicator")]
        [SerializeField]
        protected bool showIndicator = true;

        protected AbilityIndicator abilityIndicator;

        // Upgrade activation settings
        protected bool isUpgradeActive = false;
        protected float lastUpgradeCooldownMultiplier = 1f;

        // Upgrade input tracking
        private InputAction leftStickPressAction;
        private InputAction rightStickPressAction;
        private InputAction upgradeButtonAction;
        private bool leftStickPressed = false;
        private bool rightStickPressed = false;
        private float lastLeftStickPressTime = 0f;
        private float lastRightStickPressTime = 0f;
        private const float COMBO_WINDOW = 0.15f;

        // Override these in derived classes to enable upgrades
        public virtual bool HasUpgrade => false;
        public virtual string UpgradePerkName => $"{PerkName}Upgrade";
        public virtual string UpgradePerkDisplayName => "Upgrade";
        public virtual string UpgradePerkDescription => "Enhanced version of the ability.";
        public virtual float UpgradeCooldownMultiplier => 3f;

        public virtual string[] ActivationButtons => new string[] { "<keyboard>/q", "<Gamepad>/leftshoulder" };
        
        // Upgrade activation: E key (dual stick combo handled separately)
        public virtual string UpgradeActivationButton => "<Keyboard>/e";

        // Base values that abilities should override
        public virtual float BaseDuration => 5f;
        public virtual float BaseCooldown => 30f;

        // How much each perk level affects the values
        public virtual float DurationPerPerkLevel => 0f;
        public virtual float CooldownPerPerkLevel => 0f;

        // Computed values based on perk levels
        public virtual float Duration => BaseDuration + (PerksManager.Instance?.GetPerkLevel("abilityDuration") ?? 0) * DurationPerPerkLevel;
        public virtual float CooldownTime => BaseCooldown - (PerksManager.Instance?.GetPerkLevel("abilityCooldown") ?? 0) * CooldownPerPerkLevel;

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

            // Find and register with InputInterceptor only if the ability should be registered
            inputInterceptor = GetComponentInParent<InputInterceptor>();
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
            else
            {
                Logger.LogWarning($"InputInterceptor not found for {GetType().Name} on player {playerInput?.playerIndex}");
            }

            // Set up upgrade activation inputs if this ability has upgrades
            if (HasUpgrade)
            {
                SetupUpgradeInputs();
            }

            // Create ability indicator if enabled and ability is unlocked
            CreateAbilityIndicator();
        }

        private void SetupUpgradeInputs()
        {
            try
            {
                // Dual stick combo: left stick press
                leftStickPressAction = new InputAction(
                    name: $"{GetType().Name}_LeftStickPress",
                    type: InputActionType.Button,
                    binding: "<Gamepad>/leftStickPress"
                );
                leftStickPressAction.performed += OnLeftStickPressed;
                leftStickPressAction.canceled += OnLeftStickReleased;
                leftStickPressAction.Enable();

                // Dual stick combo: right stick press
                rightStickPressAction = new InputAction(
                    name: $"{GetType().Name}_RightStickPress",
                    type: InputActionType.Button,
                    binding: "<Gamepad>/rightStickPress"
                );
                rightStickPressAction.performed += OnRightStickPressed;
                rightStickPressAction.canceled += OnRightStickReleased;
                rightStickPressAction.Enable();

                // Keyboard upgrade button
                if (!string.IsNullOrEmpty(UpgradeActivationButton))
                {
                    upgradeButtonAction = new InputAction(
                        name: $"{GetType().Name}_UpgradeButton",
                        type: InputActionType.Button,
                        binding: UpgradeActivationButton
                    );
                    upgradeButtonAction.performed += OnUpgradeButtonPressed;
                    upgradeButtonAction.Enable();
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error setting up upgrade inputs for {GetType().Name}: {ex.Message}");
            }
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
                    ActivateUpgrade();
                }
            }
        }

        private void OnUpgradeButtonPressed(InputAction.CallbackContext context)
        {
            if (!IsDeviceAssigned(context.control.device)) return;
            ActivateUpgrade();
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

        protected virtual void Update()
        {
            if (spiderHealthSystem == null && playerController != null)
            {
                spiderHealthSystem = playerController.spiderHealthSystem;

                // Try to create indicator now that we have spiderHealthSystem
                if (abilityIndicator == null && showIndicator && IsUnlocked())
                {
                    CreateAbilityIndicator();
                }
            }
        }

        protected virtual void CreateAbilityIndicator()
        {
            if (!showIndicator || !IsUnlocked() || abilityIndicator != null)
            {
                return;
            }

            // Need spiderHealthSystem for the target transform
            if (spiderHealthSystem == null)
            {
                // Will try again in Update when spiderHealthSystem is available
                return;
            }

            // Create a new GameObject for the indicator
            GameObject indicatorObj = new GameObject($"{GetType().Name}_Indicator");
            abilityIndicator = indicatorObj.AddComponent<AbilityIndicator>();

            // Initialize with this ability and the spider's transform
            // Radius and offset can be adjusted via Unity Inspector on the AbilityIndicator component
            abilityIndicator.Initialize(this, spiderHealthSystem.transform);

            // Apply configuration values from ModConfig
            try
            {
                abilityIndicator.SetRadius(ModConfig.IndicatorRadius);
                abilityIndicator.SetOffset(new Vector3(ModConfig.IndicatorOffsetX, ModConfig.IndicatorOffsetY, 0f));
                abilityIndicator.SetAvailableColor(ModConfig.IndicatorAvailableColor);
                abilityIndicator.SetCooldownColor(ModConfig.IndicatorCooldownColor);
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
                if (isUpgradeActive)
                {
                    isUpgradeActive = false;
                    OnDeactivateUpgrade();
                }
                OnDeactivate();
            }
        }

        public virtual void ActivateUpgrade()
        {
            if (!IsUnlocked())
            {
                return;
            }

            if (!IsUpgradeUnlocked())
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
            isUpgradeActive = true;
            lastUpgradeCooldownMultiplier = UpgradeCooldownMultiplier;
            OnActivateUpgrade();

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

        public bool IsUpgradeUnlocked()
        {
            return HasUpgrade && PerksManager.Instance != null && PerksManager.Instance.GetPerkLevel(UpgradePerkName) > 0;
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
        protected virtual void OnActivateUpgrade() { OnActivate(); }
        protected virtual void OnDeactivateUpgrade() { }

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

            float cooldown = CooldownTime * lastUpgradeCooldownMultiplier;
            lastUpgradeCooldownMultiplier = 1f; // Reset for next activation

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

            // Clean up upgrade input actions
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
            if (upgradeButtonAction != null)
            {
                upgradeButtonAction.performed -= OnUpgradeButtonPressed;
                upgradeButtonAction.Disable();
                upgradeButtonAction.Dispose();
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