using System.Windows;
using System.Windows.Input;
using MonsterPerformans.Cekirdek;
using MonsterPerformans.Cekirdek.Guc;
using MonsterPerformans.Sayfalar;
using Wpf.Ui.Controls;

namespace MonsterPerformans;

/// <summary>Tepsi simgesine sol tıkla açılan panel: sıcaklıklar, 3 hazır profil, varsa tek kullanıcı profili.</summary>
public partial class HizliPanel : Window
{
    public HizliPanel()
    {
        InitializeComponent();
        foreach (var p in HazirProfiller.Hepsi)
        {
            var d = new Wpf.Ui.Controls.Button { Content = p.Ad, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(3, 0, 3, 0), Tag = p };
            d.Click += async (_, _) => await App.Uygula(p);
            HazirDugmeler.Children.Add(d);
        }
        KullaniciDugmesi.Click += async (_, _) =>
        {
            if (KullaniciDugmesi.Tag is Profil p) await App.Uygula(p);
        };
        App.DurumDegisti += Profiller;
        App.YeniOlcum += Olcum;
        Closed += (_, _) =>
        {
            App.DurumDegisti -= Profiller;
            App.YeniOlcum -= Olcum;
        };
    }

    void Profiller()
    {
        var etkin = App.DonanimVar && !App.Uygulaniyor;
        foreach (Wpf.Ui.Controls.Button d in HazirDugmeler.Children)
        {
            d.IsEnabled = etkin;
            d.Appearance = ((Profil)d.Tag).Ad == App.EtkinProfil ? ControlAppearance.Primary : ControlAppearance.Secondary;
        }
        var k = App.Ayarlar.Profiller.FirstOrDefault(p => p.Ad == App.Ayarlar.TepsiProfili);
        KullaniciDugmesi.Visibility = k is null ? Visibility.Collapsed : Visibility.Visible;
        KullaniciDugmesi.Tag = k;
        KullaniciDugmesi.Content = k?.Ad;
        KullaniciDugmesi.IsEnabled = etkin;
        KullaniciDugmesi.Appearance = k is not null && k.Ad == App.EtkinProfil ? ControlAppearance.Primary : ControlAppearance.Secondary;
        Islem.Text = App.Uygulaniyor ? "Uygulanıyor…" : App.SonIslem ?? "";
        Islem.Visibility = string.IsNullOrEmpty(Islem.Text) ? Visibility.Collapsed : Visibility.Visible;
    }

    void Olcum(Olcum o)
    {
        if (!IsVisible) return;
        if (o.Dchu.Gecerli) PanoSayfasi.Sicaklik(CpuC, o.Dchu.CpuC);
        else CpuC.Text = "–";
        if (o.Gpu?.C is { } g) PanoSayfasi.Sicaklik(GpuC, g);
        else
        {
            GpuC.Text = "uykuda";
            GpuC.SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
        }
    }

    /// <summary>Tepsi simgesinin üstünde, çalışma alanının sağ alt köşesinde açılır.</summary>
    public void Goster()
    {
        Show(); // Olcum yalnız görünürken çizer
        Profiller();
        if (App.Gecmis.Son(1) is [var son]) Olcum(son);
        UpdateLayout(); // SizeToContent: boyut içerik dolunca kesinleşsin
        var alan = SystemParameters.WorkArea;
        Left = alan.Right - ActualWidth;
        Top = alan.Bottom - ActualHeight;
        Activate();
    }

    void Ac_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        App.PencereyiGoster();
    }

    void Window_Deactivated(object sender, EventArgs e) => Hide();

    void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Hide();
    }
}
