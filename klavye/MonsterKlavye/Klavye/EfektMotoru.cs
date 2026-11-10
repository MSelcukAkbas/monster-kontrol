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

    bool Calisiyor => _temel.Acik && !ReferenceEquals(_efekt, Efektler.Sabit); // kilit içinde çağrılır

    void Uygula()
    {
        lock (_kilit)
        {
            if (Calisiyor) _uyan.Release();
            else _gonder(_temel);
        }
        Degisti?.Invoke();
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
            _faz += dt * _ayarlar.Carpan;
            var kare = _efekt.Hesapla(new(_faz, _temel.Renk, _ayarlar.Skala, _sicaklik));
            // Efekt parlaklığı renge katılır, donanım parlaklığı kullanıcınınkinde kalır: karede en çok 1 DLL yazması.
            // Ayrı parlaklık komutu yazma sayısını ikiye katlayıp CPU'yu %1'in üstüne çıkarıyordu (Ateş: %1,4).
            _gonder(_temel with { Renk = kare.Renk.Olcekle(kare.Parlaklik) });
        }
    }

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
