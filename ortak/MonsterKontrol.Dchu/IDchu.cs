namespace MonsterKontrol.Dchu;

/// <summary>InsydeDCHU.dll üzerindeki işlemler. Testlerde sahte uygulama kullanılır.</summary>
public interface IDchu
{
    /// <summary>
    /// Komut yazar. Dönüş değeri başarı kodu DEĞİL (DLL komut numarasını geri döndürüyor; docs/01),
    /// bu yüzden void. Doğrulama gerekiyorsa geri okunmalı.
    /// </summary>
    void Yaz(int komut, uint arguman);

    /// <summary>Komutun 256 baytlık okuma tamponunu döndürür.</summary>
    byte[] TamponOku(int komut);

    int TamsayiOku(int komut);
}
