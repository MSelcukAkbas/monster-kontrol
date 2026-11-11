using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MonsterPerformans.Cekirdek.Fan;
using MonsterPerformans.Cekirdek.Guc;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace MonsterPerformans.Sayfalar;

public partial class ProfillerSayfasi : UserControl
{
    static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    // Tasarımdaki sıra (docs/04), DCHU değerinin sırası değil
    static readonly (object Deger, string Ad)[] Gucler =
        [(GucSeviyesi.Tasarruf, "Tasarruf"), (GucSeviyesi.Sessiz, "Sessiz"), (GucSeviyesi.Eglence, "Eğlence"), (GucSeviyesi.Performans, "Performans")];
    static readonly (object Deger, string Ad)[] Fanlar =
        [(FanModu.Otomatik, "Otomatik"), (FanModu.Maksimum, "Maksimum"), (FanModu.Ozel, "Özel eğri")];

    bool _yukleniyor;
    string? _seciliAd;

    Profil? Secili => (Liste.SelectedItem as ListBoxItem)?.Tag as Profil;

    public ProfillerSayfasi()
    {
        InitializeComponent();
        Doldur(GucKutusu, Gucler);
        Doldur(FanKutusu, Fanlar);
        try { Doldur(PlanKutusu, WindowsGucPlani.Planlar().Select(p => ((object)p.Guid, p.Ad))); }
        catch (InvalidOperationException) { } // plan adı okunamadı: liste boş kalır, profil planı korunur
        Loaded += (_, _) =>
        {
            App.DurumDegisti += Yenile;
            Yenile();
        };
        Unloaded += (_, _) => App.DurumDegisti -= Yenile;
    }

    static void Doldur(ComboBox k, IEnumerable<(object Deger, string Ad)> ogeler)
    {
        foreach (var (d, ad) in ogeler) k.Items.Add(new ComboBoxItem { Content = ad, Tag = d });
    }

    static void Sec(ComboBox k, object deger) =>
        k.SelectedItem = k.Items.Cast<ComboBoxItem>().FirstOrDefault(o => o.Tag.Equals(deger));

    static T? Deger<T>(ComboBox k) => k.SelectedItem is ComboBoxItem { Tag: T d } ? d : default;

