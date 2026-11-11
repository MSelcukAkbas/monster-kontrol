using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MonsterPerformans.Cekirdek;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace MonsterPerformans.Sayfalar;

public partial class PanoSayfasi : UserControl
{
    static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    static readonly Brush Sari = Dondur(Color.FromRgb(0xFF, 0xB9, 0x00));
    static readonly Brush Turuncu = Dondur(Color.FromRgb(0xF7, 0x63, 0x0C));
    static readonly Brush Kirmizi = Dondur(Color.FromRgb(0xFF, 0x5A, 0x5F));

    int _pencere = 60;

    public PanoSayfasi()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            App.YeniOlcum += Guncelle;
            App.DurumDegisti += Profiller;
            Profiller();
            if (App.Gecmis.Son(1) is [var son]) Guncelle(son);
        };
        Unloaded += (_, _) =>
        {
            App.YeniOlcum -= Guncelle;
            App.DurumDegisti -= Profiller;
        };
    }

    void Profiller()
    {
        ProfilDugmeleri.Children.Clear();
        foreach (var p in App.Ayarlar.Hepsi)
        {
            var b = new Wpf.Ui.Controls.Button
            {
                Content = p.Ad,
                Margin = new Thickness(0, 4, 8, 4),
                Appearance = p.Ad == App.EtkinProfil ? ControlAppearance.Primary : ControlAppearance.Secondary,
                IsEnabled = App.DonanimVar && !App.Uygulaniyor,
            };
            b.Click += async (_, _) => await App.Uygula(p);
            ProfilDugmeleri.Children.Add(b);
        }
        Islem.Text = App.Uygulaniyor ? "Uygulanıyor…" : App.SonIslem ?? "Bu oturumda profil uygulanmadı.";
    }

    void Guncelle(Olcum o)
    {
        var d = o.Dchu;
        if (d.Gecerli) Sicaklik(CpuC, d.CpuC);
        else { CpuC.Text = "–"; CpuC.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush"); }
        CpuAyrinti.Text = string.Format(Tr, "{0:0.0} GHz · yük %{1:0}", o.GercekMHz / 1000, o.Pdh[0]);

        if (o.Gpu is { C: { } gc } g)
        {
            Sicaklik(GpuC, gc);
            GpuAyrinti.Text = string.Format(Tr, "{0} MHz · {1:0} W · yük %{2}", g.MHz, g.Watt, g.Kullanim);
        }
        else
        {
            GpuC.Text = "–";
            GpuC.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            GpuAyrinti.Text = App.NvmlVar ? "Uykuda" : "NVIDIA sürücüsü okunamadı";
        }

        CpuFan.Text = $"{d.CpuRpm} RPM";
        CpuFanAyrinti.Text = $"%{d.CpuDuty}";
        GpuFan.Text = $"{d.GpuRpm} RPM";
        GpuFanAyrinti.Text = d.GpuRpm == 0 ? $"%{d.GpuDuty} · durdu (soğukken normal)" : $"%{d.GpuDuty}";

        Durum.Text = $"Windows planı: {App.PlanAdi ?? "–"} · Son ölçüm {o.Zaman:HH:mm:ss}";
        Cizim.Ayarla(App.Gecmis.Son(_pencere), _pencere);
    }

    internal static void Sicaklik(TextBlock t, int c)
    {
        t.Text = $"{c}°C";
        switch (Cekirdek.Sicaklik.Band(c))
        {
            case SicaklikBandi.Normal: t.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush"); break;
            case SicaklikBandi.Ilik: t.Foreground = Sari; break;
            case SicaklikBandi.Sicak: t.Foreground = Turuncu; break;
            default: t.Foreground = Kirmizi; break;
        }
    }

    void Pencere_Click(object sender, RoutedEventArgs e)
    {
        var secili = (Wpf.Ui.Controls.Button)sender;
        _pencere = int.Parse((string)secili.Tag, CultureInfo.InvariantCulture);
        foreach (var b in new[] { Dk1, Dk5, Dk30 })
            b.Appearance = ReferenceEquals(b, secili) ? ControlAppearance.Primary : ControlAppearance.Secondary;
        Cizim.Ayarla(App.Gecmis.Son(_pencere), _pencere);
    }

    static Brush Dondur(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}
