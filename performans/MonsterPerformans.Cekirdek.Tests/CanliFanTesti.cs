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
    /// <summary>
    /// %35 tabanı EC'de mi? 2. nokta %20 (50°C), 3. nokta %30 (90°C); 1. eğim 0 (negatif eğim EC'ye anlamsız).
    /// MONSTER_CANLI_FAN_TABAN=1 iken. 40 sn izler, CPU ≥ 92°C'de keser; sonra eski eğri ve güvenli durum
    /// (güç Sessiz → fan otomatik → Eğlence, docs/01 fırlama kuralı).
    /// </summary>
    [Fact]
    public void Egri_35_altina_iner_mi()
    {
        if (Environment.GetEnvironmentVariable("MONSTER_CANLI_FAN_TABAN") != "1")
            return;

        using var dchu = new DchuCihazi();
        var fan = new FanKontrol(dchu);
        var once = dchu.TamponOku(13);
        var (cpu0, gpu0) = fan.EcEgrisi();
        cikti.WriteLine($"Önceki: CPU {cpu0}, GPU {gpu0}");

        var e = new FanEgrisi(50, 20, 90, 30);
        var b = FanKontrol.Komut14(e, e);
        try
        {
            dchu.TamponYaz(FanKontrol.EgriYazma, b);
            Thread.Sleep(300);
            Assert.Equal((e, e), fan.EcEgrisi());
            fan.ModAyarla(FanModu.Ozel);
            for (var s = 0; s < 40; s++)
            {
                var d = DchuDurumu.Coz(dchu.TamponOku(Komutlar.DurumTamponu));
                cikti.WriteLine($"{s,2} sn: CPU {d.CpuC}°C, fan %{d.CpuDuty}/%{d.GpuDuty}, {d.CpuRpm}/{d.GpuRpm} RPM");
                Assert.True(d.Gecerli && d.CpuC < 92, $"CPU {d.CpuC}°C: sensör hatası ya da ≥ 92");
                Thread.Sleep(1000);
            }
        }
        finally
        {
            var geri = false;
            try { geri = fan.EgriYaz(cpu0, gpu0); }
            finally
            {
                dchu.Yaz(Komutlar.Secenek, 0x1900_0000u); // güç Sessiz: Eğlence'de otomatiğe geçiş fanı fırlatır
                fan.GuvenliDurum();
                dchu.Yaz(Komutlar.Secenek, 0x1900_0003u);
            }
            cikti.WriteLine($"Geri yazıldı: {geri}, tampon 13 [16..31] aynı: {once.AsSpan(16, 16).SequenceEqual(dchu.TamponOku(13).AsSpan(16, 16))}");
        }
    }
    /// <summary>
    /// Güç Sessiz'de düz eğri %20 → %15 → %10 → %5 (50–90°C arası sabit), 20'şer sn. MONSTER_CANLI_FAN_DUSUK=1 iken.
    /// CPU ≥ 92°C ya da GPU ≥ 85°C'de keser. Windows planına dokunmaz; sonunda eski eğri, fan otomatik, güç Eğlence.
    /// </summary>
    [Fact]
    public void Sessizde_dusuk_fan_merdiveni()
    {
        if (Environment.GetEnvironmentVariable("MONSTER_CANLI_FAN_DUSUK") != "1")
            return;

        using var dchu = new DchuCihazi();
        using var nvml = Nvml.Ac();
        var fan = new FanKontrol(dchu);
        var (cpu0, gpu0) = fan.EcEgrisi();
        cikti.WriteLine($"Önceki: CPU {cpu0}, GPU {gpu0}");

        void Izle(string ad, int saniye)
        {
            for (var s = 0; s < saniye; s++)
            {
                var d = DchuDurumu.Coz(dchu.TamponOku(Komutlar.DurumTamponu));
                var g = nvml?.Oku()?.C;
                cikti.WriteLine($"{ad} {s,2} sn: CPU {d.CpuC}°C GPU {g?.ToString() ?? "-"}°C, fan %{d.CpuDuty}/%{d.GpuDuty}, {d.CpuRpm}/{d.GpuRpm} RPM");
                Assert.True(d.Gecerli && d.CpuC < 92 && (g ?? 0) < 85, $"CPU {d.CpuC}°C / GPU {g}°C: sensör hatası ya da sınır");
                Thread.Sleep(1000);
            }
        }

        try
        {
            dchu.Yaz(Komutlar.Secenek, 0x1900_0000u); // güç Sessiz (fan otomatikte, fırlatmaz)
            Izle("Sessiz %35", 30);
            var ilk = true;
            foreach (var y in new[] { 20, 15, 10, 5 })
            {
                var e = new FanEgrisi(50, y, 90, y);
                var b = FanKontrol.Komut14(e, e);
                dchu.TamponYaz(FanKontrol.EgriYazma, b);
                Thread.Sleep(300);
                Assert.Equal((e, e), fan.EcEgrisi());
                if (ilk) { fan.ModAyarla(FanModu.Ozel); ilk = false; }
                Izle($"%{y,2}", 20);
            }
        }
        finally
        {
            var geri = false;
            try { geri = fan.EgriYaz(cpu0, gpu0); }
            finally
            {
                dchu.Yaz(Komutlar.Secenek, 0x1900_0000u);
                fan.GuvenliDurum();
                dchu.Yaz(Komutlar.Secenek, 0x1900_0003u);
            }
            cikti.WriteLine($"Eski eğri geri yazıldı: {geri}");
            Izle("Dönüş", 8);
        }
    }
}
