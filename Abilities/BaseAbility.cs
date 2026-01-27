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

        public virtual string[] ActivationButtons => new string[] { "<Gamepad>/buttonSouth", "<keyboard>/q" };
        public virtual float Duration => 5f;
        public virtual float CooldownTime => 10f;

        protected virtual void Awake()
        {
            playerInput = GetComponentInParent<PlayerInput>();
            if (playerInput != null)
            {
                Logger.LogInfo($"{GetType().Name} initialized for player {playerInput.playerIndex}");
            }
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
                        Logger.LogInfo($"{GetType().Name} registered with InputInterceptor using button {button} for player {playerInput?.playerIndex}");
                    }
                }
            }
            else if (!ShouldRegister())
            {
                Logger.LogInfo($"{GetType().Name} not registered with InputInterceptor (perk not unlocked) for player {playerInput?.playerIndex}");
            }
            else if (ActivationButtons == null || ActivationButtons.Length == 0)
            {
                Logger.LogInfo($"{GetType().Name} has no activation buttons defined - manual activation only");
            }
            else
            {
                Logger.LogWarning($"InputInterceptor not found for {GetType().Name} on player {playerInput?.playerIndex}");
            }
        }

        protected virtual void Update()
        {
            if (spiderHealthSystem == null && playerController != null)
            {
                spiderHealthSystem = playerController.spiderHealthSystem;
            }
        }

        public virtual void Activate()
        {
            if (!IsUnlocked())
            {
                Logger.LogInfo($"{GetType().Name} ability is not unlocked for player {playerInput.playerIndex}!");
                return;
            }

            if (onCooldown)
            {
                Logger.LogInfo($"{GetType().Name} ability is on cooldown for player {playerInput.playerIndex}!");
                return;
            }

            if (isActive)
            {
                Logger.LogInfo($"{GetType().Name} ability is already active for player {playerInput.playerIndex}!");
                return;
            }

            isActive = true;
            OnActivate();
            Logger.LogInfo($"{GetType().Name} ACTIVATED for player {playerInput.playerIndex}!");

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
                OnDeactivate();
                Logger.LogInfo($"{GetType().Name} DEACTIVATED for player {playerInput.playerIndex}!, cooldown: {CooldownTime}s");
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
            Logger.LogInfo($"{GetType().Name} cooldown reset for player {playerInput.playerIndex}");
        }

        public bool IsUnlocked()
        {
            return PerksManager.Instance != null && PerksManager.Instance.GetPerkLevel(PerkName) > 0;
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
                        Logger.LogInfo($"{GetType().Name} registered with InputInterceptor using button {button} for player {playerInput?.playerIndex}");
                    }
                }
            }
        }

        protected abstract void OnActivate();
        protected virtual void OnDeactivate() { }

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

            yield return new WaitForSeconds(CooldownTime);

            onCooldown = false;
            Logger.LogInfo($"{GetType().Name} cooldown finished for player {playerInput.playerIndex}");
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

            if (durationCoroutine != null)
            {
                StopCoroutine(durationCoroutine);
            }
            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
            }
        }
    }
}