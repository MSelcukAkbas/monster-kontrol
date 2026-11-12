using System.Runtime.InteropServices;
using System.Windows;

namespace MonsterKontrol;

/// <summary>
/// İki uygulamanın motorlarını tek süreçte başlatır, sayfalarını tek pencerede gösterir.
/// </summary>
public partial class App : Application
{
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
        // Tek örnek: ikinci açılış ilkini gösterip çıkar (Windows ile başlatma iki kayıtla iki kez açabilir).
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

        MonsterKlavye.App.Baslat(Dispatcher);
        MonsterPerformans.App.Baslat(Dispatcher);
        MainWindow = new MainWindow();
        _klavyeTepsi = new MonsterKlavye.Tepsi();
        _performansTepsi = new MonsterPerformans.Tepsi();
        if (!e.Args.Contains(MonsterKlavye.WindowsIleBaslat.TepsiArgumani)) MainWindow.Show();
        base.OnStartup(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _oturumBitti = true; // Windows kapanışını tepsiye gizleyerek engelleme
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _klavyeTepsi?.Dispose();
        _performansTepsi?.Dispose();
        MonsterKlavye.App.Durdur();
        MonsterPerformans.App.Durdur();
        _tekOrnek?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);
}
