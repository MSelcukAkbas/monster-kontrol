using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using MonsterPerformans.Cekirdek;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace MonsterPerformans;

/// <summary>
/// Tepsi simgesi: CPU sıcaklığı rakamla, renk sıcaklık bandına göre. Simge yalnız sayı değişince yeniden çizilir.
/// Sol tık: hızlı panel. Sağ tık: Aç / Çıkış. (Yapı Monster Klavye'deki Tepsi ile aynı.)
/// </summary>
sealed class Tepsi : IDisposable
{
    readonly Forms.NotifyIcon _simge = new() { Visible = true, Text = "Monster Performans" };
    readonly HizliPanel _panel = new();
    readonly ContextMenu _menu = new() { Style = (Style)Application.Current.FindResource("TepsiMenusu") };
    Drawing.Icon? _ikon;
    int? _cizilen = -1;
    DateTime _panelGizlendi;

    public Tepsi()
    {
        _menu.Items.Add(Oge("Monster Performans'ı aç", App.PencereyiGoster));
        _menu.Items.Add(Oge("Çıkış", App.Cik));

        _panel.IsVisibleChanged += (_, _) => { if (!_panel.IsVisible) _panelGizlendi = DateTime.UtcNow; };
        _simge.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) PaneliAcKapa();
            else if (e.Button == Forms.MouseButtons.Right) MenuyuAc();
        };
        App.YeniOlcum += Olcum;
        Ciz(null);
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

    void Olcum(Olcum o)
    {
        int? c = o.Dchu.Gecerli ? o.Dchu.CpuC : null;
        var gpu = o.Gpu?.C is { } g ? $"{g}°C" : "uykuda";
        _simge.Text = c is { } v ? $"Monster Performans · CPU {v}°C · GPU {gpu}" : "Monster Performans · sensör okunamadı";
        if (c != _cizilen) Ciz(c);
    }

    void Ciz(int? c)
    {
        _cizilen = c;
        var eski = _ikon;
        _ikon = IkonCiz(c);
        _simge.Icon = _ikon;
        if (eski is not null)
        {
            DestroyIcon(eski.Handle); // Icon.FromHandle tutamacı sahiplenmez: elle bırakılmazsa GDI sızıntısı
            eski.Dispose();
        }
    }

    static Drawing.Icon IkonCiz(int? c)
    {
        var boy = Forms.SystemInformation.SmallIconSize; // ölçeğe göre 16/20/24/32: rakam bulanıklaşmasın
        var renk = c is not { } v ? Drawing.Color.Gray : Sicaklik.Band(v) switch
        {
            SicaklikBandi.Normal => Drawing.Color.White,
            SicaklikBandi.Ilik => Drawing.Color.FromArgb(0xFF, 0xB9, 0x00),
            SicaklikBandi.Sicak => Drawing.Color.FromArgb(0xF7, 0x63, 0x0C),
            _ => Drawing.Color.FromArgb(0xFF, 0x5A, 0x5F),
        };
        var metin = c?.ToString(CultureInfo.InvariantCulture) ?? "–";

        using var bmp = new Drawing.Bitmap(boy.Width, boy.Height);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            var bicim = Drawing.StringFormat.GenericTypographic;
            // 3 hane (≥ 100°C) sığana kadar küçült
            var px = boy.Height * 0.9f;
            Drawing.Font yazi;
            while (true)
            {
                yazi = new Drawing.Font("Segoe UI", px, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
                if (g.MeasureString(metin, yazi, int.MaxValue, bicim).Width <= boy.Width || px < 6) break;
                yazi.Dispose();
                px -= 1;
            }
            using (yazi)
            using (var firca = new Drawing.SolidBrush(renk))
            {
                var o = g.MeasureString(metin, yazi, int.MaxValue, bicim);
                g.DrawString(metin, yazi, firca, (boy.Width - o.Width) / 2, (boy.Height - o.Height) / 2, bicim);
            }
        }
        return Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        App.YeniOlcum -= Olcum;
        _simge.Visible = false; // yoksa süreç bitince tepside hayalet simge kalır
        _simge.Dispose();
        if (_ikon is not null) DestroyIcon(_ikon.Handle);
        _panel.Close();
    }

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);
}
