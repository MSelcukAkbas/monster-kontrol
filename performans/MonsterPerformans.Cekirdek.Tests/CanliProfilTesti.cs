using MonsterKontrol.Dchu;
using MonsterPerformans.Cekirdek.Fan;
using MonsterPerformans.Cekirdek.Guc;
using Xunit.Abstractions;

namespace MonsterPerformans.Cekirdek.Tests;

/// <summary>
/// Gerçek sisteme yazar: yalnız kullanıcı onayıyla, MONSTER_CANLI_PROFIL=1 iken çalışır.
/// Sonunda güvenli durum (fan mod 0 + 0x69, eğlence, Dengeli) ve kopya plan silinir.
/// </summary>
public class CanliProfilTesti(ITestOutputHelper cikti)
{
    [Fact]
    public void Profil_uygulanir_ve_dengeli_degismez()
    {
        if (Environment.GetEnvironmentVariable("MONSTER_CANLI_PROFIL") != "1")
            return;

        using var dchu = new DchuCihazi();
        var plan = new WindowsGucPlani();
        var fan = new FanKontrol(dchu);
        var d = WindowsGucPlani.Dengeli;
        var frekans0 = plan.IslemciAyariOku(d, WindowsGucPlani.FrekansSiniri);
        var turbo0 = plan.IslemciAyariOku(d, WindowsGucPlani.TurboModu);
        cikti.WriteLine($"Önce: etkin {WindowsGucPlani.Ad(plan.Etkin)}, Dengeli frekans {frekans0}, turbo {turbo0}");

        var kopya = WindowsGucPlani.KopyaGuid(d);
        try
        {
            var s = new ProfilUygulayici(dchu, plan, fan)
                .Uygula(new("Canlı test", GucSeviyesi.Sessiz, d, 70, false, FanModu.Otomatik));
            foreach (var a in s) cikti.WriteLine($"{a.Adim}: {a.Durum} {a.Hata}");
            cikti.WriteLine($"Etkin: {WindowsGucPlani.Ad(plan.Etkin)}");
            Assert.All(s, a => Assert.NotEqual(AdimDurumu.Basarisiz, a.Durum));
            Assert.Equal(kopya, plan.Etkin);
            Thread.Sleep(3000);
        }
        finally
        {
            try { fan.GuvenliDurum(); }
            finally
            {
                try { dchu.Yaz(Komutlar.Secenek, (0x19u << 24) | (uint)GucSeviyesi.Eglence); }
                finally
                {
                    plan.Etkinlestir(d);
                    plan.Sil(kopya);
                }
            }
        }
        Assert.Equal(d, plan.Etkin);
        Assert.Equal(frekans0, plan.IslemciAyariOku(d, WindowsGucPlani.FrekansSiniri));
        Assert.Equal(turbo0, plan.IslemciAyariOku(d, WindowsGucPlani.TurboModu));
        cikti.WriteLine("Sonra: Dengeli etkin, değerleri aynı, kopya silindi");
    }
}
