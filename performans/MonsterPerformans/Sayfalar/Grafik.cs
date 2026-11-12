using System.Globalization;
using System.Windows;
using System.Windows.Media;
using MonsterPerformans.Cekirdek;

namespace MonsterPerformans.Sayfalar;

/// <summary>Hafif çizgi grafiği (bağımlılık yok): CPU °C, GPU °C, CPU fanı %. Dikey eksen 0–100 ortak.</summary>
public sealed class Grafik : FrameworkElement
{
    public static readonly Brush GpuRengi = Dondur(new SolidColorBrush(Color.FromRgb(0x6C, 0xCB, 0x5F)));
    public static readonly Brush FanRengi = Dondur(new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)));
    static readonly Pen Izgara = Dondur(new Pen(new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)), 1));
    static readonly Brush EtiketRengi = Dondur(new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)));

    IReadOnlyList<Olcum> _veri = [];
    int _pencere = 60;

    /// <summary>Pencere = gösterilen saniye; az veri varsa çizgi sağdan başlar.</summary>
    public void Ayarla(IReadOnlyList<Olcum> veri, int pencere)
    {
        _veri = veri;
        _pencere = pencere;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        const double sol = 28;
        double g = ActualWidth - sol, y = ActualHeight;
        if (g <= 0 || y <= 0) return;
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (var d in new[] { 0, 25, 50, 75, 100 })
        {
            var yy = Y(d);
            dc.DrawLine(Izgara, new Point(sol, yy), new Point(ActualWidth, yy));
            var yazi = new FormattedText(d.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, EtiketRengi, dip);
            dc.DrawText(yazi, new Point(sol - 6 - yazi.Width, Math.Clamp(yy - yazi.Height / 2, 0, y - yazi.Height)));
        }

        var vurgu = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue;
        Ciz(dc, o => o.Dchu.Gecerli ? o.Dchu.CpuC : null, new Pen(vurgu, 2));
        Ciz(dc, o => o.Gpu?.C, new Pen(GpuRengi, 2));
        Ciz(dc, o => o.Dchu.Gecerli ? o.Dchu.CpuDuty : null, new Pen(FanRengi, 1.5) { DashStyle = DashStyles.Dash });

        double Y(double deger) => 4 + (y - 8) * (1 - Math.Clamp(deger, 0, 100) / 100);

        void Ciz(DrawingContext d, Func<Olcum, int?> sec, Pen kalem)
        {
            var geo = new StreamGeometry();
            using (var c = geo.Open())
            {
                var acik = false;
                var kayma = _pencere - _veri.Count;
                for (var i = 0; i < _veri.Count; i++)
                {
                    if (sec(_veri[i]) is not { } v) continue; // boş ölçüm atlanır, çizgi kopmaz (NVML GPU sıcaklığını aralıklı döndürür)
                    var p = new Point(sol + g * (i + kayma) / Math.Max(1, _pencere - 1), Y(v));
                    if (acik) c.LineTo(p, true, false);
                    else { c.BeginFigure(p, false, false); acik = true; }
                }
            }
            geo.Freeze();
            d.DrawGeometry(null, kalem, geo);
        }
    }

    static T Dondur<T>(T f) where T : Freezable { f.Freeze(); return f; }
}
