using System.Windows.Controls;
using MonsterPerformans.Sayfalar;
using Wpf.Ui.Controls;

namespace MonsterPerformans;

public partial class MainWindow : FluentWindow, IGezgin
{
    readonly PanoSayfasi _pano = new();
    readonly FanSayfasi _fan = new();
    readonly ProfillerSayfasi _profiller = new();
    readonly AyarlarSayfasi _ayarlar = new();

    public MainWindow()
    {
        InitializeComponent();
        Icerik.Content = _pano;
        if (App.DonanimHatasi is { } hata)
        {
            HataCubugu.Message = hata;
            HataCubugu.IsOpen = true;
        }
        // Kapatınca tepsiye gizlenir; çıkış tepsi menüsünden
        Closing += (_, e) =>
        {
            if (App.Cikiliyor) return;
            e.Cancel = true;
            Hide();
        };
    }

    /// <summary>Başka sayfadan gezinti (ör. Profiller → Fan, o profilin eğrisiyle).</summary>
    public void Git(string sayfa, string? profil = null)
    {
        if (profil is not null) _fan.ProfilSec(profil);
        foreach (var liste in new[] { Menu, AltMenu })
            foreach (ListBoxItem o in liste.Items)
                if (Equals(o.Tag, sayfa)) o.IsSelected = true;
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
            "fan" => _fan,
            "profiller" => _profiller,
            "ayarlar" => _ayarlar,
            _ => (object)_pano,
        };
    }
}
