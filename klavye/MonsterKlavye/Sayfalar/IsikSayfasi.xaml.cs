using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonsterKlavye.Klavye;
using Wpf.Ui.Appearance;

namespace MonsterKlavye.Sayfalar;

public partial class IsikSayfasi : UserControl
{
    internal static readonly (string Ad, Rgb Renk)[] HazirRenkler =
    [
        ("Ateş turuncusu", new(0xFF, 0x6A, 0x00)), ("Kırmızı", new(0xFF, 0x20, 0x20)),
        ("Pembe", new(0xFF, 0x2D, 0x8A)), ("Mor", new(0x9B, 0x4D, 0xFF)),
        ("Mavi", new(0x2F, 0x7B, 0xFF)), ("Camgöbeği", new(0x00, 0xD5, 0xE0)),
        ("Yeşil", new(0x2B, 0xD9, 0x6B)), ("Beyaz", new(0xF2, 0xF2, 0xF2)),
    ];

    // Tuş satırları: (etiket, genişlik oranı)
    static readonly (string, double)[][] Satirlar =
    [
        [.. "Esc F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12".Split(' ').Select(k => (k, 1d))],
        [.. "` 1 2 3 4 5 6 7 8 9 0 -".Split(' ').Select(k => (k, 1d)), ("⌫", 1.8)],
        [("Tab", 1.4), .. "Q W E R T Y U I O P [ ]".Split(' ').Select(k => (k, 1d))],
        [("Caps", 1.7), .. "A S D F G H J K L ;".Split(' ').Select(k => (k, 1d)), ("Enter", 2)],
        [("Shift", 2.2), .. "Z X C V B N M , .".Split(' ').Select(k => (k, 1d)), ("Shift", 2.4)],
        [("Fn", 1), ("Ctrl", 1), ("⊞", 1), ("Alt", 1), ("", 6), ("Alt", 1), ("Ctrl", 1), ("◀", 1), ("▼", 1), ("▶", 1)],
    ];

    readonly SolidColorBrush _tusZemin = new(), _tusKenar = new(), _tusYazi = new();

    Rgb _renk;
    string _renkAdi = "";
    int _parlaklik;
    bool _acik;
    bool _surukleniyor;

    public IsikSayfasi()
    {
        InitializeComponent();
        TuslariOlustur();
        HazirlariOlustur();
        App.Motor.Degisti += MotordanAl; // Efektler sayfası da rengi / açıklığı değiştirebilir
        MotordanAl();
    }

    /// <summary>Durumun sahibi motor (açılışta klavyeye o yazar); sayfa yalnız gösterir.</summary>
    void MotordanAl()
    {
        var t = App.Motor.Temel;
        if (t.Renk != _renk || _renkAdi == "") _renkAdi = AdiBul(t.Renk);
        (_renk, _parlaklik, _acik) = (t.Renk, t.ParlaklikYuzde, t.Acik);
        Yenile(gonder: false);
    }

    static string AdiBul(Rgb renk) => HazirRenkler.FirstOrDefault(h => h.Renk == renk).Ad ?? "Özel renk";

    // ---------- durum → ekran + klavye ----------

    void Yenile(bool gonder = true)
    {
        var p = _acik ? _parlaklik : 0;
        var renk = Color.FromRgb(_renk.R, _renk.G, _renk.B);

        RenkAdi.Text = _acik ? _renkAdi : "Kapalı";
        DurumYazi.Text = _acik ? $"Açık · %{_parlaklik} parlaklık · {App.Motor.Efekt.Ad}" : "Kapalı";
        GucDugmesi.IsChecked = _acik;
        ParlaklikYazi.Text = $"%{_parlaklik}";
        if (ParlaklikKaydirici.Value != _parlaklik) ParlaklikKaydirici.Value = _parlaklik;
        if (!HexKutusu.IsKeyboardFocusWithin) HexKutusu.Text = _renk.Hex;

        Parilti.Color = Color.FromArgb((byte)(p / 100d * 140), renk.R, renk.G, renk.B);
        KasaParilti.Color = renk;
        KasaParilti.Opacity = p / 100d * 0.75;
        _tusZemin.Color = Karistir(Color.FromRgb(0x25, 0x25, 0x29), renk, p / 100d * 0.28);
        _tusKenar.Color = Karistir(Color.FromRgb(0x33, 0x33, 0x38), renk, p / 100d * 0.6);
        _tusYazi.Color = p > 25 ? Color.FromRgb(0xF4, 0xF4, 0xF4) : Color.FromRgb(0x77, 0x77, 0x7C);

        TonIsaretiniKonumla();
        HazirSeciminiGoster();
        // Uygulamanın vurgu rengi = klavye rengi
        ApplicationAccentColorManager.Apply(renk, ApplicationTheme.Dark);

        if (gonder) App.Motor.TemelAyarla(new KlavyeDurumu(_renk, _parlaklik, _acik));
    }

