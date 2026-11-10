# 01 — Donanım ve DCHU komut referansı

Tüm değerler 2026-11-10'da bu makinede **ölçülerek** doğrulandı. "✅" = okuma ile doğrulandı, "👁" = kullanıcı gözle doğruladı, "❓" = doğrulanmadı.

## Makine

| | |
|---|---|
| Model | Monster ABRA A5 V16.6 (Clevo şasi, ACPI `CLV0001` + `CLV0002`) |
| CPU | Intel Core i5-10200H (Comet Lake) — taban 2,4 GHz, turbo 1/2/3/4 çekirdek 4,1/4,1/4,0/4,0 GHz, **TjMax 100°C, TCC ofseti 10 → işlemci 90°C'de kendini kısıyor** (MSR 0x1A2) |
| GPU | NVIDIA GTX 1650 Ti + Intel UHD |
| BIOS | INSYDE 1.07.10TFB5 (2020-10-05) |
| Pil | Yok (ölü, çıkarıldı). Sürekli prizde. Pil/DC kodları gereksiz. |
| Soğutma | CPU ve GPU **ortak bakır ısı borusu** üzerinde, 2 fan |
| Klavye | RGB, **tek bölge — yalnız F0 komutu etkili** (gözle test: F1/F2/F3 yok sayılıyor). BIOS bayrağı yanlışlıkla 3-bölge diyor. CC'de bu model için efekt yok (LED Modu: yalnız "Klavye") |
| OS | Windows 11 Pro 26H2, yönetici olmayan oturumda tüm DCHU çağrıları çalışıyor |

## Yazılım yığını

```
Uygulama (bizim / Control Center)
   │  P/Invoke
InsydeDCHU.dll  (DriverStore\acpibridge1.inf_amd64_<hash>\)
   │  CreateFile + DeviceIoControl
AcpiBridge.sys  (ACPI\CLV0001, arayüz {86994c74-ad43-4812-b7e7-0c420b5c5fd7})
   │  ACPI _DSM, UUID 93f224e4-fbdc-4bbf-add6-db71bdc0afad
BIOS / EC
```

