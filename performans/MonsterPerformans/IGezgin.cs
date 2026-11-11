namespace MonsterPerformans;

/// <summary>Sayfalar arası gezinti (Profiller → Fan); hem kendi penceresi hem Monster Kontrol uygular.</summary>
public interface IGezgin
{
    void Git(string sayfa, string? profil = null);
}

