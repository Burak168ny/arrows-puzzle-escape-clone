using System;
using Arrows.Model;
using Arrows.Model.Serialization;

namespace Arrows.Core
{
    /// <summary>
    /// Maps the player's ever-growing level number onto the level database and loads it.
    /// After the last level the sequence loops, skipping the first <c>loopStartIndex</c> (tutorial) levels.
    /// </summary>
    public sealed class LevelManager
    {
        private readonly LevelDatabase _database;
        private readonly int _loopStartIndex;

        public int Count => _database != null ? _database.Count : 0;

        public LevelManager(LevelDatabase database, int loopStartIndex = 0)
        {
            _database = database;
            _loopStartIndex = loopStartIndex;
        }

        public LevelModel Load(int levelNumber)
        {
            int index = IndexFor(levelNumber, Count, _loopStartIndex);
            var asset = _database != null ? _database.Get(index) : null;
            if (asset == null) throw new InvalidOperationException($"No level asset at index {index}.");
            return LevelModel.FromData(LevelSerializer.FromJson(asset.text));
        }

        /// <summary>Database index for a 1-based level number.</summary>
        public static int IndexFor(int levelNumber, int count, int loopStartIndex)
        {
            if (count <= 0) return -1;
            int i = Math.Max(0, levelNumber - 1);
            if (i < count) return i;

            int loopStart = Math.Clamp(loopStartIndex, 0, count - 1);
            return loopStart + (i - count) % (count - loopStart);
        }
    }
}
