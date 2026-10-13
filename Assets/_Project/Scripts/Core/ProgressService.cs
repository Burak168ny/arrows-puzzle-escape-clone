using System;
using UnityEngine;

namespace Arrows.Core
{
    /// <summary>Tiny persistence abstraction so progress logic can be tested without PlayerPrefs.</summary>
    public interface IKeyValueStore
    {
        int GetInt(string key, int defaultValue);
        void SetInt(string key, int value);
        void Save();
    }

    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    /// <summary>Player progress and settings. The level number is 1-based and keeps growing past the last level.</summary>
    public sealed class ProgressService
    {
        private const string LevelKey = "arrows.level";
        private const string SoundKey = "arrows.sound";
        private const string HapticsKey = "arrows.haptics";

        private readonly IKeyValueStore _store;

        public int LevelNumber { get; private set; }
        public bool SoundEnabled { get; private set; }
        public bool HapticsEnabled { get; private set; }

        public event Action SettingsChanged;

        public ProgressService(IKeyValueStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            LevelNumber = Math.Max(1, _store.GetInt(LevelKey, 1));
            SoundEnabled = _store.GetInt(SoundKey, 1) != 0;
            HapticsEnabled = _store.GetInt(HapticsKey, 1) != 0;
        }

        /// <summary>Marks the current level as beaten and moves to the next one.</summary>
        public void CompleteLevel()
        {
            LevelNumber++;
            _store.SetInt(LevelKey, LevelNumber);
            _store.Save();
        }

        public void SetSound(bool enabled)
        {
            if (SoundEnabled == enabled) return;
            SoundEnabled = enabled;
            _store.SetInt(SoundKey, enabled ? 1 : 0);
            _store.Save();
            SettingsChanged?.Invoke();
        }

        public void SetHaptics(bool enabled)
        {
            if (HapticsEnabled == enabled) return;
            HapticsEnabled = enabled;
            _store.SetInt(HapticsKey, enabled ? 1 : 0);
            _store.Save();
            SettingsChanged?.Invoke();
        }
    }
}
