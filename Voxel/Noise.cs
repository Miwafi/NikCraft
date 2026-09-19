namespace NikCraft.Voxel;

/// <summary>Classic Perlin noise with fBm helpers. Thread safe (read-only after construction).</summary>
public sealed class Noise
{
    private readonly int[] _permutation = new int[512];

    public Noise(int seed)
    {
        var values = new int[256];
        for (int i = 0; i < 256; i++)
        {
            values[i] = i;
        }

        var rng = new Random(seed);
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        for (int i = 0; i < 512; i++)
        {
            _permutation[i] = values[i & 255];
        }
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Grad2(int hash, float x, float y) => (hash & 7) switch
    {
        0 => x + y,
        1 => -x + y,
        2 => x - y,
        3 => -x - y,
        4 => x,
        5 => -x,
        6 => y,
        _ => -y,
    };

    private static float Grad3(int hash, float x, float y, float z) => (hash & 15) switch
    {
        0 => x + y,
        1 => -x + y,
        2 => x - y,
        3 => -x - y,
        4 => x + z,
        5 => -x + z,
        6 => x - z,
        7 => -x - z,
        8 => y + z,
        9 => -y + z,
        10 => y - z,
        11 => -y - z,
        12 => y + x,
        13 => -y + z,
        14 => y - x,
        _ => -y - z,
    };

    /// <summary>Returns roughly -1..1.</summary>
    public float Noise2D(float x, float y)
    {
        int xi = (int)MathF.Floor(x);
        int yi = (int)MathF.Floor(y);
        float xf = x - xi;
        float yf = y - yi;
        xi &= 255;
        yi &= 255;

        float u = Fade(xf);
        float v = Fade(yf);

        int a = _permutation[xi] + yi;
        int b = _permutation[xi + 1] + yi;

        float x1 = Lerp(Grad2(_permutation[a], xf, yf), Grad2(_permutation[b], xf - 1f, yf), u);
        float x2 = Lerp(Grad2(_permutation[a + 1], xf, yf - 1f), Grad2(_permutation[b + 1], xf - 1f, yf - 1f), u);

        return Math.Clamp(Lerp(x1, x2, v) * 1.4f, -1f, 1f);
    }

    /// <summary>Returns roughly -1..1.</summary>
    public float Noise3D(float x, float y, float z)
    {
        int xi = (int)MathF.Floor(x);
        int yi = (int)MathF.Floor(y);
        int zi = (int)MathF.Floor(z);
        float xf = x - xi;
        float yf = y - yi;
        float zf = z - zi;
        xi &= 255;
        yi &= 255;
        zi &= 255;

        float u = Fade(xf);
        float v = Fade(yf);
        float w = Fade(zf);

        int a = _permutation[xi] + yi;
        int b = _permutation[xi + 1] + yi;
        int aa = _permutation[a] + zi;
        int ab = _permutation[a + 1] + zi;
        int ba = _permutation[b] + zi;
        int bb = _permutation[b + 1] + zi;

        float x1 = Lerp(Grad3(_permutation[aa], xf, yf, zf), Grad3(_permutation[ba], xf - 1f, yf, zf), u);
        float x2 = Lerp(Grad3(_permutation[ab], xf, yf - 1f, zf), Grad3(_permutation[bb], xf - 1f, yf - 1f, zf), u);
        float y1 = Lerp(x1, x2, v);

        x1 = Lerp(Grad3(_permutation[aa + 1], xf, yf, zf - 1f), Grad3(_permutation[ba + 1], xf - 1f, yf, zf - 1f), u);
        x2 = Lerp(Grad3(_permutation[ab + 1], xf, yf - 1f, zf - 1f), Grad3(_permutation[bb + 1], xf - 1f, yf - 1f, zf - 1f), u);
        float y2 = Lerp(x1, x2, v);

        return Math.Clamp(Lerp(y1, y2, w) * 1.2f, -1f, 1f);
    }

    public float Fbm2D(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
    {
        float sum = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float normalization = 0f;

        for (int i = 0; i < octaves; i++)
        {
            sum += Noise2D(x * frequency, y * frequency) * amplitude;
            normalization += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        return normalization > 0f ? sum / normalization : 0f;
    }

    public float Fbm3D(float x, float y, float z, int octaves, float lacunarity = 2f, float gain = 0.5f)
    {
        float sum = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float normalization = 0f;

        for (int i = 0; i < octaves; i++)
        {
            sum += Noise3D(x * frequency, y * frequency, z * frequency) * amplitude;
            normalization += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        return normalization > 0f ? sum / normalization : 0f;
    }
}
