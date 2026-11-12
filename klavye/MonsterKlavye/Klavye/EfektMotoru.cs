using System.Diagnostics;

namespace MonsterKlavye.Klavye;

/// <summary>
/// Klavye durumunun tek sahibi: Işık sayfasının temel durumu (renk, parlaklık, açık) + etkin efekt.
/// Klavyeye yalnız bu sınıf gönderir; gönderim kilit içinde yapıldığından eski bir efekt karesi
/// kapatma ya da parlaklık değişikliğinin üzerine yazılamaz. Değişeni süzmeyi <see cref="KlavyeKontrol"/> yapar.
/// </summary>
public sealed class EfektMotoru : IDisposable
{
    public const int KareHizi = 15; // fan uygulamasıyla aynı DLL; 30'a çıkmadan önce test 26 (docs/11 §2.3)

    readonly Action<KlavyeDurumu> _gonder;
    readonly Func<double> _sicaklikOku;
    readonly Lock _kilit = new();
    readonly SemaphoreSlim _uyan = new(0);
    readonly CancellationTokenSource _iptal = new();
    readonly Task? _dongu;

    KlavyeDurumu _temel;
    Efekt _efekt = Efektler.Sabit;
    EfektAyarlari _ayarlar = new(5, Efektler.TamCark);
    double _faz;
    bool _duraklatildi;
    Rgb? _sonKare; // son gönderilen efekt rengi; null = sıradaki kare koşulsuz gider
    double _sonKareYasi;
    Rgb _uyariRengi;
    double _uyariKalan, _uyariFaz; // saniye; kalan > 0 iken uyarı flaşı her şeyin önüne geçer
    double _sicaklik = 50, _sicaklikYasi = double.MaxValue; // yalnız döngü iş parçacığı kullanır

    /// <param name="gonder">Durumu klavyeye iletir (hızlı dönmeli; kilit içinde çağrılır).</param>
    /// <param name="sicaklikOku">CPU °C; yalnız Sıcaklık efektinde saniyede bir çağrılır.</param>
    public EfektMotoru(KlavyeDurumu temel, Action<KlavyeDurumu> gonder, Func<double> sicaklikOku, bool donguyuBaslat = true)
    {
        (_temel, _gonder, _sicaklikOku) = (temel, gonder, sicaklikOku);
        Uygula(); // açılışta istenen durumu yaz (BIOS kendi kaydını uygulamış olabilir)
        if (donguyuBaslat) _dongu = Task.Run(Dongu);
    }

    /// <summary>Temel durum ya da efekt değişti (UI iş parçacığında, değiştiren çağrıdan).</summary>
    public event Action? Degisti;

    public KlavyeDurumu Temel { get { lock (_kilit) return _temel; } }
    public Efekt Efekt { get { lock (_kilit) return _efekt; } }
    public EfektAyarlari Ayarlar { get { lock (_kilit) return _ayarlar; } }

    public void TemelAyarla(KlavyeDurumu temel)
    {
        lock (_kilit) _temel = temel;
        Uygula();
    }

    /// <summary>Aynı efektin ayarı değişirse faz korunur (hız kaydırıcısı efekti zıplatmaz).</summary>
    public void EfektAyarla(Efekt efekt, EfektAyarlari? ayarlar = null)
    {
        lock (_kilit)
        {
            if (!ReferenceEquals(efekt, _efekt)) _faz = 0;
            _efekt = efekt;
            _ayarlar = ayarlar ?? new(5, efekt.VarsayilanSkala ?? Efektler.TamCark);
        }
        Uygula();
    }

    /// <summary>Uyku / kilit: efekt durur, klavyede temel (sabit) renk kalır; kalkınca efekt kaldığı yerden sürer.</summary>
    public bool Duraklatildi
    {
        get { lock (_kilit) return _duraklatildi; }
        set { lock (_kilit) _duraklatildi = value; Gonder(); } // kullanıcı ayarı değil: Degisti yok
    }

    /// <summary>
    /// Anlık uyarı: tam parlaklıkta <paramref name="kez"/> kez yanıp söner (0,25 sn yanık / 0,25 sn sönük), sonra önceki
    /// duruma döner. Işık kapalıyken ya da kilitliyken de yanar. Kullanıcı ayarı değildir: kaydedilmez, Degisti yok.
    /// </summary>
    public void Uyar(Rgb renk, int kez)
    {
        lock (_kilit) (_uyariRengi, _uyariKalan, _uyariFaz) = (renk, kez * UyariPeriyodu, 0);
        Gonder();
    }

