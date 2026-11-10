using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonsterKlavye.Klavye;
using MonsterKlavye.Sayfalar;

namespace MonsterKlavye;

/// <summary>Tepsi simgesine sol tıkla açılan küçük panel. Kendi durumu yok: her şeyi App.Motor'dan okur ve ona yazar.</summary>
public partial class HizliPanel : Window
{
    static readonly string[] PaneldekiEfektler = ["ates", "nefes", "sabit"];
    static EfektMotoru Motor => App.Motor;
    bool _yukleniyor;

    public HizliPanel()
    {
        InitializeComponent();
        foreach (var (ad, renk) in IsikSayfasi.HazirRenkler)
        {
            var d = new Button
            {
                Style = (Style)FindResource("RenkDugmesi"),
                Width = 26, Height = 26, Margin = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(renk.R, renk.G, renk.B)),
                ToolTip = ad,
                DataContext = renk,
            };
            AutomationProperties.SetName(d, ad);
            d.Click += (_, _) => RenkSec(renk);
            Renkler.Children.Add(d);
        }
        foreach (var id in PaneldekiEfektler)
        {
            var efekt = Efektler.Hepsi.Single(e => e.Id == id);
            var d = new Button
            {
                Style = (Style)FindResource("PanelDugmesi"),
                Content = id == "sabit" ? "Sabit" : efekt.Ad,
                Margin = new Thickness(3, 0, 3, 0),
                DataContext = efekt,
            };
            d.Click += (_, _) => EfektSec(efekt);
            EfektDugmeleri.Children.Add(d);
        }
        Motor.Degisti += Yenile;
        Yenile();
    }

    void Yenile()
    {
        _yukleniyor = true;
        var t = Motor.Temel;
        Ornek.Background = new SolidColorBrush(Color.FromRgb(t.Renk.R, t.Renk.G, t.Renk.B));
        Guc.IsChecked = t.Acik;
        Parlaklik.Value = t.ParlaklikYuzde;
        ParlaklikYazi.Text = $"%{t.ParlaklikYuzde}";
        foreach (Button d in Renkler.Children) d.Tag = d.DataContext is Rgb r && r == t.Renk ? "secili" : null;
        foreach (Button d in EfektDugmeleri.Children) d.Tag = ReferenceEquals(d.DataContext, Motor.Efekt) ? "secili" : null;
        _yukleniyor = false;
    }

    /// <summary>Tepsi simgesinin üstünde, çalışma alanının sağ alt köşesinde açılır.</summary>
    public void Goster()
    {
        Show(); // SizeToContent boyutu ilk gösterimde hesaplanır
        var alan = SystemParameters.WorkArea;
        Left = alan.Right - ActualWidth;
        Top = alan.Bottom - ActualHeight;
        Activate();
    }

    void RenkSec(Rgb renk)
    {
        Motor.TemelAyarla(Motor.Temel with { Renk = renk, Acik = true });
        if (Motor.Efekt.SkalaVar) Motor.EfektAyarla(Efektler.Sabit); // Işık sayfasıyla aynı: skalalı efekt seçilen rengi kullanmaz
    }

    void EfektSec(Efekt efekt)
    {
        if (!Motor.Temel.Acik) Motor.TemelAyarla(Motor.Temel with { Acik = true });
        Motor.EfektAyarla(efekt, ReferenceEquals(efekt, Motor.Efekt) ? Motor.Ayarlar : null);
    }

    // Click değil Checked/Unchecked: UIA (ekran okuyucu) Toggle, Click olayını tetiklemiyor
    void Guc_Click(object sender, RoutedEventArgs e)
    {
        if (!_yukleniyor) Motor.TemelAyarla(Motor.Temel with { Acik = Guc.IsChecked == true });
    }

    void Parlaklik_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_yukleniyor || !IsInitialized) return;
        var p = (int)Math.Round(e.NewValue);
        Motor.TemelAyarla(Motor.Temel with { ParlaklikYuzde = p, Acik = Motor.Temel.Acik || p > 0 });
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
