using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using MonsterKontrol.Dchu;
using MonsterPerformans.Cekirdek;
using MonsterPerformans.Cekirdek.Guc;
using MonsterPerformans.Cekirdek.Sensorler;

namespace MonsterPerformans;

public partial class App : Application
{
    public static string? DonanimHatasi { get; private set; }
    public static bool NvmlVar { get; private set; }

    /// <summary>Son 30 dk (1 Hz). Yalnız arayüz iş parçacığında yazılır ve okunur.</summary>
    public static Gecmis<Olcum> Gecmis { get; } = new(1800);
    public static string? PlanAdi { get; private set; }
    public static event Action<Olcum>? YeniOlcum;

    const string Ad = @"Local\MonsterPerformans";
    static Mutex? _tekOrnek; // alan: yerel değişken olsa çöp toplayıcı bırakır, koruma sessizce kalkar
    static EventWaitHandle? _gosterIstegi;

    DchuCihazi? _dchu;
    PdhSayaclari? _pdh;
    Nvml? _nvml;
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

        try { _dchu = new DchuCihazi(); }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException) { DonanimHatasi = ex.Message; }
        _pdh = new PdhSayaclari();
        _nvml = Nvml.Ac();
        NvmlVar = _nvml is not null;

        // Bu aşamada donanıma hiçbir şey yazılmaz: yalnız okuma
        if (_dchu is not null)
            _ornekleme = Task.Run(() => Ornekle(new Ornekleyici(_dchu, _pdh, _nvml), _dur.Token));

        MainWindow = new MainWindow();
        MainWindow.Show();
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

    public static void PencereyiGoster()
    {
        var p = Current.MainWindow;
        p.Show();
        if (p.WindowState == WindowState.Minimized) p.WindowState = WindowState.Normal;
        p.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _dur.Cancel();
        _ornekleme?.Wait(TimeSpan.FromSeconds(2));
        _nvml?.Dispose();
        _pdh?.Dispose();
        _dchu?.Dispose();
        _tekOrnek?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);
}