    const double UyariPeriyodu = 0.5;

    bool Calisiyor => _uyariKalan > 0 || (!_duraklatildi && _temel.Acik && !ReferenceEquals(_efekt, Efektler.Sabit)); // kilit içinde çağrılır

    void Uygula()
    {
        Gonder();
        Degisti?.Invoke();
    }

    void Gonder()
    {
        lock (_kilit)
        {
            _sonKare = null;
            if (Calisiyor) _uyan.Release();
            else _gonder(_temel);
        }
    }

    /// <summary>Bir kare ilerlet ve gönder (dt saniye).</summary>
    internal void Adim(double dt)
    {
        if (ReferenceEquals(Efekt, Efektler.Sicaklik) && (_sicaklikYasi += dt) >= 1)
        {
            _sicaklikYasi = 0;
            try { _sicaklik = _sicaklikOku(); }
            catch (Exception) { } // okunamazsa son değerle devam (salt okuma, efekti durdurmaya değmez)
        }

        lock (_kilit)
        {
            if (!Calisiyor) return;
            if (_uyariKalan > 0)
            {
                _uyariKalan -= dt;
                _uyariFaz += dt;
                if (_uyariKalan > 0)
                {
                    var yanik = _uyariFaz % UyariPeriyodu < UyariPeriyodu / 2;
                    _gonder(_temel with { Renk = yanik ? _uyariRengi : new(0, 0, 0), ParlaklikYuzde = 100, Acik = true });
                    return;
                }
                _sonKare = null; // uyarı bitti: önceki durum / efekt koşulsuz geri yazılır
                if (!Calisiyor) { _gonder(_temel); return; }
            }
            _faz += dt * _efekt.Carpan(_ayarlar);
            var kare = _efekt.Hesapla(new(_faz, _temel.Renk, _ayarlar.Skala, _sicaklik));
            // Efekt parlaklığı renge katılır, donanım parlaklığı kullanıcınınkinde kalır: karede en çok 1 DLL yazması.
            // Ayrı parlaklık komutu yazma sayısını ikiye katlayıp CPU'yu %1'in üstüne çıkarıyordu (Ateş: %1,4).
            var renk = kare.Renk.Olcekle(kare.Parlaklik);
            _sonKareYasi += dt;
            // Her yazma ~2,7 ms çekirdek CPU'su (sürücü): gözün seçemeyeceği küçük adımlar atlanır,
            // ama fark 0,5 sn'den uzun beklemez (nefesin tepesi / dibi yanlış renkte takılı kalmasın).
            if (_sonKare is { } son && (son == renk || (!Gorunur(son, renk) && _sonKareYasi < 0.5))) return;
            (_sonKare, _sonKareYasi) = (renk, 0);
            _gonder(_temel with { Renk = renk });
        }
    }

    /// <summary>Kanallardan biri %4'ten çok değiştiyse (göz parlaklığı oransal algılar: koyu tonda 1 adım bile görünür).</summary>
    internal static bool Gorunur(Rgb a, Rgb b) =>
        Oran(a.R, b.R) >= 0.04 || Oran(a.G, b.G) >= 0.04 || Oran(a.B, b.B) >= 0.04;

    static double Oran(byte x, byte y) => Math.Abs(x - y) / (double)Math.Max(Math.Max(x, y), (byte)1);

    async Task Dongu()
    {
        using var zamanlayici = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / KareHizi));
        var saat = Stopwatch.StartNew();
        try
        {
            while (true)
            {
                bool calisiyor;
                lock (_kilit) calisiyor = Calisiyor;
                if (!calisiyor)
                {
                    await _uyan.WaitAsync(_iptal.Token); // boştayken hiç uyanma
                    saat.Restart();
                    continue;
                }
                await zamanlayici.WaitForNextTickAsync(_iptal.Token);
                Adim(Math.Min(saat.Elapsed.TotalSeconds, 0.25)); // uykudan dönüşte faz zıplamasın
                saat.Restart();
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _iptal.Cancel();
        try { _dongu?.Wait(TimeSpan.FromSeconds(1)); } catch (AggregateException) { }
        _uyan.Dispose();
        _iptal.Dispose();
    }
}
