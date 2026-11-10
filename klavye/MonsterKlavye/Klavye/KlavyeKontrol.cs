using MonsterKontrol.Dchu;

namespace MonsterKlavye.Klavye;

/// <summary>Klavyenin istenen durumu. Parlaklık %0–100; kapalıyken donanıma 0 gider.</summary>
public sealed record KlavyeDurumu(Rgb Renk, int ParlaklikYuzde, bool Acik)
{
    // Tamsayı ile yuvarla: 50 × 2.55 kayan noktada 127.4999… çıkıp 127'ye iniyordu.
    public byte DonanimParlakligi => Acik ? (byte)((Math.Clamp(ParlaklikYuzde, 0, 100) * 255 + 50) / 100) : (byte)0;
}

/// <summary>
/// İstenen durumu klavyeye gönderir. Hızlı gelen değişiklikleri (kaydırıcı sürükleme) birleştirir:
/// arka plandaki döngü her turda yalnız EN SON durumu ve yalnız DEĞİŞEN komutları yazar.
/// Arayüz iş parçacığı hiç beklemez.
/// </summary>
public sealed class KlavyeKontrol : IDisposable
{
    readonly IDchu _dchu;
    readonly SemaphoreSlim _sinyal = new(0);
    readonly CancellationTokenSource _iptal = new();
    readonly Task _dongu;
    readonly Lock _kilit = new();
    KlavyeDurumu? _istenen;
    KlavyeDurumu? _gonderilen;

    public KlavyeKontrol(IDchu dchu)
    {
        _dchu = dchu;
        _dongu = Task.Run(Dongu);
    }

    /// <summary>Son gönderme hatası (DLL yoksa vb.); arayüz gösterebilir.</summary>
    public Exception? SonHata { get; private set; }

    public void Ayarla(KlavyeDurumu durum)
    {
        lock (_kilit) _istenen = durum;
        _sinyal.Release();
    }

    /// <summary>Her şeyi yeniden yaz (açılış, uykudan dönüş: BIOS kendi kaydını uygulamış olabilir).</summary>
    public void YenidenUygula()
    {
        lock (_kilit) _gonderilen = null;
        _sinyal.Release();
    }

    /// <summary>İki durum arasındaki farkı DCHU argümanlarına çevirir (saf fonksiyon, test edilir).</summary>
    internal static List<uint> Fark(KlavyeDurumu? onceki, KlavyeDurumu yeni)
    {
        var komutlar = new List<uint>(2);
        // Kapalıyken renk göndermeye gerek yok, ama açılınca doğru renk olsun diye renk önce yazılır.
        if (onceki is null || onceki.Renk != yeni.Renk)
            komutlar.Add(Komutlar.KlavyeRenk(yeni.Renk.R, yeni.Renk.G, yeni.Renk.B));
        if (onceki is null || onceki.DonanimParlakligi != yeni.DonanimParlakligi)
            komutlar.Add(Komutlar.KlavyeParlaklik(yeni.DonanimParlakligi));
        return komutlar;
    }

    internal void BirTurGonder()
    {
        KlavyeDurumu? istenen, onceki;
        lock (_kilit) (istenen, onceki) = (_istenen, _gonderilen);
        if (istenen is null) return;
        try
        {
            foreach (var arguman in Fark(onceki, istenen))
                _dchu.Yaz(Komutlar.KlavyeIsigi, arguman);
            lock (_kilit)
                if (ReferenceEquals(_gonderilen, onceki)) _gonderilen = istenen; // arada YenidenUygula geldiyse ezme
            SonHata = null;
        }
        catch (Exception e)
        {
            SonHata = e;
        }
    }

    async Task Dongu()
    {
        try
        {
            while (true)
            {
                await _sinyal.WaitAsync(_iptal.Token);
                while (_sinyal.CurrentCount > 0) await _sinyal.WaitAsync(_iptal.Token); // biriken sinyalleri yut
                BirTurGonder();
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _iptal.Cancel();
        try { _dongu.Wait(TimeSpan.FromSeconds(1)); } catch (AggregateException) { }
        _sinyal.Dispose();
        _iptal.Dispose();
    }
}
