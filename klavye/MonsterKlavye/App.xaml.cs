using System.Windows;
using MonsterKlavye.Klavye;
using MonsterKontrol.Dchu;

namespace MonsterKlavye;

public partial class App : Application
{
    /// <summary>Klavye denetimi; DLL bulunamadıysa null (arayüz uyarı gösterir, yine açılır).</summary>
    public static KlavyeKontrol? Kontrol { get; private set; }

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
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Kontrol?.Dispose();
        _dchu?.Dispose();
        base.OnExit(e);
    }
}
