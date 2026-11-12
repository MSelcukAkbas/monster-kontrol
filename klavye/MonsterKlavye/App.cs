using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using MonsterKlavye.Klavye;
using MonsterKlavye.Uzaktan;
using MonsterKontrol.Dchu;

namespace MonsterKlavye;

public static class App
{
    /// <summary>Klavye denetimi; DLL bulunamadıysa null (arayüz uyarı gösterir, yine açılır).</summary>
    public static KlavyeKontrol? Kontrol { get; private set; }

    /// <summary>Klavye durumunun tek sahibi (temel durum + efekt). DLL yoksa da vardır, gönderim boşa gider.</summary>
    public static EfektMotoru Motor { get; private set; } = null!;

    public static string? DonanimHatasi { get; private set; }
    public static string? DllYolu { get; private set; }

    /// <summary>Çıkış başladı: pencere kapanışı artık tepsiye küçültmez.</summary>
    public static bool Cikiliyor { get; private set; }

    static DchuCihazi? _dchu;
    static KomutKanali? _kanal;
    static Dispatcher _arayuz = null!;
    static readonly DispatcherTimer _kayitZamanlayici = new() { Interval = TimeSpan.FromMilliseconds(500) };

    /// <summary>Pencere ve tepsi dışında her şeyi başlatır (Monster Kontrol çağırır).</summary>
    public static void Baslat(Dispatcher arayuz)
    {
        _arayuz = arayuz;
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
        Motor = new EfektMotoru(ayar.Durum,
            d => Kontrol?.Ayarla(d),
            () => (_dchu ?? throw new DllNotFoundException()).TamponOku(Komutlar.DurumTamponu)[18]); // bayt 18 = CPU °C (docs/01)
        Motor.EfektAyarla(ayar.EtkinEfekt, ayar.EfektAyarlari);
        Motor.Degisti += AyarDegisti;
        _kayitZamanlayici.Tick += (_, _) => Kaydet();

        WindowsIleBaslat.YoluTazele();
        GecikmeliYenidenUygula();
        SystemEvents.PowerModeChanged += GucDegisti;
        SystemEvents.SessionSwitch += OturumDegisti;
        _kanal = new KomutKanali(arayuz);
    }

    /// <summary>Oturum açılışında / uyanınca BIOS veya CC kendi kaydını (mavi) geç yazabiliyor: birkaç saniye sonra bir kez daha yaz.</summary>
    static void GecikmeliYenidenUygula() =>
        _ = Task.Delay(TimeSpan.FromSeconds(4)).ContinueWith(_ => Kontrol?.YenidenUygula());

    // SystemEvents kendi iş parçacığında çağırabilir: motor Degisti'yi UI'da bekler, işi UI iş parçacığına taşı.
    static void GucDegisti(object sender, PowerModeChangedEventArgs e) => _arayuz.BeginInvoke(() =>
    {
        if (e.Mode == PowerModes.Suspend) Duraklat(true);
        else if (e.Mode == PowerModes.Resume) Duraklat(false);
    });

    static void OturumDegisti(object sender, SessionSwitchEventArgs e) => _arayuz.BeginInvoke(() =>
    {
        if (e.Reason == SessionSwitchReason.SessionLock) Duraklat(true);
        else if (e.Reason == SessionSwitchReason.SessionUnlock) Duraklat(false);
    });

    /// <summary>Uyku / kilitte efekt durur (sabit renk kalır); dönüşte sürer ve her şey yeniden yazılır.</summary>
    static void Duraklat(bool dur)
    {
        Motor.Duraklatildi = dur;
        if (dur) return;
        Kontrol?.YenidenUygula();
        GecikmeliYenidenUygula();
    }

    public static void PencereyiGoster()
    {
        var p = Application.Current.MainWindow;
        p.Show();
        if (p.WindowState == WindowState.Minimized) p.WindowState = WindowState.Normal;
        p.Activate();
    }

    public static void Cik()
    {
        Cikiliyor = true;
        Application.Current.Shutdown();
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
        try { Ayarlar.Al(Motor).Yaz(Ayarlar.VarsayilanYol); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // bir sonraki değişiklikte yeniden denenir
    }

    /// <summary>Güvenli kapanış: kanal, kayıt, son sabit renk, DLL.</summary>
    public static void Durdur()
    {
        if (Motor is not null) // ikinci örnek hiçbir şey başlatmadı; ayar dosyasının üzerine varsayılan yazmasın
        {
            _kanal?.Dispose(); // önce kanal: kapanırken dışarıdan komut gelmesin
            SystemEvents.PowerModeChanged -= GucDegisti; // statik olaylar: bırakılmazsa sızar (belgelenmiş)
            SystemEvents.SessionSwitch -= OturumDegisti;
            Kaydet();
            Motor.Dispose(); // önce motor: kapanırken Kontrol'e kare göndermesin
            // Güvenli kapanış: efektin son karesi (ör. nefesin karanlık anı) klavyede kalmasın, sabit renk yazılır.
            Kontrol?.Ayarla(Motor.Temel);
            Kontrol?.BirTurGonder();
        }
        Kontrol?.Dispose();
        _dchu?.Dispose();
    }
}
