using Arrows.Utils;
using UnityEngine;

namespace Arrows.UI
{
    /// <summary>
    /// A screen or popup that fades in/out, optionally popping a content card.
    /// Hidden panels are deactivated so they cost nothing.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPanel : MonoBehaviour
    {
        [Tooltip("Optional child that scales in with a slight overshoot.")]
        [SerializeField] private RectTransform _content;
        [SerializeField] private float _duration = 0.22f;
        [SerializeField] private float _hiddenScale = 0.85f;

        private CanvasGroup _group;
        private float _progress;
        private bool _visible;

        private CanvasGroup Group => _group ? _group : _group = GetComponent<CanvasGroup>();
        public bool IsVisible => _visible;

        public void SetVisible(bool visible, bool instant = false)
        {
            if (visible) Show(instant);
            else Hide(instant);
        }

        public void Show(bool instant = false)
        {
            _visible = true;
            gameObject.SetActive(true);
            Group.blocksRaycasts = true;
            Group.interactable = true;
            if (instant) _progress = 1f;
            Apply();
            enabled = !instant;
        }

        public void Hide(bool instant = false)
        {
            _visible = false;
            Group.blocksRaycasts = false;
            Group.interactable = false;
            if (instant || !gameObject.activeInHierarchy)
            {
                _progress = 0f;
                Apply();
                gameObject.SetActive(false);
                return;
            }
            enabled = true;
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, _duration);
            _progress = Mathf.Clamp01(_progress + (_visible ? step : -step));
            Apply();

            if (_visible && _progress >= 1f) enabled = false;
            if (!_visible && _progress <= 0f) gameObject.SetActive(false);
        }

        private void Apply()
        {
            Group.alpha = Easing.OutQuad(_progress);
            if (_content)
            {
                float s = _visible
                    ? Mathf.LerpUnclamped(_hiddenScale, 1f, Easing.OutBack(_progress))
                    : Mathf.Lerp(_hiddenScale, 1f, _progress);
                _content.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
