using System.Globalization;

namespace MonsterKlavye.Klavye;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>"#FF6A00" ya da "ff6a00" → Rgb. Geçersizse null (kullanıcı yazarken ara durumlar normal).</summary>
    public static Rgb? HexCoz(string? metin)
    {
        var s = metin?.Trim().TrimStart('#');
        if (s is not { Length: 6 } || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return null;
        return new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    /// <summary>Ton (0–360°) → tam doygun, tam parlak renk (HSV, S = V = 1).</summary>
    public static Rgb TondanRgb(double ton)
    {
        ton = ((ton % 360) + 360) % 360;
        var x = 1 - Math.Abs(ton / 60 % 2 - 1);
        var (r, g, b) = (int)(ton / 60) switch
        {
            0 => (1d, x, 0d),
            1 => (x, 1d, 0d),
            2 => (0d, 1d, x),
            3 => (0d, x, 1d),
            4 => (x, 0d, 1d),
            _ => (1d, 0d, x),
        };
        return new Rgb((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }

    /// <summary>Her kanalı k (0–1) ile çarpar: efektin parlaklığı.</summary>
    public Rgb Olcekle(double k)
    {
        k = Math.Clamp(k, 0, 1);
        return new((byte)Math.Round(R * k), (byte)Math.Round(G * k), (byte)Math.Round(B * k));
    }

    /// <summary>Rengin tonu (0–360°); gri tonlarda 0.</summary>
    public double Ton
    {
        get
        {
            double r = R / 255d, g = G / 255d, b = B / 255d;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            if (d == 0) return 0;
            var t = max == r ? (g - b) / d % 6 : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
            return (t * 60 + 360) % 360;
        }
    }
}
