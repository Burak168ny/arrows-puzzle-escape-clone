using Arrows.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Arrows.UI
{
    /// <summary>
    /// Presenter between <see cref="GameManager"/> and the screens. Shows the panel for each
    /// game state and forwards button clicks to the game API. Screens know nothing about the game.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private GameManager _game;

        [Header("Menu")]
        [SerializeField] private UIPanel _menuPanel;
        [SerializeField] private TMP_Text _menuLevelText;
        [SerializeField] private Button _playButton;

        [Header("HUD")]
        [SerializeField] private UIPanel _hudPanel;
        [SerializeField] private HudView _hud;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _hintButton;

        [Header("Results")]
        [SerializeField] private UIPanel _winPanel;
        [SerializeField] private TMP_Text _winTitle;
        [SerializeField] private Button _nextButton;
        [SerializeField] private UIPanel _failPanel;
        [SerializeField] private Button _retryButton;

        [Header("Popups")]
        [SerializeField] private SettingsPanel _settingsPanel;

        private void Awake()
        {
            _playButton.onClick.AddListener(_game.Play);
            _restartButton.onClick.AddListener(_game.Restart);
            _hintButton.onClick.AddListener(_game.ShowHint);
            _nextButton.onClick.AddListener(_game.NextLevel);
            _retryButton.onClick.AddListener(_game.Restart);
            _settingsButton.onClick.AddListener(() => _settingsPanel.Show());

            _menuPanel.Hide(true);
            _hudPanel.Hide(true);
            _winPanel.Hide(true);
            _failPanel.Hide(true);
            _settingsPanel.Hide(true);
        }

        private void OnEnable()
        {
            _game.StateChanged += OnStateChanged;
            _game.LevelLoaded += OnLevelLoaded;
            _game.LivesChanged += _hud.SetLives;
        }

        private void OnDisable()
        {
            _game.StateChanged -= OnStateChanged;
            _game.LevelLoaded -= OnLevelLoaded;
            _game.LivesChanged -= _hud.SetLives;
        }

        private void Start()
        {
            // GameManager.Awake has created the progress service by now.
            if (_game.Progress != null) _settingsPanel.Bind(_game.Progress);
        }

        private void OnLevelLoaded(int number)
        {
            _hud.SetLevel(number);
            _menuLevelText.SetText("Level {0}", number);
            _winTitle.SetText("Level {0}\nComplete!", number);
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Loading) return; // keep the current screens during a reload

            _menuPanel.SetVisible(state == GameState.Menu);
            _hudPanel.SetVisible(state == GameState.Playing || state == GameState.Win || state == GameState.Fail);
            _winPanel.SetVisible(state == GameState.Win);
            _failPanel.SetVisible(state == GameState.Fail);
            if (state != GameState.Playing) _settingsPanel.Hide();
        }
    }
}
