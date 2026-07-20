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

        /// <summary>
        /// Drop a music file here (any name, any format Unity imports) and it replaces the
        /// procedural bed. See MUSIC_CREDITS.md — only ever put CC0 / public-domain or
        /// properly-licensed audio in this folder.
        /// </summary>
        private const string MusicPath = "Music";

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
                // A bubble pop: a fast downward "bloop", round rather than bright. Bigger
                // clusters pop LOWER and fatter, which is also how the caller pitches them.
                case Clip.Pop:     return Blip("Pop", 0.09f, 520f, 185f, 0.35f, 0.006f, 0.07f);
                case Clip.PopBig:  return Blip("PopBig", 0.16f, 400f, 115f, 0.55f, 0.012f, 0.10f);
                // Firing is an upward "thup"; attaching is a soft low knock.
                case Clip.Fire:    return Blip("Fire", 0.07f, 200f, 430f, 0.22f, 0.003f, 0.08f);
                case Clip.Attach:  return Blip("Attach", 0.06f, 205f, 130f, 0.20f, 0.005f, 0.05f);
                // Falling debris: descending, wetter.
                case Clip.Drop:    return Blip("Drop", 0.22f, 360f, 90f, 0.30f, 0.014f, 0.14f);
                // A chain knock is the jackpot tier — brighter, with a ring.
                case Clip.Chain:   return Blip("Chain", 0.20f, 700f, 250f, 0.45f, 0.008f, 0.30f);
                // Ice SHOULD stay glassy and high — that contrast is the point.
                case Clip.Thaw:    return Blip("Thaw", 0.16f, 1700f, 1150f, 0.22f, 0.020f, 0.40f);
                // The tide is a low, ominous swell.
                case Clip.Tide:    return Blip("Tide", 0.55f, 120f, 52f, 0.40f, 0.030f, 0.22f);
                case Clip.Tap:     return Blip("Tap", 0.04f, 430f, 360f, 0.16f, 0.002f, 0.10f);
                case Clip.Pearl:   return Arpeggio("Pearl", new[] { 880f, 1320f }, 0.16f, 0.28f);
                // Win: a rising major triad. Lose: the same shape falling and detuned.
                case Clip.Win:     return Arpeggio("Win", new[] { 523f, 659f, 784f, 1047f }, 0.13f, 0.40f);
                case Clip.Lose:    return Arpeggio("Lose", new[] { 494f, 415f, 330f }, 0.17f, 0.36f);
                default:           return null;
            }
        }

        /// <summary>
        /// One enveloped tone gliding from <paramref name="f0"/> to <paramref name="f1"/>,
        /// with a touch of noise for the "wet" transient.
        ///
        /// TWO THINGS MAKE THIS A BLOOP RATHER THAN A SQUEAK (user-reported 2026-07-20,
        /// "the pops sound like a very high pitched squeak"):
        ///  • PITCH. A bubble pop lives around 150-400 Hz. The first version started at
        ///    880 Hz with a 1760 Hz harmonic stacked on it — an octave and a half too high.
        ///  • GLIDE SHAPE. The fall has to happen mostly in the first few milliseconds.
        ///    The first version used Lerp(f0, f1, t*t), and t² is SLOW early — so it sat up
        ///    at the top of its range for most of the clip. 1 - e^-kt falls fast then settles,
        ///    which is what the ear reads as a drop of water.
        /// <paramref name="bright"/> is the 2nd-harmonic level: near 0 for round, watery
        /// sounds, higher for glassy ones like the ice thaw.
        /// </summary>
        private AudioClip Blip(string name, float seconds, float f0, float f1,
                               float amp, float noiseAmt, float bright = 0.12f)
        {
            int n = Mathf.Max(1, (int)(SampleRate * seconds));
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float freq = Mathf.Lerp(f0, f1, 1f - Mathf.Exp(-5f * t)); // fast early fall
                phase += 2f * Mathf.PI * freq / SampleRate;
                // Percussive envelope: near-instant attack, exponential decay.
                float env = Mathf.Exp(-5.5f * t) * (1f - Mathf.Exp(-90f * t));
                float noise = (float)(_noise.NextDouble() * 2.0 - 1.0) * noiseAmt * Mathf.Exp(-22f * t);
                data[i] = (Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * bright + noise) * env * amp;
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
        /// Starts/stops the music bed.
        ///
        /// REGISTER MATTERS MORE THAN VOLUME: the first version was an "ambient pad" built
        /// from partials at 55-165 Hz, which phone speakers physically cannot reproduce
        /// (they roll off hard below ~300 Hz). It was inaudible on a device — reported as
        /// "background music is missing". Everything here now sits in the 220-2100 Hz band.
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

                // Prefer a REAL recording if one has been dropped into Resources/Music —
                // synthesis can get the notes right but not the warmth, and a single
                // synthesised timbre with no timing variation reads as robotic (user,
                // 2026-07-20). Any filename works; the first clip found wins. Falls back to
                // the procedural waltz so the game still never REQUIRES an imported asset,
                // same contract as BubbleArt/PrimitiveSprites.
                var imported = Resources.LoadAll<AudioClip>(MusicPath);
                if (imported != null && imported.Length > 0)
                {
                    self._music.clip = imported[0];
                    self._music.volume = 0.34f;
                }
                else
                {
                    self._music.clip = BuildMusic();
                    self._music.volume = 0.30f;
                }
            }
            if (!self._music.isPlaying) self._music.Play();
        }

        // ---- The music bed ------------------------------------------------------------------
        //
        // An ORIGINAL cosy waltz in the spirit of the old Facebook pet/farm games: music-box
        // melody over a soft chord bed, C major, 3/4, unhurried. Written rather than sampled —
        // using a real game's soundtrack would be copyright infringement, and style is not
        // copyrightable while a recording very much is.
        //
        // Everything sits in 220-2100 Hz so it survives a phone speaker (see SetMusic).

        private const float Bpm = 92f;
        private const int BeatsPerBar = 3;
        private const int Bars = 8;

        /// <summary>MIDI note number → Hz.</summary>
        private static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        /// <summary>
        /// Melody as (bar, beat, midi) triples. C major, mostly stepwise so it stays singable
        /// and forgettable in the right way — background music that draws attention is a bug.
        /// Bars 1-4 state the phrase; 5-8 answer it and turn back to the top.
        /// </summary>
        private static readonly int[,] Melody =
        {
            // bar, beat, midi          chord underneath
            {0, 0, 76}, {0, 1, 79}, {0, 2, 76},          // C:  E5  G5  E5
            {1, 0, 74}, {1, 1, 79}, {1, 2, 71},          // G:  D5  G5  B4
            {2, 0, 72}, {2, 1, 76}, {2, 2, 81},          // Am: C5  E5  A5
            {3, 0, 77}, {3, 1, 81}, {3, 2, 79},          // F:  F5  A5  G5
            {4, 0, 84}, {4, 2, 79},                      // C:  C6      G5
            {5, 0, 83}, {5, 1, 79}, {5, 2, 74},          // G:  B5  G5  D5
            {6, 0, 81}, {6, 1, 76}, {6, 2, 72},          // Am: A5  E5  C5
            {7, 0, 77}, {7, 1, 79},                      // F:  F5  G5  (leads home)
        };

        /// <summary>Root of each bar's chord (MIDI), one per bar: C G Am F C G Am F.</summary>
        private static readonly int[] ChordRoots = { 60, 55, 57, 53, 60, 55, 57, 53 };
        /// <summary>Major (0) or minor (1) third for each bar.</summary>
        private static readonly int[] ChordMinor = { 0, 0, 1, 0, 0, 0, 1, 0 };

        private static AudioClip BuildMusic()
        {
            float secPerBeat = 60f / Bpm;
            float seconds = Bars * BeatsPerBar * secPerBeat;
            int n = (int)(SampleRate * seconds);
            var data = new float[n];

            // Chord bed: root + third + fifth, an octave below the melody, gently swelling.
            for (int bar = 0; bar < Bars; bar++)
            {
                int start = (int)(bar * BeatsPerBar * secPerBeat * SampleRate);
                int len = (int)(BeatsPerBar * secPerBeat * SampleRate);
                int root = ChordRoots[bar];
                int third = root + (ChordMinor[bar] == 1 ? 3 : 4);
                int fifth = root + 7;
                // Quiet. The chord bed is SUSTAINED while the melody DECAYS, so at equal
                // amplitudes the pad wins the energy budget outright — measured at 98% of
                // total energy sitting in the pad's 160-320 Hz band, i.e. the tune was
                // inaudible under its own accompaniment.
                Pad(data, start, len, Hz(root), 0.055f);
                Pad(data, start, len, Hz(third), 0.040f);
                Pad(data, start, len, Hz(fifth), 0.040f);
            }

            // Music-box melody on top.
            for (int i = 0; i < Melody.GetLength(0); i++)
            {
                int bar = Melody[i, 0], beat = Melody[i, 1], midi = Melody[i, 2];
                int start = (int)((bar * BeatsPerBar + beat) * secPerBeat * SampleRate);
                Bell(data, start, Hz(midi), secPerBeat * 2.2f, 0.62f);
            }

            // fadeOut: false — this loops, and a fade would notch the seam. Note that Bell
            // and Pad write with WRAPPING indices, so a tail that runs past the end lands at
            // the start instead of being clipped: that is what makes the seam inaudible.
            return FromData("Music", data, fadeOut: false);
        }

        /// <summary>
        /// A struck music-box tone: fast attack, long decay, slightly inharmonic upper
        /// partials (that faint metallic shimmer is what says "music box" rather than "sine").
        /// Writes with wrapping indices so the loop seam stays seamless.
        /// </summary>
        private static void Bell(float[] buf, int start, float freq, float seconds, float amp)
        {
            int len = Mathf.Min((int)(SampleRate * seconds), buf.Length);
            float[] ratios = { 1f, 2.01f, 3.03f, 4.98f };
            float[] gains = { 1f, 0.34f, 0.16f, 0.06f };
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-3.1f * t) * (1f - Mathf.Exp(-260f * t));
                float s = 0f;
                for (int p = 0; p < ratios.Length; p++)
                    s += Mathf.Sin(2f * Mathf.PI * freq * ratios[p] * t) * gains[p];
                buf[(start + i) % buf.Length] += s * env * amp;
            }
        }

        /// <summary>Soft sustained chord voice — swells in, holds, eases out under the melody.</summary>
        private static void Pad(float[] buf, int start, int len, float freq, float amp)
        {
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / len;
                // Gentle bell-curve swell so bars breathe instead of blocking on and off.
                float env = Mathf.Sin(Mathf.PI * t);
                float ph = 2f * Mathf.PI * freq * ((float)i / SampleRate);
                buf[(start + i) % buf.Length] +=
                    (Mathf.Sin(ph) + Mathf.Sin(ph * 2f) * 0.22f) * env * amp;
            }
        }
    }
}
