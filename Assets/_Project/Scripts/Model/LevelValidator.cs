using System.Collections.Generic;

namespace Arrows.Model
{
    /// <summary>Structural checks on level data (does not check solvability, see <see cref="LevelSolver"/>).</summary>
    public static class LevelValidator
    {
        public static List<string> Validate(LevelData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("Level data is null.");
                return errors;
            }

            if (data.Width <= 0 || data.Height <= 0)
            {
                errors.Add($"Invalid grid size {data.Width}x{data.Height}.");
                return errors;
            }

            var owner = new Dictionary<GridPos, int>();
            for (int a = 0; a < data.Arrows.Count; a++)
            {
                var cells = data.Arrows[a].Cells;
                if (cells == null || cells.Count < 2)
                {
                    errors.Add($"Arrow {a}: needs at least 2 cells.");
                    continue;
                }

                for (int i = 0; i < cells.Count; i++)
                {
                    var c = cells[i];
                    if (c.X < 0 || c.Y < 0 || c.X >= data.Width || c.Y >= data.Height)
                        errors.Add($"Arrow {a}: cell {c} is out of bounds.");

                    if (i > 0 && !(c - cells[i - 1]).IsUnitDirection)
                        errors.Add($"Arrow {a}: cells {cells[i - 1]} and {c} are not adjacent.");

                    if (owner.TryGetValue(c, out int other))
                        errors.Add(other == a
                            ? $"Arrow {a}: crosses itself at {c}."
                            : $"Arrow {a}: overlaps arrow {other} at {c}.");
                    else
                        owner[c] = a;
                }
            }

            return errors;
        }
    }
}
