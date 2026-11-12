namespace MonsterKlavye.Klavye;

/// <summary>
/// Renk skalası: <see cref="BasTon"/>'dan <see cref="Yay"/> derece ilerleyen tam doygun renkler.
/// Ton üzerinde gezilir; RGB karıştırmak ara renkleri soldurur (neon'un ortası lavanta olurdu).
/// </summary>
public sealed record Skala(string Ad, double BasTon, double Yay)
{
    public bool TamCark => Math.Abs(Yay) >= 360;

    /// <summary>x = 0 başlangıç, 1 bitiş rengi.</summary>
    public Rgb Renk(double x) => Rgb.TondanRgb(BasTon + Yay * Math.Clamp(x, 0, 1));

    /// <summary>Skaladan n renkli dizinin k. rengi. Tam çarkta son renk ilkiyle aynı olacağından n'e bölünür.</summary>
    public Rgb Adim(long k, int n) => Renk((double)(k % n) / (TamCark ? n : n - 1));
}

/// <summary>Efektin bir anki çıktısı: renk ve parlaklık çarpanı (0–1).</summary>
public readonly record struct Kare(Rgb Renk, double Parlaklik);

/// <summary>Faz: hızla ölçeklenmiş saniye. Renk: Işık sayfasındaki renk. Sıcaklık: CPU °C.</summary>
public readonly record struct EfektGirdisi(double Faz, Rgb Renk, Skala Skala, double Sicaklik);

/// <summary>Efekt = saf fonksiyon (girdi → kare). Skalası olmayan efektler Işık sayfasındaki rengi kullanır.</summary>
public sealed record Efekt(string Id, string Ad, string Aciklama, Func<EfektGirdisi, Kare> Hesapla,
    Skala? VarsayilanSkala = null, bool Uyari = false, bool Ayarsiz = false)
{
    public bool SkalaVar => VarsayilanSkala is not null;

    /// <summary>Ayarsız efekt kendi hızında ve renginde çalışır: kullanıcının hız/skala seçimi yok sayılır.</summary>
    public double Carpan(EfektAyarlari a) => Ayarsiz ? 1 : a.Carpan;
}

/// <summary>Kullanıcının seçtiği hız (1–10, 5 = prototip hızı) ve skala.</summary>
public sealed record EfektAyarlari(int Hiz, Skala Skala)
{
    public double Carpan => Math.Clamp(Hiz, 1, 10) / 5.0;
}

public static class Efektler
{
    // Tonlar tasarımdaki uç renklerden; yay kısa yoldan (docs: Monster Klavye Tasarımı, Efektler ekranı).
    public static readonly Skala TamCark = new("Tam çark", 0, 360);
    public static readonly Skala AtesSkalasi = new("Ateş", 10, 31);
    public static readonly Skala OkyanusSkalasi = new("Okyanus", 176, 59);
    public static readonly Skala GunBatimiSkalasi = new("Gün batımı", 341, 55);
    public static readonly Skala Neon = new("Neon", 310, -126);
    public static readonly Skala Orman = new("Orman", 136, -64);
    public static readonly Skala PolisSkalasi = new("Polis", 0, 218);
    public static readonly Skala SicaklikSkalasi = new("Sıcaklık", 145, -145);

    public static readonly Skala[] Skalalar =
        [TamCark, AtesSkalasi, OkyanusSkalasi, GunBatimiSkalasi, Neon, Orman, PolisSkalasi, SicaklikSkalasi];

    public static readonly Efekt Sabit = new("sabit", "Sabit renk", "Seçilen renk sürekli yanar.", g => new(g.Renk, 1));

    public static readonly Efekt Sicaklik = new("sicaklik", "Sıcaklık",
        "CPU sıcaklığı skalada yer seçer: 50°C baş, 90°C son renk.",
        g => new(g.Skala.Renk((g.Sicaklik - 50) / 40), 1), SicaklikSkalasi);