    static Color Karistir(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    void RenkSec(Rgb renk, string ad)
    {
        (_renk, _renkAdi) = (renk, ad);
        if (!_acik) _acik = true; // renk seçmek ışığı açar
        Yenile(); // önce yeni renk motora gitsin: EfektAyarla'nın Degisti'si alanları motordan geri okur
        // Skalalı efekt seçilen rengi kullanmaz: kullanıcı rengi görsün diye sabit renge geç.
        // Nefes, kalp, flaş bu rengi kullanır, sürer.
        if (App.Motor.Efekt.SkalaVar) App.Motor.EfektAyarla(Efektler.Sabit);
    }

    // ---------- klavye çizimi ----------

    void TuslariOlustur()
    {
        foreach (var satir in Satirlar)
        {
            var izgara = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            foreach (var (etiket, oran) in satir)
            {
                izgara.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(oran, GridUnitType.Star) });
                var tus = new Border
                {
                    Height = 22, Margin = new Thickness(2, 0, 2, 0), CornerRadius = new CornerRadius(4),
                    BorderThickness = new Thickness(1), Background = _tusZemin, BorderBrush = _tusKenar,
                    Child = new TextBlock
                    {
                        Text = etiket, FontSize = 9, Foreground = _tusYazi,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    },
                };
                Grid.SetColumn(tus, izgara.ColumnDefinitions.Count - 1);
                izgara.Children.Add(tus);
            }
            TusSatirlari.Children.Add(izgara);
        }
    }

    // ---------- hazır renkler ----------

    void HazirlariOlustur()
    {
        foreach (var (ad, renk) in HazirRenkler)
        {
            var dugme = new Button
            {
                Style = (Style)FindResource("RenkDugmesi"),
                Background = new SolidColorBrush(Color.FromRgb(renk.R, renk.G, renk.B)),
                ToolTip = ad,
                DataContext = renk,
            };
            AutomationPropertiesAdi(dugme, ad);
            dugme.Click += (_, _) => RenkSec(renk, ad);
            Hazirlar.Items.Add(dugme);
        }
    }

    static void AutomationPropertiesAdi(DependencyObject o, string ad) =>
        System.Windows.Automation.AutomationProperties.SetName(o, ad);

    void HazirSeciminiGoster()
    {
        foreach (Button d in Hazirlar.Items)
            d.Tag = d.DataContext is Rgb r && r == _renk ? "secili" : null;
    }

    // ---------- ton şeridi ----------

    void TonIsaretiniKonumla()
    {
        var genislik = TonSerit.ActualWidth - TonIsaret.Width;
        if (genislik <= 0) return;
        Canvas.SetLeft(TonIsaret, _renk.Ton / 360 * genislik);
        TonIsaret.Fill = new SolidColorBrush(Color.FromRgb(_renk.R, _renk.G, _renk.B));
    }

    void TonuKonumdanSec(double x)
    {
        var genislik = Math.Max(1, TonSerit.ActualWidth - TonIsaret.Width);
        var ton = Math.Clamp((x - TonIsaret.Width / 2) / genislik, 0, 1) * 359;
        RenkSec(Rgb.TondanRgb(ton), "Özel renk");
    }

    void Ton_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _surukleniyor = true;
        TonSerit.Focus();
        TonSerit.CaptureMouse();
        TonuKonumdanSec(e.GetPosition(TonSerit).X);
    }

    void Ton_MouseMove(object sender, MouseEventArgs e)
    {
        if (_surukleniyor) TonuKonumdanSec(e.GetPosition(TonSerit).X);
    }

    void Ton_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _surukleniyor = false;
        TonSerit.ReleaseMouseCapture();
    }

    void Ton_KeyDown(object sender, KeyEventArgs e)
    {
        var adim = e.Key switch { Key.Left => -5, Key.Right => 5, Key.PageDown => -30, Key.PageUp => 30, _ => 0 };
        if (adim == 0) return;
        RenkSec(Rgb.TondanRgb(_renk.Ton + adim), "Özel renk");
        e.Handled = true;
    }

    void Ton_SizeChanged(object sender, SizeChangedEventArgs e) => TonIsaretiniKonumla();

    // ---------- HEX ----------

    void Hex_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) HexUygula();
    }

    void Hex_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => HexUygula();

    void HexUygula()
    {
        if (Rgb.HexCoz(HexKutusu.Text) is { } renk)
        {
            var hazir = HazirRenkler.FirstOrDefault(h => h.Renk == renk);
            RenkSec(renk, hazir.Ad ?? "Özel renk");
        }
        HexKutusu.Text = _renk.Hex; // geçersizse eski değere dön
    }

    // ---------- parlaklık / güç ----------

    void Parlaklik_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized || (int)Math.Round(e.NewValue) == _parlaklik) return;
        _parlaklik = (int)Math.Round(e.NewValue);
        if (!_acik && _parlaklik > 0) _acik = true;
        Yenile();
    }

    void Kademe_Click(object sender, RoutedEventArgs e)
    {
        _parlaklik = int.Parse((string)((FrameworkElement)sender).Tag);
        _acik = true;
        Yenile();
    }

    void Guc_Click(object sender, RoutedEventArgs e)
    {
        _acik = GucDugmesi.IsChecked == true;
        Yenile();
    }
}
