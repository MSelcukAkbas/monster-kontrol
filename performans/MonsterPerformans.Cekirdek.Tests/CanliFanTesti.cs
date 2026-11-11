using MonsterKontrol.Dchu;
using MonsterPerformans.Cekirdek.Fan;
using MonsterPerformans.Cekirdek.Sensorler;
using Xunit.Abstractions;

namespace MonsterPerformans.Cekirdek.Tests;

/// <summary>
/// Gerçek EC'ye yazar: yalnız kullanıcı onayıyla, MONSTER_CANLI_FAN=1 iken çalışır (prototip/testE ile aynı akış).
/// Sonunda eski eğri geri yazılır ve doğrulanır, sonra güvenli durum (mod 0 + 0x69).
/// </summary>
public class CanliFanTesti(ITestOutputHelper cikti)
{
    [Fact]
    public void Ozel_egri_yazilir_dogrulanir_ve_geri_alinir()
    {
        if (Environment.GetEnvironmentVariable("MONSTER_CANLI_FAN") != "1")
            return;

        using var dchu = new DchuCihazi();
        var fan = new FanKontrol(dchu);
        var once = dchu.TamponOku(13);
        var (cpu0, gpu0) = fan.EcEgrisi();
        cikti.WriteLine($"Önceki: CPU {cpu0}, GPU {gpu0}");

        var cpu = new FanEgrisi(55, 45, 75, 70);
        var gpu = new FanEgrisi(60, 45, 80, 70);
        try
        {
            Assert.True(fan.OzelEgriUygula(cpu, gpu), "eğri tampon 13'te doğrulanamadı");
            Assert.Equal((cpu, gpu), fan.EcEgrisi());
            for (var s = 0; s < 30; s++)
            {
                var d = DchuDurumu.Coz(dchu.TamponOku(Komutlar.DurumTamponu));
                cikti.WriteLine($"{s,2} sn: CPU {d.CpuC}°C, fan %{d.CpuDuty}/%{d.GpuDuty}, {d.CpuRpm}/{d.GpuRpm} RPM");
                Assert.True(d.Gecerli && d.CpuC < 95, $"CPU {d.CpuC}°C: sensör hatası ya da ≥ 95");
                Thread.Sleep(1000);
            }
        }
        finally
        {
            var geri = false;
            // Eski eğri yazılamasa da güvenli duruma mutlaka dön
            try { geri = fan.EgriYaz(cpu0, gpu0); }
            finally { fan.GuvenliDurum(); }
            var sonra = dchu.TamponOku(13);
            cikti.WriteLine($"Geri yazıldı: {geri}, tampon 13 [16..31] aynı: {once.AsSpan(16, 16).SequenceEqual(sonra.AsSpan(16, 16))}");
        }
        Assert.Equal(once.AsSpan(16, 16).ToArray(), dchu.TamponOku(13).AsSpan(16, 16).ToArray());
    }
}
