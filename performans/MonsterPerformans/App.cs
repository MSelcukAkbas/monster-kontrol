using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using MonsterKontrol.Dchu;
using MonsterPerformans.Cekirdek;
using MonsterPerformans.Cekirdek.Fan;
using MonsterPerformans.Cekirdek.Guc;
using MonsterPerformans.Cekirdek.Sensorler;
using MonsterPerformans.Cekirdek.Uzaktan;

namespace MonsterPerformans;

public static class App
{
    public static string? DonanimHatasi { get; private set; }
    public static string? DllYolu { get; private set; }
    public static bool NvmlVar { get; private set; }

    /// <summary>Son 30 dk (1 Hz). Yalnız arayüz iş parçacığında yazılır ve okunur.</summary>
    public static Gecmis<Olcum> Gecmis { get; } = new(1800);
    public static string? PlanAdi { get; private set; }
    public static event Action<Olcum>? YeniOlcum;

    /// <summary>Kullanıcı profilleri ve tepsi profili. Yalnız arayüz iş parçacığında.</summary>
    public static PerformansAyarlari Ayarlar { get; private set; } = new();

    /// <summary>Bu oturumda son uygulanan profil. Açılışta null: uygulama açılırken donanıma yazmaz.</summary>
    public static string? EtkinProfil { get; private set; }

    /// <summary>Son uygulamanın adım adım özeti (pano durum satırı).</summary>
    public static string? SonIslem { get; private set; }

    /// <summary>Bir uygulama sürüyor: tüm profil düğmeleri griye döner (iki profilin yazmaları karışmasın).</summary>
    public static bool Uygulaniyor { get; private set; }

    public static bool DonanimVar => _uygulayici is not null;

    /// <summary>"Ölçüm yap" sürüyor. Profil düğmeleri griye döner; acil durum yine çalışır.</summary>
    public static bool OlcumSuruyor { get; private set; }

    /// <summary>Kullanıcının profil yazması beklemeli.</summary>
    public static bool Mesgul => Uygulaniyor || OlcumSuruyor;

    /// <summary>Profil listesi, etkin profil ya da uygulama durumu değişti.</summary>
    public static event Action? DurumDegisti;

    /// <summary>Çıkış başladı: pencere kapanışı artık tepsiye gizlemez.</summary>
    public static bool Cikiliyor { get; private set; }

    static ProfilUygulayici? _uygulayici;
    static FanKontrol? _fan;
    static bool _fanDegisti; // fan bu oturumda otomatikten çıkarıldı: çıkışta güvenli duruma dön (bekçi yok)

    /// <summary>Kullanıcının son seçtiği profil: acil durum bunun kısılmış halini yazar, bitince bunu geri yükler.</summary>
    static Profil? _temel;
    static readonly AcilDurum _acil = new();
    static AcilKademe _uygulananKademe;

    /// <summary>Uygulanmış acil kademe (Yok: acil durum yok).</summary>
    public static AcilKademe AcilKademe => _uygulananKademe;

    const int KayitGun = 30;
    static volatile string? _kayitKlasoru; // null: kayıt kapalı. Örnekleyici iş parçacığı okur
    static CsvKayitci? _csv; // yalnız örnekleyici iş parçacığında
    static SurecKayitci? _surecler; // kayıt kapalıyken de ölçer (Pano özeti)
    public static Gecmis<IReadOnlyList<SurecPayi>> SurecGecmisi { get; } = new(1800);
    static readonly int CpuMWSirasi = Array.FindIndex(PdhSayaclari.Sayaclar, s => s.Sutun == "cpu_mW");
    static string? _csvKlasoru;
    static DateOnly _temizlik;

    static DchuCihazi? _dchu;
    static PdhSayaclari? _pdh;
    static Nvml? _nvml;
    static KomutKanali? _kanal;
    static readonly CancellationTokenSource _dur = new();
    static Task? _ornekleme;
    static Dispatcher _arayuz = null!;

