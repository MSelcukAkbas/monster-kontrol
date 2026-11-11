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

    protected override void OnStartup(StartupEventArgs e)
    {
        KlavyeVar = MonsterKlavye.App.Baslat(Dispatcher);
        PerformansVar = MonsterPerformans.App.Baslat(Dispatcher);
        MainWindow = new MainWindow();
        MainWindow.Show();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (KlavyeVar) MonsterKlavye.App.Durdur();
        if (PerformansVar) MonsterPerformans.App.Durdur();
        base.OnExit(e);
    }
}
