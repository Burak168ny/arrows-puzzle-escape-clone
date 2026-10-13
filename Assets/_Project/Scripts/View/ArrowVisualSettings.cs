using System;
using UnityEngine;

namespace Arrows.View
{
    /// <summary>Shape and animation tuning for arrows. Sizes are in cells.</summary>
    [Serializable]
    public sealed class ArrowVisualSettings
    {
        [Header("Shape")]
        [Range(0.05f, 0.6f)] public float LineWidth = 0.13f;
        [Range(0.1f, 1f)] public float HeadLength = 0.4f;
        [Range(0.1f, 1.2f)] public float HeadWidth = 0.44f;
        [Tooltip("0 = sharp corners like the original game.")]
        [Range(0, 12)] public int CornerVertices = 0;
        [Range(0, 12)] public int CapVertices = 0;
        public int SortingOrder = 10;
        public int MovingSortingOrderBoost = 20;

        [Header("Exit")]
        public float ExitBaseDuration = 0.22f;
        public float ExitDurationPerCell = 0.03f;
        [Tooltip("Cells travelled past the grid edge before the arrow is gone.")]
        public float ExitExtraCells = 3f;
        [Range(0f, 1f)] public float ExitEaseIn = 0.75f;
        [Tooltip("Line width multiplier reached as the arrow fades out.")]
        [Range(0.1f, 1f)] public float ExitEndWidth = 0.6f;

        [Header("Spawn / Tap")]
        public float SpawnDuration = 0.32f;
        [Tooltip("Total time over which arrows pop in, staggered from the center outwards.")]
        public float SpawnStagger = 0.55f;
        public float PunchDuration = 0.16f;
        public float PunchScale = 0.5f;
        [Tooltip("How much a tapped arrow brightens. 0 keeps head and body exactly the ink color.")]
        [Range(0f, 1f)] public float PunchBrighten = 0f;

        [Header("Blocked")]
        [Tooltip("How far past the last free cell the head travels before hitting (cells).")]
        public float BumpOvershoot = 0.12f;
        public float BumpSpeed = 22f;
        public float BounceBackDuration = 0.45f;

        [Header("Flash")]
        public Color FlashColor = new Color(0.95f, 0.22f, 0.22f);
        public float FlashDuration = 0.5f;

        [Header("Hint")]
        public float HighlightDuration = 1.2f;
        public float HighlightScale = 0.9f;
        public int HighlightPulses = 3;

        /// <summary>Thin lines, sharp corners and small heads, matching the original game.</summary>
        public void ApplyClassicShape()
        {
            LineWidth = 0.13f;
            HeadLength = 0.4f;
            HeadWidth = 0.44f;
            PunchBrighten = 0f;
            CornerVertices = 0;
            CapVertices = 0;
            PunchScale = 0.5f;
            HighlightScale = 0.9f;
        }
    }

    /// <summary>Colors and materials used to draw the board.</summary>
    [Serializable]
    public sealed class BoardTheme
    {
        public static readonly Color Ink = new Color(0.063f, 0.071f, 0.141f); // #101224, near-black navy

        [Tooltip("Unlit sprite material used for arrows and dots (URP 2D Sprite-Unlit-Default).")]
        public Material SpriteMaterial;
        public Color BackgroundColor = Color.white;

        [Header("Dots")]
        public bool ShowDots = false;
        public Color DotColor = new Color(0f, 0f, 0f, 0.12f);
        [Range(0.02f, 0.4f)] public float DotSize = 0.12f;

        [Header("Trail dots")]
        [Tooltip("Reveal small grid dots in the cells a moving arrow passes over, then fade them out.")]
        public bool TrailDots = true;
        public Color TrailDotColor = new Color(0.063f, 0.071f, 0.141f, 0.35f);
        [Range(0.02f, 0.4f)] public float TrailDotSize = 0.1f;
        [Tooltip("Seconds a dot stays fully visible after the arrow last covered it.")]
        public float TrailHold = 0.12f;
        public float TrailFade = 0.55f;

        [Tooltip("Arrow colors, cycled by arrow id. A single entry gives the classic monochrome look.")]
        public Color[] Palette = { Ink };

        [Tooltip("Colors for celebration effects (confetti).")]
        public Color[] ConfettiPalette =
        {
            new Color(0.27f, 0.49f, 0.91f), // blue
            new Color(0.96f, 0.55f, 0.18f), // orange
            new Color(0.30f, 0.72f, 0.48f), // green
            new Color(0.62f, 0.38f, 0.86f), // purple
            new Color(0.93f, 0.33f, 0.49f), // pink
            new Color(0.98f, 0.80f, 0.20f), // yellow
        };

        /// <summary>White board, monochrome ink arrows, no dots.</summary>
        public void ApplyClassicColors()
        {
            BackgroundColor = Color.white;
            ShowDots = false;
            TrailDots = true;
            TrailDotColor = new Color(Ink.r, Ink.g, Ink.b, 0.35f);
            TrailDotSize = 0.1f;
            Palette = new[] { Ink };
            if (ConfettiPalette == null || ConfettiPalette.Length == 0) ConfettiPalette = new BoardTheme().ConfettiPalette;
        }
    }
}
