using System;
using System.Collections.Generic;

namespace Arrows.Model
{
    /// <summary>
    /// Runtime state of a level: the grid occupancy and its arrows. Owns the movement rules.
    /// </summary>
    public sealed class LevelModel
    {
        private readonly ArrowModel[] _arrows;

        public GridModel Grid { get; }
        public IReadOnlyList<ArrowModel> Arrows => _arrows;
        public int RemainingCount { get; private set; }
        public bool IsCleared => RemainingCount == 0;

        public event Action<ArrowModel> ArrowRemoved;

        private LevelModel(GridModel grid, ArrowModel[] arrows, int remaining)
        {
            Grid = grid;
            _arrows = arrows;
            RemainingCount = remaining;
        }

        /// <summary>Builds a level from data. Throws <see cref="ArgumentException"/> if the data is invalid.</summary>
        public static LevelModel FromData(LevelData data)
        {
            var errors = LevelValidator.Validate(data);
            if (errors.Count > 0)
                throw new ArgumentException("Invalid level:\n" + string.Join("\n", errors));

            var grid = new GridModel(data.Width, data.Height);
            var arrows = new ArrowModel[data.Arrows.Count];
            for (int i = 0; i < arrows.Length; i++)
            {
                var arrow = new ArrowModel(i, data.Arrows[i].Cells, data.Arrows[i].Color);
                arrows[i] = arrow;
                foreach (var c in arrow.Cells) grid.Set(c, i);
            }

            return new LevelModel(grid, arrows, arrows.Length);
        }

        public LevelModel Clone()
        {
            var arrows = new ArrowModel[_arrows.Length];
            for (int i = 0; i < arrows.Length; i++) arrows[i] = _arrows[i].CloneInternal();
            return new LevelModel(Grid.Clone(), arrows, RemainingCount);
        }

        public ArrowModel GetArrow(int id) => id >= 0 && id < _arrows.Length ? _arrows[id] : null;

        /// <summary>
        /// Computes what would happen if the arrow were launched, without changing state.
        /// The body follows the head like a snake, so a cell of the arrow's own body at index i
        /// (0 = tail) is already vacated once the head has advanced more than i steps.
        /// </summary>
        public MoveResult Evaluate(int arrowId)
        {
            var arrow = GetArrow(arrowId);
            if (arrow == null || arrow.IsRemoved)
                throw new InvalidOperationException($"Arrow {arrowId} is not on the board.");

            var dir = arrow.Direction;
            var pos = arrow.Head;
            for (int step = 1; ; step++)
            {
                pos += dir;
                if (!Grid.InBounds(pos))
                    return MoveResult.Exit(arrowId, step - 1);

                int occupant = Grid.Get(pos);
                if (occupant == GridModel.Empty) continue;

                if (occupant == arrowId && step > arrow.IndexOf(pos)) continue;

                return MoveResult.Blocked(arrowId, step - 1, pos, occupant);
            }
        }

        public bool CanExit(int arrowId) => Evaluate(arrowId).IsExit;

        /// <summary>Launches the arrow. On exit it is removed from the grid immediately.</summary>
        public MoveResult Launch(int arrowId)
        {
            var result = Evaluate(arrowId);
            if (result.IsExit) Remove(_arrows[arrowId]);
            return result;
        }

        private void Remove(ArrowModel arrow)
        {
            foreach (var c in arrow.Cells) Grid.Set(c, GridModel.Empty);
            arrow.IsRemoved = true;
            RemainingCount--;
            ArrowRemoved?.Invoke(arrow);
        }

        /// <summary>
        /// Finds the arrow closest to a point given in continuous grid units (cell centers are integers).
        /// Searches the cells around the point and measures the distance to each arrow's center line,
        /// so thin lines remain easy to tap. Returns -1 if nothing is within <paramref name="tolerance"/>.
        /// </summary>
        public int FindArrowNear(float x, float y, float tolerance)
        {
            int cx = (int)Math.Round(x);
            int cy = (int)Math.Round(y);
            int reach = (int)Math.Ceiling(tolerance) + 1;

            int best = -1;
            float bestDist = tolerance;
            for (int gy = cy - reach; gy <= cy + reach; gy++)
            for (int gx = cx - reach; gx <= cx + reach; gx++)
            {
                var cell = new GridPos(gx, gy);
                int id = Grid.Get(cell);
                if (id == GridModel.Empty) continue;

                float d = DistanceToArrowAround(_arrows[id], cell, x, y);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = id;
                }
            }

            return best;
        }

        // Distance from point to the arrow segments touching the given cell.
        private static float DistanceToArrowAround(ArrowModel arrow, GridPos cell, float px, float py)
        {
            int i = arrow.IndexOf(cell);
            var cells = arrow.Cells;
            float best = PointDistance(px, py, cell.X, cell.Y);
            if (i > 0) best = Math.Min(best, SegmentDistance(px, py, cells[i - 1], cell));
            if (i < cells.Count - 1) best = Math.Min(best, SegmentDistance(px, py, cell, cells[i + 1]));
            return best;
        }

        private static float PointDistance(float px, float py, float x, float y)
        {
            float dx = px - x, dy = py - y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float SegmentDistance(float px, float py, GridPos a, GridPos b)
        {
            float abx = b.X - a.X, aby = b.Y - a.Y;
            float t = ((px - a.X) * abx + (py - a.Y) * aby) / (abx * abx + aby * aby);
            t = Math.Max(0f, Math.Min(1f, t));
            return PointDistance(px, py, a.X + abx * t, a.Y + aby * t);
        }
    }
}
