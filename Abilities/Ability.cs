using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Logger = Silk.Logger;

namespace SpiderSurge.Abilities
{
    public abstract class Ability : MonoBehaviour
    {
        protected PlayerInput playerInput;
        protected PlayerController playerController;
        protected SpiderHealthSystem spiderHealthSystem;
        protected InputInterceptor inputInterceptor;

        protected bool isActive = false;
        protected bool onCooldown = false;
        protected Coroutine durationCoroutine;
        protected Coroutine cooldownCoroutine;

        public virtual string ActivationButton => "<Gamepad>/buttonSouth";
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

            // Find and register with InputInterceptor
            inputInterceptor = GetComponentInParent<InputInterceptor>();
            if (inputInterceptor != null && !string.IsNullOrEmpty(ActivationButton))
            {
                inputInterceptor.RegisterAbility(this);
                Logger.LogInfo($"{GetType().Name} registered with InputInterceptor using button {ActivationButton}");
            }
            else if (string.IsNullOrEmpty(ActivationButton))
            {
                Logger.LogInfo($"{GetType().Name} has no activation button defined - manual activation only");
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
            if (onCooldown)
            {
                Logger.LogError($"{GetType().Name} ability is on cooldown for player {playerInput.playerIndex}!");
                return;
            }

            if (isActive)
            {
                Logger.LogError($"{GetType().Name} ability is already active for player {playerInput.playerIndex}!");
                return;
            }

            if (!CanActivate())
            {
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
                Logger.LogInfo($"{GetType().Name} DEACTIVATED for player {playerInput.playerIndex}!");
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

        protected virtual bool CanActivate()
        {
            return true;
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
            Logger.LogInfo($"{GetType().Name} cooldown started for player {playerInput.playerIndex} ({CooldownTime}s)");

            yield return new WaitForSeconds(CooldownTime);

            onCooldown = false;
            Logger.LogInfo($"{GetType().Name} cooldown finished for player {playerInput.playerIndex}");
        }

        protected virtual void OnDestroy()
        {
            // Unregister from InputInterceptor
            if (inputInterceptor != null)
            {
                inputInterceptor.UnregisterAbility(this);
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
