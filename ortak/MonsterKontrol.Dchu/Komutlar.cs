namespace MonsterKontrol.Dchu;

/// <summary>
/// DCHU komut numaraları ve argüman kodlamaları. Hepsi bu makinede ölçülerek doğrulandı (docs/01).
/// </summary>
public static class Komutlar
{
    /// <summary>Okuma tamponu: fan, sıcaklık, profil (bayt haritası docs/01).</summary>
    public const int DurumTamponu = 12;

    /// <summary>Klavye ışığı (TUXEDO: CLEVO_CMD_SET_KB_RGB_LEDS, 0x67).</summary>
    public const int KlavyeIsigi = 0x67;

    /// <summary>Sabit fan hızı: fan1 | fan2 &lt;&lt; 8 (0–255).</summary>
    public const int FanHizi = 0x68;

    /// <summary>Fanları firmware otomatiğine döndür; argüman 0x0F (fan başına bir bit). 0 etkisiz.</summary>
    public const int FanOtomatik = 0x69;

    /// <summary>Seçenek komutu: alt komut üst baytta (0x01 fan modu, 0x19 güç profili).</summary>
    public const int Secenek = 0x79;

    /// <summary>Tek bölgeli klavye: yalnız F0 bölgesi etkili (gözle test edildi). Renk sırası B, R, G.</summary>
    public static uint KlavyeRenk(byte r, byte g, byte b) =>
        0xF000_0000u | ((uint)b << 16) | ((uint)r << 8) | g;

    /// <summary>Parlaklık 0 (kapalı) – 255.</summary>
    public static uint KlavyeParlaklik(byte deger) => 0xF400_0000u | deger;

    /// <summary>uint32 argümanı DLL'in beklediği 4 baytlık little-endian diziye çevirir (x64 Windows hep little-endian).</summary>
    public static byte[] Bayt(uint arguman) => BitConverter.GetBytes(arguman);
}
