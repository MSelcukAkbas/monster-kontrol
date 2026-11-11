using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using MonsterKontrol.Dchu;
using MonsterPerformans.Cekirdek;
using MonsterPerformans.Cekirdek.Fan;
using MonsterPerformans.Cekirdek.Guc;
using MonsterPerformans.Cekirdek.Sensorler;

namespace MonsterPerformans;

public partial class App : Application
{
    public static string? DonanimHatasi { get; private set; }
    public static string? DllYolu { get; private set; }
    public static bool NvmlVar { get; private set; }

    /// <summary>Son 30 dk (1 Hz). Yalnız arayüz iş parçacığında yazılır ve okunur.</summary>
    public static Gecmis<Olcum> Gecmis { get; } = new(1800);
    public static string? PlanAdi { get; private set; }
    public static event Action<Olcum>? YeniOlcum;

    /// <summary>Kullanıcı profilleri ve tepsi profili. Yalnız arayüz iş parçacığında.</summary>
    public static PerformansAyarlari Ayarlar { get; private set; } = new();

    /// <summary>Bu oturumda son uygulanan profil. Açılışta null: uygulama açılırken donanıma yazmaz.</summary>
    public static string? EtkinProfil { get; private set; }

    /// <summary>Son uygulamanın adım adım özeti (pano durum satırı).</summary>
    public static string? SonIslem { get; private set; }

    /// <summary>Bir uygulama sürüyor: tüm profil düğmeleri griye döner (iki profilin yazmaları karışmasın).</summary>
    public static bool Uygulaniyor { get; private set; }

    public static bool DonanimVar => _uygulayici is not null;

    /// <summary>Profil listesi, etkin profil ya da uygulama durumu değişti.</summary>
    public static event Action? DurumDegisti;

    /// <summary>Çıkış başladı: pencere kapanışı artık tepsiye gizlemez.</summary>
    public static bool Cikiliyor { get; private set; }

    const string Ad = @"Local\MonsterPerformans";
    static Mutex? _tekOrnek; // alan: yerel değişken olsa çöp toplayıcı bırakır, koruma sessizce kalkar
    static EventWaitHandle? _gosterIstegi;

    static ProfilUygulayici? _uygulayici;
    static FanKontrol? _fan;
    static bool _fanDegisti; // fan bu oturumda otomatikten çıkarıldı: çıkışta güvenli duruma dön (bekçi yok)

    DchuCihazi? _dchu;
    PdhSayaclari? _pdh;
    Nvml? _nvml;
    Tepsi? _tepsi;
    readonly CancellationTokenSource _dur = new();
    Task? _ornekleme;

    protected override void OnStartup(StartupEventArgs e)
    {
        _tekOrnek = new Mutex(true, Ad, out var ilk);
        _gosterIstegi = new EventWaitHandle(false, EventResetMode.AutoReset, Ad + ".Goster");
        if (!ilk)
        {
            AllowSetForegroundWindow(-1); // ASFW_ANY: ilk örnek penceresini öne alabilsin
            _gosterIstegi.Set();
            Shutdown();
            return;
        }
        ThreadPool.RegisterWaitForSingleObject(_gosterIstegi, (_, _) => Dispatcher.BeginInvoke(PencereyiGoster), null, -1, false);

        try
        {
            _dchu = new DchuCihazi();
            DllYolu = _dchu.DllYolu;
            _fan = new FanKontrol(_dchu);
            _uygulayici = new ProfilUygulayici(_dchu, new WindowsGucPlani(), _fan);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException) { DonanimHatasi = ex.Message; }
        _pdh = new PdhSayaclari();
        _nvml = Nvml.Ac();
        NvmlVar = _nvml is not null;
        Ayarlar = PerformansAyarlari.Oku(PerformansAyarlari.VarsayilanYol);

        // Açılışta yalnız okuma; donanıma yazan tek şey kullanıcının bastığı düğmeler
        if (_dchu is not null)
            _ornekleme = Task.Run(() => Ornekle(new Ornekleyici(_dchu, _pdh, _nvml), _dur.Token));

        WindowsIleBaslat.YoluTazele();
        MainWindow = new MainWindow();
        _tepsi = new Tepsi();
        if (!e.Args.Contains(WindowsIleBaslat.TepsiArgumani)) MainWindow.Show();
        base.OnStartup(e);
    }

