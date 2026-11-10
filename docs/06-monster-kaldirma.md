# 06 — Monster (Clevo) uygulamalarını güvenle kaldırma

**Ne zaman:** Bizim uygulama Aşama 2'ye (servis) gelip en az birkaç gün sorunsuz çalıştıktan sonra. Önce [05](05-yarin-test-listesi.md) test 3, 4, 5.

## Envanter (2026-11-10)

| Bileşen | Tür | Ne yapıyor | Karar |
|---|---|---|---|
| `CLEVOCO.ControlCenter3.0` 7.68.1.0 | Store uygulaması | Ana CC ekranı (güç, LED, fan menüsü) | **Kaldır** |
| `CLEVOCO.LedKeyboardSetting` 7.68.0.0 | Store uygulaması | Klavye ışığı (103 komutları burada) | **Kaldır** |
| `CLEVOCO.504814C03D814` 7.87.0.0 | Store uygulaması | `FanSpeedSetting.exe` | **Kaldır** |
| `CLEVOCO.FnhotkeysandOSD` 7.101.0.0 | Store uygulaması | `FnKey.exe` — Fn tuşları ve ekran bildirimleri (OSD) | **Bırak** ⚠️ |
| `acpibridge.inf` (Insyde 1.0.0.7) | Sürücü, `ACPI\CLV0001` | `AcpiBridge.sys` — DLL'in konuştuğu çekirdek sürücü | **Bırak — zorunlu** |
| `acpibridge1.inf` (CLEVO 1.0.40.0) | Sürücü, `ACPI\CLV0002` | `DCHUService.exe` (CCDCHUService, LocalSystem, otomatik) + **`InsydeDCHU.dll`** | **Bırak — zorunlu** |

### Neden FnKey kalmalı

`acpibridge1.inf` içinde:

```ini
[AcpiBridge1_Device.NT.Software]
AddSoftware = ClevoCtrlPnl,,Software_Inst
[Software_Inst]
SoftwareType = 2
SoftwareID = pfn://CLEVOCO.FnhotkeysandOSD_6h6z29zh29qx0
```

Windows, sürücüye bağlı bu uygulamayı **otomatik yeniden kurar**. Kaldırmak boşa çaba olur, Fn tuşları da çalışmaz hale gelebilir. Diğer üç uygulama sürücüye bağlı değil, kaldırılınca geri gelmez (Store "otomatik güncelleme" ile de gelmez; yine de 1 hafta izle).

### Neden sürücüler kalmalı

Bizim uygulama `InsydeDCHU.dll` → `AcpiBridge.sys` yolunu kullanıyor. Sürücü kaldırılırsa hiçbir kontrol çalışmaz.

## Önce (ikinci görüş, [09](09-ikinci-gorus.md))

- **Bir hafta "açma, başlangıçtan kapat"** ile yaşa: üç Store uygulaması açılmadıkça çalışmıyor; aynı etki, tam geri dönüş. Kaldırmanın kazancı küçük (çakışma riskini taşıyan CCDCHUService ve FnKey zaten kalıyor).
- Kaldırmadan **önce** kapat: test 13 (CC Performans ile bizim yazmamız arasında frekans farkı) ve anlamı bilinmeyen 0x79 alt komutları (0x18, 0x1E, 0x22). Sonra karşılaştıracak referans kalmaz.
- `Get-AppxProvisionedPackage -Online | ? DisplayName -match CLEVO` (🔐): hazır kurulu ise büyük Windows güncellemesinde geri gelebilir.
- Monster'ın sürücü sayfasından Control Center kurulum paketini indirip sakla (Store aramasında üretici uygulamaları çıkmayabilir).

**Paket aile adları (geri kurulum için):**

| Paket | PackageFamilyName |
|---|---|
| Control Center 3.0 | `CLEVOCO.ControlCenter3.0_6h6z29zh29qx0` |
| LED Keyboard | `CLEVOCO.LedKeyboardSetting_6h6z29zh29qx0` |
| Fan Speed Setting | `CLEVOCO.504814C03D814_6h6z29zh29qx0` |
| Fn hotkeys & OSD (kalacak) | `CLEVOCO.FnhotkeysandOSD_6h6z29zh29qx0` |

## Adımlar

1. **Yedek:** `docs/appsettings-yedek.json` zaten alındı. Kaldırmadan hemen önce tekrar al:
   ```bash
   python prototip\ayar_yedek.py docs\appsettings-yedek-kaldirma-oncesi.json
   ```
2. CC'de son ayarları not et (ekran görüntüsü yeterli).
3. Kaldır (kullanıcı hesabı, yönetici gerekmez). Not: `Get-AppxPackage` virgüllü isim listesi kabul etmiyor (denendi, hata veriyor); her paket ayrı sorgulanmalı:
   ```powershell
   'CLEVOCO.ControlCenter3.0','CLEVOCO.LedKeyboardSetting','CLEVOCO.504814C03D814' | % { Get-AppxPackage $_ } | Remove-AppxPackage
   ```
4. Yeniden başlat.
5. Doğrula:
   - `Get-AppxPackage *CLEVO*` → yalnız FnhotkeysandOSD.
   - `Get-Service CCDCHUService` → Running.
   - `python prototip\oku.py` → tampon 12 okunuyor.
   - Bizim uygulama: fan, klavye, profil çalışıyor.
   - Fn tuşları (test 4 sonuçlarıyla karşılaştır).
6. Geri alma: Microsoft Store'da "Control Center 3.0" ara ve kur (kullanıcı bunu daha önce Store'dan kurmuştu).

## Riskler

- CCDCHUService açılışta kendi kayıtlı (CC) ayarlarını uyguluyorsa, bizim servis sonra yeniden uygulamalı (test 5 bunu söyleyecek).
- Fn tuşuyla CC açılmaya çalışılırsa (ör. "Control Center" kısayolu) boşa düşer; zararsız.
- Bir BIOS/sürücü güncellemesi komut numaralarını değiştirirse uygulama açılışta öz-sınama yapmalı (tampon 12 makul mü, RPM > 0 mı) ve başarısızsa güvenli moda (firmware otomatik) geçmeli.
