using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MonsterPerformans.Cekirdek;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace MonsterPerformans.Sayfalar;

/// <summary>Son 5/30 dk en çok CPU/GPU kullanan uygulamalar (App.SurecGecmisi); 5 sn'de bir yenilenir.</summary>
public partial class UygulamalarSayfasi : UserControl
{
    static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    const int Satir = 25;
    int _pencere = 1800;

    public UygulamalarSayfasi()
    {
        InitializeComponent();
        Loaded += (_, _) => { App.YeniOlcum += Olcum; Yenile(); };
        Unloaded += (_, _) => App.YeniOlcum -= Olcum;
    }

    void Olcum(Olcum o) { if (o.Zaman.Second % 5 == 0) Yenile(); }

    void Pencere_Click(object sender, RoutedEventArgs e)
    {
        var secili = (Wpf.Ui.Controls.Button)sender;
        _pencere = int.Parse((string)secili.Tag, CultureInfo.InvariantCulture);
        foreach (var b in new[] { Dk5, Dk30 })
            b.Appearance = ReferenceEquals(b, secili) ? ControlAppearance.Primary : ControlAppearance.Secondary;
        Yenile();
    }

    void Yenile()
    {
        var ornekler = App.SurecGecmisi.Son(_pencere);
        Hazirla(Baslik);
        Hazirla(Tablo);
        var dk = Math.Max(1, (ornekler.Count + 30) / 60);
        string[] basliklar = [ornekler.Count < _pencere ? $"Uygulama · {dk} dk ölçüldü" : "Uygulama", "CPU şimdi", "CPU ort.", "GPU şimdi", "GPU ort.", "Enerji", "Süre"];
        // Anlık: son 5 sn ortalaması (sayfa 5 sn'de bir yenilenir; tek saniye çok oynak)
        var anlik = SurecKayitci.Ozet(App.SurecGecmisi.Son(5)).ToDictionary(o => o.Ad);
        for (var i = 0; i < basliklar.Length; i++) Hucre(Baslik, 0, i, basliklar[i], ayrinti: true);

        var r = 0;
        foreach (var u in SurecKayitci.Ozet(ornekler).Take(Satir))
        {
            var ad = new StackPanel { Margin = new Thickness(0, 4, 8, 4) };
            var (baslik, dosya) = Adlar(u.Ad);
            ad.Children.Add(new TextBlock { Text = baslik, TextTrimming = TextTrimming.CharacterEllipsis });
            if (dosya is not null)
                ad.Children.Add(new TextBlock { Text = dosya, Style = (Style)FindResource("Ayrinti"), TextTrimming = TextTrimming.CharacterEllipsis });
            Ekle(Tablo, ad, r, 0);
            var a = anlik.GetValueOrDefault(u.Ad);
            Hucre(Tablo, r, 1, a.Cpu.ToString("0.0", Tr));
            Hucre(Tablo, r, 2, u.Cpu.ToString("0.0", Tr), ayrinti: true);
            Hucre(Tablo, r, 3, a.Gpu.ToString("0.0", Tr));
            Hucre(Tablo, r, 4, u.Gpu.ToString("0.0", Tr), ayrinti: true);
            Hucre(Tablo, r, 5, u.Wh is { } wh ? $"{wh.ToString("0.00", Tr)} Wh" : "–");
            Hucre(Tablo, r, 6, u.Saniye >= 60 ? $"{u.Saniye / 60} dk" : $"{u.Saniye} sn");
            r++;
        }
        if (r == 0) Hucre(Tablo, 0, 0, "Henüz örnek yok");
    }

    // Başlık ve tablo aynı sütun genişliklerini kullanır: hizalı kalırlar
    static readonly double[] Genislik = [80, 70, 80, 70, 80, 60];

    static void Hazirla(Grid g)
    {
        g.Children.Clear();
        g.RowDefinitions.Clear();
        if (g.ColumnDefinitions.Count > 0) return;
        g.ColumnDefinitions.Add(new ColumnDefinition());
        foreach (var w in Genislik) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
    }

