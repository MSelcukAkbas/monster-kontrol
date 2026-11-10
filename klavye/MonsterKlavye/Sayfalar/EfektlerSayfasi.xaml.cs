using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MonsterKlavye.Klavye;

namespace MonsterKlavye.Sayfalar;

/// <summary>
/// Efekt galerisi. Önizlemeler klavyeye giden saf efekt fonksiyonlarıyla çizilir (önizleme = klavye).
/// Kart önizlemesi yalnız fare üzerindeyken ya da ▶ ile oynar; zamanlayıcı sayfa açıkken çalışır.
/// </summary>
public partial class EfektlerSayfasi : UserControl
{
    sealed class Kart
    {
        public required Efekt Efekt;
        public required Border Cerceve;
        public required Rectangle Onizleme;
        public required Button Oynat;
        public required TextBlock EtkinIsareti;
        public required Border SkalaIzi;
        public bool Uzerinde, Oynuyor;
        public double Faz;
        public bool Canli => Uzerinde || Oynuyor;
    }

    static readonly string[] PaneldekiRenkler = ["Ateş turuncusu", "Kırmızı", "Mavi", "Yeşil", "Beyaz"];

    readonly List<Kart> _kartlar = [];
    readonly Dictionary<Efekt, EfektAyarlari> _ayarlar =
        Efektler.Hepsi.ToDictionary(e => e, e => new EfektAyarlari(5, e.VarsayilanSkala ?? Efektler.TamCark));
    readonly DispatcherTimer _zamanlayici = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly System.Diagnostics.Stopwatch _saat = new();
    Efekt _secili;
    double _seciliFaz;

    static EfektMotoru Motor => App.Motor;

    public EfektlerSayfasi()
    {
        InitializeComponent();
        SeciliOnizleme.Fill = new SolidColorBrush();
        _secili = Motor.Efekt;
        if (!ReferenceEquals(_secili, Efektler.Sabit)) _ayarlar[_secili] = Motor.Ayarlar;
        KartlariOlustur();
        SkalalariOlustur();
        RenkleriOlustur();
        _zamanlayici.Tick += (_, _) => Ciz();
        Motor.Degisti += Yenile;
        Yenile();
    }

    // ---------- oluşturma ----------

    void KartlariOlustur()
    {
        foreach (var efekt in Efektler.Hepsi)
        {
            var onizleme = new Rectangle { Fill = new SolidColorBrush() };
            var oynat = new Button
            {
                Style = (Style)Resources["OynatDugmesi"], Content = "▶",
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 6, 6),
            };
            AutomationProperties.SetName(oynat, $"Önizle: {efekt.Ad}");
            var ad = new Button { Style = (Style)Resources["AdDugmesi"], Content = efekt.Ad };
            AutomationProperties.SetName(ad, $"Seç: {efekt.Ad}");
            var etkin = new TextBlock
            {
                Text = "●", FontSize = 10, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Klavyede şu an bu efekt var",
            };
            etkin.SetResourceReference(TextBlock.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
            var skalaIzi = new Border
            {
                Width = 28, Height = 8, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Renk skalası ayarlanabilir",
                Visibility = efekt.SkalaVar ? Visibility.Visible : Visibility.Collapsed,
            };

            var adSatiri = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            DockPanel.SetDock(etkin, Dock.Left);
            DockPanel.SetDock(skalaIzi, Dock.Right);
            adSatiri.Children.Add(etkin);
            adSatiri.Children.Add(skalaIzi);
            adSatiri.Children.Add(ad);

            var cerceve = new Border
            {
                Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(10), CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(2), Cursor = Cursors.Hand,
                Child = new StackPanel
                {
                    Children =
                    {
                        new Border
                        {
                            Height = 54, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)),
                            Child = new Grid { Children = { onizleme, oynat } },
                        },
                        adSatiri,
                    },
                },
            };
            cerceve.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
            // Önizleme köşeleri kartın iç köşesiyle aynı yuvarlaklıkta kalsın
            onizleme.RadiusX = onizleme.RadiusY = 8;