    /// <summary>1 Hz; DCHU ve PDH okumaları bloklar, arayüz iş parçacığında yapılmaz.</summary>
    async Task Ornekle(Ornekleyici ornekleyici, CancellationToken dur)
    {
        var plan = new WindowsGucPlani();
        using var saat = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                try
                {
                    var o = ornekleyici.Oku();
                    var ad = WindowsGucPlani.Ad(plan.Etkin);
                    _ = Dispatcher.BeginInvoke(() =>
                    {
                        Gecmis.Ekle(o);
                        PlanAdi = ad;
                        YeniOlcum?.Invoke(o);
                    });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Debug.WriteLine($"Örnek alınamadı: {ex.Message}"); // tek örnek kaçar, döngü sürer
                }
            } while (await saat.WaitForNextTickAsync(dur));
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Profili arka planda uygular (eğri doğrulaması ~1 sn bekleyebilir). Aynı anda yalnız bir uygulama.</summary>
    public static Task Uygula(Profil p) => Calistir(u =>
    {
        if (p.Fan != FanModu.Otomatik) _fanDegisti = true; // yazma yarıda kalsa da çıkışta geri alınsın
        return u.Uygula(p);
    }, p.Ad, p.Ad);

    public static Task GuvenliDurumaDon() => Calistir(u =>
    {
        var s = u.GuvenliDurum();
        _fanDegisti = false;
        return s;
    }, "Güvenli durum", null);

    static async Task Calistir(Func<ProfilUygulayici, IReadOnlyList<AdimSonucu>> islem, string baslik, string? etkin)
    {
        if (_uygulayici is not { } u || Uygulaniyor) return;
        Uygulaniyor = true;
        DurumDegisti?.Invoke();
        try
        {
            var s = await Task.Run(() => islem(u));
            EtkinProfil = etkin;
            SonIslem = $"{baslik} · {Ozet(s)}";
        }
        finally
        {
            Uygulaniyor = false;
            DurumDegisti?.Invoke();
        }
    }

    public static string Ozet(IReadOnlyList<AdimSonucu> s) => string.Join(" · ", s.Select(a => a.Durum switch
    {
        AdimDurumu.Dogrulandi => $"{a.Adim} doğrulandı ✓",
        AdimDurumu.Yazildi => $"{a.Adim} yazıldı",
        _ => $"{a.Adim} başarısız: {a.Hata}",
    }));

    /// <summary>Profil listesi değişince çağrılır: diske yazar ve sayfaları/tepsiyi yeniler.</summary>
    public static void AyarlarDegisti()
    {
        try { Ayarlar.Yaz(PerformansAyarlari.VarsayilanYol); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Debug.WriteLine($"Ayarlar yazılamadı: {ex.Message}"); }
        DurumDegisti?.Invoke();
    }

    public static void PencereyiGoster()
    {
        var p = Current.MainWindow;
        p.Show();
        if (p.WindowState == WindowState.Minimized) p.WindowState = WindowState.Normal;
        p.Activate();
    }

    public static void Cik()
    {
        Cikiliyor = true;
        Current.Shutdown();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        Cikiliyor = true; // Windows kapanışını tepsiye gizleyerek engelleme
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _dur.Cancel();
        _ornekleme?.Wait(TimeSpan.FromSeconds(2));
        if (_fanDegisti)
        {
            try { _fan?.GuvenliDurum(); }
            catch (Exception ex) { Debug.WriteLine($"Çıkışta fan güvenli duruma dönmedi: {ex.Message}"); }
        }
        _tepsi?.Dispose();
        _nvml?.Dispose();
        _pdh?.Dispose();
        _dchu?.Dispose();
        _tekOrnek?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);
}
