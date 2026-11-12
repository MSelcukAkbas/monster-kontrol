# Monster Kontrol

Monster ABRA A5 V16.6 (Clevo) dizüstü için üreticinin Control Center uygulamasının yerine geçen tek bir Windows uygulaması: klavye ışığı, fan ve güç denetimi, sistem izleme.

## Özellikler

- **Klavye:** renk, parlaklık, 11 efekt (nefes, ateş, sıcaklığa göre renk…), tepsi simgesi ve hızlı panel.
- **Performans:** CPU/GPU sıcaklık, fan, frekans ve güç panosu; hazır ve özel fan/güç profilleri; aşırı ısınmada acil durum korumaları.
- **Uygulamalar:** en çok CPU/GPU kullanan programlar, anlık ve ortalama değerlerle.
- **Kayıt:** ölçümler günlük CSV dosyalarına yazılır; açılışta son 30 dakika geri yüklenir.
- **MCP sunucuları:** Claude gibi yapay zekâ araçları klavyeyi yönetebilir ve performans durumunu okuyabilir (`klavye/MonsterKlavye.Mcp`, `performans/MonsterPerformans.Mcp`).

## Uyumluluk

> ⚠️ Yalnız **Monster ABRA A5 V16.6** üzerinde test edildi. Fan ve güç komutları donanıma doğrudan yazar; başka modelde kullanmak risklidir.

| Parça | Gereken | Not |
|---|---|---|
| Donanım erişimi | Clevo "Acpi Bridge" sürücüsü (`InsydeDCHU.dll`) | Clevo tabanlı olmayan bilgisayarda klavye ve fan çalışmaz. |
| Klavye | Tek bölgeli RGB | Çok bölgeli klavyelerde doğru çalışmaz. |
| Fan | Bu modelin DCHU fan komutları | Başka Clevo modellerinde farklı olabilir. |
| İşlemci | Herhangi bir x64 işlemci | Frekans sınırı yüzde olarak verilir; yüzde, Performans ayarlarındaki **En yüksek frekans** değerine göre MHz'e çevrilir (varsayılan 4100: i5-10200H). |
| Ekran kartı | NVIDIA (isteğe bağlı) | GPU verileri `nvml.dll` ile okunur; NVIDIA yoksa bu alanlar boş kalır. |
| İşletim sistemi | Windows 10/11 x64 | |

## İndirme

[Releases](../../releases) sayfasından son `MonsterKontrol.exe` dosyasını indirip çalıştırın. Tek dosyadır, kurulum ya da .NET gerektirmez. Uygulama kapatılınca tepside çalışmaya devam eder; tamamen çıkmak için tepsi simgesinden **Çıkış** seçin.

## Derleme ve çalıştırma

[.NET 10 SDK](https://dotnet.microsoft.com/download) gerekir.

```bat
MonsterKontrol.bat
```

Bat dosyası uygulamayı derleyip başlatır; `MonsterKontrol.bat temiz` her şeyi sıfırdan derler. Uygulama açıksa önce tepsi simgesinden **Çıkış** yapın.

Testler:

```bat
dotnet test klavye\MonsterKlavye.Tests
dotnet test performans\MonsterPerformans.Cekirdek.Tests
dotnet test ortak\MonsterKontrol.Dchu.Tests
```

## Yeni sürüm yayınlama

`v` ile başlayan bir etiket gönderin; GitHub Actions testleri çalıştırır, exe'yi derler ve Release'e ekler:

```bat
git tag v1.0.0
git push origin v1.0.0
```

## Proje yapısı

| Klasör | İçerik |
|---|---|
| `kontrol/` | Monster Kontrol: tek pencere, iki bölümü bir arada başlatır |
| `klavye/` | Klavye motoru, efektler, sayfalar ve MCP sunucusu |
| `performans/` | Sensörler, fan/güç profilleri, kayıt, sayfalar ve MCP sunucusu |
| `ortak/` | `InsydeDCHU.dll` sarmalayıcısı ve komut tanımları |
| `docs/` | Donanım araştırması, ölçümler, kararlar (başlangıç: `docs/README.md`) |
| `prototip/` | Araştırma sırasında kullanılan Python/PowerShell betikleri ve ölçüm kayıtları |

## Lisans

[MIT](LICENSE)
