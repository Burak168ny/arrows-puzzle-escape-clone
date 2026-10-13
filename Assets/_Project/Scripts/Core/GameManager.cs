using System;
using System.Collections;
using System.Collections.Generic;
using Arrows.Model;
using Arrows.View;
using UnityEngine;

namespace Arrows.Core
{
    /// <summary>
    /// Composition root and flow state machine: Loading → Menu → Playing → Win/Fail.
    /// Wires input, model and board together and exposes events for the UI.
    /// Game rules live in the model; UI lives in <c>Arrows.UI</c> and only talks to this class.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private BoardView _board;
        [SerializeField] private CameraController _camera;
        [SerializeField] private InputHandler _input;

        private LevelManager _levels;
        private GameSession _session;
        private Coroutine _endRoutine;
        private readonly List<int> _openArrows = new List<int>();
        private int _lastHint = -1;

        public GameState State { get; private set; } = GameState.Loading;
        public GameSession Session => _session;
        public ProgressService Progress { get; private set; }

        /// <summary>Number of the level currently on the board (1-based).</summary>
        public int CurrentLevelNumber { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<int> LevelLoaded;
        public event Action<int, int> LivesChanged;

        /// <summary>An arrow was launched by the player (exit or blocked).</summary>
        public event Action<MoveResult> ArrowLaunched;

        /// <summary>A launched arrow hit something. Raised at the moment of impact.</summary>
        public event Action<ArrowView> ArrowImpact;

        public event Action<int> HintShown;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            Application.targetFrameRate = 60;
            Progress = new ProgressService(new PlayerPrefsStore());
            _levels = new LevelManager(_config.Levels, _config.LoopStartIndex);
            _camera.SetBackground(_config.Theme.BackgroundColor);
        }

        private void OnEnable()
        {
            if (_input == null || _board == null) return;
            _input.Tapped += OnTapped;
            _input.Panned += OnPanned;
            _input.Zoomed += OnZoomed;
            _board.ArrowImpact += OnArrowImpact;
        }

        private void OnDisable()
        {
            if (_input == null || _board == null) return;
            _input.Tapped -= OnTapped;
            _input.Panned -= OnPanned;
            _input.Zoomed -= OnZoomed;
            _board.ArrowImpact -= OnArrowImpact;
        }

        private void Start()
        {
            if (LoadLevel()) SetState(GameState.Menu);
        }

        // ---- Public API used by the UI ----

        public void Play()
        {
            if (State == GameState.Menu) SetState(GameState.Playing);
        }

        public void Restart()
        {
            if (LoadLevel()) SetState(GameState.Playing);
        }

        public void NextLevel()
        {
            if (State != GameState.Win) return;
            if (LoadLevel()) SetState(GameState.Playing);
        }

        public void ShowHint()
        {
            if (State != GameState.Playing) return;
            // Any open arrow is a correct hint (the greedy solver is complete). Prefer one that is
            // idle and, on repeated presses, a different one than last time.
            LevelSolver.CollectOpen(_session.Level, _openArrows);
            int id = -1;
            foreach (int candidate in _openArrows)
            {
                var view = _board.GetView(candidate);
                if (view == null || view.IsBusy) continue;
                id = candidate;
                if (candidate != _lastHint) break;
            }
            if (id < 0) return;

            _lastHint = id;
            _board.ShowHint(id, _session.Level.Evaluate(id).FreeCells);
            HintShown?.Invoke(id);
        }

        // ---- Flow ----

        private bool LoadLevel()
        {
            SetState(GameState.Loading);
            if (_endRoutine != null) StopCoroutine(_endRoutine);
            _endRoutine = null;

            if (_session != null)
            {
                _session.Finished -= OnSessionFinished;
                _session.LivesChanged -= OnLivesChanged;
            }

            CurrentLevelNumber = Progress.LevelNumber;
            _lastHint = -1;
            LevelModel level;
            try
            {
                level = _levels.Load(CurrentLevelNumber);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameManager] Failed to load level {CurrentLevelNumber}: {e.Message}");
                return false;
            }

            _session = new GameSession(level, _config.Lives);
            _session.Finished += OnSessionFinished;
            _session.LivesChanged += OnLivesChanged;

            _board.Build(level, _config.Theme, _config.Arrows);
            _camera.Fit(_board.WorldBounds);

            LevelLoaded?.Invoke(CurrentLevelNumber);
            LivesChanged?.Invoke(_session.Lives, _session.MaxLives);
            return true;
        }

        private void OnSessionFinished(SessionResult result)
        {
            bool won = result == SessionResult.Won;
            if (won) Progress.CompleteLevel(); // save immediately, before any animation
            _endRoutine = StartCoroutine(EndAfterAnimations(won ? GameState.Win : GameState.Fail));
        }

        private IEnumerator EndAfterAnimations(GameState endState)
        {
            yield return null; // the final move's animation starts after TryLaunch returns
            yield return new WaitWhile(() => _board.AnyBusy);
            yield return new WaitForSeconds(_config.EndScreenDelay);
            SetState(endState);
            _endRoutine = null;
        }

        private void OnLivesChanged(int lives) => LivesChanged?.Invoke(lives, _session.MaxLives);

        private void SetState(GameState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }

        // ---- Input ----

        private void OnTapped(Vector2 screenPosition)
        {
            if (State != GameState.Playing) return;

            Vector2 grid = _board.WorldToGrid(_camera.ScreenToWorld(screenPosition));
            int id = _session.Level.FindArrowNear(grid.x, grid.y, _config.TapToleranceCells);
            if (id < 0) return;

            var view = _board.GetView(id);
            if (view == null || view.IsBusy) return;
            if (!_session.TryLaunch(id, out var result)) return;

            _board.HideHint();
            if (result.IsExit) view.PlayExit(result.FreeCells);
            else view.PlayBump(result.FreeCells);
            ArrowLaunched?.Invoke(result);
        }

        private void OnPanned(Vector2 delta)
        {
            if (State == GameState.Playing) _camera.Pan(delta);
        }

        private void OnZoomed(float multiplier, Vector2 pivot)
        {
            if (State == GameState.Playing) _camera.Zoom(multiplier, pivot);
        }

        private void OnArrowImpact(ArrowView view)
        {
            var result = _session.Level.Evaluate(view.Model.Id);
            if (!result.IsBlocked) return;

            _board.GetView(result.BlockerId)?.Flash();
            if (result.BlockerId != view.Model.Id) view.Flash();
            ArrowImpact?.Invoke(view);
        }

        private bool ValidateReferences()
        {
            bool ok = true;
            void Check(UnityEngine.Object o, string name)
            {
                if (o != null) return;
                Debug.LogError($"[GameManager] '{name}' is not assigned. Run Tools > Arrows > Setup Game Scene.", this);
                ok = false;
            }

            Check(_config, nameof(_config));
            Check(_board, nameof(_board));
            Check(_camera, nameof(_camera));
            Check(_input, nameof(_input));
            if (_config != null) Check(_config.Levels, "GameConfig.Levels");
            return ok;
        }
    }
}
