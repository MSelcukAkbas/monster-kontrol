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

    /// <summary>
    /// 256 baytlık tampon yazar (ör. 14 = fan eğrisi). Başarı dönmez: geri okuyarak doğrula.
    /// Varsayılan gövde: klavyedeki sahte uygulamalar bunu yazmak zorunda kalmasın.
    /// </summary>
    void TamponYaz(int komut, byte[] veri) => throw new NotSupportedException();

    int TamsayiOku(int komut);
}
