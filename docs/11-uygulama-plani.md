# 11 — Uygulama planı (kodlama)

2026-11-10 16:00. Kullanıcı kararı: **iki ayrı C# uygulaması**, ikisi de arayüzlü ve tek exe; birbirine bağımlı değil (performans kararsız çalışırsa klavye tek başına devam eder). **Önce Monster Klavye.** Mimari: [03](03-mimari.md).

## 0. Ön koşullar (kodlamadan önce, kullanıcı onayı gereken)

| # | İş | Not |
|---|---|---|
| 0.1 | **Proje klasörü** | Şu an oturumun geçici klasörü. Kalıcı bir yere taşınmalı (ör. `D:\Projeler\MonsterKontrol` — D: sabit disk, bol yer). `docs/` ve `prototip/` taşınır |
| 0.2 | **Git** | `git init`, `.gitignore` (bin/obj, *.etl, perf_watt ham verisi isteğe bağlı) |
| 0.3 | **.NET 10 SDK** | Kurulu değil. Seçenek A: `dotnet-install.ps1 -Channel 10.0` kullanıcı kapsamında (**UAC yok**). Seçenek B: `winget install Microsoft.DotNet.SDK.10` (🔐). Kurulumda güncel LTS sürümü doğrulanır |
| 0.4 | **NuGet paketleri** | `WPF-UI` (lepoco), `WPF-UI.Tray`; testler: `xunit`. Sürümler kurulumda Context7/NuGet'ten doğrulanır |

## 1. Ortak kütüphane: `MonsterKontrol.Dchu` (küçük)

| Öğe | Ayrıntı |
|---|---|
| DLL bulma | `HKLM\...\Services\CCDCHUService\ImagePath` dizini → yoksa `DriverStore\FileRepository\acpibridge1.inf_amd64_*\InsydeDCHU.dll`. Bulunamazsa açık hata |
| P/Invoke | `SetDCHU_Data(int, byte[], int)`, `GetDCHU_Data_Buffer(int, ref byte)` (**512 B tampon**), `GetDCHU_Data_Integer(int, out int)`, `ReadAppSettings(int,int,int, ref byte)` |
| Kuyruk | Tüm çağrılar tek adanmış iş parçacığında (`BlockingCollection`) |
| Kodlama | `uint32` little-endian; klavye rengi `0xF0000000 \| B<<16 \| R<<8 \| G`; parlaklık `0xF4000000 \| 0..255` |
| Arayüz | `IDchu` → testlerde sahte |
| Süreçler arası | Test 26 sorun gösterirse `Global\MonsterKontrolDchu` mutex'i (yalnız bu kütüphanede) |

## 2. Monster Klavye — kapsam

### 2.1 İlk sürüm (MVP)

| Özellik | Ayrıntı | Kaynak |
|---|---|---|
| Aç / kapat | Parlaklık 0 ↔ son değer | ✅ doğrulandı |
| Parlaklık | Kaydırıcı %0–100 → 0–255 | ✅ |
| Renk | Renk tekerleği + 8 hazır renk + HEX; **anında** klavyeye | ✅ yalnız F0 |
| Efektler | Nefes, gökkuşağı, renkli nefes, kalp, polis, **ateş** (kullanıcı beğendi), okyanus (geniş aralık), gün batımı, flaş (uyarılı), **sıcaklık rengi** | ✅ prototipte (`klavye.py`) |
| Efekt ayarı | Hız (periyot) + renk (tek renkli efektlerde) | yeni |
| Tepsi | Simge: o anki renk; sol tık hızlı panel, sağ tık menü (aç/kapa, efektler, uygulamayı aç, çıkış) | |
| Windows ile başlat | `HKCU\...\Run` (yönetici yok) | |
| Tek örnek | İkinci açılış mevcut pencereyi öne getirir | |
| Yeniden uygulama | Oturum açılışı, kilit açılışı, uykudan dönüş (`SystemEvents.PowerModeChanged`, `SessionSwitch`) → son ayarı yeniden yaz | ✅ açılışta BIOS CC kaydını (mavi) uyguluyor |
| Ayarlar | `%AppData%\MonsterKlavye\ayarlar.json` | |
| Güvenli kapanış | Çıkışta efekt durur, son sabit renk + parlaklık bırakılır | |

### 2.2 Sonra

- **Uyku zamanlayıcısı:** N dk klavye/fare hareketsizliği (`GetLastInputInfo`) → ışık kapalı, hareketle geri.
  ⚠️ CC'nin kendi zamanlayıcısı **açık, 6 dk** (sayfa 2, ofset 36–39). Firmware bunu kendisi uyguluyor olabilir → test 27.
