using System.Collections.Generic;
using Arrows.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Arrows.UI
{
    /// <summary>In-game HUD: level number and hearts. A lost heart pops and fades out.</summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private RectTransform _heartsRoot;
        [SerializeField] private Sprite _heartSprite;
        [SerializeField] private float _heartSize = 64f;
        [SerializeField] private Color _heartFull = new Color(0.93f, 0.27f, 0.33f);
        [SerializeField] private Color _heartEmpty = new Color(0f, 0f, 0f, 0.15f);
        [SerializeField] private float _loseDuration = 0.45f;

        private readonly List<Image> _hearts = new List<Image>();
        private readonly List<float> _loseTimers = new List<float>();
        private int _lives = -1;

        public void SetLevel(int number)
        {
            if (_levelText) _levelText.SetText("Level {0}", number);
        }

        public void SetLives(int current, int max)
        {
            while (_hearts.Count < max)
            {
                _hearts.Add(CreateHeart());
                _loseTimers.Add(-1f);
            }

            bool animate = _lives >= 0 && current < _lives;
            for (int i = 0; i < _hearts.Count; i++)
            {
                _hearts[i].gameObject.SetActive(i < max);
                bool losing = animate && i >= current && i < _lives;
                _loseTimers[i] = losing ? 0f : -1f;
                if (!losing) SetHeart(i, i < current ? _heartFull : _heartEmpty, 1f);
            }

            _lives = current;
            enabled = animate;
        }

        private void Update()
        {
            bool running = false;
            for (int i = 0; i < _hearts.Count; i++)
            {
                if (_loseTimers[i] < 0f) continue;
                _loseTimers[i] += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_loseTimers[i] / _loseDuration);

                // Pop up, then shrink back while draining to the empty color.
                float scale = t < 0.3f ? Mathf.Lerp(1f, 1.45f, Easing.OutQuad(t / 0.3f))
                                       : Mathf.Lerp(1.45f, 1f, Easing.OutBack((t - 0.3f) / 0.7f));
                SetHeart(i, Color.Lerp(_heartFull, _heartEmpty, Easing.InQuad(t)), scale);

                if (t >= 1f) _loseTimers[i] = -1f;
                else running = true;
            }
            enabled = running;
        }

        private void SetHeart(int i, Color color, float scale)
        {
            _hearts[i].color = color;
            _hearts[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private Image CreateHeart()
        {
            var go = new GameObject("Heart", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_heartsRoot, false);
            rt.sizeDelta = new Vector2(_heartSize, _heartSize);
            var image = go.GetComponent<Image>();
            image.sprite = _heartSprite ? _heartSprite : ProceduralSprites.Heart;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }
    }
}
