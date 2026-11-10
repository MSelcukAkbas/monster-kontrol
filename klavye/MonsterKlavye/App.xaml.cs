using System.Windows;
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

    DchuCihazi? _dchu;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            _dchu = new DchuCihazi();
            Kontrol = new KlavyeKontrol(_dchu);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
        {
            DonanimHatasi = ex.Message;
        }
        // shortcut: açılış durumu sabit; ayar kaydı K4'te gelince oradan okunacak
        Motor = new EfektMotoru(new KlavyeDurumu(new Rgb(0xFF, 0x6A, 0x00), 80, true),
            d => Kontrol?.Ayarla(d),
            () => (_dchu ?? throw new DllNotFoundException()).TamponOku(Komutlar.DurumTamponu)[18]); // bayt 18 = CPU °C (docs/01)
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Motor?.Dispose(); // önce motor: kapanırken Kontrol'e kare göndermesin
        Kontrol?.Dispose();
        _dchu?.Dispose();
        base.OnExit(e);
    }
}
