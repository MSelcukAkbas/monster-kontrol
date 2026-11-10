using MonsterKontrol.Dchu;

namespace MonsterKontrol.Dchu.Tests;

/// <summary>
/// Gerçek donanımda YALNIZ OKUMA yapan testler. DLL bulunamayan makinede (CI vb.) kendini atlar.
/// </summary>
public class DonanimTestleri
{
    static DchuCihazi? Cihaz()
    {
        try { return new DchuCihazi(); }
        catch (DllNotFoundException) { return null; }
    }

    [Fact]
    public void Durum_tamponu_makul_CPU_sicakligi_verir()
    {
        using var cihaz = Cihaz();
        if (cihaz is null) return;

        var t = cihaz.TamponOku(Komutlar.DurumTamponu);

        Assert.Equal(DchuCihazi.VeriBoyu, t.Length);
        Assert.InRange(t[18], 20, 105); // bayt 18 = CPU °C (Intel DTS ile ±1°C doğrulandı)
    }

    [Fact]
    public void Tampon_tasma_hatasi_tekrar_tekrar_okumada_cokmez()
    {
        // 256'lık tamponla birkaç okumada 0xC0000005 çöküşü yaşanıyordu (docs/01); 512 ile 200 okuma sorunsuz olmalı.
        using var cihaz = Cihaz();
        if (cihaz is null) return;

        for (var i = 0; i < 200; i++)
            Assert.InRange(cihaz.TamponOku(Komutlar.DurumTamponu)[18], 20, 105);
    }

    [Fact]
    public void Cagrilar_farkli_is_parcaciklarindan_guvenle_siraya_girer()
    {
        using var cihaz = Cihaz();
        if (cihaz is null) return;

        Parallel.For(0, 50, _ => Assert.InRange(cihaz.TamponOku(Komutlar.DurumTamponu)[18], 20, 105));
    }
}
