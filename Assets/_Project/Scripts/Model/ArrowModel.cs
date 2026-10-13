using System;
using System.Collections.Generic;

namespace Arrows.Model
{
    /// <summary>
    /// An arrow is a path of orthogonally adjacent cells. The last cell is the head;
    /// the direction is the difference of the last two cells.
    /// </summary>
    public sealed class ArrowModel
    {
        private readonly GridPos[] _cells;

        public int Id { get; }
        public IReadOnlyList<GridPos> Cells => _cells;
        public int Length => _cells.Length;
        public GridPos Tail => _cells[0];
        public GridPos Head => _cells[_cells.Length - 1];
        public GridPos Direction { get; }

        /// <summary>Optional color override from level data (hex like "#FF8800"), or null.</summary>
        public string Color { get; }

        public bool IsRemoved { get; internal set; }

        public ArrowModel(int id, IReadOnlyList<GridPos> cells, string color = null)
        {
            if (cells == null || cells.Count < 2)
                throw new ArgumentException("An arrow needs at least two cells.", nameof(cells));

            Id = id;
            Color = color;
            _cells = new GridPos[cells.Count];
            for (int i = 0; i < cells.Count; i++) _cells[i] = cells[i];

            Direction = _cells[_cells.Length - 1] - _cells[_cells.Length - 2];
        }

        /// <summary>Index of a cell along the body (0 = tail), or -1.</summary>
        public int IndexOf(GridPos cell)
        {
            for (int i = 0; i < _cells.Length; i++)
                if (_cells[i] == cell) return i;
            return -1;
        }

        internal ArrowModel CloneInternal()
        {
            return new ArrowModel(Id, _cells, Color) { IsRemoved = IsRemoved };
        }
    }
}
