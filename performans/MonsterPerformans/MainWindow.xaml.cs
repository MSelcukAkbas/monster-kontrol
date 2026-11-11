using System.Windows;
using System.Windows.Controls;
using MonsterPerformans.Sayfalar;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace MonsterPerformans;

public partial class MainWindow : FluentWindow
{
    readonly PanoSayfasi _pano = new();

    public MainWindow()
    {
        InitializeComponent();
        Icerik.Content = _pano;
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
        Icerik.Content = secili.Tag is "pano" ? _pano : Yakinda();
    }

    static TextBlock Yakinda() => new()
    {
        Text = "Bu sayfa sonraki aşamada.",
        Margin = new Thickness(32),
        Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("TextFillColorSecondaryBrush"),
    };
}
