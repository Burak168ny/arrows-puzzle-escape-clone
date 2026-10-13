using Arrows.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Arrows.UI
{
    /// <summary>Squashes a button while it is held and raises the shared UI press signal (sound/haptics).</summary>
    public sealed class ButtonPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float _pressedScale = 0.92f;
        [SerializeField] private float _speed = 18f;

        private float _target = 1f;

        public void OnPointerDown(PointerEventData eventData)
        {
            SetTarget(_pressedScale);
            UIEvents.RaiseButtonPressed();
        }
        public void OnPointerUp(PointerEventData eventData) => SetTarget(1f);
        public void OnPointerExit(PointerEventData eventData) => SetTarget(1f);

        private void SetTarget(float target)
        {
            _target = target;
            enabled = true;
        }

        private void OnDisable() => transform.localScale = Vector3.one;

        private void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, _target, 1f - Mathf.Exp(-_speed * Time.unscaledDeltaTime));
            if (Mathf.Abs(s - _target) < 0.001f)
            {
                s = _target;
                enabled = false;
            }
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
