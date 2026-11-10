using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MonsterKlavye.Sayfalar;
using Wpf.Ui.Controls;

namespace MonsterKlavye;

public partial class MainWindow : FluentWindow
{
    readonly IsikSayfasi _isik = new();
    readonly EfektlerSayfasi _efektler = new(); // tek örnek: seçim ve ayarlar menü değişince kaybolmasın
    AyarlarSayfasi? _ayarlar;

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
            "efektler" => _efektler,
            _ => _ayarlar ??= new AyarlarSayfasi(),
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (App.TepsiyeKucult && !App.Cikiliyor)
        {
            e.Cancel = true; // efekt tepside sürer
            Hide();
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!App.Cikiliyor) App.Cik(); // tepsiye küçültme kapalıysa pencereyi kapatmak çıkıştır
    }
}
