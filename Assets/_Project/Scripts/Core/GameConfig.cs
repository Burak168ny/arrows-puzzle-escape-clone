using Arrows.View;
using UnityEngine;

namespace Arrows.Core
{
    /// <summary>All tunables in one asset, so designers never touch the scene.</summary>
    [CreateAssetMenu(menuName = "Arrows/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public LevelDatabase Levels;
        [Tooltip("After the last level, the sequence loops from this index (skips the tutorials).")]
        [Min(0)] public int LoopStartIndex = 3;

        [Header("Rules")]
        [Min(1)] public int Lives = 3;

        [Header("Input")]
        [Tooltip("How far from an arrow's center line a tap still selects it, in cells.")]
        [Range(0.2f, 1.5f)] public float TapToleranceCells = 0.65f;

        [Header("Flow")]
        public float EndScreenDelay = 0.35f;

        [Header("Visuals")]
        public BoardTheme Theme = new BoardTheme();
        public ArrowVisualSettings Arrows = new ArrowVisualSettings();
    }
}
