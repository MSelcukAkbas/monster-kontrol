using MonsterPerformans.Cekirdek.Sensorler;

namespace MonsterPerformans.Cekirdek;

/// <summary>Bir saniyelik örnek: tüm kaynaklar + ham tampon (bilinmeyen baytlar sonradan incelenebilsin).</summary>
public sealed record Olcum(DateTime Zaman, DchuDurumu Dchu, double?[] Pdh, GpuDurumu? Gpu, byte[] HamTampon)
{
    /// <summary>Gerçek ortalama frekans = taban × % İşlemci Performansı (Processor Frequency tek başına tabanı verir).</summary>
    public double? GercekMHz => Pdh[2] * Pdh[1] / 100;
}
