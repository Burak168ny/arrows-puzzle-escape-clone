using System.Collections.Generic;
using Arrows.Utils;
using UnityEngine;

namespace Arrows.Core.Audio
{
    /// <summary>
    /// Plays one-shot effects through a small pool of AudioSources (round-robin, no allocation).
    /// Clips come from the <see cref="AudioLibrary"/>; missing ones use procedural placeholders.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioLibrary _library;
        [SerializeField, Range(1, 16)] private int _voices = 8;

        private AudioSource[] _sources;
        private int _next;
        private readonly Dictionary<SoundId, AudioClip> _fallbacks = new Dictionary<SoundId, AudioClip>();

        public bool Enabled { get; set; } = true;

        private void Awake()
        {
            _sources = new AudioSource[_voices];
            for (int i = 0; i < _voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _sources[i] = source;
            }

            if (_library == null || _library.UseProceduralFallback) BuildFallbacks();
        }

        /// <param name="pitch">Base pitch; the entry's random variance is added on top.</param>
        public void Play(SoundId id, float pitch = 1f, float volumeScale = 1f)
        {
            if (!Enabled) return;

            var entry = _library != null ? _library.Find(id) : null;
            AudioClip clip = null;
            if (entry != null && entry.Clips.Length > 0) clip = entry.Clips[Random.Range(0, entry.Clips.Length)];
            if (clip == null) _fallbacks.TryGetValue(id, out clip);
            if (clip == null) return;

            float volume = entry?.Volume ?? 0.8f;
            float variance = entry?.PitchVariance ?? 0.05f;

            var source = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            source.pitch = pitch + Random.Range(-variance, variance);
            source.PlayOneShot(clip, volume * volumeScale);
        }

        private void BuildFallbacks()
        {
            _fallbacks[SoundId.Button] = ProceduralAudio.Create(ProceduralAudio.Kind.Tick);
            _fallbacks[SoundId.Tap] = _fallbacks[SoundId.Button];
            _fallbacks[SoundId.Exit] = ProceduralAudio.Create(ProceduralAudio.Kind.Swoosh);
            _fallbacks[SoundId.Bump] = ProceduralAudio.Create(ProceduralAudio.Kind.Thud);
            _fallbacks[SoundId.Hint] = ProceduralAudio.Create(ProceduralAudio.Kind.Chime);
            _fallbacks[SoundId.Win] = ProceduralAudio.Create(ProceduralAudio.Kind.Fanfare);
            _fallbacks[SoundId.Fail] = ProceduralAudio.Create(ProceduralAudio.Kind.Sad);
        }

        private void OnDestroy()
        {
            var destroyed = new HashSet<AudioClip>();
            foreach (var clip in _fallbacks.Values)
                if (clip != null && destroyed.Add(clip)) Destroy(clip);
        }
    }
}
