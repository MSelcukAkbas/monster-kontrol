// K1 duman testi: C# kütüphanesi üzerinden klavyeyi gerçekten sürer. Kullanıcı gözle doğrular.
using MonsterKontrol.Dchu;

using var dchu = new DchuCihazi();
Console.WriteLine($"DLL: {dchu.DllYolu}");
Console.WriteLine($"CPU sıcaklığı: {dchu.TamponOku(Komutlar.DurumTamponu)[18]}°C");

void Adim(string ad, uint arguman, int ms)
{
    Console.WriteLine($"{ad,-22} {arguman:X8}");
    dchu.Yaz(Komutlar.KlavyeIsigi, arguman);
    Thread.Sleep(ms);
}

Adim("parlaklık 255", Komutlar.KlavyeParlaklik(255), 500);
Adim("kırmızı", Komutlar.KlavyeRenk(255, 0, 0), 3000);
Adim("yeşil", Komutlar.KlavyeRenk(0, 255, 0), 3000);
Adim("mavi", Komutlar.KlavyeRenk(0, 0, 255), 3000);
Adim("kapalı (parlaklık 0)", Komutlar.KlavyeParlaklik(0), 2000);
Adim("açık (parlaklık 255)", Komutlar.KlavyeParlaklik(255), 500);
Adim("turuncu (bitiş)", Komutlar.KlavyeRenk(255, 106, 0), 0);
Console.WriteLine("BİTTİ");
