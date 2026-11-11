using System.Buffers.Binary;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MonsterPerformans.Cekirdek.Fan;
using MonsterPerformans.Cekirdek.Guc;
using Wpf.Ui.Controls;

namespace MonsterPerformans.Sayfalar;

/// <summary>Kullanıcı profilinin CPU ve GPU eğrisi. Hazır profillerin eğrisi yok (fanları Otomatik/Maksimum).</summary>
public partial class FanSayfasi : UserControl
{
    const int CpuT4 = 100, GpuT4 = 99; // EC'deki 4. nokta (docs/04)

    FanEgrisi _cpu, _gpu;
    bool _gpuSecili, _yukleniyor;
    string? _profilAd, _sonMesaj;

    Profil? Profil => App.Ayarlar.Profiller.FirstOrDefault(p => p.Ad == _profilAd);

    public FanSayfasi()
    {
        InitializeComponent();
        Editor.Degisti += e =>
        {
            if (_gpuSecili) _gpu = e;
            else _cpu = e;
            Bilgi();
        };
        Loaded += (_, _) =>
        {
            App.DurumDegisti += Dugmeler;
            Yenile();
        };
        Unloaded += (_, _) => App.DurumDegisti -= Dugmeler;
    }

    /// <summary>Profiller sayfasındaki "Eğriyi düzenle".</summary>
    public void ProfilSec(string ad)
    {
        _profilAd = ad;
        if (IsLoaded) Yenile();
    }

    void Yenile()
    {
        _yukleniyor = true;
        ProfilKutusu.Items.Clear();
        foreach (var p in App.Ayarlar.Profiller) ProfilKutusu.Items.Add(new ComboBoxItem { Content = p.Ad, Tag = p.Ad });
        if (Profil is null) _profilAd = App.Ayarlar.Profiller.FirstOrDefault()?.Ad;
        ProfilKutusu.SelectedItem = ProfilKutusu.Items.Cast<ComboBoxItem>().FirstOrDefault(o => Equals(o.Tag, _profilAd));
        _yukleniyor = false;

        var bos = Profil is null;
        Bos.Visibility = bos ? Visibility.Visible : Visibility.Collapsed;
        foreach (var o in new UIElement[] { Ust, Cerceve, Baytlar, Alt })
            o.Visibility = bos ? Visibility.Collapsed : Visibility.Visible;
        ProfilKutusu.IsEnabled = !bos;
        Yukle();
    }

    void Yukle()
    {
        if (Profil is not { } p) return;
        _cpu = Gecerli(p.CpuEgrisi, CpuT4);
        _gpu = Gecerli(p.GpuEgrisi, GpuT4);
        _sonMesaj = null;
        Goster();
    }

    // Boş ya da elle bozulmuş eğri Dengeli ile başlar
    static FanEgrisi Gecerli(FanEgrisi e, int t4) =>
        e.Hata(t4) is null && e.T3 < EgriEditoru.KorumaBandi ? e : HazirEgriler.Dengeli;

    void Goster()
    {
        Editor.Ayarla(_gpuSecili ? _gpu : _cpu, _gpuSecili ? GpuT4 : CpuT4);
        CpuDugmesi.Appearance = _gpuSecili ? ControlAppearance.Secondary : ControlAppearance.Primary;
        GpuDugmesi.Appearance = _gpuSecili ? ControlAppearance.Primary : ControlAppearance.Secondary;
        Bilgi();
        Dugmeler();
    }

    void Bilgi()
    {
        var k = FanKontrol.Komut14(_cpu, _gpu);
        string Egim(int i) => string.Join(" / ", Enumerable.Range(0, 3)
            .Select(j => BinaryPrimitives.ReadUInt16BigEndian(k.AsSpan(14 + 6 * i + 2 * j)).ToString(CultureInfo.InvariantCulture)));
        Baytlar.Text = $"Komut 14 · noktalar {string.Join(" ", k[2..10].Select(b => b.ToString("X2")))}"
                       + $" · CPU eğimleri {Egim(0)} · GPU eğimleri {Egim(1)}";

        var p = Profil;
        Durum.Text = p is null ? ""
            : p.CpuEgrisi != _cpu || p.GpuEgrisi != _gpu ? "Kaydedilmemiş değişiklik var."
            : _sonMesaj ?? (p.Fan == FanModu.Ozel ? "Kayıtlı." : "Kayıtlı eğri; profilin fan modu Özel değil (kaydedince Özel olur).");
    }

    void Dugmeler()
    {
        var profilVar = Profil is not null;
        KaydetDugmesi.IsEnabled = profilVar;
        UygulaDugmesi.IsEnabled = profilVar && App.DonanimVar && !App.Uygulaniyor;
        UygulaDugmesi.Content = App.Uygulaniyor ? "Uygulanıyor…" : "Kaydet ve uygula";
    }

    /// <summary>Eğriler profile yazılır, profilin fan modu Özel olur.</summary>
    Profil? Kaydet()
    {
        if (Profil is not { } p) return null;
        var yeni = p with { CpuEgrisi = _cpu, GpuEgrisi = _gpu, Fan = FanModu.Ozel };
        if (yeni != p)
        {
            App.Ayarlar.Degistir(p, yeni);
            App.AyarlarDegisti();
        }
        return yeni;
    }

    void Kaydet_Click(object sender, RoutedEventArgs e)
    {
        if (Kaydet() is null) return;
        _sonMesaj = "Kaydedildi (fan modu: Özel eğri). Donanıma henüz yazılmadı.";
        Bilgi();
    }

    async void Uygula_Click(object sender, RoutedEventArgs e)
    {
        if (Kaydet() is not { } p) return;
        await App.Uygula(p);
        _sonMesaj = App.SonIslem;
        Bilgi();
    }

    void Profil_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_yukleniyor || ProfilKutusu.SelectedItem is not ComboBoxItem { Tag: string ad }) return;
        _profilAd = ad;
        Yukle();
    }

    void Fan_Click(object sender, RoutedEventArgs e)
    {
        _gpuSecili = ReferenceEquals(sender, GpuDugmesi);
        Goster();
    }

    void Hazir_Click(object sender, RoutedEventArgs e)
    {
        _cpu = _gpu = HazirEgriler.Hepsi[int.Parse((string)((FrameworkElement)sender).Tag, CultureInfo.InvariantCulture)].Egri;
        Goster();
    }
}
