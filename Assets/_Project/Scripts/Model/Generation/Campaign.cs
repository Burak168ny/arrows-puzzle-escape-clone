using System.Collections.Generic;
using System.Linq;

namespace Arrows.Model.Generation
{
    /// <summary>
    /// The shipped level sequence: three small tutorials, then boards that grow in size,
    /// arrow length and curviness, alternating plain rectangles with silhouettes.
    /// Deterministic: the same code always produces the same 30 levels.
    /// </summary>
    public static class Campaign
    {
        public const int TutorialCount = 3;

        private struct Slot
        {
            public int W, H, Max;
            public ShapeKind Shape;
            public float Fill, Turn;
            public Slot(int w, int h, ShapeKind shape, float fill, int max, float turn)
            {
                W = w; H = h; Shape = shape; Fill = fill; Max = max; Turn = turn;
            }
        }

        private static readonly Slot[] Slots =
        {
            // Tutorials: tiny, straight, mostly free arrows.
            new Slot(4, 4, ShapeKind.Rectangle, 0.55f, 3, 0.0f),
            new Slot(5, 5, ShapeKind.Rectangle, 0.70f, 3, 0.1f),
            new Slot(5, 6, ShapeKind.Rectangle, 0.80f, 4, 0.2f),
            // Early
            new Slot(6, 7, ShapeKind.Rectangle, 0.88f, 4, 0.25f),
            new Slot(6, 8, ShapeKind.Rectangle, 0.90f, 5, 0.25f),
            new Slot(7, 8, ShapeKind.Diamond, 0.92f, 5, 0.3f),
            new Slot(7, 9, ShapeKind.Rectangle, 0.92f, 5, 0.3f),
            new Slot(8, 9, ShapeKind.Circle, 0.92f, 5, 0.3f),
            new Slot(8, 10, ShapeKind.Rectangle, 0.93f, 6, 0.3f),
            new Slot(9, 9, ShapeKind.Heart, 0.93f, 6, 0.35f),
            // Mid
            new Slot(9, 11, ShapeKind.Rectangle, 0.93f, 6, 0.35f),
            new Slot(10, 11, ShapeKind.Diamond, 0.94f, 6, 0.35f),
            new Slot(10, 12, ShapeKind.Rectangle, 0.94f, 7, 0.35f),
            new Slot(11, 12, ShapeKind.Circle, 0.94f, 7, 0.4f),
            new Slot(11, 13, ShapeKind.Rectangle, 0.94f, 7, 0.4f),
            new Slot(12, 13, ShapeKind.Heart, 0.94f, 7, 0.4f),
            new Slot(12, 14, ShapeKind.Rectangle, 0.95f, 8, 0.4f),
            new Slot(13, 14, ShapeKind.Ring, 0.95f, 8, 0.4f),
            new Slot(13, 16, ShapeKind.Rectangle, 0.95f, 8, 0.4f),
            new Slot(14, 15, ShapeKind.Heart, 0.95f, 8, 0.45f),
            // Late
            new Slot(15, 17, ShapeKind.Rectangle, 0.95f, 9, 0.45f),
            new Slot(16, 16, ShapeKind.Circle, 0.95f, 9, 0.45f),
            new Slot(16, 19, ShapeKind.Rectangle, 0.95f, 9, 0.45f),
            new Slot(18, 18, ShapeKind.Diamond, 0.95f, 9, 0.45f),
            new Slot(18, 21, ShapeKind.Rectangle, 0.96f, 10, 0.45f),
            new Slot(20, 20, ShapeKind.Heart, 0.96f, 10, 0.45f),
            new Slot(21, 24, ShapeKind.Rectangle, 0.96f, 10, 0.5f),
            new Slot(24, 24, ShapeKind.Ring, 0.96f, 10, 0.5f),
            new Slot(26, 28, ShapeKind.Rectangle, 0.96f, 10, 0.5f),
            new Slot(30, 30, ShapeKind.Heart, 0.96f, 10, 0.5f),
        };

        public static int Count => Slots.Length;

        public static GeneratorSettings SettingsFor(int index, int baseSeed = 1000)
        {
            var slot = Slots[index];
            bool tutorial = index < TutorialCount;
            return new GeneratorSettings
            {
                Width = slot.W,
                Height = slot.H,
                Shape = slot.Shape,
                FillRatio = slot.Fill,
                MinLength = 2,
                MaxLength = slot.Max,
                TurnChance = slot.Turn,
                Seed = baseSeed + index * 17,
                Candidates = slot.W * slot.H > 300 ? 4 : 8,
                // Tutorials take the easiest candidate, everything else the hardest.
                TargetDifficulty = tutorial ? 0.01f : 0f,
            };
        }

        /// <summary>Levels per difficulty tier after the tutorials. Sorting happens within a tier.</summary>
        public const int TierSize = 9;

        /// <summary>
        /// Generates every slot, then orders the levels of each tier by difficulty (easy → hard).
        /// Tiers keep the board size progression; a pure global sort would put a 30x30 board
        /// right after a 6x6 one whenever the small board happens to be more constrained.
        /// </summary>
        public static List<GeneratedLevel> Generate(int baseSeed = 1000, System.Action<int, int> progress = null)
        {
            var levels = new List<GeneratedLevel>(Count);
            for (int i = 0; i < Count; i++)
            {
                progress?.Invoke(i, Count);
                levels.Add(LevelGenerator.Generate(SettingsFor(i, baseSeed)));
            }

            var ordered = levels.Take(TutorialCount).ToList();
            for (int start = TutorialCount; start < levels.Count; start += TierSize)
                ordered.AddRange(levels.Skip(start).Take(TierSize).OrderBy(l => l.Difficulty));
            return ordered;
        }
    }
}
