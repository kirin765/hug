using UnityEngine;

namespace Hug
{
    /// <summary>Tiny synthesised sound effects — no audio assets needed.</summary>
    public static class Sound
    {
        public enum Kind
        {
            Click,
            Hug,
            Pop,
            Error,
            Dash,
        }

        const int SampleRate = 44100;

        static AudioSource _source;
        static AudioClip _click;
        static AudioClip _hug;
        static AudioClip _pop;
        static AudioClip _error;
        static AudioClip _dash;
        public static bool Enabled = true;

        static void EnsureSource()
        {
            if (_source != null) return;
            var go = new GameObject("Audio");
            Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0.5f;
        }

        public static void Play(Kind kind)
        {
            if (!Enabled) return;
            EnsureSource();

            switch (kind)
            {
                case Kind.Click:
                    _click ??= Make("click", 0.07f, new[] { 1200f }, new[] { 1f }, 0.35f);
                    _source.PlayOneShot(_click, 0.35f);
                    break;
                case Kind.Hug:
                    _hug ??= Make("hug", 0.45f, new[] { 659.25f, 987.77f, 1318.5f }, new[] { 1f, 0.8f, 0.45f }, 0.30f);
                    _source.PlayOneShot(_hug, 0.5f);
                    break;
                case Kind.Pop:
                    _pop ??= Make("pop", 0.16f, new[] { 520f, 780f }, new[] { 1f, 0.6f }, 0.30f);
                    _source.PlayOneShot(_pop, 0.45f);
                    break;
                case Kind.Error:
                    _error ??= Make("error", 0.18f, new[] { 180f }, new[] { 1f }, 0.25f);
                    _source.PlayOneShot(_error, 0.4f);
                    break;
                case Kind.Dash:
                    _dash ??= MakeWhoosh("dash", 0.22f, 0.22f);
                    _source.PlayOneShot(_dash, 0.35f);
                    break;
            }
        }

        /// <summary>Short filtered-noise sweep for the dash.</summary>
        static AudioClip MakeWhoosh(string name, float duration, float amplitude)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];
            float previous = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.65f));
                float noise = Random.Range(-1f, 1f);
                // one-pole low pass, opening up as the sweep progresses
                float cutoff = Mathf.Lerp(0.06f, 0.5f, t);
                previous = Mathf.Lerp(previous, noise, cutoff);
                data[i] = previous * env * amplitude;
            }

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Mixes a few decaying sine partials into a little blip.</summary>
        static AudioClip Make(string name, float duration, float[] freqs, float[] gains, float amplitude)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * (6f / duration)) * Mathf.Clamp01(t * 400f);
                float v = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    float detune = 1f + 0.004f * f;
                    v += Mathf.Sin(2f * Mathf.PI * freqs[f] * detune * t) * gains[f];
                }
                data[i] = v * env * amplitude;
            }

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
