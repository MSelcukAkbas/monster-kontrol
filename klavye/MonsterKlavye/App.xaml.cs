using System.IO;
﻿using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using MonsterKlavye.Klavye;
using MonsterKontrol.Dchu;

namespace MonsterKlavye;

public partial class App : Application
{
    /// <summary>Klavye denetimi; DLL bulunamadıysa null (arayüz uyarı gösterir, yine açılır).</summary>
    public static KlavyeKontrol? Kontrol { get; private set; }

    /// <summary>Klavye durumunun tek sahibi (temel durum + efekt). DLL yoksa da vardır, gönderim boşa gider.</summary>
    public static EfektMotoru Motor { get; private set; } = null!;

    public static string? DonanimHatasi { get; private set; }
    public static string? DllYolu { get; private set; }

    public static bool TepsiyeKucult { get; set { field = value; AyarDegisti(); } } = true;

    /// <summary>Çıkış başladı: pencere kapanışı artık tepsiye küçültmez.</summary>
    public static bool Cikiliyor { get; private set; }

    const string Ad = @"Local\MonsterKlavye";
    static Mutex? _tekOrnek; // alan olarak tutulur: yerel değişken olsa çöp toplayıcı bırakır, koruma sessizce kalkar
    static EventWaitHandle? _gosterIstegi;

    DchuCihazi? _dchu;
    Tepsi? _tepsi;
    static readonly DispatcherTimer _kayitZamanlayici = new() { Interval = TimeSpan.FromMilliseconds(500) };

    protected override void OnStartup(StartupEventArgs e)
    {
        // Tek örnek: DLL'e ve klavyeye dokunmadan ÖNCE. İkinci açılış ilkine "göster" der ve çıkar.
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
            Kontrol = new KlavyeKontrol(_dchu);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
        {
            DonanimHatasi = ex.Message;
        }

        var ayar = Ayarlar.Oku(Ayarlar.VarsayilanYol);
        TepsiyeKucult = ayar.TepsiyeKucult;
        Motor = new EfektMotoru(ayar.Durum,
            d => Kontrol?.Ayarla(d),
            () => (_dchu ?? throw new DllNotFoundException()).TamponOku(Komutlar.DurumTamponu)[18]); // bayt 18 = CPU °C (docs/01)
        Motor.EfektAyarla(ayar.EtkinEfekt, ayar.EfektAyarlari);
        Motor.Degisti += AyarDegisti;
        _kayitZamanlayici.Tick += (_, _) => Kaydet();

        WindowsIleBaslat.YoluTazele();
        // Oturum açılışında BIOS / CC kendi kaydını (mavi) geç yazabiliyor: birkaç saniye sonra bir kez daha yaz.
        _ = Task.Delay(TimeSpan.FromSeconds(4)).ContinueWith(_ => Kontrol?.YenidenUygula());

        MainWindow = new MainWindow();
        _tepsi = new Tepsi();
        if (!e.Args.Contains(WindowsIleBaslat.TepsiArgumani)) MainWindow.Show();
        base.OnStartup(e);
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

    /// <summary>Kaydırıcı sürüklenirken her değişimde yazmamak için 500 ms birleştirilir.</summary>
    static void AyarDegisti()
    {
        _kayitZamanlayici.Stop();
        _kayitZamanlayici.Start();
    }

    static void Kaydet()
    {
        _kayitZamanlayici.Stop();
        try { Ayarlar.Al(Motor, TepsiyeKucult).Yaz(Ayarlar.VarsayilanYol); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // bir sonraki değişiklikte yeniden denenir
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        Cikiliyor = true; // Windows kapanışını tepsiye küçülterek engelleme
        Kaydet();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Motor is not null) // ikinci örnek hiçbir şey başlatmadı; ayar dosyasının üzerine varsayılan yazmasın
        {
            Kaydet();
            Motor.Dispose(); // önce motor: kapanırken Kontrol'e kare göndermesin
            // Güvenli kapanış: efektin son karesi (ör. nefesin karanlık anı) klavyede kalmasın, sabit renk yazılır.
            Kontrol?.Ayarla(Motor.Temel);
            Kontrol?.BirTurGonder();
        }
        _tepsi?.Dispose();
        Kontrol?.Dispose();
        _dchu?.Dispose();
        _tekOrnek?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);
}
