using MonsterKontrol.Dchu;
using MonsterPerformans.Cekirdek.Sensorler;

namespace MonsterPerformans.Cekirdek;

/// <summary>Tüm sensörlerden tek örnek alır. Donanıma yazmaz (gölge mod).</summary>
public sealed class Ornekleyici(IDchu dchu, PdhSayaclari pdh, Nvml? nvml, BaglamOkuyucu? baglam = null)
{
    public Olcum Oku()
    {
        var tampon = dchu.TamponOku(Komutlar.DurumTamponu);
        return new Olcum(DateTime.Now, DchuDurumu.Coz(tampon), pdh.Oku(), nvml?.Oku(), tampon[..32], baglam?.Oku());
    }
}
