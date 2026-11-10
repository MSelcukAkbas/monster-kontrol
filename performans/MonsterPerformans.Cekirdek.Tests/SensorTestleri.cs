using MonsterPerformans.Cekirdek;
using MonsterPerformans.Cekirdek.Sensorler;

namespace MonsterPerformans.Cekirdek.Tests;

public class SensorTestleri
{
    // Gerçek tampon 12 (2026-11-10 21:07, prototip/kayit.py): CPU 72°C, fanlar %35, 2671/2267 RPM
    static readonly byte[] Gercek =
        [0, 0, 3, 39, 3, 183, 0, 0, 0, 0, 65, 0, 1, 0, 0, 0, 89, 53, 72, 89, 1, 20, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0];

    [Fact]
    public void Tampon12_python_prototipiyle_ayni_cozulur()
    {
        var d = DchuDurumu.Coz(Gercek);
        Assert.Equal(new DchuDurumu(72, 53, 35, 35, 2671, 2267, 1), d);
        Assert.True(d.Gecerli);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(127, false)]
    [InlineData(1, true)]
    [InlineData(126, true)]
    public void Sensor_hatasi_sinirlari(byte c, bool gecerli)
    {
        var b = (byte[])Gercek.Clone();
        b[18] = c;
        Assert.Equal(gecerli, DchuDurumu.Coz(b).Gecerli);
    }

    [Fact]
    public void Duran_fan_sifir_rpm()
    {
        var b = new byte[32];
        Assert.Equal(0, DchuDurumu.Coz(b).CpuRpm);
    }

    [Fact]
    public void Csv_satiri_baslikla_ayni_sayida_alan_ve_nokta_ondalik()
    {
        var o = new Olcum(new DateTime(2026, 11, 10, 21, 7, 22), DchuDurumu.Coz(Gercek),
            [16.5, 150.0, 2400.0, null, 100.0, 0.0], new GpuDurumu(null, null, 9.15, 300), Gercek);
        var satir = CsvKayitci.Satir(o);
        Assert.Equal(CsvKayitci.Baslik.Split(',').Length, satir.Split(',').Length);
        Assert.StartsWith("21:07:22,72,53,35,2671,35,2267,1,16.5,150,2400,3600,,100,0,,,9.2,300,", satir);
    }

    [Fact]
    public void Gun_degisince_yeni_dosya_ve_baslik()
    {
        var klasor = Path.Combine(Path.GetTempPath(), "mp_test_" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var k = new CsvKayitci(klasor))
            {
                k.Yaz(new Olcum(new DateTime(2026, 11, 10, 23, 59, 59), DchuDurumu.Coz(Gercek), new double?[6], null, Gercek));
                k.Yaz(new Olcum(new DateTime(2026, 11, 11, 0, 0, 0), DchuDurumu.Coz(Gercek), new double?[6], null, Gercek));
            }
            foreach (var gun in new[] { "2026-11-10", "2026-11-11" })
            {
                var satirlar = File.ReadAllLines(Path.Combine(klasor, gun + ".csv"));
                Assert.Equal([CsvKayitci.Baslik], satirlar[..1]);
                Assert.Equal(2, satirlar.Length);
            }
        }
        finally { Directory.Delete(klasor, true); }
    }
}
