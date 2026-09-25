using UnityEngine;

/// <summary>
/// The demons' voices, written as samples at runtime like the rest of the game's sound. Each demon
/// has its own calls: a restless sound while it wanders, a few snarls or grunts for when it notices
/// something (one picked at random each time), and a scream for when it catches you. The voices
/// share nothing, so a player learns who is near from the sound alone.
/// </summary>
public static class DemonSounds
{
    public enum Voice { Groaner, Whisperer, Clicker, Listener }

    public struct Set
    {
        public AudioClip idle;
        public AudioClip[] notices;
        public AudioClip scream;
    }

    const int SampleRate = 44100;
    static Set[] cache;

    public static Set For(Voice voice)
    {
        if (cache == null) cache = new Set[4];
        int index = (int)voice;
        if (cache[index].idle != null) return cache[index];

        var random = new System.Random(1337 + index * 101);
        Set set;
        switch (voice)
        {
            case Voice.Groaner:
                set.idle = Groan("Demon_Groan", random);
                set.notices = new[]
                {
                    Snarl("Demon_Groan_Snarl", random),
                    Snarl("Demon_Groan_Deep", random, 45f, 80f, 1.3f, 18f),
                    Grunt("Demon_Groan_Grunt", random, 60f, new[] { 0f }),
                };
                set.scream = Scream("Demon_Groan_Scream", random, 92f, 1.3f, 0.6f);
                break;
            case Voice.Whisperer:
                set.idle = Whisper("Demon_Whisper", random);
                set.notices = new[] { Snarl("Demon_Whisper_Snarl", random, 100f, 170f, 0.5f, 34f) };
                set.scream = Scream("Demon_Whisper_Scream", random, 330f, 1.1f, 0.35f);
                break;
            case Voice.Clicker:
                set.idle = Clicks("Demon_Click", random, 14, 0.07f, 1800f);
                set.notices = new[]
                {
                    Grunt("Demon_Click_Grunt", random),
                    Grunt("Demon_Click_Grunts", random, 70f, new[] { 0f, 0.42f, 0.84f }),
                };
                set.scream = Scream("Demon_Click_Scream", random, 175f, 1.2f, 0.5f);
                break;
            default:
                set.idle = Sniff("Demon_Listener", random);
                set.notices = new[] { Snarl("Demon_Listener_Snarl", random, 130f, 60f, 1.0f, 22f) };
                set.scream = Scream("Demon_Listener_Scream", random, 58f, 1.6f, 0.8f);
                break;
        }
        cache[index] = set;
        return set;
    }

