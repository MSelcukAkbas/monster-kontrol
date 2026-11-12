using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using K = MonsterKlavye.Sayfalar;
using P = MonsterPerformans.Sayfalar;

namespace MonsterKontrol;

public partial class MainWindow : FluentWindow, MonsterPerformans.IGezgin
{
    // Tek örnek: seçimler menü değişince kaybolmasın
    readonly Dictionary<string, UIElement> _sayfalar = [];

    public MainWindow()
    {
        InitializeComponent();
        _sayfalar["isik"] = new K.IsikSayfasi();
        _sayfalar["efektler"] = new K.EfektlerSayfasi();
        _sayfalar["klavye-ayar"] = new K.AyarlarSayfasi();
        _sayfalar["pano"] = new P.PanoSayfasi();
        _sayfalar["uygulamalar"] = new P.UygulamalarSayfasi();
        _sayfalar["fan"] = new P.FanSayfasi();
        _sayfalar["profiller"] = new P.ProfillerSayfasi();
        _sayfalar["ayarlar"] = new P.AyarlarSayfasi();

        var uyarilar = new List<string>();
        if (MonsterKlavye.App.DonanimHatasi is { } kh) uyarilar.Add("Klavye: " + kh);
        if (MonsterPerformans.App.DonanimHatasi is { } ph) uyarilar.Add("Performans: " + ph);
        if (uyarilar.Count > 0)
        {
            HataCubugu.Title = "Bazı bölümler çalışmıyor";
            HataCubugu.Message = string.Join(Environment.NewLine, uyarilar);
            HataCubugu.IsOpen = true;
        }

        Git("isik");
    }

    ListBox[] Listeler => [KlavyeMenu, PerformansMenu, AltMenu];

    public void Git(string sayfa, string? profil = null)
    {
        if (profil is not null && _sayfalar.GetValueOrDefault("fan") is P.FanSayfasi fan) fan.ProfilSec(profil);
        foreach (var liste in Listeler)
            foreach (ListBoxItem o in liste.Items)
                if (Equals(o.Tag, sayfa)) o.IsSelected = true;
    }

    void Menu_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox liste || liste.SelectedItem is not ListBoxItem secili) return;
        // Üç liste tek menü gibi davransın
        foreach (var diger in Listeler)
            if (diger is not null && !ReferenceEquals(diger, liste)) diger.SelectedItem = null;
        if (Icerik is not null && _sayfalar.TryGetValue((string)secili.Tag, out var s)) Icerik.Content = s; // Icerik null: InitializeComponent sırasında
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!App.Cikiliyor)
        {
            e.Cancel = true; // efektler ve fan denetimi tepside sürer; çıkış tepsi menüsünden
            Hide();
        }
        base.OnClosing(e);
    }
}