using UnityEngine;

/// <summary>
/// The game's whole soundtrack, written as samples at runtime so no audio files ship with it.
/// Three voices, one per story beat: a wooden thunk like a cash drawer closing, two sawtooth
/// notes climbing for the moment you are noticed, and one flat sine with no shine on it for
/// the moment the debt is called in.
/// </summary>
public static class ProceduralTones
{
    const int SampleRate = 44100;

    /// <summary>Low and blunt, swept 110 Hz down to 48 Hz and filtered dark. The drawer closing.</summary>
    public static AudioClip Thunk()
    {
        const float length = 0.26f;
        int count = Mathf.CeilToInt(length * SampleRate);
        var data = new float[count];

        float phase = 0f;
        float filtered = 0f;
        float cutoffCoefficient = Coefficient(700f);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float frequency = 110f * Mathf.Pow(48f / 110f, Mathf.Clamp01(t / 0.11f));
            phase += frequency / SampleRate;
            float raw = Triangle(phase);
            filtered += cutoffCoefficient * (raw - filtered);
            data[i] = filtered * 0.34f * Mathf.Exp(-t / 0.055f);
        }

        return Build("Tone_Thunk", data);
    }

    /// <summary>Two sawtooth notes a fifth and a bit apart, the second arriving late. Being noticed.</summary>
    public static AudioClip Sting()
    {
        const float length = 0.56f;
        int count = Mathf.CeilToInt(length * SampleRate);
        var data = new float[count];

        AddSaw(data, 196.00f, 0f, 0.4f, 0.13f);
        AddSaw(data, 311.13f, 0.11f, 0.4f, 0.13f);

        return Build("Tone_Sting", data);
    }

    /// <summary>One sine held flat then let go. Nothing decorative about it.</summary>
    public static AudioClip Flat(string name, float frequency, float level)
    {
        const float length = 0.95f;
        int count = Mathf.CeilToInt(length * SampleRate);
        var data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float envelope;
            if (t < 0.02f) envelope = t / 0.02f;            // just enough attack to avoid a click
            else if (t < 0.5f) envelope = 1f;
            else envelope = Mathf.Max(0f, 1f - (t - 0.5f) / 0.4f);

            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * level * envelope;
        }

        return Build(name, data);
    }

    /// <summary>A dull filtered click. Footsteps, and the coin hitting the floor.</summary>
    public static AudioClip Click(string name, float frequency, float length, float level)
    {
        int count = Mathf.CeilToInt(length * SampleRate);
        var data = new float[count];
        float filtered = 0f;
        float k = Coefficient(frequency * 3f);
        int seed = name.Length * 7919;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            // A deterministic pseudo-noise burst; Random is avoided so clips are identical every run.
            seed = seed * 1103515245 + 12345;
            float noise = ((seed >> 16) & 0x7fff) / 16383.5f - 1f;
            filtered += k * (noise - filtered);
            data[i] = filtered * level * Mathf.Exp(-t / (length * 0.22f));
        }
        return Build(name, data);
    }

    /// <summary>A soft low thud with a pitch drop. The heartbeat, and the moment you are caught.</summary>
    public static AudioClip Thump(string name, float frequency, float length, float level)
    {
        int count = Mathf.CeilToInt(length * SampleRate);
        var data = new float[count];
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float f = frequency * Mathf.Pow(0.45f, t / length); // drops as it decays, like a real thud
            phase += f / SampleRate;
            float envelope = Mathf.Exp(-t / (length * 0.3f));
            if (t < 0.004f) envelope *= t / 0.004f;
            data[i] = Mathf.Sin(2f * Mathf.PI * phase) * level * envelope;
        }
        return Build(name, data);
    }

    /// <summary>
    /// A lub-dub. A bare sine at heartbeat pitch vanishes on laptop speakers, so each thump carries
    /// two overtones and is soft-clipped, which is what makes it felt in the chest rather than heard.
    /// </summary>
    public static AudioClip HeartBeat(string name, float level)
    {
        const float lubFrequency = 58f;
        const float dubFrequency = 49f;
        const float dubDelay = 0.17f;
        const float thumpLength = 0.24f;

        float length = dubDelay + thumpLength + 0.05f;
        int count = Mathf.CeilToInt(length * SampleRate);
        var data = new float[count];

        AddThump(data, 0f, lubFrequency, thumpLength, 1f);
        AddThump(data, dubDelay, dubFrequency, thumpLength, 0.7f);

        float peak = 0f;
        for (int i = 0; i < count; i++)
        {
            data[i] = (float)System.Math.Tanh(data[i] * 1.8f);
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        float gain = peak > 0f ? level / peak : 0f;
        for (int i = 0; i < count; i++) data[i] *= gain;
        return Build(name, data);
    }

    static void AddThump(float[] data, float start, float frequency, float length, float weight)
    {
        int first = Mathf.RoundToInt(start * SampleRate);
        float phase = 0f;
        for (int i = first; i < data.Length; i++)
        {
            float t = (i - first) / (float)SampleRate;
            if (t > length) break;
            float f = frequency * Mathf.Pow(0.6f, t / length);
            phase += f / SampleRate;
            float envelope = Mathf.Exp(-t / (length * 0.28f));
            if (t < 0.003f) envelope *= t / 0.003f;
            float angle = 2f * Mathf.PI * phase;
            float wave = Mathf.Sin(angle) + 0.5f * Mathf.Sin(2f * angle) + 0.25f * Mathf.Sin(3f * angle);
            data[i] += wave * envelope * weight;
        }
    }

    /// <summary>A run of rising sine notes. Picking the jewel up, and getting out with it.</summary>
    public static AudioClip Arpeggio(string name, float[] frequencies, float noteSeconds, float level)
    {
        float length = noteSeconds * frequencies.Length + 0.35f;
        int count = Mathf.CeilToInt(length * SampleRate);
        var data = new float[count];

        for (int n = 0; n < frequencies.Length; n++)
        {
            int start = Mathf.RoundToInt(n * noteSeconds * SampleRate);
            for (int i = start; i < count; i++)
            {
                float t = (i - start) / (float)SampleRate;
                float envelope = Mathf.Exp(-t / 0.16f);
                if (t < 0.005f) envelope *= t / 0.005f;
                if (envelope < 0.0005f) break;
                data[i] += Mathf.Sin(2f * Mathf.PI * frequencies[n] * t) * level * envelope;
            }
        }

        // The notes overlap, so make sure the sum never clips.
        float peak = 0f;
        for (int i = 0; i < count; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        if (peak > 0.95f)
        {
            float scale = 0.95f / peak;
            for (int i = 0; i < count; i++) data[i] *= scale;
        }
        return Build(name, data);
    }

    static void AddSaw(float[] data, float frequency, float startSeconds, float decaySeconds, float level)
    {
        int start = Mathf.RoundToInt(startSeconds * SampleRate);
        float phase = 0f;

        for (int i = start; i < data.Length; i++)
        {
            float t = (i - start) / (float)SampleRate;
            phase += frequency / SampleRate;
            float attack = t < 0.012f ? t / 0.012f : 1f;
            float envelope = attack * Mathf.Exp(-t / (decaySeconds * 0.35f));
            data[i] += Saw(phase) * level * envelope;
        }
    }

    /// <summary>-1..1 sawtooth from a phase in turns.</summary>
    static float Saw(float phase)
    {
        return 2f * (phase - Mathf.Floor(phase)) - 1f;
    }

    /// <summary>-1..1 triangle from a phase in turns.</summary>
    static float Triangle(float phase)
    {
        float wrapped = phase - Mathf.Floor(phase);
        return 4f * Mathf.Abs(wrapped - 0.5f) - 1f;
    }

    /// <summary>One-pole low pass coefficient for a cutoff in hertz at the fixed sample rate.</summary>
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
}
