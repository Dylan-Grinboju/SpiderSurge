using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SpiderSurge
{
    public sealed class ChallengeHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ISubmitHandler, IDeselectHandler
    {
        private HoldConfirmation _hold;
        private Button _button;
        private ButtonControl _control;
        private RectTransform _progress;
        private Canvas _canvas;
        private bool _pointer;
        private bool _accept;

        public void Initialize(Button button, TMP_Text label, string title, bool accept)
        {
            _button = button;
            _accept = accept;
            _hold = new HoldConfirmation(accept ? HoldConfirmation.Duration : 0f);
            _canvas = GetComponentInParent<Canvas>();
            SettingsPage.SetText(label, accept ? "Hold to accept" : "Skip challenge");
            var bar = new GameObject("Hold progress", typeof(RectTransform), typeof(Image));
            _progress = (RectTransform)bar.transform;
            _progress.SetParent(transform, false);
            _progress.anchorMin = Vector2.zero;
            _progress.anchorMax = Vector2.zero;
            _progress.pivot = Vector2.zero;
            _progress.anchoredPosition = new Vector2(4, 4);
            _progress.sizeDelta = Vector2.zero;
            var image = bar.GetComponent<Image>();
            image.color = Hud.Pink;
            image.raycastTarget = false;
        }

        private bool Available => Net.IsAuthority && ChallengeController.Pending && _canvas != null && _canvas.enabled &&
            _button != null && _button.IsInteractable() && Application.isFocused && (HudController.instance == null || !HudController.instance.paused);

        private static InputSystemUIInputModule InputModule => EventSystem.current?.currentInputModule as InputSystemUIInputModule;

        private static bool ControlsReleased()
        {
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return false;
            var action = InputModule?.submit?.action;
            if (action != null)
                foreach (var control in action.controls)
                    if (control is ButtonControl button && button.isPressed) return false;
            return true;
        }

        private void Update()
        {
            bool selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            bool held = _control != null && _control.isPressed && (_pointer || selected);
            bool confirmed = _hold.Tick(Time.unscaledTime, held, ControlsReleased(), Available);
            if (!held) _control = null;
            if (_progress != null)
                _progress.sizeDelta = new Vector2(Mathf.Max(0, ((RectTransform)transform).rect.width - 8) * _hold.Progress, 4);
            if (confirmed) ChaosNet.SendChallenge(_accept);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Available || eventData.button != PointerEventData.InputButton.Left) return;
            _control = Mouse.current?.leftButton;
            _pointer = true;
            TrySkip();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && _pointer) Cancel();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_pointer) Cancel();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!Available) return;
            _control = InputModule?.submit?.action?.activeControl as ButtonControl;
            _pointer = false;
            TrySkip();
        }

        private void TrySkip()
        {
            if (!_accept && _hold.Tick(Time.unscaledTime, true, false, Available)) ChaosNet.SendChallenge(false);
        }

        public void OnDeselect(BaseEventData eventData) => Cancel();
        private void OnDisable() => Cancel();

        public void Cancel()
        {
            _control = null;
            _pointer = false;
            _hold?.Reset();
            if (_progress != null) _progress.sizeDelta = Vector2.zero;
        }
    }
}
