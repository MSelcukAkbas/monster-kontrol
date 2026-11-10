using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MonsterKlavye.Sayfalar;

public partial class AyarlarSayfasi : UserControl
{
    public AyarlarSayfasi()
    {
        InitializeComponent();
        Baslat.IsChecked = WindowsIleBaslat.Acik;
        Tepsi.IsChecked = App.TepsiyeKucult;
        if (App.DonanimHatasi is { } hata)
        {
            DurumNoktasi.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x45));
            DonanimBaslik.Text = "Clevo sürücüsü bulunamadı";
            DonanimAyrinti.Text = hata;
        }
        else
        {
            DonanimAyrinti.Text = App.DllYolu;
            DonanimAyrinti.ToolTip = App.DllYolu;
        }
    }

    void Baslat_Click(object sender, RoutedEventArgs e)
    {
        WindowsIleBaslat.Acik = Baslat.IsChecked == true;
        Baslat.IsChecked = WindowsIleBaslat.Acik; // kayıt defterinde gerçekte ne olduğunu göster
    }

    void Tepsi_Click(object sender, RoutedEventArgs e) => App.TepsiyeKucult = Tepsi.IsChecked == true;

    void YenidenUygula_Click(object sender, RoutedEventArgs e) => App.Kontrol?.YenidenUygula();
}
