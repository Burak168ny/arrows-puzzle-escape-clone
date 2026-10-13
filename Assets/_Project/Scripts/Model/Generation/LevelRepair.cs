using System.Collections.Generic;

namespace Arrows.Model.Generation
{
    /// <summary>
    /// Makes an arbitrary (e.g. hand-edited or imported) level solvable.
    /// Repeatedly solves; for each stuck arrow tries reversing it (the tail becomes the head),
    /// keeping the reversal only if more arrows become removable. Arrows that cannot be fixed
    /// that way are deleted as a last resort.
    /// </summary>
    public static class LevelRepair
    {
        public sealed class Report
        {
            public int Reversed;
            public int Removed;
            public bool Changed => Reversed + Removed > 0;
            public override string ToString() => $"reversed {Reversed}, removed {Removed}";
        }

        public static Report Repair(LevelData data)
        {
            var report = new Report();
            if (LevelValidator.Validate(data).Count > 0) return report;

            var result = Solve(data);
            while (!result.IsSolvable)
            {
                bool improved = false;
                foreach (int id in result.StuckArrows)
                {
                    data.Arrows[id].Cells.Reverse();
                    var attempt = Solve(data);
                    if (attempt.Order.Count > result.Order.Count)
                    {
                        report.Reversed++;
                        result = attempt;
                        improved = true;
                        break;
                    }
                    data.Arrows[id].Cells.Reverse(); // undo
                }

                if (improved) continue;

                // No single reversal helps: drop the first stuck arrow and try again.
                data.Arrows.RemoveAt(result.StuckArrows[0]);
                report.Removed++;
                result = Solve(data);
            }

            return report;
        }

        private static SolveResult Solve(LevelData data) => LevelSolver.Solve(LevelModel.FromData(data));

        /// <summary>Convenience for tools: true if the data is valid and solvable.</summary>
        public static bool IsPlayable(LevelData data, out List<string> errors)
        {
            errors = LevelValidator.Validate(data);
            if (errors.Count > 0) return false;
            if (Solve(data).IsSolvable) return true;
            errors.Add("Level is not solvable.");
            return false;
        }
    }
}
