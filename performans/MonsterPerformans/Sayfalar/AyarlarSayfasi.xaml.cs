using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MonsterPerformans.Sayfalar;

public partial class AyarlarSayfasi : UserControl
{
    static readonly Brush Kirmizi = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x45));

    public AyarlarSayfasi()
    {
        InitializeComponent();
        Baslat.IsChecked = WindowsIleBaslat.Acik;
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

    void Dugme() => GuvenliDugmesi.IsEnabled = App.DonanimVar && !App.Uygulaniyor;

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
