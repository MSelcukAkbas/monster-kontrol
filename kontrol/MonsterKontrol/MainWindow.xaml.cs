using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using K = MonsterKlavye.Sayfalar;
using P = MonsterPerformans.Sayfalar;

namespace MonsterKontrol;

public partial class MainWindow : FluentWindow, MonsterPerformans.IGezgin
{
    // Tek örnek: seçimler menü değişince kaybolmasın. Bölüm başlamadıysa sayfası da yok (motoru null).
    readonly Dictionary<string, UIElement> _sayfalar = [];

    public MainWindow()
    {
        InitializeComponent();
        if (App.KlavyeVar)
        {
            _sayfalar["isik"] = new K.IsikSayfasi();
            _sayfalar["efektler"] = new K.EfektlerSayfasi();
            _sayfalar["klavye-ayar"] = new K.AyarlarSayfasi();
        }
        if (App.PerformansVar)
        {
            _sayfalar["pano"] = new P.PanoSayfasi();
            _sayfalar["fan"] = new P.FanSayfasi();
            _sayfalar["profiller"] = new P.ProfillerSayfasi();
            _sayfalar["ayarlar"] = new P.AyarlarSayfasi();
        }
        foreach (var liste in Listeler)
            foreach (ListBoxItem o in liste.Items)
                o.IsEnabled = _sayfalar.ContainsKey((string)o.Tag);

        var uyarilar = new List<string>();
        if (!App.KlavyeVar) uyarilar.Add("Monster Klavye ayrı uygulama olarak açık: klavye bölümü kapalı.");
        if (!App.PerformansVar) uyarilar.Add("Monster Performans ayrı uygulama olarak açık: performans bölümü kapalı.");
        if (App.KlavyeVar && MonsterKlavye.App.DonanimHatasi is { } kh) uyarilar.Add("Klavye: " + kh);
        if (App.PerformansVar && MonsterPerformans.App.DonanimHatasi is { } ph) uyarilar.Add("Performans: " + ph);
        if (uyarilar.Count > 0)
        {
            HataCubugu.Title = "Bazı bölümler çalışmıyor";
            HataCubugu.Message = string.Join(Environment.NewLine, uyarilar);
            HataCubugu.IsOpen = true;
        }

        Git(App.KlavyeVar ? "isik" : "pano");
    }

    ListBox[] Listeler => [KlavyeMenu, PerformansMenu, AltMenu];

    public void Git(string sayfa, string? profil = null)
    {
        if (profil is not null && _sayfalar.GetValueOrDefault("fan") is P.FanSayfasi fan) fan.ProfilSec(profil);
        foreach (var liste in Listeler)
            foreach (ListBoxItem o in liste.Items)
                if (Equals(o.Tag, sayfa) && o.IsEnabled) o.IsSelected = true;
    }

    void Menu_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox liste || liste.SelectedItem is not ListBoxItem secili) return;
        // Üç liste tek menü gibi davransın
        foreach (var diger in Listeler)
            if (diger is not null && !ReferenceEquals(diger, liste)) diger.SelectedItem = null;
        if (Icerik is not null && _sayfalar.TryGetValue((string)secili.Tag, out var s)) Icerik.Content = s; // Icerik null: InitializeComponent sırasında
    }
}
