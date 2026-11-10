using Microsoft.Win32;

namespace MonsterKlavye;

/// <summary>HKCU\...\Run kaydı (yönetici gerekmez). Açılışta pencere gösterilmez, yalnız tepsi.</summary>
static class WindowsIleBaslat
{
    const string Anahtar = @"Software\Microsoft\Windows\CurrentVersion\Run", Ad = "MonsterKlavye";
    public const string TepsiArgumani = "--tepsi";

    public static bool Acik
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(Anahtar);
            return k?.GetValue(Ad) is not null;
        }
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(Anahtar);
            if (value) k.SetValue(Ad, $"\"{Environment.ProcessPath}\" {TepsiArgumani}");
            else k.DeleteValue(Ad, throwOnMissingValue: false);
        }
    }

    /// <summary>Exe taşındıysa (ör. Debug → yayın) kayıt eski yolu göstermesin.</summary>
    public static void YoluTazele()
    {
        if (Acik) Acik = true;
    }
}