            var kart = new Kart { Efekt = efekt, Cerceve = cerceve, Onizleme = onizleme, Oynat = oynat, EtkinIsareti = etkin, SkalaIzi = skalaIzi };
            cerceve.MouseEnter += (_, _) => { kart.Uzerinde = true; ZamanlayiciyiAyarla(); };
            cerceve.MouseLeave += (_, _) => { kart.Uzerinde = false; DurgunCiz(kart); };
            cerceve.MouseLeftButtonUp += (_, _) => Sec(efekt); // düğmelere tıklama buraya gelmez (düğme olayı yutar)
            ad.Click += (_, _) => Sec(efekt);
            oynat.Click += (_, _) =>
            {
                kart.Oynuyor = !kart.Oynuyor;
                oynat.Content = kart.Oynuyor ? "❚❚" : "▶";
                AutomationProperties.SetName(oynat, (kart.Oynuyor ? "Önizlemeyi durdur: " : "Önizle: ") + efekt.Ad);
                if (!kart.Canli) DurgunCiz(kart);
            };
            _kartlar.Add(kart);
            Kartlar.Items.Add(cerceve);
        }
    }

    void SkalalariOlustur()
    {
        foreach (var skala in Efektler.Skalalar)
        {
            var d = new Button { Style = (Style)Resources["SkalaDugmesi"], Background = SkalaFircasi(skala), ToolTip = skala.Ad, DataContext = skala };
            AutomationProperties.SetName(d, $"Skala: {skala.Ad}");
            d.Click += (_, _) => AyarDegistir(_ayarlar[_secili] with { Skala = skala });
            SkalaDugmeleri.Children.Add(d);
        }
    }

    void RenkleriOlustur()
    {
        foreach (var ad in PaneldekiRenkler)
        {
            var renk = IsikSayfasi.HazirRenkler.First(h => h.Ad == ad).Renk;
            var d = new Button
            {
                Style = (Style)FindResource("RenkDugmesi"), Width = 26, Height = 26,
                Background = new SolidColorBrush(Renk(renk)), ToolTip = ad, DataContext = renk,
            };
            AutomationProperties.SetName(d, ad);
            d.Click += (_, _) => Motor.TemelAyarla(Motor.Temel with { Renk = renk, Acik = true });
            Renkler.Items.Add(d);
        }
    }

    // ---------- durum → ekran ----------

    void Sec(Efekt efekt)
    {
        if (ReferenceEquals(efekt, _secili)) return;
        _secili = efekt;
        _seciliFaz = 0;
        Yenile();
    }

    void AyarDegistir(EfektAyarlari ayar)
    {
        _ayarlar[_secili] = ayar;
        if (ReferenceEquals(Motor.Efekt, _secili)) Motor.EfektAyarla(_secili, ayar); // çalışan efekt anında değişir (Yenile'yi o çağırır)
        else Yenile();
    }

    void Yenile()
    {
        var etkin = Motor.Efekt;
        if (!ReferenceEquals(etkin, Efektler.Sabit)) _ayarlar[etkin] = Motor.Ayarlar; // tepsiden seçilen efektin ayarı da görünsün
        var ayar = _ayarlar[_secili];

        foreach (var k in _kartlar)
        {
            k.Cerceve.SetResourceReference(Border.BorderBrushProperty,
                ReferenceEquals(k.Efekt, _secili) ? "AccentFillColorDefaultBrush" : "CardStrokeColorDefaultBrush");
            k.EtkinIsareti.Visibility = ReferenceEquals(k.Efekt, etkin) ? Visibility.Visible : Visibility.Collapsed;
            k.SkalaIzi.Background = SkalaFircasi(_ayarlar[k.Efekt].Skala);
            if (!k.Canli) DurgunCiz(k);
        }

        SeciliAd.Text = _secili.Ad;
        SeciliAciklama.Text = _secili.Aciklama;
        var hareketli = !ReferenceEquals(_secili, Efektler.Sabit);
        HizKaydirici.IsEnabled = hareketli;
        if (HizKaydirici.Value != ayar.Hiz) HizKaydirici.Value = ayar.Hiz;
        HizYazi.Text = !hareketli ? "—" : ayar.Hiz <= 3 ? "Yavaş" : ayar.Hiz <= 6 ? "Orta" : "Hızlı";

        SkalaPaneli.Visibility = _secili.SkalaVar ? Visibility.Visible : Visibility.Collapsed;
        RenkPaneli.Visibility = _secili.SkalaVar ? Visibility.Collapsed : Visibility.Visible;
        Uyari.Visibility = _secili.Uyari ? Visibility.Visible : Visibility.Collapsed;
        SkalaBas.Fill = new SolidColorBrush(Renk(ayar.Skala.Renk(0)));
        SkalaSon.Fill = new SolidColorBrush(Renk(ayar.Skala.Renk(1)));
        SkalaIz.Background = SkalaFircasi(ayar.Skala);
        foreach (Button d in SkalaDugmeleri.Children) d.Tag = ReferenceEquals(d.DataContext, ayar.Skala) ? "secili" : null;
        foreach (Button d in Renkler.Items) d.Tag = d.DataContext is Rgb r && r == Motor.Temel.Renk ? "secili" : null;

        var uygulandi = ReferenceEquals(etkin, _secili) && Motor.Temel.Acik;
        UygulaDugmesi.IsEnabled = !uygulandi;
        UygulaDugmesi.Content = uygulandi ? "Klavyede açık" : "Klavyeye uygula";

        if (!hareketli) OnizlemeCiz(SeciliOnizleme, _secili, 0, ayar, durgun: true);
    }

    void Hiz_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized || _secili is null) return;
        var hiz = (int)Math.Round(e.NewValue);
        if (hiz != _ayarlar[_secili].Hiz) AyarDegistir(_ayarlar[_secili] with { Hiz = hiz });
    }

    void Uygula_Click(object sender, RoutedEventArgs e)
    {
        if (!Motor.Temel.Acik) Motor.TemelAyarla(Motor.Temel with { Acik = true }); // efekt uygulamak ışığı açar
        Motor.EfektAyarla(_secili, _ayarlar[_secili]);
    }

    // ---------- önizleme çizimi ----------

    void Sayfa_Loaded(object sender, RoutedEventArgs e)
    {
        _saat.Restart();
        _zamanlayici.Start();
    }

    void Sayfa_Unloaded(object sender, RoutedEventArgs e) => _zamanlayici.Stop();

    void ZamanlayiciyiAyarla()
    {
        if (IsLoaded && !_zamanlayici.IsEnabled) Sayfa_Loaded(this, new RoutedEventArgs());
    }

    void Ciz()
    {
        var dt = Math.Min(_saat.Elapsed.TotalSeconds, 0.1);
        _saat.Restart();
        if (!IsVisible || Window.GetWindow(this) is { WindowState: WindowState.Minimized }) return; // tepsiye gizliyken de çizme

        foreach (var k in _kartlar.Where(k => k.Canli))
        {
            var ayar = _ayarlar[k.Efekt];
            k.Faz += dt * ayar.Carpan;
            OnizlemeCiz(k.Onizleme, k.Efekt, k.Faz, ayar);
        }
        if (!ReferenceEquals(_secili, Efektler.Sabit))
        {
            var ayar = _ayarlar[_secili];
            _seciliFaz += dt * ayar.Carpan;
            OnizlemeCiz(SeciliOnizleme, _secili, _seciliFaz, ayar);
        }
    }

    void DurgunCiz(Kart k)
    {
        k.Faz = 0;
        OnizlemeCiz(k.Onizleme, k.Efekt, 0, _ayarlar[k.Efekt], durgun: true);
    }

    /// <summary>Durgun önizleme rengi tam parlak gösterir (nefes 0'da karanlık başlar, kart boş görünmesin).</summary>
    static void OnizlemeCiz(Rectangle r, Efekt efekt, double faz, EfektAyarlari ayar, bool durgun = false)
    {
        // Önizlemede sıcaklık 50–90°C arasında gezer, skalanın tamamı görünsün
        var kare = efekt.Hesapla(new(faz, Motor.Temel.Renk, ayar.Skala, 70 + 20 * Math.Sin(faz * 0.8)));
        ((SolidColorBrush)r.Fill).Color = Renk(kare.Renk);
        r.Opacity = durgun ? 1 : Math.Max(kare.Parlaklik, 0.06);
    }

    static Color Renk(Rgb r) => Color.FromRgb(r.R, r.G, r.B);

    static LinearGradientBrush SkalaFircasi(Skala s)
    {
        var f = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        for (var i = 0; i <= 8; i++) f.GradientStops.Add(new GradientStop(Renk(s.Renk(i / 8.0)), i / 8.0));
        f.Freeze();
        return f;
    }
}