    /// <summary>A long wet groan: two detuned saws a tritone apart, wobbling, filtered dark.</summary>
    static AudioClip Groan(string name, System.Random random)
    {
        const float length = 2.2f;
        var data = new float[Mathf.CeilToInt(length * SampleRate)];
        float a = 0f, b = 0f, c = 0f, low = 0f, lowCo = Coefficient(520f);
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float bend = 1f + 0.06f * Mathf.Sin(t * 4.1f) - 0.18f * (t / length);
            float f = 62f * bend;
            a += f / SampleRate;
            b += f * 1.012f / SampleRate;
            c += f * 1.414f / SampleRate; // the tritone that makes it wrong
            float raw = Saw(a) + Saw(b) + 0.5f * Saw(c) + 0.35f * Noise(random);
            // A throat that opens and closes.
            float throat = 0.55f + 0.45f * Mathf.Sin(t * 2.3f + 0.7f * Mathf.Sin(t * 9f));
            low += lowCo * throat * (raw - low);
            data[i] = low * Swell(t, length, 0.35f, 0.7f);
        }
        return Build(name, Normalise(data, 0.8f));
    }

    /// <summary>Breath through teeth that flutters, like something saying words you cannot catch.</summary>
    static AudioClip Whisper(string name, System.Random random)
    {
        const float length = 1.9f;
        var data = new float[Mathf.CeilToInt(length * SampleRate)];
        float band = 0f, low = 0f;
        float hiCo = Coefficient(5200f), loCo = Coefficient(1400f);
        float syllable = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float n = Noise(random);
            low += loCo * (n - low);
            band += hiCo * (n - band);
            float hiss = band - low; // band-passed noise, the "s" and "sh" of a whisper
            syllable += (5.5f + 3f * Mathf.Sin(t * 1.7f)) / SampleRate;
            float mouth = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * syllable)), 3f);
            data[i] = hiss * (0.25f + mouth) * Swell(t, length, 0.2f, 0.5f);
        }
        return Build(name, Normalise(data, 0.7f));
    }

    /// <summary>Bone on bone: a run of hard clicks at an uneven pace, then a short rattle.</summary>
    static AudioClip Clicks(string name, System.Random random, int count, float gap, float pitch)
    {
        float length = count * gap * 1.4f + 0.5f;
        var data = new float[Mathf.CeilToInt(length * SampleRate)];
        float at = 0.02f;
        for (int k = 0; k < count; k++)
        {
            float f = pitch * (0.8f + 0.4f * (float)random.NextDouble());
            AddClick(data, at, f, 0.9f - 0.4f * k / count, random);
            at += gap * (0.6f + 0.8f * (float)random.NextDouble());
        }
        // The rattle in the throat after the clicks.
        float phase = 0f;
        int start = Mathf.RoundToInt(at * SampleRate);
        for (int i = start; i < data.Length; i++)
        {
            float t = (i - start) / (float)SampleRate;
            phase += 70f / SampleRate;
            float pulse = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * t * 31f));
            data[i] += (Saw(phase) * 0.5f + Noise(random) * 0.5f) * pulse * 0.35f * Mathf.Exp(-t / 0.12f);
        }
        return Build(name, Normalise(data, 0.8f));
    }

    /// <summary>The blind one smelling the air: three quick sniffs and a wet tongue click.</summary>
    static AudioClip Sniff(string name, System.Random random)
    {
        const float length = 1.5f;
        var data = new float[Mathf.CeilToInt(length * SampleRate)];
        float band = 0f, low = 0f, hiCo = Coefficient(3000f), loCo = Coefficient(600f);
        float[] starts = { 0.05f, 0.24f, 0.4f };
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float n = Noise(random);
            low += loCo * (n - low);
            band += hiCo * (n - band);
            float env = 0f;
            foreach (float s in starts)
            {
                float u = t - s;
                if (u > 0f && u < 0.13f) env += Mathf.Sin(Mathf.PI * u / 0.13f);
            }
            data[i] = (band - low) * env * 0.9f;
        }
        AddClick(data, 0.95f, 700f, 1f, random);
        AddClick(data, 1.05f, 520f, 0.7f, random);
        return Build(name, Normalise(data, 0.75f));
    }

    /// <summary>A throat snarl: a low buzzing growl that rattles and climbs, then cuts off.</summary>
    public static AudioClip Snarl(string name, System.Random random) => Snarl(name, random, 70f, 120f, 0.9f, 26f);

    /// <summary>A snarl from one pitch to another; rattleRate is how fast the throat chops it.</summary>
    public static AudioClip Snarl(string name, System.Random random, float from, float to, float length, float rattleRate)
    {
        var data = new float[Mathf.CeilToInt(length * SampleRate)];
        float a = 0f, b = 0f, low = 0f, lowCo = Coefficient(900f);
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float f = Mathf.Lerp(from, to, Mathf.Pow(t / length, 0.7f));
            a += f / SampleRate;
            b += f * 1.5f * 1.01f / SampleRate;
            // The rattle: the growl chopped about 28 times a second, unevenly.
            float rattle = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Mathf.PI * t * (rattleRate + 6f * Mathf.Sin(t * 7f))));
            float raw = Saw(a) + 0.6f * Saw(b) + 0.4f * Noise(random);
            low += lowCo * (raw - low);
            data[i] = (float)System.Math.Tanh(low * rattle * 2.2f) * Swell(t, length, 0.05f, 0.2f);
        }
        return Build(name, Normalise(data, 0.85f));
    }

    /// <summary>A short deep grunt, two of them, like something clearing its throat to hunt.</summary>
    public static AudioClip Grunt(string name, System.Random random) => Grunt(name, random, 95f, new[] { 0f, 0.36f });

    /// <summary>Grunts at the given pitch, one starting at each time in starts.</summary>
    public static AudioClip Grunt(string name, System.Random random, float pitch, float[] starts)
    {
        float length = starts[starts.Length - 1] + 0.4f;
        var data = new float[Mathf.CeilToInt(length * SampleRate)];
        foreach (float start in starts)
        {
            float a = 0f, low = 0f, lowCo = Coefficient(600f);
            int first = Mathf.RoundToInt(start * SampleRate);
            for (int i = first; i < data.Length; i++)
            {
                float t = (i - first) / (float)SampleRate;
                if (t > 0.3f) break;
                a += (pitch - 60f * t) / SampleRate;
                low += lowCo * (Saw(a) + 0.5f * Noise(random) - low);
                data[i] += low * Mathf.Sin(Mathf.PI * t / 0.3f);
            }
        }
        return Build(name, Normalise(data, 0.85f));
    }

    /// <summary>
    /// The scream when it catches you: a cluster of detuned saws a semitone and a tritone apart, sliding down,
    /// with noise in it, clipped hard so it tears on small speakers.
    /// </summary>
    static AudioClip Scream(string name, System.Random random, float pitch, float length, float roughness)
    {
        var data = new float[Mathf.CeilToInt(length * SampleRate)];
        float[] ratios = { 1f, 1.007f, 1.059f, 1.414f, 2.02f };
        var phases = new float[ratios.Length];
        float low = 0f, lowCo = Coefficient(3200f);
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float slide = Mathf.Lerp(2.2f, 1f, Mathf.Clamp01(t / 0.12f)) * Mathf.Lerp(1f, 0.72f, t / length);
            float vibrato = 1f + 0.03f * Mathf.Sin(t * 38f);
            float sum = 0f;
            for (int k = 0; k < ratios.Length; k++)
            {
                phases[k] += pitch * ratios[k] * slide * vibrato / SampleRate;
                sum += Saw(phases[k]);
            }
            sum += Noise(random) * roughness * 3f;
            low += lowCo * (sum - low);
            data[i] = (float)System.Math.Tanh(low * 1.6f) * Swell(t, length, 0.01f, 0.55f);
        }
        return Build(name, Normalise(data, 0.9f));
    }

    static void AddClick(float[] data, float start, float frequency, float weight, System.Random random)
    {
        int first = Mathf.RoundToInt(start * SampleRate);
        for (int i = first; i < data.Length; i++)
        {
            float t = (i - first) / (float)SampleRate;
            if (t > 0.03f) break;
            float env = Mathf.Exp(-t / 0.004f);
            data[i] += (Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.6f * Noise(random)) * env * weight;
        }
    }

    /// <summary>Rises over attack, holds, and falls away over the last release share.</summary>
    static float Swell(float t, float length, float attack, float releaseShare)
    {
        float up = Mathf.Clamp01(t / attack);
        float releaseStart = length * (1f - releaseShare);
        float down = t < releaseStart ? 1f : 1f - (t - releaseStart) / (length - releaseStart);
        return up * Mathf.Clamp01(down);
    }

    static float[] Normalise(float[] data, float level)
    {
        float peak = 0f;
        for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        if (peak <= 0f) return data;
        float gain = level / peak;
        for (int i = 0; i < data.Length; i++) data[i] *= gain;
        return data;
    }

    static float Noise(System.Random random) => (float)(random.NextDouble() * 2.0 - 1.0);

    static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase)) - 1f;

    static float Coefficient(float cutoff)
    {
        float dt = 1f / SampleRate;
        float rc = 1f / (2f * Mathf.PI * cutoff);
        return dt / (rc + dt);
    }

    static AudioClip Build(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Reload Domain is off: drop clips the last play session made.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => cache = null;
}