    /// <summary>Pencere ve tepsi dışında her şeyi başlatır (Monster Kontrol çağırır).</summary>
    public static void Baslat(Dispatcher arayuz)
    {
        _arayuz = arayuz;
        try
        {
            _dchu = new DchuCihazi();
            DllYolu = _dchu.DllYolu;
            _fan = new FanKontrol(_dchu);
            _uygulayici = new ProfilUygulayici(_dchu, new WindowsGucPlani(), _fan);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException) { DonanimHatasi = ex.Message; }
        _pdh = new PdhSayaclari();
        _nvml = Nvml.Ac();
        NvmlVar = _nvml is not null;
        Ayarlar = PerformansAyarlari.Oku(PerformansAyarlari.VarsayilanYol);
        AyarlariYansit();
        // Önceki oturumun son 30 dk'sı: grafik ve Uygulamalar sayfası yeniden açılışta boş kalmasın (örnekleme başlamadan)
        if (_kayitKlasoru is { } gecmisKlasoru)
            try
            {
                foreach (var o in GecmisYukleyici.Olcumler(gecmisKlasoru, DateTime.Now, 1800)) Gecmis.Ekle(o);
                foreach (var p in GecmisYukleyici.Surecler(gecmisKlasoru, DateTime.Now, 1800)) SurecGecmisi.Ekle(p);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Debug.WriteLine($"Geçmiş okunamadı: {ex.Message}"); }

        // Açılışta yalnız okuma; yeniden uygulama ayarı açıksa son profil 3 sn sonra yazılır
        if (_dchu is not null)
        {
            _ornekleme = Task.Run(() => Ornekle(new Ornekleyici(_dchu, _pdh, _nvml, new BaglamOkuyucu(new WindowsGucPlani())), _dur.Token));
            _ = YenidenUygula("Açılış");
            SystemEvents.PowerModeChanged += GucDegisti;
        }

        _kanal = new KomutKanali(new Dictionary<string, Func<object>>
        {
            ["durum"] = Durum,
            ["profiller"] = () => new { tamam = true, etkin = EtkinProfil, profiller = Ayarlar.Hepsi.Select(p => new { ad = p.Ad, guc = p.Guc.ToString(), fan = p.Fan.ToString(), hazir = HazirProfiller.Kilitli(p) }) },
        }, f => arayuz.Invoke(f));

        WindowsIleBaslat.YoluTazele();
    }

