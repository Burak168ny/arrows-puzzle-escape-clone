using System.Collections.Generic;

namespace Arrows.Model
{
    /// <summary>Plain serializable description of a level (mirrors the JSON format).</summary>
    public sealed class LevelData
    {
        public int Width;
        public int Height;
        public List<ArrowData> Arrows = new List<ArrowData>();
    }

    public sealed class ArrowData
    {
        public List<GridPos> Cells = new List<GridPos>();
        public string Color;

        public ArrowData() { }

        public ArrowData(IEnumerable<GridPos> cells, string color = null)
        {
            Cells.AddRange(cells);
            Color = color;
        }
    }
}