- Uygulamanın vurgu rengi = klavye rengi (04).
- Kısayol tuşları (ör. Ctrl+Alt+K aç/kapa).
- Müzik / ekran rengi efektleri (07).
- Açılış rengini firmware'e kalıcı yazmak (`WriteAppSettings`, test 9 sonrası, tek seferlik — **döngüde asla**).

### 2.3 Efekt motoru

- `PeriodicTimer`, **15 kare/sn** (fan uygulamasıyla aynı DLL; 30'a çıkmadan önce test 26).
- Yalnız değişen değeri gönder (renk ve parlaklık ayrı ayrı karşılaştırılır).
- Efekt = saf fonksiyon `(t, ayarlar) → (renk, parlaklık)` → birim testli.
- Ekran kapalı / kilit / uyku → duraklat, sabit son renk.
- Sıcaklık efekti tampon 12 bayt 18'i saniyede bir okur (salt okuma).

### 2.4 Kabul ölçütleri

- Renk/parlaklık değişimi gözle < 100 ms.
- 15 kare/sn efekt: uygulamanın CPU kullanımı < %1, bellek < 150 MB.
- 8 saat kesintisiz efekt: çökme yok (DLL taşma hatası 512 B tamponla önlendi).
- Uyku → uyanma ve yeniden başlatma sonrası doğru renk.
- Performans uygulaması kapalıyken / çöktüğünde tam çalışır.

### 2.5 Kilometre taşları

| # | İş | Doğrulama |
|---|---|---|
| K0 ✅ | Çözüm iskeleti, `MonsterKontrol.Dchu`, birim testleri | `dotnet test` 14/14 (3'ü donanımda salt okuma) |
| K1 ✅ | Konsol duman testi: kırmızı → yeşil → mavi → kapalı → turuncu | kullanıcı gözle doğruladı |
| K2 ✅ | WPF + WPF UI 4.3.0 pencere: güç, ton şeridi, hazır renkler, HEX, parlaklık + kademeler; vurgu = klavye rengi; birleştiren gönderim (`KlavyeKontrol`) | 33/33 test, ekran görüntüsüyle kontrol |
| K3 | Efekt motoru + galeri | 15 kare/sn, CPU ölçümü |
| K4 | Tepsi, Windows ile başlat, tek örnek, ayar kaydı | yeniden başlatma testi |
| K5 | Uyku/uyanma/kilit yeniden uygulama | uyku testi |
| K6 | Tek dosya yayın (`dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`) | temiz çalıştırma |

## 3. Monster Performans — kilometre taşları (özet)

Ayrıntı: [03](03-mimari.md), [10](10-veri-envanteri-ve-ogrenen-model.md). Önce testler: 8 → 3/20 → 19 → 17 → 16 → 26.

| # | İş |
|---|---|
| P0 | Sensör okuma + kayıt (gölge mod): tampon 12, PDH (İngilizce adlarla), NVML, (PawnIO/LHM) → 1 Hz CSV |
| P1 | Pano arayüzü (sıcaklıklar, fanlar, frekans, güç, profil) — salt okuma |
| P2 | Güç profili + frekans tavanı (Windows planı) kontrolü |
| P3 | Fan eğrisi + acil durum + geri okuma karşılaştırması, tek exe içinde |
| P4 | Fan bekçisi (servis ya da bekçi süreç) + kaçış yolları |
| P5 | Çevrimiçi optimizasyon (tek düğme: frekans tavanı) |
| P6 | Monster uygulamalarını kaldırma (06) |

## 4. Riskler

| Risk | Önlem |
|---|---|
| İki süreç aynı DLL'i aynı anda çağırıyor | Test 26; gerekirse sistem mutex'i |
| DLL süreci düşürür (taşma vb.) | 512 B tampon; klavye uygulaması çökerse yalnız ışık etkilenir |
| Firmware/CCDCHUService rengi değiştirir (açılış, Fn tuşu) | Olaylarda yeniden uygulama; Fn tuşu algılanamaz → kullanıcı tepsiden "yeniden uygula" |
| Sürücü güncellemesi DLL yolunu değiştirir | Çalışma anında arama |
| CC açılırsa kendi rengini yazar | Belgelenmiş; kaldırma planı (06) |

## 5. Yeni test

- **27 — CC uyku zamanlayıcısı firmware'de mi:** CC zamanlayıcısı açık (6 dk) iken CC kapalıyken 7 dk dokunmadan bekle → ışık söndü mü; efekt çalışırken ne oluyor.
