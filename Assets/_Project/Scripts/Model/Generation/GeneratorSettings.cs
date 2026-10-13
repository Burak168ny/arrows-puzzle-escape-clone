using System;

namespace Arrows.Model.Generation
{
    /// <summary>Input for <see cref="LevelGenerator"/>. Lengths are in cells.</summary>
    [Serializable]
    public sealed class GeneratorSettings
    {
        public int Width = 8;
        public int Height = 10;
        public ShapeKind Shape = ShapeKind.Rectangle;

        /// <summary>Target share of usable cells covered by arrows (0..1).</summary>
        public float FillRatio = 0.9f;

        public int MinLength = 2;
        public int MaxLength = 6;

        /// <summary>Chance per step that the body turns instead of going straight (0..1).</summary>
        public float TurnChance = 0.3f;

        public int Seed = 1;

        /// <summary>Candidates generated per call; the one closest to <see cref="TargetDifficulty"/> wins.</summary>
        public int Candidates = 1;

        /// <summary>Desired <see cref="SolveResult.DifficultyScore"/>; 0 or less picks the hardest candidate.</summary>
        public float TargetDifficulty = 0f;

        public GeneratorSettings Clone() => (GeneratorSettings)MemberwiseClone();

        public void Sanitize()
        {
            Width = Math.Clamp(Width, 2, 40);
            Height = Math.Clamp(Height, 2, 40);
            FillRatio = Math.Clamp(FillRatio, 0.05f, 1f);
            MinLength = Math.Max(2, MinLength);
            MaxLength = Math.Max(MinLength, MaxLength);
            TurnChance = Math.Clamp(TurnChance, 0f, 1f);
            Candidates = Math.Clamp(Candidates, 1, 200);
        }
    }
}
