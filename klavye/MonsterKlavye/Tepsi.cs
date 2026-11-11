using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using MonsterKlavye.Klavye;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace MonsterKlavye;

/// <summary>
/// Tepsi simgesi (WinForms NotifyIcon: pencere olmadan da çalışır). Simge o anki klavye renginde.
/// Sol tık: hızlı panel. Sağ tık: WPF menüsü (Fluent görünümlü).
/// </summary>
public sealed class Tepsi : IDisposable
{
    readonly Forms.NotifyIcon _simge = new() { Visible = true };
    readonly HizliPanel _panel = new();
    readonly ContextMenu _menu = new() { Style = (Style)Application.Current.FindResource("TepsiMenusu") };
    readonly MenuItem _guc = new();
    readonly MenuItem _efekt = new() { Header = "Efekt" };
    Drawing.Icon? _ikon;
    DateTime _panelGizlendi;

    static EfektMotoru Motor => App.Motor;

    public Tepsi()
    {
        _guc.Click += (_, _) => Motor.TemelAyarla(Motor.Temel with { Acik = !Motor.Temel.Acik });
        foreach (var e in Efektler.Hepsi)
        {
            var o = new MenuItem { Header = e.Ad, DataContext = e };
            o.Click += (_, _) =>
            {
                if (!Motor.Temel.Acik) Motor.TemelAyarla(Motor.Temel with { Acik = true });
                Motor.EfektAyarla(e, ReferenceEquals(e, Motor.Efekt) ? Motor.Ayarlar : null);
            };
            _efekt.Items.Add(o);
        }
        _menu.Items.Add(_guc);
        _menu.Items.Add(_efekt);
        _menu.Items.Add(Oge("Şimdi yeniden uygula", () => App.Kontrol?.YenidenUygula()));
        _menu.Items.Add(new Separator());
        _menu.Items.Add(Oge("Uygulamayı aç", App.PencereyiGoster));
        _menu.Items.Add(Oge("Çıkış", App.Cik));

        _panel.IsVisibleChanged += (_, _) => { if (!_panel.IsVisible) _panelGizlendi = DateTime.UtcNow; };
        _simge.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) PaneliAcKapa();
            else if (e.Button == Forms.MouseButtons.Right) MenuyuAc();
        };
        Motor.Degisti += Yenile;
        Yenile();
    }

    static MenuItem Oge(string baslik, Action eylem)
    {
        var o = new MenuItem { Header = baslik };
        o.Click += (_, _) => eylem();
        return o;
    }

    void PaneliAcKapa()
    {
        // Simgeye tıklamak paneli önce odaksız bırakıp gizler; hemen ardından gelen tıklama yeniden açmasın.
        if (_panel.IsVisible || (DateTime.UtcNow - _panelGizlendi).TotalMilliseconds < 300) _panel.Hide();
        else _panel.Goster();
    }

    void MenuyuAc()
    {
        // WPF menüsü, uygulama önde değilse dışarı tıklanınca kapanmaz: önce kendi penceremizi öne al.
        SetForegroundWindow(new WindowInteropHelper(_panel).EnsureHandle());
        _menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        _menu.IsOpen = true;
    }

    /// <summary>Yalnız temel durum değişince (karede değil): simge rengi, ipucu, menü işaretleri.</summary>
    void Yenile()
    {
        var t = Motor.Temel;
        _guc.Header = t.Acik ? "Işığı kapat" : "Işığı aç";
        foreach (MenuItem o in _efekt.Items) o.IsChecked = ReferenceEquals(o.DataContext, Motor.Efekt);
        _simge.Text = t.Acik ? $"Monster Klavye · {Motor.Efekt.Ad} · %{t.ParlaklikYuzde}" : "Monster Klavye · kapalı";

        var eski = _ikon;
        _ikon = IkonCiz(t.Acik ? Drawing.Color.FromArgb(t.Renk.R, t.Renk.G, t.Renk.B) : Drawing.Color.Gray);
        _simge.Icon = _ikon;
        if (eski is not null)
        {
            DestroyIcon(eski.Handle); // Icon.FromHandle tutamacı sahiplenmez: elle bırakılmazsa GDI sızıntısı
            eski.Dispose();
        }
    }

    /// <summary>Klavye silueti: renkli çerçeve + boşluk tuşu.</summary>
    static Drawing.Icon IkonCiz(Drawing.Color renk)
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var kalem = new Drawing.Pen(renk, 3);
            using var firca = new Drawing.SolidBrush(renk);
            g.DrawRoundedRectangle(kalem, new Drawing.Rectangle(2, 7, 28, 18), new Drawing.Size(6, 6));
            g.FillRectangle(firca, 10, 18, 12, 3);
            for (var x = 7; x <= 22; x += 5) g.FillRectangle(firca, x, 12, 3, 3);
        }
        return Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        Motor.Degisti -= Yenile;
        _simge.Visible = false; // yoksa süreç bitince tepside hayalet simge kalır
        _simge.Dispose();
        if (_ikon is not null) DestroyIcon(_ikon.Handle);
        _panel.Close();
    }

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);
}
