# 05 — Yarın birlikte yapılacak testler

Komutlar `prototip/` klasöründen çalıştırılır.

**Öncelik sırası (ikinci görüşten sonra, [09](09-ikinci-gorus.md)):** **8** (custom mod EC'de mi — mimariyi belirler) → **3 + 20** (servis bağlamı, CCDCHUService durdurulunca) → **19** (firmware otomatiği temiz koşulda) → **17** (GPU/oyun yükü) → **16** (uzun denge ölçümleri) → diğerleri. Test 13 ve 0x79 bilinmeyen alt komutları Monster kaldırılmadan **önce** kapanmalı.

| # | Test | Nasıl | Beklenen / karar |
|---|---|---|---|
| 1 | ✅ **Klavye bölgesi — YAPILDI** | İki kez, gözle | Tek bölge, yalnız F0 etkili → yalnız F0'a yaz |
| 2 | ✅ **CPU sıcaklığı doğruluğu — YAPILDI** | LibreHardwareMonitor + PawnIO (sonra kaldırıldı) ile Intel DTS ↔ bayt 18 | Fark ±1°C → bayt 18 yeterli, eşikler değişmiyor |
| 3 | **Servis bağlamında DLL** | Küçük test servisi (LocalSystem) tampon 12 okusun, sonucu dosyaya yazsın (🔐 tek seferlik `sc create`) | Çalışırsa mimari servis; çalışmazsa tek exe + zamanlanmış görev |
| 4 | **Fn tuşları** | Fn+klavye ışığı, Fn+fan (varsa), Fn+F-tuşları: hangisi çalışıyor, ekranda OSD çıkıyor mu | FnKey'in neyi yönettiğini anlamak (kaldırma planı) |
| 5 | **Açılışta / uykudan dönüşte ne oluyor** | Klavyeyi yeşil + fanı sabit %30 yap → uyku → uyan; sonra yeniden başlat | EC/servis bizim ayarı koruyor mu, CC kaydını mı uyguluyor? → yeniden uygulama mantığı |
| 6 | **Duran fanı düşük hızla başlatma** | GPU fanı %0 → bekle → %10 / %15 / %20; RPM 5 sn'de > 0 mı | Kick-start eşiği |
| 7 | ✅ **Donanım efektleri — YAPILDI** | 7 efekt 8'er sn | Hiçbiri çalışmadı; CC de bu modelde efekt sunmuyor → yazılım efektleri |
| 8 | **Fan modu 5 / 6** | `fan.py mod maxq`, `mod custom` + tampon 12 | Custom'da CC eğrisi firmware'de mi uygulanıyor? ✅ **2026-11-10: eğriyi EC/firmware uyguluyor (kullanıcı modunda yazar yok; CCDCHUService kesin dışlanması test 20); maxQ = otomatik = düz %35** ([12](12-performans-arka-plan.md)) |
| 9 | **WriteAppSettings** (opsiyonel, dikkatli) | Yalnız sayfa 2 ofset 35 (parlaklık) tek bayt yaz → CC'de görünüyor mu → eski değeri geri yaz | Firmware'e kalıcı yazabiliyor muyuz (açılış rengi için) |
| 10 | **Uzun süreli sessiz profil** | 2,9 GHz + fan %15, 10 dk tam yük | Dengedeki gerçek sıcaklık (35 sn'lik testten 1–3°C fazla olabilir) |
| 11 | **Webcam / touchpad anahtarı** (ekstra) | 0x06/0x09 oku; 0x22/0x2A yaz → durum değişiyor mu | Ekstra özellik olarak eklenebilir mi |
| 13 | **Bizim profil yazma ↔ CC'nin Performans modu** | Aynı koşulda (fan Max, 20 sn yük): önce `fan.py profil performans`, sonra CC'den Performans seç; frekansları karşılaştır | Gece bizimki 3764, CC'ninki 3875 MHz gördü (farklı koşullar). Fark büyükse CC ek bir şey yazıyor → `il2.ps1` ile ControlCenter30.exe'de ara |
| 14 | **Fan kombinasyon haritası** (bkz. 08) | 3,2 GHz tam yük; (CPU,GPU) fan = (50,50) (70,30) (30,70) (60,40) (40,60), 60'ar sn | Ortak boruda hangi fan daha etkili; ses kullanıcı kulağıyla |
| 15 | ✅ **Performans/watt — YAPILDI** | PawnIO + LHM, fan %60 | 2,9 GHz 25 W (en verimli) → 3,6 GHz 42 W; bkz. 02 |
| 16 | **Uzun denge ölçümleri** (09) | Profil noktaları için 10–15 dk/nokta, karışık sıra, 2 tekrar, oda sıcaklığı not | Gerçek denge sıcaklıkları; eğri kötü koşula göre |
| 17 | **GPU / oyun yükü** (09) | Bir oyun ya da GPU stres + CPU; NVML GPU °C/W, bayt 18, fanlar | Ortak boruda GPU'nun etkisi; "oyunda CPU tavanı" kuralı buna göre |
| 18 | ✅ **4 güç profilinin gerçek farkı — YAPILDI** | MSR 0x610 (PawnIO, salt okuma) | PL1/PL2: sessiz 15/30, tasarruf 30/30, eğlence 45/107, performans 60/135 W — hepsi farklı (10) |
| 23 | **PDH kısma bayrakları yük altında** (10) | "Performans Sınırı Bayrakları/Yüzdesi" 90°C'de | 0x64F (hep 0) yerine sürücüsüz kısma bilgisi ⏳ 86°C'ye kadar 0 / %100 — 90°C'de tekrar ([12](12-performans-arka-plan.md)) |
| 24 | **ETW PMC çözümü** (10) | TraceEvent ile `pmc.etl` → IPC/LLC; LHM kapalıyken `PmcCtrConrruption` | IPC gerçekten okunabiliyor mu |
| 25 | **PresentMon FPS** (10) | Oyunda yönetici olmadan/ile | Oyun için gerçek performans vekili |
| 19 | **Firmware otomatiği temiz koşulda** (09) | Soğuk açılış, **hiçbir DCHU yazması yapmadan**, 5–10 dk tam yük, 4 profil | "%35'te kalıyor" bizim yazmalarımızın yan etkisi mi? |
| 20 | **CCDCHUService durdurulunca** (09) | 🔐 servis durdur → `oku.py`, Fn tuşları, Fn+ışık sonrası renk → servisi başlat | İkinci yazar riski |
| 21 | **Fan gürültüsü** (09) | Telefonla dB ölçer; %10…%100 adımları, rezonans yapan RPM aralıkları | Eğri o aralıkları atlar |
| 22 | **Undervolt kilidi** (09, opsiyonel) | ThrottleStop ile yalnız okuma (kullanıcı onayıyla, uygulama dışı) | Kilitli değilse güç azaltmanın en etkili yolu |
| 26 | **İki süreçten eşzamanlı DLL** (03) | Süreç A tampon 12'yi 1 Hz okur, süreç B 15 kare/sn renk yazar, 10 dk | Çökme / bozuk okuma yoksa iki uygulama bağımsız; varsa `Global\MonsterKontrolDchu` mutex'i ✅ kısmi: 10 dk, sorun yok ([12](12-performans-arka-plan.md)) |
| 27 | **CC uyku zamanlayıcısı firmware'de mi** (11) | CC kapalı, zamanlayıcı kaydı 6 dk açık; 7 dk dokunma; efekt çalışırken de dene | Klavye uygulamasının kendi zamanlayıcısı gerekli mi / çakışır mı |
| 12 | **Monster kaldırma provası** | Bkz. 06; kaldır → yeniden başlat → 1, 4, 5'i tekrar | Bağımlılık kalmadığını doğrula |

## Gece yapılan hazırlık: soğuk açılış gözlemi (test 5'in ilk verisi)

Kapanmadan hemen önce klavye bizim komutla **yeşil (0,255,0), parlaklık 255** yapıldı. CC'nin kaydı ise **mavi (0,0,200), parlaklık 1, mod 8 (tek renk)**. Kapanış `shutdown /s /t 0` ile yapıldı (tam kapanış; Başlat menüsünden kapatma "hızlı başlangıç" hibrit kapanışıdır ve farklı davranabilir).

Sabah açılışta klavyeye ilk bakış:

| Görülen | Anlam |
|---|---|
| Soluk mavi | Açılışta CC'nin kaydı uygulanıyor (BIOS/EC veya CCDCHUService) → bizim uygulama açılışta yeniden uygulamalı |
| Yeşil, parlak | EC bizim son değerimizi koruyor |
| Başka bir şey | Firmware varsayılanı → açılış efekti ayarına (sayfa 2, ofset 7) bak |

**Sonuç (13:20 açılışı):** Klavye **mavi** geldi → açılışta CC kaydı uygulanıyor (firmware veya CCDCHUService; CC uygulaması açılmadı). Kalan soru: Monster uygulamaları kaldırıldıktan sonra da böyle mi?

## Güvenlik kuralları (tüm testler için)

- Yük testinde koruma: CPU ≥ 95°C → test durur, fanlar Max (`yuk.py`, `arama.py` bunu yapıyor).
- Her testten sonra güvenli hale dön: `python fan.py otomatik`, `python fan.py profil eglence`, `powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e`.
- Firmware'e yazma (test 9) yalnız kullanıcı onayıyla ve geri alma değeri not edilerek.
