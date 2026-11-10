using System.Windows;
using System.Windows.Controls;
using MonsterKlavye.Sayfalar;
using Wpf.Ui.Controls;

namespace MonsterKlavye;

public partial class MainWindow : FluentWindow
{
    readonly IsikSayfasi _isik = new();

    public MainWindow()
    {
        InitializeComponent();
        Icerik.Content = _isik;
        if (App.DonanimHatasi is { } hata)
        {
            HataCubugu.Message = hata;
            HataCubugu.IsOpen = true;
        }
    }

    void Menu_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox liste || liste.SelectedItem is not ListBoxItem secili) return;
        // İki liste tek menü gibi davransın: birinde seçim olunca diğerini temizle
        var diger = ReferenceEquals(liste, Menu) ? AltMenu : Menu;
        if (diger is not null) diger.SelectedItem = null;

        if (Icerik is null) return; // InitializeComponent sırasında
        Icerik.Content = secili.Tag switch
        {
            "isik" => _isik,
            "efektler" => YakindaSayfasi("Efektler", "Efekt galerisi bir sonraki adımda (K3) gelecek."),
            _ => YakindaSayfasi("Ayarlar", "Ayarlar K4'te gelecek: Windows ile başlat, tepsi, uyku zamanlayıcısı."),
        };
    }

    static UIElement YakindaSayfasi(string baslik, string aciklama) => new StackPanel
    {
        Margin = new Thickness(28, 22, 28, 22),
        Children =
        {
            new System.Windows.Controls.TextBlock { Text = baslik, FontSize = 28, FontWeight = FontWeights.SemiBold },
            new System.Windows.Controls.TextBlock { Text = aciklama, Margin = new Thickness(0, 8, 0, 0), Opacity = 0.8 },
        },
    };
}
