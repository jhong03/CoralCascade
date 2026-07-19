using System.Collections.Generic;
using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// All game audio, SYNTHESISED AT RUNTIME — no imported clips, same principle as
    /// PrimitiveSprites for art: the game must never require an asset it can generate.
    /// (It also sidesteps the licence/attribution question entirely for the store build.)
    ///
    /// Every sound is a short additive/noise burst with an envelope, built once on first
    /// use and cached. A small pooled voice bank plays them so a 12-bubble cascade doesn't
    /// spawn 12 AudioSources.
    ///
    /// FAIRNESS: audio is fired only from presentation code AFTER the deterministic layer
    /// has resolved (same contract as PopEffects), so it can never influence a shot.
    ///
    /// PAUSE: audio is NOT stopped on pause. The game pauses via timeScale = 0, which does
    /// not affect audio, and AudioListener.pause is deliberately left alone so the ambient
    /// bed keeps playing under the pause menu. Nothing fires one-shots while paused anyway.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx Instance { get; private set; }

        public enum Clip
        {
            Tap,        // UI press
            Fire,       // launcher
            Attach,     // bubble sticks without matching
            Pop,        // match
            PopBig,     // match of 6+
            Drop,       // detached bubbles falling
            Chain,      // secondary chain knock
            Thaw,       // ice breaking
            Tide,       // the board dropping
            Win,
            Lose,
            Pearl       // currency / purchase
        }

        private const int SampleRate = 44100;
        private const int Voices = 8;

        private readonly Dictionary<Clip, AudioClip> _cache = new Dictionary<Clip, AudioClip>();
        private AudioSource[] _voices;
        private int _nextVoice;
        private AudioSource _music;

        // Deterministic noise so a given clip sounds identical every session.
        private System.Random _noise = new System.Random(0xC0A1);

        private void Awake()
        {
            Instance = this;
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;      // 2D: the board is always on screen
                src.ignoreListenerPause = false;
                _voices[i] = src;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Fire a sound. Null-safe via Instance checks at the call sites.</summary>
        public static void Play(Clip clip, float volume = 1f, float pitch = 1f)
        {
            var self = Instance;
            if (self == null || !GameSettings.SfxEnabled) return;
            var src = self._voices[self._nextVoice];
            self._nextVoice = (self._nextVoice + 1) % Voices;
            src.pitch = Mathf.Clamp(pitch, 0.35f, 3f);
            src.volume = Mathf.Clamp01(volume) * 0.7f;
            src.clip = self.Get(clip);
            if (src.clip != null) src.Play();
        }

        private AudioClip Get(Clip clip)
        {
            AudioClip cached;
            if (_cache.TryGetValue(clip, out cached)) return cached;
            cached = Build(clip);
            _cache[clip] = cached;
            return cached;
        }

        // ---- Synthesis ---------------------------------------------------------------------

        private AudioClip Build(Clip clip)
        {
            switch (clip)
            {
                // A bubble pop: a fast downward pitch blip with a little noise transient.
                case Clip.Pop:     return Blip("Pop", 0.10f, 880f, 420f, 0.35f, 0.004f);
                case Clip.PopBig:  return Blip("PopBig", 0.18f, 1180f, 320f, 0.55f, 0.010f);
                // Firing is an upward "thup"; attaching is a soft low knock.
                case Clip.Fire:    return Blip("Fire", 0.07f, 300f, 620f, 0.22f, 0.002f);
                case Clip.Attach:  return Blip("Attach", 0.06f, 240f, 170f, 0.20f, 0.004f);
                // Falling debris: descending, wetter.
                case Clip.Drop:    return Blip("Drop", 0.22f, 520f, 120f, 0.30f, 0.012f);
                // A chain knock is the jackpot tier — brighter, with a ring.
                case Clip.Chain:   return Blip("Chain", 0.20f, 1400f, 700f, 0.45f, 0.006f);
                case Clip.Thaw:    return Blip("Thaw", 0.16f, 2100f, 1500f, 0.22f, 0.020f);
                // The tide is a low, ominous swell.
                case Clip.Tide:    return Blip("Tide", 0.55f, 150f, 70f, 0.40f, 0.030f);
                case Clip.Tap:     return Blip("Tap", 0.04f, 640f, 560f, 0.16f, 0.001f);
                case Clip.Pearl:   return Arpeggio("Pearl", new[] { 880f, 1320f }, 0.16f, 0.28f);
                // Win: a rising major triad. Lose: the same shape falling and detuned.
                case Clip.Win:     return Arpeggio("Win", new[] { 523f, 659f, 784f, 1047f }, 0.13f, 0.40f);
                case Clip.Lose:    return Arpeggio("Lose", new[] { 494f, 415f, 330f }, 0.17f, 0.36f);
                default:           return null;
            }
        }

        /// <summary>
        /// One enveloped tone gliding from <paramref name="f0"/> to <paramref name="f1"/>,
        /// with a touch of noise for the "wet" transient. A pure sine reads as a beep; the
        /// glide plus noise is what makes it read as a bubble.
        /// </summary>
        private AudioClip Blip(string name, float seconds, float f0, float f1,
                               float amp, float noiseAmt)
        {
            int n = Mathf.Max(1, (int)(SampleRate * seconds));
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float freq = Mathf.Lerp(f0, f1, t * t);          // fast early glide
                phase += 2f * Mathf.PI * freq / SampleRate;
                // Percussive envelope: near-instant attack, exponential decay.
                float env = Mathf.Exp(-5.5f * t) * (1f - Mathf.Exp(-90f * t));
                float noise = (float)(_noise.NextDouble() * 2.0 - 1.0) * noiseAmt * Mathf.Exp(-22f * t);
                data[i] = (Mathf.Sin(phase) * 0.75f + Mathf.Sin(phase * 2f) * 0.2f + noise) * env * amp;
            }
            return FromData(name, data);
        }

        /// <summary>
        /// A short sequence of tones for the win/lose/purchase stingers. Each note RINGS ON
        /// past its own slot into the notes after it — the first version confined every note
        /// to its slot, and since the envelope decays to ~4% within the slot, the result was
        /// four detached blips with audible silence between them instead of a flourish.
        /// </summary>
        private AudioClip Arpeggio(string name, float[] freqs, float noteSeconds, float amp)
        {
            int per = Mathf.Max(1, (int)(SampleRate * noteSeconds));
            int ring = per * 3;                       // how long a note keeps sounding
            var data = new float[per * (freqs.Length - 1) + ring];
            for (int k = 0; k < freqs.Length; k++)
            {
                float phase = 0f;
                int start = k * per;
                for (int i = 0; i < ring && start + i < data.Length; i++)
                {
                    float t = (float)i / ring;
                    phase += 2f * Mathf.PI * freqs[k] / SampleRate;
                    float env = Mathf.Exp(-3.4f * t) * (1f - Mathf.Exp(-60f * t));
                    data[start + i] +=
                        (Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 2.01f) * 0.18f) * env * amp;
                }
            }
            return FromData(name, data);
        }

        /// <summary>
        /// Packs samples into a clip. Normalises to a fixed peak so every sound arrives at a
        /// predictable level (the per-call volume argument is what expresses intent), and
        /// ramps the final few ms to zero so a clip can never end on a non-zero sample —
        /// that discontinuity is an audible click.
        ///
        /// fadeOut MUST be false for looping clips: the ramp would carve a hole at the seam.
        /// </summary>
        private static AudioClip FromData(string name, float[] data, bool fadeOut = true)
        {
            const float TargetPeak = 0.9f;
            float peak = 0.0001f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = TargetPeak / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;

            if (fadeOut)
            {
                int fade = Mathf.Min(data.Length, SampleRate / 300); // ~3ms
                for (int i = 0; i < fade; i++)
                    data[data.Length - 1 - i] *= (float)i / fade;
            }

            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ---- Ambient music bed ---------------------------------------------------------------

        /// <summary>
        /// A slow underwater pad: two detuned sines a fifth apart with a very slow tremolo,
        /// looped. Deliberately near-featureless — it should sit under an hour of play
        /// without becoming a melody anyone can get sick of.
        /// </summary>
        public static void SetMusic(bool on)
        {
            var self = Instance;
            if (self == null) return;
            if (!GameSettings.MusicEnabled) on = false;

            if (!on)
            {
                if (self._music != null) self._music.Stop();
                return;
            }
            if (self._music == null)
            {
                var go = new GameObject("Music");
                go.transform.SetParent(self.transform, false);
                self._music = go.AddComponent<AudioSource>();
                self._music.playOnAwake = false;
                self._music.loop = true;
                self._music.spatialBlend = 0f;
                self._music.volume = 0.13f;
                self._music.clip = BuildPad();
            }
            if (!self._music.isPlaying) self._music.Play();
        }

        private static AudioClip BuildPad()
        {
            const float seconds = 8f; // loops seamlessly: all partials complete whole cycles
            int n = (int)(SampleRate * seconds);
            var data = new float[n];
            // Frequencies chosen so each completes an integer number of cycles in 8s.
            float[] partials = { 55f, 82.5f, 110f, 164.5f };
            float[] gains = { 0.5f, 0.35f, 0.25f, 0.12f };
            for (int p = 0; p < partials.Length; p++)
            {
                float cycles = Mathf.Round(partials[p] * seconds);
                float freq = cycles / seconds;
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    // Slow tremolo, also an integer number of cycles so the loop is seamless.
                    float trem = 0.82f + 0.18f * Mathf.Sin(2f * Mathf.PI * (2f / seconds) * t + p);
                    data[i] += Mathf.Sin(2f * Mathf.PI * freq * t) * gains[p] * trem;
                }
            }
            // fadeOut: false — this one loops, and a fade would notch the seam.
            return FromData("Pad", data, fadeOut: false);
        }
    }
}
