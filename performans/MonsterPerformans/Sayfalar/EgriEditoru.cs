using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MonsterPerformans.Cekirdek.Fan;

namespace MonsterPerformans.Sayfalar;

/// <summary>
/// 4 noktalı EC eğrisi. 1. (40°C %35) ve 4. nokta (T4, %100) sabit; yalnız 2. ve 3. nokta fareyle sürüklenir
/// ya da klavyeyle taşınır (2/3 ya da Boşluk: nokta seç, oklar: 1°C / %1). ≥ 92°C koruma bandına nokta konmaz.
/// </summary>
public sealed class EgriEditoru : FrameworkElement
{
    public const int KorumaBandi = 92;
    const int XMin = 30, XMax = 100;
    const double Sol = 36, Alt = 24, Ust = 8, Sag = 8, Yakalama = 16;

    static readonly Pen Izgara = Dondur(new Pen(new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)), 1));
    static readonly Brush EtiketRengi = Dondur(new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)));
    static readonly Brush BantRengi = Dondur(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x5A, 0x5F)));
    static readonly Brush BantYazisi = Dondur(new SolidColorBrush(Color.FromRgb(0xFF, 0x5A, 0x5F)));
    static readonly Pen SabitKalem = Dondur(new Pen(new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)), 2));
    static readonly Pen UcKalem = Dondur(new Pen(new SolidColorBrush(Color.FromArgb(0x80, 0x9E, 0x9E, 0x9E)), 1.5) { DashStyle = DashStyles.Dash });

    FanEgrisi _egri = HazirEgrilerDengeli;
    int _t4 = 100;
    int _secili = 2; // klavyeyle taşınan nokta
    int _surukle;    // 0 = sürüklenmiyor

    static FanEgrisi HazirEgrilerDengeli => Cekirdek.Guc.HazirEgriler.Dengeli;

    public event Action<FanEgrisi>? Degisti;

    public EgriEditoru()
    {
        Focusable = true;
        FocusVisualStyle = null; // odak kendi çizimimizde (seçili nokta halkası + çerçeve)
        Cursor = Cursors.Arrow;
    }

    public FanEgrisi Egri => _egri;

    /// <summary>Dışarıdan ayar: Degisti tetiklenmez.</summary>
    public void Ayarla(FanEgrisi egri, int t4)
    {
        _egri = egri;
        _t4 = t4;
        InvalidateVisual();
    }

    double G => Math.Max(1, ActualWidth - Sol - Sag);
    double Y => Math.Max(1, ActualHeight - Ust - Alt);
    double Px(double t) => Sol + G * (t - XMin) / (XMax - XMin);
    double Py(double d) => Ust + Y * (1 - d / 100);
    double Tx(double x) => XMin + (x - Sol) / G * (XMax - XMin);
    double Dy(double y) => (1 - (y - Ust) / Y) * 100;

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= Sol + Sag || ActualHeight <= Ust + Alt) return;
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var vurgu = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight)); // boşluğa da tıklanabilsin

        dc.DrawRectangle(BantRengi, null, new Rect(new Point(Px(KorumaBandi), Py(100)), new Point(Px(XMax), Py(0))));
        Yazi(dc, "≥92°C koruma", 11, BantYazisi, new Point(Px(KorumaBandi) + 4, Py(0) - 18), dip); // altta: eğri sağ üstten geçer

        for (var d = 0; d <= 100; d += 20)
        {
            dc.DrawLine(Izgara, new Point(Sol, Py(d)), new Point(Sol + G, Py(d)));
            var y = Yazi(dc, $"%{d}", 11, EtiketRengi, default, dip, olcuYalniz: true);
            Yazi(dc, $"%{d}", 11, EtiketRengi, new Point(Sol - 6 - y.Width, Math.Clamp(Py(d) - y.Height / 2, 0, ActualHeight - y.Height)), dip);
        }
        for (var t = XMin; t <= XMax; t += 10)
        {
            dc.DrawLine(Izgara, new Point(Px(t), Ust), new Point(Px(t), Ust + Y));
            var y = Yazi(dc, $"{t}°", 11, EtiketRengi, default, dip, olcuYalniz: true);
            Yazi(dc, $"{t}°", 11, EtiketRengi, new Point(Math.Clamp(Px(t) - y.Width / 2, 0, ActualWidth - y.Width), Ust + Y + 4), dip);
        }

        Point p1 = new(Px(FanEgrisi.T1), Py(FanEgrisi.D1)), p2 = new(Px(_egri.T2), Py(_egri.D2)),
              p3 = new(Px(_egri.T3), Py(_egri.D3)), p4 = new(Px(_t4), Py(100));
        // EC tabanı (40°C altı %35) ve 4. noktadan sonrası: sabit, kesikli
        dc.DrawLine(UcKalem, new Point(Px(XMin), p1.Y), p1);
        dc.DrawLine(UcKalem, p4, new Point(Px(XMax), p4.Y));
        var cizgi = new Pen(vurgu, 2.5);
        dc.DrawLine(cizgi, p1, p2);
        dc.DrawLine(cizgi, p2, p3);
        dc.DrawLine(cizgi, p3, p4);

        dc.DrawEllipse(Brushes.Transparent, SabitKalem, p1, 5, 5);
        dc.DrawEllipse(Brushes.Transparent, SabitKalem, p4, 5, 5);
        foreach (var (n, p, t, d) in new[] { (2, p2, _egri.T2, _egri.D2), (3, p3, _egri.T3, _egri.D3) })
        {
            if (n == _secili && IsKeyboardFocused)
                dc.DrawEllipse(null, new Pen(Brushes.White, 2), p, 11, 11);
            dc.DrawEllipse(vurgu, new Pen(Brushes.White, 1.5), p, 7, 7);
            var etiket = $"{t}°C · %{d}";
            var o = Yazi(dc, etiket, 12, Brushes.White, default, dip, olcuYalniz: true);
            Yazi(dc, etiket, 12, Brushes.White,
                new Point(Math.Clamp(p.X - o.Width / 2, Sol, ActualWidth - o.Width), p.Y - 14 - o.Height < 0 ? p.Y + 12 : p.Y - 14 - o.Height), dip);
        }
        if (IsKeyboardFocused)
            dc.DrawRectangle(null, new Pen(EtiketRengi, 1), new Rect(0.5, 0.5, ActualWidth - 1, ActualHeight - 1));
    }

    static FormattedText Yazi(DrawingContext dc, string s, double boy, Brush renk, Point yer, double dip, bool olcuYalniz = false)
    {
        var f = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), boy, renk, dip);
        if (!olcuYalniz) dc.DrawText(f, yer);
        return f;
    }

    /// <summary>Noktayı kurallar içinde taşır: 40 &lt; t2 &lt; t3 &lt; 92, 35 ≤ d2 ≤ d3 ≤ 100.</summary>
    void Tasi(int nokta, int t, int d)
    {
        var e = _egri;
        var tMax = Math.Max(e.T2 + 1, Math.Min(_t4, KorumaBandi) - 1); // elle düzenlenmiş eğri bandın içindeyse Clamp patlamasın
        e = nokta == 2
            ? e with { T2 = Math.Clamp(t, FanEgrisi.T1 + 1, e.T3 - 1), D2 = Math.Clamp(d, FanEgrisi.EnAz, e.D3) }
            : e with { T3 = Math.Clamp(t, e.T2 + 1, tMax), D3 = Math.Clamp(d, e.D2, FanEgrisi.D4) };
        if (e == _egri) return;
        _egri = e;
        InvalidateVisual();
        Degisti?.Invoke(e);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus();
        var m = e.GetPosition(this);
        double u2 = (m - new Point(Px(_egri.T2), Py(_egri.D2))).Length, u3 = (m - new Point(Px(_egri.T3), Py(_egri.D3))).Length;
        var n = u2 <= u3 ? 2 : 3;
        if (Math.Min(u2, u3) > Yakalama) return;
        _surukle = _secili = n;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var m = e.GetPosition(this);
        if (_surukle != 0)
            Tasi(_surukle, (int)Math.Round(Tx(m.X)), (int)Math.Round(Dy(m.Y)));
        else
        {
            var yakin = Math.Min((m - new Point(Px(_egri.T2), Py(_egri.D2))).Length, (m - new Point(Px(_egri.T3), Py(_egri.D3))).Length);
            Cursor = yakin <= Yakalama ? Cursors.SizeAll : Cursors.Arrow;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _surukle = 0;
        ReleaseMouseCapture();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var (t, d) = _secili == 2 ? (_egri.T2, _egri.D2) : (_egri.T3, _egri.D3);
        switch (e.Key)
        {
            case Key.D2 or Key.NumPad2: _secili = 2; break;
            case Key.D3 or Key.NumPad3: _secili = 3; break;
            case Key.Space: _secili = _secili == 2 ? 3 : 2; break;
            case Key.Left: Tasi(_secili, t - 1, d); break;
            case Key.Right: Tasi(_secili, t + 1, d); break;
            case Key.Up: Tasi(_secili, t, d + 1); break;
            case Key.Down: Tasi(_secili, t, d - 1); break;
            default: return;
        }
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) => InvalidateVisual();
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) => InvalidateVisual();

    static T Dondur<T>(T f) where T : Freezable { f.Freeze(); return f; }
}
