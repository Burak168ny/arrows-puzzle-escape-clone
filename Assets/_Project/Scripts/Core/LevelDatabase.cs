using System.Collections.Generic;
using UnityEngine;

namespace Arrows.Core
{
    /// <summary>Ordered list of level JSON files. Filled by Tools/Arrows/Setup Game Scene or by hand.</summary>
    [CreateAssetMenu(menuName = "Arrows/Level Database", fileName = "LevelDatabase")]
    public sealed class LevelDatabase : ScriptableObject
    {
        public List<TextAsset> Levels = new List<TextAsset>();

        public int Count => Levels.Count;

        public TextAsset Get(int index) => index >= 0 && index < Levels.Count ? Levels[index] : null;
    }
}
