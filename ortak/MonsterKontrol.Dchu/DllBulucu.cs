using Microsoft.Win32;

namespace MonsterKontrol.Dchu;

/// <summary>
/// InsydeDCHU.dll'i bulur. Sürücü paketinin parçasıdır; DriverStore klasör adındaki hash sürücü
/// güncellenince değiştiği için yol sabit yazılmaz. Store uygulamasının kopyası yüklenemiyor (erişim engellendi).
/// </summary>
public static class DllBulucu
{
    public const string DllAdi = "InsydeDCHU.dll";
    const string ServisAnahtari = @"SYSTEM\CurrentControlSet\Services\CCDCHUService";

    public static string Bul() =>
        ServistenBul()
        ?? DriverStoredanBul(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\DriverStore\FileRepository"))
        ?? throw new DllNotFoundException(
            "InsydeDCHU.dll bulunamadı. Clevo/Monster 'Acpi Bridge' sürücüsü (CCDCHUService) kurulu mu?");

    static string? ServistenBul()
    {
        using var anahtar = Registry.LocalMachine.OpenSubKey(ServisAnahtari);
        if (anahtar?.GetValue("ImagePath") is not string yol)
            return null;
        // ImagePath tırnaklı ve %SystemRoot% içerebilir
        yol = Environment.ExpandEnvironmentVariables(yol.Trim().Trim('"'));
        var aday = Path.Combine(Path.GetDirectoryName(yol) ?? "", DllAdi);
        return File.Exists(aday) ? aday : null;
    }

    /// <summary>Yedek: acpibridge1.inf_amd64_* klasörleri arasında DLL'i içeren en yenisini seçer.</summary>
    internal static string? DriverStoredanBul(string fileRepository)
    {
        if (!Directory.Exists(fileRepository))
            return null;
        return Directory.EnumerateDirectories(fileRepository, "acpibridge1.inf_amd64_*")
            .Select(d => Path.Combine(d, DllAdi))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }
}
