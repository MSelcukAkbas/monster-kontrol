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

    static string Deger(string satir, string sutun) => satir.Split(',')[Array.IndexOf(CsvKayitci.Baslik.Split(','), sutun)];

    [Fact]
    public void Csv_satiri_baslikla_ayni_sayida_alan_ve_nokta_ondalik()
    {
        var o = new Olcum(new DateTime(2026, 11, 10, 21, 7, 22), DchuDurumu.Coz(Gercek),
            [16.5, 150.0, 2400.0, null, 100.0, 0.0], new GpuDurumu(null, null, 9.15, 300, Kisit: 0x4),
            Gercek, new Baglam("chrome", 5, true, 87, 12, "Dengeli, kopya", 0, 2, 50));
        var satir = CsvKayitci.Satir(o, new UygulamaDurumu("Sessiz", "Ozel", "Yok", false));
        Assert.Equal(CsvKayitci.Baslik.Split(',').Length, satir.Split(',').Length);
        Assert.StartsWith("21:07:22,72,53,35,2671,35,2267,1,16.5,150,2400,3600,,100,0,", satir);
        Assert.Equal("", Deger(satir, "cek0_mhz")); // kısa Pdh dizisi boşla dolar
        Assert.Equal("9.2", Deger(satir, "gpu_W"));
        Assert.Equal("0x4", Deger(satir, "gpu_kisit"));
        Assert.Equal("Dengeli; kopya", Deger(satir, "plan")); // virgül sütun kaydırmaz
        Assert.Equal("1", Deger(satir, "priz"));
        Assert.Equal("Sessiz", Deger(satir, "uyg_profil"));
        Assert.Equal("0", Deger(satir, "uyg_olcum"));
        Assert.EndsWith("," + Convert.ToHexString(Gercek), satir);
        Assert.Equal(CsvKayitci.BaslikHamsiz.Split(',').Length, CsvKayitci.Satir(o, ham: false).Split(',').Length);
    }

    [Fact]
    public void Temizlik_eski_gunleri_ozetler_sonra_siler()
    {
        var klasor = Directory.CreateTempSubdirectory("mp_test_").FullName;
        try
        {
            foreach (var ad in new[] { "2026-10-11.csv", "2026-10-10_101500.csv", "2026-10-13.csv", "2026-11-11.csv", "notlar.csv", "2026-10-01x.csv", "2026-10-01.txt" })
                File.WriteAllText(Path.Combine(klasor, ad), "zaman,a,b,ham\n");
            File.AppendAllText(Path.Combine(klasor, "2026-10-11.csv"),
                "10:00:01,1,x,FF\n10:00:02,2,y,FF\n10:00:03,,y,FF\n10:01:00,4.5,0x1,FF\nbozuk\n");
            Assert.Equal(2, CsvKayitci.Temizle(klasor, 30, new DateOnly(2026, 11, 11)));
            Assert.Equal(["2026-10-01.txt", "2026-10-01x.csv", "2026-10-13.csv", "2026-11-11.csv", "notlar.csv"],
                Directory.GetFiles(klasor).Select(Path.GetFileName).Order());
            Assert.Equal(["dakika,ornek,a,b", "10:00,3,1.5,y", "10:01,1,4.5,0x1"],
                File.ReadAllLines(Path.Combine(klasor, "ozet", "2026-10-11.csv")));
            Assert.Equal(["dakika,ornek,a,b"], File.ReadAllLines(Path.Combine(klasor, "ozet", "2026-10-10_101500.csv")));
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Fact]
    public void Sutunlar_degisince_eski_dosyaya_eklenmez()
    {
        var klasor = Directory.CreateTempSubdirectory("mp_test_").FullName;
        try
        {
            File.WriteAllText(Path.Combine(klasor, "2026-11-11.csv"), "zaman,eski\n");
            Olcum O(int sn) => new(new DateTime(2026, 11, 11, 14, 0, sn), DchuDurumu.Coz(Gercek), new double?[6], null, Gercek);
            using (var k = new CsvKayitci(klasor)) k.Yaz(O(5));
            using (var k = new CsvKayitci(klasor)) k.Yaz(O(9)); // yeniden açılış aynı yeni dosyayı sürdürür
            Assert.Equal(["zaman,eski"], File.ReadAllLines(Path.Combine(klasor, "2026-11-11.csv")));
            var yeni = File.ReadAllLines(Path.Combine(klasor, "2026-11-11_140005.csv"));
            Assert.Equal(CsvKayitci.Baslik, yeni[0]);
            Assert.Equal(3, yeni.Length);
        }
        finally { Directory.Delete(klasor, true); }
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
