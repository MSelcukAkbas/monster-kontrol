using MonsterPerformans.Cekirdek.Fan;

namespace MonsterPerformans.Cekirdek.Guc;

/// <summary>Birikimli acil kademeleri: her kademe öncekinin üstüne ekler (docs/03 sırası: önce frekans, sonra fan, sonra güç).</summary>
public enum AcilKademe { Yok, Frekans, Fan, Guc }

/// <summary>
/// Acil durum durum makinesi (1 Hz, saf: donanıma dokunmaz). Ham sıcaklıkla çalışır, yumuşatma yok.
/// Giriş: CPU ≥ eşik ya da GPU ≥ 87°C, 3 örnek üst üste. Hâlâ sıcaksa her 30 sn bir kademe yükselir.
/// Çıkış: CPU (ve uyanıksa GPU) 85°C altında 60 sn → profil olduğu gibi geri uygulanır.
/// </summary>
public sealed class AcilDurum
{
    public const int VarsayilanEsik = 92, EnYuksekEsik = 95, GpuEsik = 87, CikisC = 85;
    public const int GirisOrnek = 3, KademeSn = 30, CikisSn = 60, GpuTutmaSn = 5;

    int _esik = VarsayilanEsik, _sicak, _kademeSuresi, _serin, _gpuYasi;
    int? _sonGpu;

    public AcilKademe Kademe { get; private set; }

    /// <summary>92–95 arası (docs/04): daha düşüğü normal yükte (TCC platosu 90–91°C) boşuna tetiklenir.</summary>
    public int Esik { get => _esik; set => _esik = Math.Clamp(value, VarsayilanEsik, EnYuksekEsik); }

    /// <summary>CPU geçersizse null (sensör hatası); GPU uykudaysa null.</summary>
    public AcilKademe Adim(int? cpuC, int? gpuC)
    {
        // NVML yükte sıcaklığı arada boş döndürür: son geçerli değer 5 sn tutulur, yoksa GPU tetiği hiç 3 örnek biriktiremez
        if (gpuC is { } g) (_sonGpu, _gpuYasi) = (g, 0);
        else if (++_gpuYasi > GpuTutmaSn) _sonGpu = null;
        gpuC = _sonGpu;

        var sicak = cpuC >= _esik || gpuC >= GpuEsik;
        if (Kademe == AcilKademe.Yok)
        {
            _sicak = sicak ? _sicak + 1 : 0;
            if (_sicak >= GirisOrnek) { Kademe = AcilKademe.Frekans; _kademeSuresi = 0; _serin = 0; }
            return Kademe;
        }

        // Geçersiz CPU okuması serin sayılmaz: sensör bozukken acil durumdan çıkılmaz
        _serin = cpuC < CikisC && (gpuC is null || gpuC < CikisC) ? _serin + 1 : 0;
        if (_serin >= CikisSn) { Kademe = AcilKademe.Yok; _sicak = 0; return Kademe; }

        _kademeSuresi = sicak ? _kademeSuresi + 1 : 0;
        if (_kademeSuresi >= KademeSn && Kademe < AcilKademe.Guc) { Kademe++; _kademeSuresi = 0; }
        return Kademe;
    }

    /// <summary>Profilin acil kademedeki hali. Kullanıcı acil durumda başka profil seçerse o da bu süzgeçten geçer.</summary>
    public static Profil Uyarla(Profil p, AcilKademe k) => k switch
    {
        AcilKademe.Yok => p,
        AcilKademe.Frekans => p with { FrekansYuzde = Math.Min(p.FrekansYuzde, 80) },
        AcilKademe.Fan => p with { FrekansYuzde = Math.Min(p.FrekansYuzde, 80), Fan = FanModu.Maksimum },
        _ => p with { FrekansYuzde = Math.Min(p.FrekansYuzde, 60), Fan = FanModu.Maksimum, Guc = GucSeviyesi.Sessiz },
    };

    public static string Ad(AcilKademe k) => k switch
    {
        AcilKademe.Frekans => "frekans %80'e indirildi",
        AcilKademe.Fan => "frekans %80, fanlar en yüksek",
        AcilKademe.Guc => "frekans %60, fanlar en yüksek, güç Sessiz",
        _ => "bitti",
    };
}
