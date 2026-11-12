using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MonsterPerformans.Cekirdek.Guc;

namespace MonsterPerformans.Sayfalar;

public partial class AyarlarSayfasi : UserControl
{
    static readonly Brush Kirmizi = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x45));
    bool _yukleniyor;

    public AyarlarSayfasi()
    {
        InitializeComponent();
        Baslat.IsChecked = WindowsIleBaslat.Acik;
        for (var c = AcilDurum.VarsayilanEsik; c <= AcilDurum.EnYuksekEsik; c++) Esik.Items.Add($"{c}°C");
        if (App.DonanimHatasi is { } hata)
        {
            DchuNoktasi.Fill = Kirmizi;
            DchuBaslik.Text = "Clevo sürücüsü bulunamadı: profiller uygulanamaz";
            DchuAyrinti.Text = hata;
        }
        else
        {
            DchuAyrinti.Text = App.DllYolu;
            DchuAyrinti.ToolTip = App.DllYolu;
        }
        if (!App.NvmlVar)
        {
            NvmlNoktasi.Fill = Kirmizi;
            NvmlBaslik.Text = "NVIDIA sürücüsü (NVML) bulunamadı";
        }
        Loaded += (_, _) =>
        {
            App.DurumDegisti += Dugme;
            Dugme();
        };
        Unloaded += (_, _) => App.DurumDegisti -= Dugme;
    }

    void Dugme()
    {
        GuvenliDugmesi.IsEnabled = App.DonanimVar && !App.Mesgul;
        var a = App.Ayarlar;
        _yukleniyor = true;
        Yeniden.IsChecked = a.YenidenUygula;
        Esik.SelectedIndex = a.AcilEsik - AcilDurum.VarsayilanEsik;
        Politika.SelectedIndex = (int)a.DogrulamaHatasi;
        Kayit.IsChecked = a.KayitTut;
        EnYuksekMhz.Value = a.EnYuksekMhz;
        _yukleniyor = false;
        SonProfilYazisi.Text = a.SonProfil is { } p ? $"Son profil: {p}" : "Son profil yok (güvenli durumda ya da hiç seçilmedi).";
        AcilYazisi.Text = App.AcilKademe == AcilKademe.Yok ? "" : $"Şu an acil durumda: {AcilDurum.Ad(App.AcilKademe)}.";
        KlasorYazisi.Text = a.Kayit;
        KlasorYazisi.ToolTip = a.Kayit;
    }

    void Ayar_Degisti(object sender, RoutedEventArgs e)
    {
        if (_yukleniyor || !IsLoaded) return;
        var a = App.Ayarlar;
        a.YenidenUygula = Yeniden.IsChecked == true;
        a.AcilEsik = AcilDurum.VarsayilanEsik + Math.Max(0, Esik.SelectedIndex);
        a.DogrulamaHatasi = (DogrulamaPolitikasi)Math.Max(0, Politika.SelectedIndex);
        a.KayitTut = Kayit.IsChecked == true;
        if (EnYuksekMhz.Value is { } mhz) a.EnYuksekMhz = Math.Clamp((int)Math.Round(mhz), PerformansAyarlari.EnAzMhz, PerformansAyarlari.EnCokMhz);
        App.AyarlarDegisti();
    }

    void KlasorAc_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(App.Ayarlar.Kayit);
            Process.Start(new ProcessStartInfo(App.Ayarlar.Kayit) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            KlasorYazisi.Text = $"Açılamadı: {ex.Message}";
        }
    }

    void KlasorDegistir_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFolderDialog { Title = "Kayıt klasörü", InitialDirectory = App.Ayarlar.Kayit };
        if (d.ShowDialog(Window.GetWindow(this)) != true) return;
        App.Ayarlar.KayitKlasoru = d.FolderName == PerformansAyarlari.VarsayilanKayitKlasoru ? null : d.FolderName;
        App.AyarlarDegisti();
    }

    void Baslat_Click(object sender, RoutedEventArgs e)
    {
        WindowsIleBaslat.Acik = Baslat.IsChecked == true;
        Baslat.IsChecked = WindowsIleBaslat.Acik; // kayıt defterinde gerçekte ne olduğunu göster
    }

    async void Guvenli_Click(object sender, RoutedEventArgs e)
    {
        await App.GuvenliDurumaDon();
        GuvenliSonuc.Text = App.SonIslem;
        GuvenliSonuc.Visibility = Visibility.Visible;
    }
}