    public static readonly Efekt[] Hepsi =
    [
        Sabit,
        new("nefes", "Nefes", "Parlaklık yavaşça artıp azalır.", g => new(g.Renk, Dalga(g.Faz, 4))),
        new("gokkusagi", "Gökkuşağı", "Renk çarkında yavaşça akar. Skala seçilirse yalnız o aralıkta gezer.",
            g => new(g.Skala.Renk(g.Skala.TamCark ? g.Faz / 6 % 1 : Dalga(g.Faz, 6)), 1), TamCark),
        // Renk, parlaklığın sıfır olduğu anda değişir: geçiş görünmez
        new("renkliNefes", "Renkli nefes", "Her nefeste skaladan sıradaki renk.",
            g => new(g.Skala.Adim((long)(g.Faz / 4), 4), Dalga(g.Faz, 4)), Neon),
        new("kalp", "Kalp atışı", "Çift atımlı nabız.", g => new(g.Renk, Kalp(g.Faz))),
        new("polis", "Polis", "Skalanın iki ucu sırayla yanar.", g => new(g.Skala.Adim((long)(g.Faz / 0.4), 2), 1), PolisSkalasi),
        new("ates", "Ateş", "Koru kızıl, alevi sarıya çıkan, kendi hızında yanan şömine ateşi.", Ates, AtesSkalasi, Ayarsiz: true),
        new("okyanus", "Okyanus", "Skalada yavaşça dalgalanır.",
            g => new(g.Skala.Renk(0.5 + 0.5 * Math.Sin(g.Faz * 0.6)), 0.55 + 0.45 * Dalga(g.Faz + 0.7, 5)), OkyanusSkalasi),
        new("gunBatimi", "Gün batımı", "Skalanın iki ucu arasında gidip gelir.",
            g => new(g.Skala.Renk(Dalga(g.Faz, 8)), 1), GunBatimiSkalasi),
        new("flas", "Flaş", "Kısa ve hızlı yanıp sönme.", g => new(g.Renk, g.Faz % 0.5 < 0.12 ? 1 : 0), Uyari: true),
        Sicaklik,
    ];

    /// <summary>0'da 0, yarım periyotta 1 olan yumuşak dalga.</summary>
    internal static double Dalga(double t, double periyot) => 0.5 - 0.5 * Math.Cos(2 * Math.PI * t / periyot);

    static double Kalp(double t)
    {
        var x = t % 1.2;
        var b = Math.Max(Math.Exp(-Math.Pow((x - 0.10) / 0.06, 2)), 0.7 * Math.Exp(-Math.Pow((x - 0.38) / 0.07, 2)));
        return 0.08 + 0.92 * b;
    }

    /// <summary>
    /// Alev yoğunluğu = farklı hızlarda üç yumuşak gürültü (yavaş kabarma + titreme + çıtırtı) ve ara sıra parlama.
    /// Yoğunluk hem rengi (koyu kızıl → turuncu → sarı, kara cisim gibi) hem parlaklığı sürer: sönük an kızıl, parlak an sarı.
    /// </summary>
    static Kare Ates(EfektGirdisi g)
    {
        var t = g.Faz * 1.4;
        var y = 0.55 * Gurultu(t * 0.7, 1) + 0.35 * Gurultu(t * 1.9, 2) + 0.10 * Gurultu(t * 4.3, 3); // hızlı katman zayıf: sert sıçrama olmasın
        var parlama = Math.Max(0, Gurultu(t * 0.35, 4) - 0.75) * 1.2; // birkaç saniyede bir kısa alev yükselmesi
        y = Math.Clamp((y - 0.25) / 0.6 + parlama, 0, 1);
        var ton = 6 + 26 * y; // 6° kızıl … 32° turuncu-sarı; dar aralık: renk kayması fark edilmeden akar (klavye LED'inde yeşil baskın; daha sarısı limonlaşır)
        return new(Rgb.TondanRgb(ton), 0.6 + 0.4 * y); // loş uçta 8 bit adımlar göze batıyordu
    }

    /// <summary>[0, 1] değer gürültüsü: tamsayı noktalarda rastgele, aralarda yumuşak (smoothstep) geçiş.</summary>
    internal static double Gurultu(double t, long tohum)
    {
        var i = (long)Math.Floor(t);
        var f = t - i;
        f = f * f * (3 - 2 * f);
        var a = Rastgele(tohum * 1_000_003 + i);
        return a + (Rastgele(tohum * 1_000_003 + i + 1) - a) * f;
    }

    /// <summary>Tamsayıdan [0, 1) sayı (splitmix64). Aynı girdi hep aynı sonucu verir: efekt saf kalır.</summary>
    internal static double Rastgele(long n)
    {
        var x = (ulong)n + 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (x >> 11) * (1.0 / (1UL << 53));
    }
}
