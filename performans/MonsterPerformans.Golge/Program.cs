// P0 gölge mod: 1 Hz tüm sensörleri okur, günlük CSV'ye yazar. Donanıma hiçbir şey yazmaz.
// Kullanım: MonsterPerformans.Golge [kayıt klasörü]   (varsayılan %LocalAppData%\MonsterPerformans\kayit) — Ctrl+C ile durur.
using MonsterKontrol.Dchu;
using MonsterPerformans.Cekirdek;
using MonsterPerformans.Cekirdek.Sensorler;

var klasor = args.Length > 0 ? args[0]
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonsterPerformans", "kayit");

using var dur = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; dur.Cancel(); };

using var dchu = new DchuCihazi();
using var pdh = new PdhSayaclari();
using var nvml = Nvml.Ac();
using var kayitci = new CsvKayitci(klasor);
var ornekleyici = new Ornekleyici(dchu, pdh, nvml);
Console.WriteLine($"Kayıt: {klasor}  DLL: {dchu.DllYolu}  NVML: {(nvml is null ? "yok" : "var")}");

using var saat = new PeriodicTimer(TimeSpan.FromSeconds(1));
var sayac = 0;
try
{
    while (await saat.WaitForNextTickAsync(dur.Token))
    {
        var o = ornekleyici.Oku();
        kayitci.Yaz(o);
        if (sayac++ % 10 == 0)
            Console.WriteLine($"{o.Zaman:HH:mm:ss} CPU {o.Dchu.CpuC}°C yük %{o.Pdh[0]:0} {o.GercekMHz:0} MHz  fan {o.Dchu.CpuDuty}%/{o.Dchu.GpuDuty}%  GPU {o.Gpu?.C?.ToString() ?? "-"}°C");
    }
}
catch (OperationCanceledException) { }
