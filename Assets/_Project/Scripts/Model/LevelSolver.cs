using System.Collections.Generic;

namespace Arrows.Model
{
    public sealed class SolveResult
    {
        public bool IsSolvable;

        /// <summary>A valid removal order (arrow ids). Partial if the level is not solvable.</summary>
        public readonly List<int> Order = new List<int>();

        /// <summary>Arrows left on the board when the solver got stuck.</summary>
        public readonly List<int> StuckArrows = new List<int>();

        /// <summary>Average number of simultaneously exitable arrows over the solution. Lower = harder.</summary>
        public float AverageOpenArrows;

        /// <summary>Smallest number of exitable arrows seen at any step. 1 means a forced move existed.</summary>
        public int MinOpenArrows;

        /// <summary>
        /// Arrows per open arrow on average: grows with level size and shrinks with choice.
        /// A board of n free arrows scores about 2; a forced chain of n arrows scores n.
        /// </summary>
        public float DifficultyScore => AverageOpenArrows > 0f ? Order.Count / AverageOpenArrows : 0f;
    }

    /// <summary>
    /// Greedy solver. Removing an arrow only frees cells and never blocks another arrow,
    /// so the set of exitable arrows is monotonic: if any order clears the board, then
    /// repeatedly removing any open arrow does too. Greedy is therefore complete.
    /// </summary>
    public static class LevelSolver
    {
        public static SolveResult Solve(LevelModel level)
        {
            var sim = level.Clone();
            var result = new SolveResult { MinOpenArrows = int.MaxValue };
            var open = new List<int>();
            long openSum = 0;
            int rounds = 0;

            while (!sim.IsCleared)
            {
                CollectOpen(sim, open);
                if (open.Count == 0) break;

                openSum += open.Count;
                rounds++;
                if (open.Count < result.MinOpenArrows) result.MinOpenArrows = open.Count;

                // Remove one arrow per step so the metric reflects the player's choices.
                int id = open[0];
                sim.Launch(id);
                result.Order.Add(id);
            }

            result.IsSolvable = sim.IsCleared;
            result.AverageOpenArrows = rounds > 0 ? (float)openSum / rounds : 0f;
            if (result.MinOpenArrows == int.MaxValue) result.MinOpenArrows = 0;

            foreach (var arrow in sim.Arrows)
                if (!arrow.IsRemoved) result.StuckArrows.Add(arrow.Id);

            return result;
        }

        public static bool IsSolvable(LevelModel level) => Solve(level).IsSolvable;

        /// <summary>Returns an arrow that can exit right now, or -1 if none.</summary>
        public static int GetHint(LevelModel level)
        {
            foreach (var arrow in level.Arrows)
                if (!arrow.IsRemoved && level.CanExit(arrow.Id))
                    return arrow.Id;
            return -1;
        }

        /// <summary>Fills <paramref name="open"/> with all arrows that can currently exit.</summary>
        public static void CollectOpen(LevelModel level, List<int> open)
        {
            open.Clear();
            foreach (var arrow in level.Arrows)
                if (!arrow.IsRemoved && level.CanExit(arrow.Id))
                    open.Add(arrow.Id);
        }
    }
}
