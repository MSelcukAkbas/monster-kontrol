# Monster Kontrol — proje belgeleri

Monster ABRA A5 V16.6 (Clevo) için Control Center'ın yerine geçecek kendi fan / klavye ışığı / güç uygulamamız.

## ⭐ KALDIĞIMIZ YER (2026-11-10 21:00) — yeni oturum buradan başlasın

- **Proje:** `C:\Users\akbas\Desktop\MonsterKontrol` (tek git deposu). Çalışma dalı **`klavye/k0-iskelet`** (K0–K3 commit edildi; K4 commit bekliyor; `main` henüz boş).
- **K4 kodu bitti, kullanıcı denemesi bekliyor:** ayar kaydı (`Ayarlar.cs`, `%AppData%\MonsterKlavye\ayarlar.json`; 500 ms birleştirerek, geçici dosya + taşıma ile yazılır; bozuk dosya → varsayılan), tek örnek (`Local\MonsterKlavye` mutex + "göster" olayı, `App.xaml.cs`), Windows ile başlat (`WindowsIleBaslat.cs`, HKCU Run + `--tepsi`; varsayılan kapalı, Ayarlar'dan açılır), tepsi (`Tepsi.cs`: WinForms NotifyIcon, simge klavye renginde, sol tık `HizliPanel`, sağ tık WPF menü), Ayarlar sayfası, kapatınca tepsiye küçült, güvenli kapanış (çıkışta sabit renk yazılır), açılıştan 4 sn sonra bir kez yeniden uygula. UIA betiğiyle doğrulandı: ikinci açılış tek süreçte kalıp pencereyi gösterir; kapat → tepsi; öldür + `--tepsi` → pencere yok, renk/efekt geri gelir; çıkış yolu (tepsiye küçült kapalı + kapat) kod 0, hata olayı yok, ayar kaydedildi; tepside nefes CPU %0,82, 156 MB. Kullanıcı tepsi simgesi/panel/menüyü gözle doğruladı; panel ve menü Tepsi tasarımına uyduruldu. **Bekleyen:** gerçek yeniden başlatma testi (kullanıcıda).
- **K4 kararları:** WPF-UI.Tray yerine yerleşik WinForms NotifyIcon (paket indirmeden; `Using Remove` ile tür çakışmaları giderildi). Ayar dosyasında yalnız ETKİN efektin hız/skalası saklanır; galerideki diğer efekt ayarları oturumluk.
- **Monster Klavye K0–K3 bitti** ve kullanıcı onayladı. K3: efekt motoru (`Klavye/EfektMotoru.cs`, 15 kare/sn, klavye durumunun tek sahibi) + 11 efekt (`Klavye/Efektler.cs`, saf fonksiyonlar, 8 renk skalası) + galeri (`Sayfalar/EfektlerSayfasi`). Kullanıcı: "gayet düzgün çalışıyorlar".
- **K3 kararları:** efekt parlaklığı renge katılır (RGB × parlaklık), donanım parlaklığı kullanıcınınkinde kalır → karede en çok 1 DLL yazması. CPU: efekt + küçültülmüş %0,72, galeri açıkken %1,22; ayrı parlaklık komutuyla %1,41'di (maliyet DLL yazma sayısında). Bellek 148–162 MB çalışma kümesi / 75 MB özel (Debug). Skalalı efekt çalışırken Işık'tan renk seçmek sabit renge geçirir; nefes/kalp/flaş Işık rengini kullanır.
- **Sıradaki:** K4 kullanıcı onayı + commit → K5 (uyku/kilit: efekti duraklat + yeniden uygula), K6.
- **Derleme:** .NET 10.0.403 SDK kullanıcı profilinde, PATH'te DEĞİL. `DOTNET_ROOT` kalıcı kullanıcı değişkeni yapıldı (exe çift tıkla açılır). Kabuktan derlerken yine:
  `$env:DOTNET_ROOT="$env:LOCALAPPDATA\Microsoft\dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"`
  Sonra `dotnet build MonsterKontrol.slnx`, `dotnet test MonsterKontrol.slnx` (75/75 geçiyor). Uygulama: `klavye\MonsterKlavye\bin\x64\Debug\net10.0-windows\MonsterKlavye.exe` (çözüm x64 derler; `bin\Debug\…` eski kopya, kullanma). Açıkken derleme exe kilidi yüzünden başarısız olur → önce kapat. Claude kabuğunda `DOTNET_ROOT` yok: exe başlatmadan önce de ayarla. UIA: hazır renk düğmeleri yalnız DataItem görünür → fareyle tıkla.
- **Kod haritası:** `ortak/MonsterKontrol.Dchu` (DLL bulma, 512 B tampon, tek iş parçacığı kuyruğu, `Komutlar`), `klavye/MonsterKlavye` (`Klavye/Renk.cs`, `Klavye/KlavyeKontrol.cs` birleştiren gönderim, `Klavye/EfektMotoru.cs`, `Klavye/Efektler.cs`, `Sayfalar/IsikSayfasi`, `Sayfalar/EfektlerSayfasi`, `MainWindow` kendi yan menüsü), testler `*.Tests`, `klavye/MonsterKlavye.Duman` (K1 konsol testi, silinebilir).
- **Notlar:** WPF UI 4.3.0; vurgu fırçası olarak `AccentFillColorDefaultBrush` kullan (`SystemAccentColorPrimaryBrush` Windows mavisini gösteriyor). Ekran görüntüsü için `PrintWindow`; arayüzü UI Automation ile sürmek için düğmelerde `AutomationProperties.Name` var ("Seç: Ateş", "Klavyeye uygula"). Yapılmayanlar: gökkuşağında dönen vurgu (doc 04); Polis skala şeridi tüm yayı gösteriyor (efekt yalnız iki ucu kullanır). PawnIO hâlâ kurulu (kullanıcı kararı bekliyor). Performans uygulaması henüz başlamadı (test 8 öncelikli, [05](05-yarin-test-listesi.md)).

## Belgeler

| # | Belge | İçerik |
|---|---|---|
| 01 | [Donanım ve komutlar](01-donanim-ve-komutlar.md) | DLL, tüm DCHU komutları, okuma tamponu bayt haritası, doğrulama durumu, çelişkiler |
| 02 | [Ölçümler](02-olcumler.md) | Frekans × fan sonuçları, güç planları, firmware fan davranışı, önerilen profiller |
| 03 | [Mimari](03-mimari.md) | C#/.NET/WPF kararları, servis gerekçesi, proje yapısı, fan algoritması, aşamalar |
| 04 | [Arayüz](04-arayuz.md) | Sayfalar, görsel dil, tepsi, eğri düzenleyici |
| 05 | [Yarın test listesi](05-yarin-test-listesi.md) | Kodlamadan önce cevaplanacak 12 test |
| 06 | [Monster'ı kaldırma](06-monster-kaldirma.md) | Neyi kaldır, neyi bırak, adımlar, geri alma |
| 07 | [Ekstra fikirler](07-ekstra-fikirler.md) | Otomatik profil, kalibrasyon sihirbazı vb. |
| 08 | [ChatGPT fikirleri](08-chatgpt-fikirleri.md) | Konuşmadaki fikirlerin ölçümlere göre değerlendirmesi: ne eklendi, ne neden yok |
| 09 | [İkinci görüş](09-ikinci-gorus.md) | Fable 5.1'in eleştirisi, doğrulamalar, plana etkisi, yeni test önceliği |
| 11 | [⭐ Uygulama planı](11-uygulama-plani.md) | **İki ayrı uygulama** (Monster Klavye önce, Monster Performans sonra), ön koşullar, kilometre taşları, kabul ölçütleri |
| 10 | [Veri envanteri ve öğrenen model](10-veri-envanteri-ve-ogrenen-model.md) | Okunabilen tüm sinyaller (sürücüsüz / PawnIO / ETW), PL1/PL2 profil tablosu, TCC 90°C, öğrenen ısı modeli tasarımı, ChatGPT 2. tur |
| — | [appsettings-yedek.json](appsettings-yedek.json) | CC ayar sayfalarının yedeği (kaldırmadan önce) |

## Durum: neyi biliyoruz?

| Özellik | Okuma | Yazma |
|---|---|---|
| Fan RPM / duty / CPU sıcaklığı (Intel DTS ile ±1°C doğrulandı) | ✅ | — |
| ⚠️ DLL hatası: `GetDCHU_Data_Buffer` 257. bayta yazıyor → tampon 512 | — | — |
| Fan sabit hız (iki fan ayrı, %0–100) | ✅ | ✅ |
| Fan modu (otomatik / max) ve otomatiğe dönüş | ✅ | ✅ |
| Güç profili (sessiz/tasarruf/eğlence/performans) | ✅ | ✅ güç sınırını gerçekten değiştiriyor |
| Klavye renk / parlaklık / kapatma | ✅ (CC kaydından) | ✅ kullanıcı gözle doğruladı |
| Yazılım klavye efektleri (10 adet) | — | ✅ |
| Donanım klavye efektleri | — | ❌ çalışmadı |
| CPU frekans sınırı, turbo, çekirdek park | ✅ | ✅ (Windows güç planı) |
| GPU çekirdek sıcaklığı | NVML gerekli (bayt 17 değil) | — |

## Yarın nereden devam?

1. Bu oturuma devam et (belgeler ve `prototip/` bu klasörde). Hafıza notu da var ama klasöre bağlı; belgeler kendi başına yeterli.
2. [05](05-yarin-test-listesi.md)'teki test 1–5'i birlikte yap (~30 dk).
3. Kullanıcıyla karar: **tek exe ile başla → servise geç** (öneri) mi, doğrudan servis mi? (bkz. [03](03-mimari.md))
4. .NET 10 SDK kurulumu (kullanıcı onayıyla; UAC'siz seçenek 03'te).
5. Aşama 1: `MonsterKontrol.Core` + testler → WPF arayüz.

## Prototip betikleri (`prototip/`)

Python 3, salt `ctypes`. Hepsi yönetici izni olmadan çalışır.

| Betik | Ne yapar |
|---|---|
| `oku.py` | Fan, sıcaklık, güç modu, klavye kaydı — salt okuma |
| `fan.py` | `durum`, `mod otomatik\|max\|maxq\|custom`, `sabit CPU% GPU%`, `otomatik`, `profil sessiz\|tasarruf\|performans\|eglence` |
| `klavye.py` | `parlaklik 0-255`, `renk R G B`, efektler (`ates 0` = sonsuz), `demo` |
| `izle.py` | N saniye sıcaklık/yük/fan izleme |
| `yuk.py` | Tam yük testi (95°C korumalı) |
| `olcum.py` | Frekans sınırlarına göre puan/sıcaklık ölçümü |
| `arama.py` | Her frekans için en düşük güvenli fan hızını arar (`FANLAR` ortam değişkeni) |
| `ayar_yedek.py` | CC ayar sayfalarını JSON'a yedekler |
| `il.ps1`, `il2.ps1` | .NET exe'lerinden DCHU çağrılarını ve sabitleri çıkarır (tersine mühendislik yardımcısı) |
| `parlaklik.py` | ⚠️ Yanlış komut (39/61, beyaz klavye) — tarihsel, kullanma |

## Güvenli duruma dönüş (her zaman)

```bash
python prototip\fan.py otomatik
```
```bash
python prototip\fan.py profil eglence
```
```bash
powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e
```