    /// <summary>Liste her ayar değişiminde yeniden kurulur (öğeler kayıt: değişince yenisi gelir); seçim adla korunur.</summary>
    void Yenile()
    {
        _yukleniyor = true;
        Liste.Items.Clear();
        foreach (var p in App.Ayarlar.Hepsi)
        {
            var kilitli = HazirProfiller.Kilitli(p);
            var o = new ListBoxItem
            {
                Tag = p,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new SymbolIcon { Symbol = kilitli ? SymbolRegular.LockClosed24 : SymbolRegular.Person24, Margin = new Thickness(0, 0, 12, 0) },
                        new TextBlock { Text = p.Ad, VerticalAlignment = VerticalAlignment.Center },
                    },
                },
            };
            AutomationProperties.SetName(o, kilitli ? $"{p.Ad}, kilitli" : p.Ad);
            Liste.Items.Add(o);
            if (p.Ad == _seciliAd) o.IsSelected = true;
        }
        if (Liste.SelectedItem is null) Liste.SelectedIndex = 0;
        _yukleniyor = false;
        Goster();
    }

    void Liste_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_yukleniyor || Secili is not { } p) return;
        _seciliAd = p.Ad;
        AdHata.Visibility = Visibility.Collapsed;
        Goster();
    }

    void Goster()
    {
        if (Secili is not { } p) return;
        _yukleniyor = true;
        var kilitli = HazirProfiller.Kilitli(p);
        _seciliAd = p.Ad;
        Alanlar.IsEnabled = !kilitli;
        KilitNotu.Visibility = kilitli ? Visibility.Visible : Visibility.Collapsed;
        SilDugmesi.IsEnabled = !kilitli;

        AdKutusu.Text = p.Ad;
        Sec(GucKutusu, p.Guc);
        Sec(PlanKutusu, p.WindowsPlani);
        Frekans.Value = p.FrekansYuzde;
        FrekansYazi.Text = FrekansMetni(p.FrekansYuzde);
        Turbo.IsChecked = p.Turbo;
        Sec(FanKutusu, p.Fan);
        EgriDugmesi.Visibility = p.Fan == FanModu.Ozel ? Visibility.Visible : Visibility.Collapsed;
        Tepside.IsChecked = App.Ayarlar.TepsiProfili == p.Ad;

        UygulaDugmesi.IsEnabled = App.DonanimVar && !App.Uygulaniyor;
        UygulaDugmesi.Content = App.Uygulaniyor ? "Uygulanıyor…" : "Uygula";
        Sonuc.Text = App.SonIslem ?? "";
        _yukleniyor = false;
    }

    static string FrekansMetni(int yuzde) => yuzde >= 100
        ? "%100 · sınırsız (4,1 GHz)"
        : string.Format(Tr, "%{0} · {1:0.00} GHz", yuzde, ProfilUygulayici.FrekansMhz(yuzde) / 1000.0);

    /// <summary>Yalnız kullanıcı profili değişir; kayıt hemen diske yazılır.</summary>
    void Degistir(Func<Profil, Profil> f)
    {
        if (_yukleniyor || Secili is not { } p || HazirProfiller.Kilitli(p)) return;
        var yeni = f(p);
        if (yeni == p) return;
        App.Ayarlar.Degistir(p, yeni);
        _seciliAd = yeni.Ad;
        App.AyarlarDegisti();
    }

    void Guc_Changed(object sender, SelectionChangedEventArgs e) => Degistir(p => p with { Guc = Deger<GucSeviyesi>(GucKutusu) });

    void Plan_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Deger<Guid>(PlanKutusu) is var g && g != Guid.Empty) Degistir(p => p with { WindowsPlani = g });
    }

    void Frekans_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        var y = (int)Math.Round(e.NewValue);
        FrekansYazi.Text = FrekansMetni(y);
        Degistir(p => p with { FrekansYuzde = y });
    }

    void Turbo_Changed(object sender, RoutedEventArgs e) => Degistir(p => p with { Turbo = Turbo.IsChecked == true });

    void Fan_Changed(object sender, SelectionChangedEventArgs e) => Degistir(p => p with { Fan = Deger<FanModu>(FanKutusu) });

    void Ad_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AdKaydet();
    }

    void Ad_LostFocus(object sender, RoutedEventArgs e) => AdKaydet();

    void AdKaydet()
    {
        if (_yukleniyor || Secili is not { } p || HazirProfiller.Kilitli(p)) return;
        var ad = AdKutusu.Text.Trim();
        if (ad == p.Ad) return;
        if (!App.Ayarlar.AdUygun(ad))
        {
            AdHata.Text = string.IsNullOrEmpty(ad) ? "Ad boş olamaz." : "Bu ad kullanılıyor.";
            AdHata.Visibility = Visibility.Visible;
            AdKutusu.Text = p.Ad;
            return;
        }
        AdHata.Visibility = Visibility.Collapsed;
        Degistir(x => x with { Ad = ad });
    }

    void Tepside_Changed(object sender, RoutedEventArgs e)
    {
        if (_yukleniyor || Secili is not { } p || HazirProfiller.Kilitli(p)) return;
        if (Tepside.IsChecked == true) App.Ayarlar.TepsiProfili = p.Ad; // eskisinin yerine geçer
        else if (App.Ayarlar.TepsiProfili == p.Ad) App.Ayarlar.TepsiProfili = null;
        App.AyarlarDegisti();
    }

    void Kopyala_Click(object sender, RoutedEventArgs e)
    {
        if (Secili is not { } p) return;
        _seciliAd = App.Ayarlar.Kopyala(p).Ad;
        App.AyarlarDegisti();
        AdKutusu.Focus();
        AdKutusu.SelectAll();
    }

    async void Sil_Click(object sender, RoutedEventArgs e)
    {
        if (Secili is not { } p || HazirProfiller.Kilitli(p)) return;
        var cevap = await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Profili sil",
            Content = $"\"{p.Ad}\" silinsin mi? Bu geri alınamaz.",
            PrimaryButtonText = "Sil",
            CloseButtonText = "Vazgeç",
        }.ShowDialogAsync();
        if (cevap != Wpf.Ui.Controls.MessageBoxResult.Primary) return;
        App.Ayarlar.Sil(p);
        _seciliAd = null;
        App.AyarlarDegisti();
    }

    async void Uygula_Click(object sender, RoutedEventArgs e)
    {
        if (Secili is { } p) await App.Uygula(p);
    }

    void Egri_Click(object sender, RoutedEventArgs e)
    {
        if (Secili is { } p && Window.GetWindow(this) is MainWindow w) w.Git("fan", p.Ad);
    }
}
