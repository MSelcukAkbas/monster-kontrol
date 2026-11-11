using System.Runtime.InteropServices;
using System.Windows;

namespace MonsterKontrol;

/// <summary>
/// İki uygulamanın motorlarını tek süreçte başlatır, sayfalarını tek pencerede gösterir.
/// Ayrı uygulamalardan biri açıksa o bölüm başlamaz (aynı tek-örnek kilidi): donanıma iki yazan olmaz.
/// </summary>
public partial class App : Application
{
    public static bool KlavyeVar { get; private set; }
    public static bool PerformansVar { get; private set; }

    /// <summary>Çıkış başladı (tepsiden Çıkış ya da Windows kapanışı): pencere kapanışı artık gizlemez.</summary>
    public static bool Cikiliyor => _oturumBitti || MonsterKlavye.App.Cikiliyor || MonsterPerformans.App.Cikiliyor;

    const string Ad = @"Local\MonsterKontrol";
    static Mutex? _tekOrnek; // alan: yerel değişken olsa çöp toplayıcı bırakır
    static EventWaitHandle? _gosterIstegi;
    static bool _oturumBitti;
    MonsterKlavye.Tepsi? _klavyeTepsi;
    MonsterPerformans.Tepsi? _performansTepsi;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Windows ile başlatma iki kayıtla (MonsterKlavye, MonsterPerformans) bu exe'yi iki kez açabilir: ikincisi ilkini gösterip çıkar.
        _tekOrnek = new Mutex(true, Ad, out var ilk);
        _gosterIstegi = new EventWaitHandle(false, EventResetMode.AutoReset, Ad + ".Goster");
        if (!ilk)
        {
            if (!e.Args.Contains(MonsterKlavye.WindowsIleBaslat.TepsiArgumani))
            {
                AllowSetForegroundWindow(-1);
                _gosterIstegi.Set();
            }
            Shutdown();
            return;
        }
        ThreadPool.RegisterWaitForSingleObject(_gosterIstegi, (_, _) => Dispatcher.BeginInvoke(MonsterKlavye.App.PencereyiGoster), null, -1, false);

        KlavyeVar = MonsterKlavye.App.Baslat(Dispatcher);
        PerformansVar = MonsterPerformans.App.Baslat(Dispatcher);
        MainWindow = new MainWindow();
        if (KlavyeVar) _klavyeTepsi = new MonsterKlavye.Tepsi();
        if (PerformansVar) _performansTepsi = new MonsterPerformans.Tepsi();
        if (!e.Args.Contains(MonsterKlavye.WindowsIleBaslat.TepsiArgumani) || !TepsiVar) MainWindow.Show();
        base.OnStartup(e);
    }

    public static bool TepsiVar => KlavyeVar || PerformansVar;

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _oturumBitti = true; // Windows kapanışını tepsiye gizleyerek engelleme
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _klavyeTepsi?.Dispose();
        _performansTepsi?.Dispose();
        if (KlavyeVar) MonsterKlavye.App.Durdur();
        if (PerformansVar) MonsterPerformans.App.Durdur();
        _tekOrnek?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);
}
