using Arrows.Core.Audio;
using Arrows.Model;
using Arrows.View;
using UnityEngine;

namespace Arrows.Core
{
    /// <summary>
    /// Translates game events into "juice": sound, haptics, camera shake and confetti.
    /// Keeps the flow (GameManager) and the visuals (views) free of feedback concerns,
    /// and is the single place that respects the player's sound/vibration settings.
    /// </summary>
    public sealed class FeedbackController : MonoBehaviour
    {
        [SerializeField] private GameManager _game;
        [SerializeField] private AudioManager _audio;
        [SerializeField] private CameraController _camera;
        [SerializeField] private GameConfig _config;

        [Header("Exit combo")]
        [Tooltip("Exits closer together than this raise the pitch.")]
        [SerializeField] private float _comboWindow = 0.8f;
        [SerializeField] private float _comboPitchStep = 0.06f;
        [SerializeField] private int _comboMaxSteps = 7;

        [Header("Impact")]
        [SerializeField] private float _shakeIntensity = 1f;
        [SerializeField] private float _shakeDuration = 0.28f;

        private readonly Haptics _haptics = new Haptics();
        private ConfettiEffect _confetti;
        private float _lastExitTime = -10f;
        private int _combo;

        private void Start()
        {
            // GameManager.Awake has created the progress service by now.
            if (_game.Progress != null)
            {
                _game.Progress.SettingsChanged += ApplySettings;
                ApplySettings();
            }

            _confetti = ConfettiEffect.Create(_config.Theme.SpriteMaterial, _config.Theme.Palette);
        }

        private void OnEnable()
        {
            _game.ArrowLaunched += OnArrowLaunched;
            _game.ArrowImpact += OnArrowImpact;
            _game.HintShown += OnHintShown;
            _game.StateChanged += OnStateChanged;
            UIEvents.ButtonPressed += OnButtonPressed;
        }

        private void OnDisable()
        {
            _game.ArrowLaunched -= OnArrowLaunched;
            _game.ArrowImpact -= OnArrowImpact;
            _game.HintShown -= OnHintShown;
            _game.StateChanged -= OnStateChanged;
            UIEvents.ButtonPressed -= OnButtonPressed;
            if (_game.Progress != null) _game.Progress.SettingsChanged -= ApplySettings;
        }

        private void ApplySettings()
        {
            _audio.Enabled = _game.Progress.SoundEnabled;
            _haptics.Enabled = _game.Progress.HapticsEnabled;
        }

        private void OnArrowLaunched(MoveResult result)
        {
            _audio.Play(SoundId.Tap, 1f, 0.5f);
            if (!result.IsExit)
            {
                _haptics.Play(HapticType.Light);
                return;
            }

            // Quick successive exits climb in pitch, rewarding fast play.
            float now = Time.unscaledTime;
            _combo = now - _lastExitTime <= _comboWindow ? Mathf.Min(_combo + 1, _comboMaxSteps) : 0;
            _lastExitTime = now;

            _audio.Play(SoundId.Exit, 1f + _combo * _comboPitchStep);
            _haptics.Play(HapticType.Light);
        }

        private void OnArrowImpact(ArrowView view)
        {
            _audio.Play(SoundId.Bump);
            _haptics.Play(HapticType.Heavy);
            _camera.Shake(_shakeIntensity, _shakeDuration);
            _combo = 0;
        }

        private void OnHintShown(int arrowId)
        {
            _audio.Play(SoundId.Hint);
            _haptics.Play(HapticType.Light);
        }

        private void OnButtonPressed()
        {
            _audio.Play(SoundId.Button, 1f, 0.6f);
            _haptics.Play(HapticType.Light);
        }

        private void OnStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Win:
                    _confetti.Play(_camera.Camera);
                    _audio.Play(SoundId.Win);
                    _haptics.Play(HapticType.Success);
                    break;
                case GameState.Fail:
                    _audio.Play(SoundId.Fail);
                    _haptics.Play(HapticType.Failure);
                    break;
            }
        }
    }
}
