namespace MonsterPerformans.Cekirdek.Tests;

public class GecmisYukleyiciTestleri
{
    [Fact]
    public void Son_pencere_okunur_bosluk_gecersizle_dolar_surecler_saniyeye_gruplanir()
    {
        var k = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllLines(Path.Combine(k, "2026-11-12.csv"),
                ["zaman,cpu_C,kart_C,cpu_duty,yuk,gpu_C", "09:00:00,50,40,30,10,", "10:00:00,60,45,35,20,55", "10:00:03,61,45,35,20,"]);
            Directory.CreateDirectory(Path.Combine(k, "surecler"));
            File.WriteAllLines(Path.Combine(k, "surecler", "2026-11-12.csv"),
                [SurecKayitci.Baslik, "10:00:00,chrome,3,12.5,4,1.2", "10:00:00,dwm,1,1,8,", "10:00:03,chrome,3,10,0,1"]);
            var simdi = new DateTime(2026, 11, 12, 10, 0, 4);

            var o = GecmisYukleyici.Olcumler(k, simdi, 1800);
            Assert.Equal(5, o.Count); // 10:00:00..10:00:04; 09:00 pencere dışı
            Assert.Equal((60, 55), (o[0].Dchu.CpuC, o[0].Gpu!.Value.C));
            Assert.False(o[1].Dchu.Gecerli);
            Assert.Equal((61, null), (o[3].Dchu.CpuC, o[3].Gpu));

            var s = GecmisYukleyici.Surecler(k, simdi, 1800);
            Assert.Equal(2, s.Count);
            Assert.Equal(new SurecPayi("chrome", 3, 12.5, 4, 1.2), s[0][0]);
            Assert.Null(s[0][1].Watt);
        }
        finally { Directory.Delete(k, true); }
    }
}
