using System.Collections.Generic;
using Arrows.Core;
using Arrows.View;
using NUnit.Framework;
using UnityEngine;

namespace Arrows.Tests
{
    public class ProgressAndFlowTests
    {
        private sealed class MemoryStore : IKeyValueStore
        {
            public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
            public int Saves;
            public int GetInt(string key, int defaultValue) => Values.TryGetValue(key, out int v) ? v : defaultValue;
            public void SetInt(string key, int value) => Values[key] = value;
            public void Save() => Saves++;
        }

        [Test]
        public void Progress_DefaultsToLevelOne_WithSettingsOn()
        {
            var progress = new ProgressService(new MemoryStore());
            Assert.AreEqual(1, progress.LevelNumber);
            Assert.IsTrue(progress.SoundEnabled);
            Assert.IsTrue(progress.HapticsEnabled);
        }

        [Test]
        public void Progress_CompleteLevel_PersistsAcrossInstances()
        {
            var store = new MemoryStore();
            var progress = new ProgressService(store);
            progress.CompleteLevel();
            progress.CompleteLevel();

            Assert.AreEqual(3, new ProgressService(store).LevelNumber);
            Assert.Greater(store.Saves, 0);
        }

        [Test]
        public void Progress_Settings_PersistAndNotify()
        {
            var store = new MemoryStore();
            var progress = new ProgressService(store);
            int notifications = 0;
            progress.SettingsChanged += () => notifications++;

            progress.SetSound(false);
            progress.SetSound(false); // no change, no event
            progress.SetHaptics(false);

            Assert.AreEqual(2, notifications);
            var reloaded = new ProgressService(store);
            Assert.IsFalse(reloaded.SoundEnabled);
            Assert.IsFalse(reloaded.HapticsEnabled);
        }

        [Test]
        public void Progress_CorruptLevelValue_IsClampedToOne()
        {
            var store = new MemoryStore();
            store.Values["arrows.level"] = -5;
            Assert.AreEqual(1, new ProgressService(store).LevelNumber);
        }

        [TestCase(1, 0)]
        [TestCase(30, 29)]
        [TestCase(31, 3)]  // loops, skipping the 3 tutorial levels
        [TestCase(57, 29)]
        [TestCase(58, 3)]
        public void LevelIndex_LoopsAfterLastLevel_SkippingTutorials(int number, int expected)
        {
            Assert.AreEqual(expected, LevelManager.IndexFor(number, 30, 3));
        }

        [Test]
        public void LevelIndex_SingleLevel_AlwaysZero()
        {
            Assert.AreEqual(0, LevelManager.IndexFor(5, 1, 3));
            Assert.AreEqual(-1, LevelManager.IndexFor(1, 0, 3));
        }

        [Test]
        public void Camera_AtFitSize_ClampReturnsFittedCenter_ForAnyDesiredCenter()
        {
            var board = new Rect(-5f, -5f, 10f, 10f);
            const float aspect = 9f / 16f, pad = 0.6f, top = 0.16f, bottom = 0.15f;
            float size = CameraController.FitOrthoSize(board.size, aspect, pad, top, bottom);

            var a = CameraController.ClampCenter(new Vector2(100f, 100f), size, aspect, board, pad, top, bottom);
            var b = CameraController.ClampCenter(new Vector2(-100f, -100f), size, aspect, board, pad, top, bottom);
            Assert.AreEqual(a.x, b.x, 1e-4f);
            Assert.AreEqual(a.y, b.y, 1e-4f);
        }

        [Test]
        public void Camera_ZoomedIn_CanPanButStaysOnBoard()
        {
            var board = new Rect(-15f, -15f, 30f, 30f);
            const float aspect = 9f / 16f;
            var c = CameraController.ClampCenter(new Vector2(1000f, 0f), 4f, aspect, board, 0.6f, 0f, 0f);

            Assert.Less(c.x, board.xMax);
            Assert.AreEqual(board.xMax - 4f * aspect + 0.6f, c.x, 1e-4f);
            var free = CameraController.ClampCenter(new Vector2(3f, -2f), 4f, aspect, board, 0.6f, 0f, 0f);
            Assert.AreEqual(new Vector2(3f, -2f), free);
        }
    }
}