- `InsydeDCHU.dll` **sürücü paketinin parçası**, Store uygulamalarının değil. Monster uygulamaları kaldırılsa da kalır.
- DriverStore klasör adındaki hash sürücü güncellenince değişir → DLL yolunu **çalışma anında** bul: `HKLM\SYSTEM\CurrentControlSet\Services\CCDCHUService\ImagePath` dizini.
- Store paketindeki kopyalar (`WindowsApps\...`) yüklenemiyor: "Erişim engellendi".
- DLL **x64**. Uygulama x64 derlenmeli. Tüm çağrılar **tek bir iş parçacığından** (kilitli kuyruk) yapılmalı; DLL'in eşzamanlılık güvencesi bilinmiyor.
- Kaynak: komut numaraları TUXEDO Linux sürücüsüyle birebir aynı — [clevo_interfaces.h](https://github.com/tuxedocomputers/tuxedo-drivers/blob/main/src/clevo_interfaces.h), [tuxedo_io.c](https://github.com/tuxedocomputers/tuxedo-drivers/blob/main/src/tuxedo_io/tuxedo_io.c), [TCC tuxedo_io_api.hh](https://github.com/tuxedocomputers/tuxedo-control-center/blob/master/src/native-lib/tuxedo_io_lib/tuxedo_io_api.hh). Okuma tamponu düzeni: [opencontrol.py gist](https://gist.github.com/sunfkny/390117f4d849db091bc4e2b5ac53ca76), [opendchu](https://github.com/hUwUtao/opendchu).

## DLL fonksiyonları (imzalar ControlCenter30.exe P/Invoke'larından)

| Fonksiyon | İmza | Not |
|---|---|---|
| `SetDCHU_Data` | `int (int cmd, byte[] data, int len)` | data = 4 bayt, **little-endian** `uint32` |
| `GetDCHU_Data_Integer` | `int (int cmd, out int value)` | Dönüş değeri de değeri taşıyor |
| `GetDCHU_Data_Buffer` | `int (int cmd, ref byte buf)` | 🐛 **DLL HATASI: 256 baytlık veriden sonra 257. bayta (indeks 256) 0 yazıyor.** 256'lık tampon → yığın bozulması → rastgele `0xC0000005` çökmesi (iki kez yaşandı, sonra yeniden üretildi). **Tamponu en az 257, tercihen 512 bayt ayır.** `ReadAppSettings` ve `GetDCHU_Data_Integer` taşmıyor (işaret baytıyla test edildi) |
| `ReadAppSettings` | `int (int page, int offset, int len, byte* buf)` | Dönüş 4096. **Geçersiz sayfa/aralık DLL'i çökertebilir** (bir kez exit 5 görüldü) → izole et |
| `WriteAppSettings` | `int (int page, int offset, int len, byte* buf)` | ❓ hiç denenmedi. Muhtemelen firmware NVRAM → **döngüde asla yazma** (flash aşınması) |
| `SetDCHU_DataEx` | `(int, byte[], int, ...)` | Kullanılmadı |

> ⚠️ **Dönüş değerleri başarı kodu DEĞİL.** `SetDCHU_Data` her zaman komut numarasını geri döndürüyor (39 → 39, ama hiçbir şey değişmedi). Her yazmadan sonra mümkünse **geri okuyarak** doğrula (fan: tampon 12). Klavye yazmalarının geri okuması yok.

## Yazma komutları

| Komut | Hex | Argüman (uint32) | Ne yapar | Durum |
|---|---|---|---|---|
| 103 | 0x67 | `0xF0000000 \| B<<16 \| R<<8 \| G` | Klavye rengi bölge 0 (F1/F2/F3 = bölge 1/2/3) | 👁 yeşil, turuncu, kırmızı |
| 103 | 0x67 | `0xF4000000 \| 0..255` | Klavye parlaklığı (0 = kapalı) | 👁 0 ve 255 |
| 103 | 0x67 | `0x1002A000` nefes, `0x33010000` döngü, `0x70000000` rastgele, `0x80000000` dans, `0x90000000` tempo, `0xA0000000` flaş, `0xB0000000` dalga | Donanım efektleri | ❌ **7'si de denendi, hiçbiri çalışmadı.** CC de bu model için efekt sunmuyor → donanımda yok. Efektler yazılımla yapılacak |
| 104 | 0x68 | `fan1 \| fan2<<8` (0–255 her biri) | **Sabit fan hızı**, iki fan bağımsız | ✅ %0–100 arası; %0'da fan durur |
| 105 | 0x69 | `0x0F` (fan başına bir bit) | **Fanları otomatiğe döndür** | ✅ hem sabit hızdan hem Max modundan dönüyor. `0` göndermek etkisiz! **Etkin moda döner:** custom'dayken `0x68` sonrası `0x69` → custom eğri (12) |
| 14 | 0x0E | 256 baytlık tampon (`SetDCHU_Data(14, tampon, 256)`): fan i = 0 CPU, 1 GPU, 2 (3. yuva): `[2+4i..5+4i]` = t₂, round(d₂%×2,55), t₃, round(d₃%×2,55); `[14+6i..19+6i]` = 3 eğim, **büyük endian**; 0–1 = 0 | **Custom fan eğrisini EC'ye yükler** (FanSpeedSetting.exe 7.87, metot 600001C). 1. ve 4. nokta gönderilmez | ✅ 12 kapı A'': tampon 13'te anında görünür, custom modda fan yeni eğriyi izler. EC belleği, NVRAM değil. Dönüş 20 |
| 121 | 0x79 | `(0x01<<24) \| mod` → mod: 0 otomatik, 1 max, 5 maxQ, 6 custom | Fan modu | ✅ hepsi (12): otomatik ve maxQ düz %35, custom = EC sayfa 4 eğrisini izler (~+%10/sn, −%5/sn); **8** = CC "Turbo fan" düğmesi, tek başına etkisiz görünüyor (12) |
| 121 | 0x79 | `(0x19<<24) \| p` → p: 0 sessiz, 1 tasarruf, 2 performans, 3 eğlence | **Güç profili** (güç sınırı) | ✅ MSR 0x610 ile doğrulandı: **PL1/PL2 = sessiz 15/30 W, tasarruf 30/30, eğlence 45/107, performans 60/135** (bkz. 10) |
| 121 | 0x79 | `0x18000000`, `0x1E000000`, `0x22000001` | LedKeyboardSetting kullanıyor, anlamı bilinmiyor | ❓ |

**Yanlış komutlar (tekrar kullanma):** 39 (0x27) ve 61 (0x3D) yalnızca *beyaz* klavyeler içindir (`SET/GET_KB_WHITE_LEDS`). Bu RGB klavyede 39 hiçbir şey yapmadı.

**Profil yazmanın yan etkisi:** Control Center'ın kendi kaydı (sayfa 1, ofset 1) değişmez. CC açılırsa kendi kaydını geri uygular.

**Fan güvenlik notları (TUXEDO'dan):**
- TUXEDO %25'in altını kısıyor (%12,5 altı → 0, arası → %25); TCC Clevo için en düşük %20 bildiriyor. Bizim %10 testi, fanlar **zaten dönerken** çalıştı. Duran fanı düşük hızla başlatmak dönmeyebilir → **kick-start**: önce ~%30 / 2 sn, sonra hedef.
- Yazdıktan sonra okumadan önce **≥100 ms bekle** (TUXEDO notu: 50 ms yetmiyor).
- **Güvenli dönüş = önce `0x79` mod 0 (`0x01000000`), sonra `0x69/0x0F`.** Yalnız `0x69` etkin moda döner: custom'daysa eğride kalır (test 8b, [12](12-performans-arka-plan.md)). `fan.py otomatik` yalnız `0x69` gönderir.
- **Firmware koruması YOK:** sabit hızda CPU 90°C iken fan %10'da kaldı. Koruma bizim sorumluluğumuzda.

## Okuma komutları

### Tampon 12 (`GetDCHU_Data_Buffer(12)`) — ana durum kaynağı

| Bayt | Anlam | Doğrulama |
|---|---|---|
| 2–3 | CPU fanı ham (büyük-endian). RPM = `60 / (5.56521739130435e-05 * ham) * 2` | ✅ CC ile birebir (5543 RPM) |
| 4–5 | GPU fanı ham, aynı formül | ✅ (4529 RPM) |
| 6–7 | 3. fan (yok) | — |
| 16 | CPU fanı duty 0–255 | ✅ |
| 17 | "GPU" sıcaklığı | ⚠️ **GPU çekirdeği DEĞİL**: nvidia-smi ortalama +7°C (boşta), +8°C (CPU yükünde) fazla gösteriyor ve bayt 17 geride kalıyor. GPU koruması için **NVML** kullan |
| 18 | CPU sıcaklığı °C | ✅ **Intel DTS ile karşılaştırıldı** (tek tur, 18 örnek; 65–88°C aralığı ek kıyas bekliyor, 09) (LibreHardwareMonitor 0.9.6 + PawnIO, `prototip/dts_kiyas.csv`): boşta bayt 18 − Paket ort −0,1°C, tam yük sabit durumda ±1°C (90 ↔ 90–91). Bayt 18 ≈ "Core Max". Yük başında 1–2 sn geriden geliyor (ilk örnekte −13). Çekirdekler arası fark ~7°C (#2 en serin) → bayt 18 en sıcak çekirdeği izlediği için koruma için doğru değer. ACPI `\_TZ.TZ0` aynı kaynak (= bayt 18 + 0,1) |
| 19 | GPU fanı duty 0–255 | ✅ |
| 21 | Boşta 20, gerçek yükte 55–61 (12) — ikinci sıcaklık sensörü olabilir. Güç sınırı **değil** (profil değişince değişmedi) | ❓ |
| 30 | Etkin güç profili: Performans = 3, diğer üçü = 1 | ✅ |
| 10–13 | Değişken (ör. 198,0,1,85) | ❓ |

### Tampon 13 (`GetDCHU_Data_Buffer(13)`) — EC'nin etkin fan eğrisi

| Bayt | Anlam | Durum |
|---|---|---|
| 16–23 | CPU eğrisi: 4 × (sıcaklık °C, duty 0–255) | ✅ 12 kapı A'' |
| 24–31 | GPU eğrisi, aynı düzen | ✅ |
| 32–39 | 3. fan yuvası (bu makinede (0,0) (100,255) (100,255) (0,0)) | ❓ |
| 0–15, 43 | Bilinmiyor (FanSpeedSetting 12 ve 43'ü okuyor); 0–15 mod ile değişebilir | ❓ |

### Tamsayı komutları (`GetDCHU_Data_Integer`)

| Komut | Okunan | Anlam |
|---|---|---|
| 0x52 BIOS_FEATURES_1 | `0x04690025` | Bit `0x00400000` (3-bölge RGB) **açık**, `0x40000000` (beyaz) kapalı |
| 0x7A BIOS_FEATURES_2 | `0x78020053` | |
| 0x63 FANINFO1 | `0x00573459` | düşük bayt = duty (0x59 = %35) |
| 0x64 FANINFO2 | `0x00140159` | |
| 0x06 / 0x07 / 0x09 | 1 / 0 / 1 | Webcam açık, uçak modu kapalı, touchpad açık |

### Control Center ayar sayfaları (`ReadAppSettings`)

Yedek: [appsettings-yedek.json](appsettings-yedek.json) (sayfa 0,1,2,4,5 dolu).

| Sayfa, ofset | Anlam |
|---|---|
| 1, 1 | Güç modu: 0 sessiz, 1 tasarruf, 2 performans, 3 eğlence |
| 2, 7 | Açılış efekti — CC'deki "Önyükleme Etkisini Geçersiz Kıl" kutusu. Açılışta kayıtlı rengin (mavi) gelmesinin muhtemel sebebi: BIOS bu sayfadaki rengi açılışta kendisi uyguluyor |
| 2, 32 | Klavye modu: 0 rastgele, 1 custom, 2 nefes, 3 döngü, 4 dalga, 5 dans, 6 tempo, 7 flaş, 8 tek renk |
| 2, 35 | Klavye parlaklığı 0–4 (CC'nin 5 kademesi) |
| 2, 36 / 37–39 | Uyku zamanlayıcısı açık / saat-dakika-saniye |
| 2, 81–83 | Renk R,G,B (85, 89, 93… diğer bölge/slotlar, `1,R,G,B` deseni) |
| 2, 84 | Işık açık |
| 4, 5 | Fan modu: 0 otomatik, 1 max, 5 maxQ, 6 custom |
| 4, 7 | Fan ofseti |
| 4, 16–18 / 22–24 | Custom eğri: duty % / sıcaklık °C (3 nokta + sabit 100°C→%100) |

## Açık çelişkiler / bilinmeyenler

1. ~~Klavye bölgesi~~ → **çözüldü**: tek bölge, yalnız F0.
2. ~~Bayt 18 doğruluğu~~ → **çözüldü**, Intel DTS ile ±1°C (yukarıda).
3. ~~Donanım efektleri~~ → **çözüldü**: bu modelde yok.
4. **Açılışta kayıtlı ayar uygulanıyor — kısmen cevaplandı.** Kapanmadan önce klavye yeşil/255 yapıldı; açılışta **mavi** geldi (CC kaydı: 0,0,200). CC uygulaması açılmamıştı → uygulayan firmware veya CCDCHUService. Hangisi olduğu, Monster uygulamaları kaldırıldıktan sonra yeniden başlatılarak anlaşılacak. Her iki durumda da bizim uygulama açılışta kendi ayarını **yeniden uygulamalı**.
5. **Servis (LocalSystem, oturum 0)** bağlamında DLL çalışıyor mu.
