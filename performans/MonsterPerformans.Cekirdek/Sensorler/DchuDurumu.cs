namespace MonsterPerformans.Cekirdek.Sensorler;

/// <summary>DCHU tampon 12'nin çözülmüş hali (bayt haritası docs/01).</summary>
public readonly record struct DchuDurumu(
    int CpuC, int KartC, int CpuDuty, int GpuDuty, int CpuRpm, int GpuRpm, byte ProfilBayti)
{
    /// <summary>0 ya da ≥ 127°C okunursa sensör hatası say (docs/03 fan algoritması madde 10).</summary>
    public bool Gecerli => CpuC is > 0 and < 127;

    public static DchuDurumu Coz(ReadOnlySpan<byte> b) =>
        new(b[18], b[17], Yuzde(b[16]), Yuzde(b[19]), Rpm(b[2], b[3]), Rpm(b[4], b[5]), b[30]);

    static int Yuzde(byte duty) => (int)Math.Round(duty / 2.55);

    // Ham değer büyük-endian; formül CC ile birebir doğrulandı (docs/01)
    static int Rpm(byte yuksek, byte dusuk)
    {
        var ham = yuksek << 8 | dusuk;
        return ham == 0 ? 0 : (int)(60.0 / (5.56521739130435e-05 * ham) * 2);
    }
}
