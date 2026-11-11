namespace MonsterPerformans.Cekirdek;

/// <summary>Sabit kapasiteli halka tampon: en eski örnek düşer (pano grafiği, 30 dk = 1800 örnek).</summary>
public sealed class Gecmis<T>(int kapasite)
{
    readonly T[] _veri = new T[kapasite];
    int _bas, _sayi;

    public int Sayi => _sayi;

    public void Ekle(T x)
    {
        _veri[(_bas + _sayi) % kapasite] = x;
        if (_sayi < kapasite) _sayi++;
        else _bas = (_bas + 1) % kapasite;
    }

    /// <summary>Son n örnek, eskiden yeniye.</summary>
    public List<T> Son(int n)
    {
        n = Math.Min(n, _sayi);
        var l = new List<T>(n);
        for (var i = _sayi - n; i < _sayi; i++)
            l.Add(_veri[(_bas + i) % kapasite]);
        return l;
    }
}

public enum SicaklikBandi { Normal, Ilik, Sicak, Kritik }

public static class Sicaklik
{
    /// <summary>docs/04: &lt; 70 nötr, 70–85 sarı, 85–92 turuncu, ≥ 92 kırmızı.</summary>
    public static SicaklikBandi Band(int c) =>
        c >= 92 ? SicaklikBandi.Kritik : c >= 85 ? SicaklikBandi.Sicak : c >= 70 ? SicaklikBandi.Ilik : SicaklikBandi.Normal;
}
