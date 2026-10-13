using System;

namespace Arrows.Model
{
    public enum SessionResult
    {
        InProgress,
        Won,
        Lost,
    }

    /// <summary>Game rules on top of a level: lives, win and loss.</summary>
    public sealed class GameSession
    {
        public const int DefaultLives = 3;

        public LevelModel Level { get; }
        public int MaxLives { get; }
        public int Lives { get; private set; }
        public SessionResult Result { get; private set; } = SessionResult.InProgress;
        public int Moves { get; private set; }

        public event Action<int> LivesChanged;
        public event Action<SessionResult> Finished;

        public GameSession(LevelModel level, int lives = DefaultLives)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            MaxLives = lives;
            Lives = lives;
        }

        /// <summary>
        /// Launches an arrow. Returns false (and no result) if the session is over or the arrow is gone.
        /// </summary>
        public bool TryLaunch(int arrowId, out MoveResult result)
        {
            result = default;
            if (Result != SessionResult.InProgress) return false;

            var arrow = Level.GetArrow(arrowId);
            if (arrow == null || arrow.IsRemoved) return false;

            result = Level.Launch(arrowId);
            Moves++;

            if (result.IsBlocked)
            {
                Lives = Math.Max(0, Lives - 1);
                LivesChanged?.Invoke(Lives);
                if (Lives == 0) Finish(SessionResult.Lost);
            }
            else if (Level.IsCleared)
            {
                Finish(SessionResult.Won);
            }

            return true;
        }

        private void Finish(SessionResult result)
        {
            Result = result;
            Finished?.Invoke(result);
        }
    }
}
