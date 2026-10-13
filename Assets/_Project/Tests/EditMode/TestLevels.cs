using System.Linq;
using Arrows.Model;

namespace Arrows.Tests
{
    /// <summary>Compact helpers for building levels in tests.</summary>
    internal static class TestLevels
    {
        public static GridPos P(int x, int y) => new GridPos(x, y);

        /// <summary>Each arrow is a flat list of coordinates: x0, y0, x1, y1, ...</summary>
        public static LevelData Data(int width, int height, params int[][] arrows)
        {
            var data = new LevelData { Width = width, Height = height };
            foreach (var flat in arrows)
            {
                var cells = Enumerable.Range(0, flat.Length / 2).Select(i => P(flat[i * 2], flat[i * 2 + 1]));
                data.Arrows.Add(new ArrowData(cells));
            }
            return data;
        }

        public static LevelModel Level(int width, int height, params int[][] arrows) =>
            LevelModel.FromData(Data(width, height, arrows));

        public static int[] A(params int[] coords) => coords;
    }
}