    void Hucre(Grid g, int satir, int sutun, string metin, bool ayrinti = false)
    {
        var h = new TextBlock { Text = metin, Margin = new Thickness(0, 4, sutun == 0 ? 8 : 0, 4), VerticalAlignment = VerticalAlignment.Center };
        if (sutun > 0) h.TextAlignment = TextAlignment.Right;
        if (ayrinti) h.Style = (Style)FindResource("Ayrinti");
        Ekle(g, h, satir, sutun);
    }

    static void Ekle(Grid g, UIElement e, int satir, int sutun)
    {
        while (g.RowDefinitions.Count <= satir) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(e, satir);
        Grid.SetColumn(e, sutun);
        g.Children.Add(e);
    }

    /// <summary>(büyük satır, küçük satır): açıklama + exe dosyası; açıklama yoksa yalnız ad.</summary>
    static (string, string?) Adlar(string ad) =>
        Sistem.TryGetValue(ad, out var s) ? (s, ad)
        : Aciklama(ad) is { } a ? (a, ad + ".exe")
        : (ad, null);

    // Dosyası olmayan çekirdek süreçleri
    static readonly Dictionary<string, string> Sistem = new(StringComparer.OrdinalIgnoreCase)
    {
        ["System"] = "Windows çekirdeği",
        ["Registry"] = "Windows kayıt defteri",
        ["Memory Compression"] = "Windows bellek sıkıştırma",
        ["Secure System"] = "Windows güvenli çekirdek",
        ["Interrupts"] = "Donanım kesmeleri",
        ["MsMpEng"] = "Microsoft Defender virüs koruması", // korumalı süreç, dosyası System32'de değil
    };

    // Exe'nin dosya açıklaması (dwm → "Masaüstü Pencere Yöneticisi"); exe adıyla aynıysa ya da okunamazsa null. Ad başına bir kez.
    static readonly Dictionary<string, string?> Aciklamalar = new(StringComparer.OrdinalIgnoreCase);

    static string? Aciklama(string ad)
    {
        if (Aciklamalar.TryGetValue(ad, out var a)) return a;
        var surecler = Process.GetProcessesByName(ad);
        // Aynı adlı farklı exe'ler olabilir (ör. iki claude.exe): açıklaması olan ilk dosya
        var yollar = surecler.Select(p => Yol(p.Id)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var p in surecler) p.Dispose();
        // Yöneticisiz açılamayan Windows süreçleri (dwm, svchost…) System32'deki dosyadır
        var sistem32 = Path.Combine(Environment.SystemDirectory, ad + ".exe");
        if (File.Exists(sistem32)) yollar.Add(sistem32);

        var sonuc = yollar.Select(y => FileVersionInfo.GetVersionInfo(y).FileDescription?.Trim())
            .FirstOrDefault(d => !string.IsNullOrEmpty(d) && !string.Equals(d, ad, StringComparison.OrdinalIgnoreCase)
                                 && !string.Equals(d, ad + ".exe", StringComparison.OrdinalIgnoreCase));
        // Süreç kapandıysa bir dahaki sefere tekrar denensin
        if (sonuc is not null || surecler.Length > 0) Aciklamalar[ad] = sonuc;
        return sonuc;
    }

    /// <summary>Korumalı süreçlerde de çalışır (MainModule erişim hatası verir).</summary>
    static string? Yol(int pid)
    {
        var h = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
        if (h == IntPtr.Zero) return null;
        try
        {
            var b = new StringBuilder(1024);
            var n = b.Capacity;
            return QueryFullProcessImageNameW(h, 0, b, ref n) ? b.ToString() : null;
        }
        finally { CloseHandle(h); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint erisim, bool miras, int pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool QueryFullProcessImageNameW(IntPtr h, int bayrak, StringBuilder ad, ref int boyut);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr h);
}
