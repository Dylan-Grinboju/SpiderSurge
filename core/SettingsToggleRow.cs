using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpiderSurge.Common
{
    public sealed class SettingsToggleRow : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public Toggle Toggle;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Toggle != null) Toggle.OnPointerDown(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Toggle != null) Toggle.OnPointerUp(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Toggle != null) Toggle.OnPointerClick(eventData);
        }
    }
}
