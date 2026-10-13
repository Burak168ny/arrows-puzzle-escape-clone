using Arrows.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Arrows.UI
{
    /// <summary>Sound and vibration toggles, persisted through <see cref="ProgressService"/>.</summary>
    public sealed class SettingsPanel : UIPanel
    {
        [SerializeField] private Button _soundButton;
        [SerializeField] private TMP_Text _soundLabel;
        [SerializeField] private Button _hapticsButton;
        [SerializeField] private TMP_Text _hapticsLabel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Color _onColor = new Color(0.30f, 0.72f, 0.48f);
        [SerializeField] private Color _offColor = new Color(0.75f, 0.75f, 0.78f);

        private ProgressService _progress;

        public void Bind(ProgressService progress)
        {
            _progress = progress;
            _soundButton.onClick.AddListener(() => _progress.SetSound(!_progress.SoundEnabled));
            _hapticsButton.onClick.AddListener(() => _progress.SetHaptics(!_progress.HapticsEnabled));
            _closeButton.onClick.AddListener(() => Hide());
            _progress.SettingsChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_progress != null) _progress.SettingsChanged -= Refresh;
        }

        private void Refresh()
        {
            Apply(_soundButton, _soundLabel, "Sound", _progress.SoundEnabled);
            Apply(_hapticsButton, _hapticsLabel, "Vibration", _progress.HapticsEnabled);
        }

        private void Apply(Button button, TMP_Text label, string name, bool on)
        {
            label.text = on ? $"{name}: ON" : $"{name}: OFF";
            button.image.color = on ? _onColor : _offColor;
        }
    }
}
