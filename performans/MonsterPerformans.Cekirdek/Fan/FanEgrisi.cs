namespace MonsterPerformans.Cekirdek.Fan;

/// <summary>
/// EC fan eğrisinin değiştirilebilen 2. ve 3. noktası (°C, fan %). 1. ve 4. nokta EC'de sabit,
/// komut 14 onları taşımaz (docs/01). EC'de CPU 4. noktası (100°C, 255), GPU'nunki (99°C, 255).
/// </summary>
public readonly record struct FanEgrisi(int T2, int D2, int T3, int D3)
{
    // Eğim hesabında CC'nin kullandığı uç noktalar (sayfa 4 kaydı; GPU için de 100°C)
    public const int T1 = 40, D1 = 35, T4 = 100, D4 = 100;

    /// <summary>Özel modda en düşük fan %. %5'te GPU fanı duruyor, CPU fanı takılıyor; %10 kararlı (docs/01, 11.11 merdiveni).</summary>
    public const int EnAz = 10;

    /// <summary>Geçersizse nedenini verir. <paramref name="t4Canli"/> = EC'deki 4. noktanın sıcaklığı (tampon 13).</summary>
    public string? Hata(int t4Canli) =>
        !(T1 < T2 && T2 < T3 && T3 < t4Canli) ? $"Sıcaklıklar {T1} < t2 < t3 < {t4Canli} olmalı (t2={T2}, t3={T3})"
        : !(EnAz <= D2 && D2 <= D3 && D3 <= D4) ? $"Fan %: {EnAz} ≤ d2 ≤ d3 ≤ {D4} olmalı (d2={D2}, d3={D3})"
        : null;

    /// <summary>Fan % → EC duty (0–255), yarım yukarı yuvarlanır.</summary>
    public static byte Duty(int yuzde) => (byte)((yuzde * 255 + 50) / 100);

    /// <summary>EC duty → fan %. Duty yüzdeden üretildiyse Duty(Yuzde(x)) == x.</summary>
    public static int Yuzde(byte duty) => (duty * 100 + 127) / 255;

    /// <summary>CC'nin eğimi: round(Δd% × 2,55 × 16 / Δt), tamsayıyla yarım yukarı.</summary>
    internal static int Egim(int t1, int d1, int t2, int d2)
    {
        int pay = (d2 - d1) * 408, payda = 10 * (t2 - t1);
        return (2 * pay + payda) / (2 * payda);
    }
}
