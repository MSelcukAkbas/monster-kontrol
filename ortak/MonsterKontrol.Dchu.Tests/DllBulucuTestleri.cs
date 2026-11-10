using MonsterKontrol.Dchu;

namespace MonsterKontrol.Dchu.Tests;

public sealed class DllBulucuTestleri : IDisposable
{
    readonly string _kok = Directory.CreateTempSubdirectory("dchu-test-").FullName;

    public void Dispose() => Directory.Delete(_kok, recursive: true);

    [Fact]
    public void DLL_iceren_acpibridge1_klasorunu_bulur()
    {
        Directory.CreateDirectory(Path.Combine(_kok, "acpibridge1.inf_amd64_bos"));
        var dolu = Directory.CreateDirectory(Path.Combine(_kok, "acpibridge1.inf_amd64_dolu")).FullName;
        File.WriteAllText(Path.Combine(dolu, DllBulucu.DllAdi), "");

        Assert.Equal(Path.Combine(dolu, DllBulucu.DllAdi), DllBulucu.DriverStoredanBul(_kok));
    }

    [Fact]
    public void Birden_fazlaysa_en_yenisini_secer()
    {
        var eski = Directory.CreateDirectory(Path.Combine(_kok, "acpibridge1.inf_amd64_eski")).FullName;
        var yeni = Directory.CreateDirectory(Path.Combine(_kok, "acpibridge1.inf_amd64_yeni")).FullName;
        File.WriteAllText(Path.Combine(eski, DllBulucu.DllAdi), "");
        File.WriteAllText(Path.Combine(yeni, DllBulucu.DllAdi), "");
        File.SetLastWriteTimeUtc(Path.Combine(eski, DllBulucu.DllAdi), DateTime.UtcNow.AddDays(-30));

        Assert.Equal(Path.Combine(yeni, DllBulucu.DllAdi), DllBulucu.DriverStoredanBul(_kok));
    }

    [Fact]
    public void DLL_yoksa_null() =>
        Assert.Null(DllBulucu.DriverStoredanBul(_kok));

    [Fact]
    public void Klasor_yoksa_null() =>
        Assert.Null(DllBulucu.DriverStoredanBul(Path.Combine(_kok, "yok")));
}
