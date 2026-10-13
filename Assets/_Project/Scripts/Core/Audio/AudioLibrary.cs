using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arrows.Core.Audio
{
    public enum SoundId
    {
        Button,
        Tap,
        Exit,
        Bump,
        Hint,
        Win,
        Fail,
    }

    /// <summary>Maps sound ids to clips. Empty entries fall back to procedural placeholders.</summary>
    [CreateAssetMenu(menuName = "Arrows/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SoundId Id;
            [Tooltip("One is picked at random each time.")]
            public AudioClip[] Clips = Array.Empty<AudioClip>();
            [Range(0f, 1f)] public float Volume = 0.8f;
            [Range(0f, 0.5f)] public float PitchVariance = 0.05f;
        }

        [Tooltip("Use generated placeholder sounds for entries without clips.")]
        public bool UseProceduralFallback = true;

        public List<Entry> Entries = new List<Entry>();

        public Entry Find(SoundId id)
        {
            foreach (var e in Entries)
                if (e.Id == id) return e;
            return null;
        }

        /// <summary>Adds an entry for every id that is missing (used by the setup tool).</summary>
        public void EnsureAllIds()
        {
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
                if (Find(id) == null) Entries.Add(new Entry { Id = id });
        }
    }
}
