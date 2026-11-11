using MonsterKontrol.Dchu;
using MonsterPerformans.Cekirdek.Fan;

namespace MonsterPerformans.Cekirdek.Guc;

/// <summary>CC güç seviyesi (0x79/0x19). Değerler DCHU'nunki, arayüz sırası değil (docs/01: PL1 15/30/45/60 W).</summary>
public enum GucSeviyesi { Sessiz = 0, Tasarruf = 1, Performans = 2, Eglence = 3 }

/// <summary>Profilin 5 ayarı (docs/04). Eğriler yalnız fan modu Özel iken kullanılır.</summary>
public sealed record Profil(
    string Ad, GucSeviyesi Guc, Guid WindowsPlani, int FrekansYuzde, bool Turbo,
    FanModu Fan, FanEgrisi CpuEgrisi = default, FanEgrisi GpuEgrisi = default);

public enum AdimDurumu { Yazildi, Dogrulandi, Basarisiz }

public sealed record AdimSonucu(string Adim, AdimDurumu Durum, string? Hata = null);

/// <summary>
/// Profili uygular: güç seviyesi → Windows planı (kopya) → fan. Adımlar birbirinden bağımsız; biri başarısız
/// olursa öncekiler geri alınmaz. Fan en sonda: tek doğrulamalı ve güvenli dönüşlü adım o.
/// </summary>
public sealed class ProfilUygulayici(IDchu dchu, IGucPlani plan, FanKontrol fan)
{
    /// <summary>i5-10200H en yüksek turbo (docs/01).</summary>
    public const int EnYuksekMhz = 4100;

    public IReadOnlyList<AdimSonucu> Uygula(Profil p) => [Guc(p), Plan(p), Fan(p)];

    AdimSonucu Guc(Profil p) => Dene("Güç seviyesi", () =>
    {
        dchu.Yaz(Komutlar.Secenek, (0x19u << 24) | (uint)p.Guc);
        return AdimDurumu.Yazildi; // geri okunacak yer bilinmiyor
    });

    AdimSonucu Plan(Profil p) => Dene("Windows planı", () =>
    {
        if (p.FrekansYuzde is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(p.FrekansYuzde), p.FrekansYuzde, "Frekans %1–100 olmalı");
        var kopya = plan.KopyaHazirla(p.WindowsPlani);
        uint mhz = FrekansMhz(p.FrekansYuzde), turbo = p.Turbo ? 2u : 0u; // 2 = Dengeli'nin kendi değeri (Agresif)
        plan.IslemciAyariYaz(kopya, WindowsGucPlani.FrekansSiniri, mhz);
        plan.IslemciAyariYaz(kopya, WindowsGucPlani.TurboModu, turbo);
        plan.Etkinlestir(kopya);
        return plan.Etkin == kopya
               && plan.IslemciAyariOku(kopya, WindowsGucPlani.FrekansSiniri) == (mhz, mhz)
               && plan.IslemciAyariOku(kopya, WindowsGucPlani.TurboModu) == (turbo, turbo)
            ? AdimDurumu.Dogrulandi
            : throw new InvalidOperationException("Plan geri okunamadı ya da etkin değil");
    });

    AdimSonucu Fan(Profil p)
    {
        var s = Dene("Fan", () =>
        {
            switch (p.Fan)
            {
                case FanModu.Otomatik: fan.GuvenliDurum(); return AdimDurumu.Yazildi;
                case FanModu.Maksimum: fan.ModAyarla(FanModu.Maksimum); return AdimDurumu.Yazildi;
                default:
                    return fan.OzelEgriUygula(p.CpuEgrisi, p.GpuEgrisi)
                        ? AdimDurumu.Dogrulandi
                        : throw new InvalidOperationException("Eğri doğrulanamadı, fan otomatiğe döndü");
            }
        });
        if (s.Durum == AdimDurumu.Basarisiz)
            Dene("Fan güvenli durum", () => { fan.GuvenliDurum(); return AdimDurumu.Yazildi; });
        return s;
    }

    /// <summary>CLAUDE.md güvenli durumu: fan otomatik (mod 0 + 0x69), güç eğlence, Dengeli planın kendisi (kopya değil).</summary>
    public IReadOnlyList<AdimSonucu> GuvenliDurum() =>
    [
        Dene("Fan", () => { fan.GuvenliDurum(); return AdimDurumu.Yazildi; }),
        Dene("Güç seviyesi", () =>
        {
            dchu.Yaz(Komutlar.Secenek, (0x19u << 24) | (uint)GucSeviyesi.Eglence);
            return AdimDurumu.Yazildi;
        }),
        Dene("Windows planı", () =>
        {
            plan.Etkinlestir(WindowsGucPlani.Dengeli);
            return plan.Etkin == WindowsGucPlani.Dengeli ? AdimDurumu.Dogrulandi : throw new InvalidOperationException("Dengeli etkinleşmedi");
        }),
    ];

    /// <summary>%100 = sınırsız (0); aksi halde en yüksek turbonun yüzdesi, MHz.</summary>
    public static uint FrekansMhz(int yuzde) => yuzde >= 100 ? 0u : (uint)(yuzde * EnYuksekMhz / 100);

    static AdimSonucu Dene(string adim, Func<AdimDurumu> islem)
    {
        try { return new(adim, islem()); }
        catch (Exception e) { return new(adim, AdimDurumu.Basarisiz, e.Message); }
    }
}