    /// <summary>1 Hz; DCHU ve PDH okumaları bloklar, arayüz iş parçacığında yapılmaz.</summary>
    static async Task Ornekle(Ornekleyici ornekleyici, CancellationToken dur)
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
                    _ = _arayuz.BeginInvoke(() =>
                    {
                        Gecmis.Ekle(o);
                        PlanAdi = ad;
                        YeniOlcum?.Invoke(o);
                        AcilKontrol(o);
                    });
                    Kaydet(o);
                    FanDongusu(o);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Debug.WriteLine($"Örnek alınamadı: {ex.Message}"); // tek örnek kaçar, döngü sürer
                }
            } while (await saat.WaitForNextTickAsync(dur));
        }
        catch (OperationCanceledException) { }
    }

    // true başlar: önceki oturum çöktüyse asılı kalan 0x68 sabit hızı ilk örnekte 0x69 ile temizlenir
    static bool _yazilimFani = true;
    static int? _sonGpuC;

    /// <summary>
    /// Örnekleyici iş parçacığında, 1 Hz. Özel fanlı profilde (acil yokken) fanları EC değil uygulama yönetir: iki eğri
    /// hesaplanıp 0x68 ile yazılır. EC'nin eğrisine dönüşte (0x69) CPU fanı %95'e fırlayıp 1 puan/sn iniyor, bu yüzden
    /// 0x69 yalnız koşul bozulunca (acil Maksimum, Özel olmayan profil) ve çıkışta. GPU uykudayken EC GPU fanını durdurur:
    /// o zaman <see cref="FanEgrisi.UykuHedefi"/>. Çökmede son hız asılı kalır; EC'nin 90°C+ koruması sürer.
    /// </summary>
    static void FanDongusu(Olcum o)
    {
        // Profil yazılırken karışma; EC'nin sıfır tamponunda son yazılan hız kalsın (0x69 sıçratır)
        if (_dchu is null || Uygulaniyor || !o.Dchu.Gecerli) return;
        var p = _temel;
        try
        {
            if (p is { Fan: FanModu.Ozel } && _uygulananKademe == AcilKademe.Yok)
            {
                // NVML yükte arada boş döner: uyanıkken son geçerli GPU sıcaklığı, hiç yoksa CPU'nunki
                int? gpuC = null;
                if (!GucDurumu.Uyuyor()) gpuC = _sonGpuC = o.Gpu?.C ?? _sonGpuC ?? o.Dchu.CpuC;
                var h = FanEgrisi.Hedef(p.CpuEgrisi, p.GpuEgrisi, o.Dchu.CpuC, gpuC);
                _dchu.Yaz(Komutlar.FanHizi, FanEgrisi.Duty(h.Cpu) | (uint)FanEgrisi.Duty(h.Gpu) << 8);
                _yazilimFani = true;
            }
            else if (_yazilimFani)
            {
                _dchu.Yaz(Komutlar.FanOtomatik, 0x0F);
                _yazilimFani = false;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"Fan döngüsü yazamadı: {ex.Message}"); }
    }

    /// <summary>
    /// Profili arka planda uygular (eğri doğrulaması ~1 sn bekleyebilir). Aynı anda yalnız bir uygulama.
    /// Acil durum sürüyorsa profilin kısılmış hali yazılır. <paramref name="kalici"/>: açılışta yeniden uygulanacak profil olarak sakla.
    /// </summary>
    public static Task Uygula(Profil p, bool kalici = true, string? neden = null)
    {
        if (_uygulayici is null || Uygulaniyor) return Task.CompletedTask;
        _temel = p;
        if (kalici && Ayarlar.SonProfil != p.Ad) { Ayarlar.SonProfil = p.Ad; AyarlarDegisti(); }
        var k = _uygulananKademe = _acil.Kademe;
        var baslik = (neden is null ? "" : neden + ": ") + p.Ad + (k == AcilKademe.Yok ? "" : $" (acil durum: {AcilDurum.Ad(k)})");
        return ProfilYaz(AcilDurum.Uyarla(p, k), baslik);
    }

    public static Task GuvenliDurumaDon()
    {
        if (_uygulayici is null || Uygulaniyor) return Task.CompletedTask;
        _temel = null;
        if (Ayarlar.SonProfil is not null) { Ayarlar.SonProfil = null; AyarlarDegisti(); }
        _uygulananKademe = AcilKademe.Yok; // acil durum sürüyorsa sonraki örnek Dengeli üstünden yeniden kısar
        return GuvenliYaz("Güvenli durum");
    }

    static Task ProfilYaz(Profil p, string baslik)
    {
        var politika = Ayarlar.DogrulamaHatasi;
        return Calistir(u =>
        {
            if (p.Fan != FanModu.Otomatik) _fanDegisti = true; // yazma yarıda kalsa da çıkışta geri alınsın
            return u.Uygula(p, politika);
        }, baslik, _temel?.Ad);
    }

    static Task GuvenliYaz(string baslik) => Calistir(u =>
    {
        var s = u.GuvenliDurum();
        _fanDegisti = false;
        return s;
    }, baslik, null);

    /// <summary>
    /// Her örnekte (arayüz iş parçacığı). Kademe değişince profili kısarak yazar; başka bir yazma sürüyorsa
    /// sonraki örnekte yeniden dener (kademe ancak yazma başlarken işaretlenir).
    /// </summary>
    static void AcilKontrol(Olcum o)
    {
        var k = _acil.Adim(o.Dchu.Gecerli ? o.Dchu.CpuC : null, o.Gpu?.C);
        if (k == _uygulananKademe || Uygulaniyor || _uygulayici is null) return;
        _uygulananKademe = k;
        var neden = (o.Dchu.Gecerli ? $"CPU {o.Dchu.CpuC}°C" : "CPU okunamadı") + (o.Gpu?.C is { } g ? $", GPU {g}°C" : "");
        if (k != AcilKademe.Yok)
            _ = ProfilYaz(AcilDurum.Uyarla(_temel ?? HazirProfiller.Dengeli, k), $"Acil durum ({neden}): {AcilDurum.Ad(k)}");
        else if (_temel is { } t)
            _ = ProfilYaz(t, $"Acil durum bitti ({neden}): {t.Ad} geri yüklendi");
        else
            _ = GuvenliYaz($"Acil durum bitti ({neden}): güvenli durum");
    }

    /// <summary>docs/03: açılışta ve uykudan dönüşte 3 sn bekle (EC ve sürücü otursun), sonra son profili yeniden yaz.</summary>
    static async Task YenidenUygula(string neden)
    {
        if (!Ayarlar.YenidenUygula) return;
        await Task.Delay(TimeSpan.FromSeconds(3));
        if (Ayarlar.YenidenUygula && !Cikiliyor && !OlcumSuruyor && (_temel ?? Ayarlar.Bul(Ayarlar.SonProfil)) is { } p)
            await Uygula(p, neden: neden);
    }

    /// <summary>
    /// Profili geçici uygular, CPU + GPU'yu <paramref name="saniye"/> boyunca yükler, sonra önceki profile
    /// (yoksa güvenli duruma) döner. CPU 95°C'yi görür ya da acil durum başlarsa hemen keser.
    /// </summary>
    public static async Task<(OlcumSonucu? Sonuc, string? Kiyas)> OlcumYap(Profil p, int saniye, Action<string> durum, CancellationToken iptal)
    {
        if (_uygulayici is null || Mesgul) return (null, null);
        OlcumSuruyor = true;
        DurumDegisti?.Invoke();
        var onceki = _temel;
        var ornekler = new List<Olcum>();
        string? kesildi = null;
        using var dur = CancellationTokenSource.CreateLinkedTokenSource(iptal);
        void Topla(Olcum o)
        {
            ornekler.Add(o);
            if (o.Dchu.Gecerli && o.Dchu.CpuC >= 95) kesildi ??= $"CPU {o.Dchu.CpuC}°C'de kesildi";
            else if (_acil.Kademe != AcilKademe.Yok) kesildi ??= "acil durum nedeniyle kesildi";
            if (kesildi is not null) dur.Cancel();
            durum($"Yük altında {ornekler.Count}/{saniye} sn · CPU {(o.Dchu.Gecerli ? $"{o.Dchu.CpuC}°C" : "–")}"
                  + (o.Gpu?.C is { } g ? $" · GPU {g}°C" : ""));
        }
        var gpuYuku = false;
        try
        {
            durum($"{p.Ad} uygulanıyor…");
            await Uygula(p, kalici: false, neden: "Ölçüm");
            dur.CancelAfter(TimeSpan.FromSeconds(saniye));
            YeniOlcum += Topla;
            var cpu = Yuk.Cpu(dur.Token);
            var gpu = Yuk.Gpu(dur.Token);
            try { gpuYuku = await gpu; }
            catch (Exception ex) // GPU yükü isteğe bağlı: ne olursa olsun CPU yükü sürer
            {
                Olay($"Ölçüm: GPU yükü başlamadı ({ex.Message}), yalnız CPU yüklendi");
            }
            await cpu;
        }
        finally
        {
            dur.Cancel(); // hata ile çıkılsa da yük iş parçacıkları dursun
            YeniOlcum -= Topla;
            durum("Önceki duruma dönülüyor…");
            while (Uygulaniyor) await Task.Delay(200); // acil yazma sürüyorsa bitsin, geri dönüş düşmesin
            if (onceki is not null) await Uygula(onceki, kalici: false, neden: "Ölçüm bitti");
            else
            {
                _temel = null;
                _uygulananKademe = AcilKademe.Yok; // acil durum sürüyorsa sonraki örnek yeniden kısar
                await GuvenliYaz("Ölçüm bitti: güvenli durum");
            }
            OlcumSuruyor = false;
            DurumDegisti?.Invoke();
        }

        if (kesildi is null && iptal.IsCancellationRequested) kesildi = "iptal edildi";
        var s = OlcumSonucu.Ozetle(p.Ad, DateTime.Now, saniye, ornekler, gpuYuku, kesildi);
        if (s is null) return (null, null);
        var kiyas = s.Kiyas(OlcumSonucu.Oku(OlcumSonucu.VarsayilanYol));
        try { OlcumSonucu.Ekle(OlcumSonucu.VarsayilanYol, s); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Debug.WriteLine($"Ölçüm saklanamadı: {ex.Message}"); }
        Olay($"Ölçüm {p.Ad} {saniye} sn: {s.Satir()}" + (kiyas is null ? "" : $" · {kiyas}"));
        return (s, kiyas);
    }

    /// <summary>Kanal "durum": son örnek ve uygulama durumu (arayüz iş parçacığında).</summary>
    static object Durum()
    {
        var o = Gecmis.Son(1).FirstOrDefault();
        var d = o?.Dchu;
        return new
        {
            tamam = true,
            donanim = DonanimVar,
            hata = DonanimHatasi,
            zaman = o?.Zaman,
            cpu = d is { Gecerli: true } ? new { c = d.Value.CpuC, duty = d.Value.CpuDuty, rpm = d.Value.CpuRpm } : null,
            gpu = d is null ? null : new { c = o!.Gpu?.C, duty = d.Value.GpuDuty, rpm = d.Value.GpuRpm, watt = o.Gpu?.Watt, kullanim = o.Gpu?.Kullanim },
            mhz = o?.GercekMHz,
            plan = PlanAdi,
            profil = EtkinProfil,
            fan = _temel?.Fan.ToString(),
            acil = _uygulananKademe.ToString(),
            mesgul = Mesgul,
            sonIslem = SonIslem,
        };
    }

    static void GucDegisti(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) _arayuz.BeginInvoke(() => YenidenUygula("Uykudan dönüş"));
    }

    /// <summary>Örnekleyici iş parçacığında: günlük CSV (ham tampon dahil); 30 günden eskiler dakikalık özete iner.</summary>
    static void Kaydet(Olcum o)
    {
        var klasor = _kayitKlasoru;
        if (klasor != _csvKlasoru)
        {
            _csv?.Dispose();
            _csv = klasor is null ? null : new CsvKayitci(klasor);
            _csvKlasoru = klasor;
        }
        try
        {
            _surecler ??= new SurecKayitci();
            _surecler.Klasor = klasor;
            var paylar = _surecler.Yaz(o.Zaman, CpuMWSirasi < o.Pdh.Length ? o.Pdh[CpuMWSirasi] : null);
            _ = _arayuz.BeginInvoke(() => SurecGecmisi.Ekle(paylar));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Debug.WriteLine($"Süreç örneği alınamadı: {ex.Message}"); // ana CSV etkilenmesin
        }
        if (_csv is null) return;
        var bugun = DateOnly.FromDateTime(o.Zaman);
        if (bugun != _temizlik)
        {
            _temizlik = bugun;
            CsvKayitci.Temizle(klasor!, KayitGun, bugun);
            SurecKayitci.Temizle(klasor!, KayitGun, bugun);
        }
        _csv.Yaz(o, new UygulamaDurumu(EtkinProfil, _temel?.Fan.ToString(), _uygulananKademe.ToString(), OlcumSuruyor));
    }

    /// <summary>Uygulama, acil durum ve doğrulama olayları: kayıt klasöründe olaylar.log.</summary>
    static void Olay(string satir)
    {
        if (_kayitKlasoru is not { } k) return;
        try
        {
            Directory.CreateDirectory(k);
            File.AppendAllText(Path.Combine(k, "olaylar.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {satir}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Debug.WriteLine($"Olay yazılamadı: {ex.Message}"); }
    }

    static async Task Calistir(Func<ProfilUygulayici, IReadOnlyList<AdimSonucu>> islem, string baslik, string? etkin)
    {
        if (_uygulayici is not { } u || Uygulaniyor) return;
        Uygulaniyor = true;
        DurumDegisti?.Invoke();
        try
        {
            var s = await Task.Run(() => islem(u));
            EtkinProfil = etkin;
            SonIslem = $"{baslik} · {Ozet(s)}";
            Olay(SonIslem);
        }
        finally
        {
            Uygulaniyor = false;
            DurumDegisti?.Invoke();
        }
    }

    public static string Ozet(IReadOnlyList<AdimSonucu> s) => string.Join(" · ", s.Select(a => a.Durum switch
    {
        AdimDurumu.Dogrulandi => $"{a.Adim} doğrulandı ✓",
        AdimDurumu.Yazildi => $"{a.Adim} yazıldı",
        _ => $"{a.Adim} başarısız: {a.Hata}",
    }));

    /// <summary>Profil listesi değişince çağrılır: diske yazar ve sayfaları/tepsiyi yeniler.</summary>
    public static void AyarlarDegisti()
    {
        AyarlariYansit();
        try { Ayarlar.Yaz(PerformansAyarlari.VarsayilanYol); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Debug.WriteLine($"Ayarlar yazılamadı: {ex.Message}"); }
        DurumDegisti?.Invoke();
    }

    static void AyarlariYansit()
    {
        _acil.Esik = Ayarlar.AcilEsik;
        _kayitKlasoru = Ayarlar.KayitTut ? Ayarlar.Kayit : null;
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

    /// <summary>Güvenli kapanış: örnekleme durur, fan bu oturumda değiştiyse otomatiğe döner.</summary>
    public static void Durdur()
    {
        SystemEvents.PowerModeChanged -= GucDegisti;
        _kanal?.Dispose();
        _dur.Cancel();
        _ornekleme?.Wait(TimeSpan.FromSeconds(2));
        _csv?.Dispose();
        _surecler?.Dispose();
        if (_fanDegisti)
        {
            try { _uygulayici?.FanOtomatigeDon(AcilDurum.Uyarla(_temel ?? HazirProfiller.Dengeli, _uygulananKademe).Guc); }
            catch (Exception ex) { Debug.WriteLine($"Çıkışta fan güvenli duruma dönmedi: {ex.Message}"); }
        }
        _nvml?.Dispose();
        _pdh?.Dispose();
        _dchu?.Dispose();
    }
}
