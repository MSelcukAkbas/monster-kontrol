# 12 — Performans arka planı: gerçek yük bulguları ve mimari sonuç

2026-11-10 21:07–21:18. Kullanıcı: "Ölçmemiz gereken her şeyi ölçelim, bugün temelleri atalım, arayüz sonra."

**Koşul:** gerçek karışık yük (Android/Gradle derleme + Chrome/YouTube + Claude), CPU yükü %15–80 dalgalı. Açılış 17:54, 19:27–19:31 uyku. Açılıştan beri bizim fan yazmamız yok. Profil Eğlence (bayt 30 = 1), plan Dengeli. Monster Klavye "gün batımı" efektiyle (15 kare/sn) sürekli yazıyordu. → Bu ölçüm test 16/19 **değil** (soğuk açılış yok, kontrollü yük yok).

Ham veri: `prototip/kayit/2026-11-10_gercek_yuk.csv` (1 Hz, `etiket` sütunu = test adımı), `2026-11-10_gpu.csv`. Betikler: `prototip/kayit.py`, `test8.py`, `test8b.py`.

## 1. Bulgular

### Fan modları (test 8 ✅)

| Mod (0x79, 0x01) | Örnek | CPU ort / maks °C | Duty | Sonuç |
|---|---|---|---|---|
| 0 Otomatik (firmware) | 53 + 87 | 86 / 90 | **sabit %35** (iki fan) | 90°C'de bile değişmiyor |
| 5 maxQ | 147 | 75,5 / 90 | **sabit %35** | otomatikle aynı |
| 6 Custom | 144 + 40 | 79 / 91 | CPU %45–99 (ort %90), GPU %52–55 | **sıcaklığı izliyor** |

- **Custom eğri = CC'nin sayfa 4 kaydı** (ofset 16–18 duty 35/50/99, 22–24 sıcaklık 40/60/80): doğrusal ara değer — 72°C'de beklenen ~%79, görülen %78–82.
- **EC'nin kendi hız sınırı var:** artış ~+%10/sn (%45→%99 beş saniyede), azalış ~−%5/sn.
- **Gürültü:** EC ham bayt 18'i izliyor; patlamalı yükte duty %73↔%99 arası salınıyor (09 #4'teki sorun EC'de de var). Eğri tasarımı bunu hesaba katmalı (§3 D).
- **GPU fanı** kendi eğrisinde (sayfa 4 ofset 32+), bayt 17 ≈ 53–55°C iken %52–55.
- **İkinci yazar kontrolü:** CC çalışmıyordu. `DCHUService.exe` ve `LaunchFnKey.exe` içinde fan döngüsü yok (yalnız WMI olayları, kısayol, PL1/PL2/voltaj ayarları; `il2.ps1`). → Eğriyi büyük olasılıkla **EC** uyguluyor. Kesin ayrım: test 20 (servis durdurulmuşken custom), 🔐.
- ~~CC eğriyi `WriteAppSettings` sayfa 4 ile yazıyor~~ → **yanlış ipucu** (aşağıda "CC IL incelemesi"): o sabitler string şifre çözücüsünden (`6000004`) geliyordu.

### Sabit hız ↔ custom (test 8b, yeni ✅)

| Adım | Sonuç |
|---|---|
| Custom'dayken `0x68` %40/%40 | Eğriyi **ezer**: 90°C'de %40'ta kaldı |
| Ardından `0x69 / 0x0F` | **Etkin moda döner** (custom eğri, %95–99) — firmware otomatiğine değil |
| `0x79` mod 0 | Düz %35'e iner (−%5/sn) |

→ `0x69` "etkin modun kendi denetimine dön" demek. Custom etkinse çıkış yolumuz eğriye düşer.

🐛 **Güvenli durum tanımı eksikti:** CLAUDE.md'deki "fan `0x69/0x0F`" custom kullanılmış bir oturumdan sonra firmware otomatiğine döndürmez. Doğru sıra: `0x79` mod 0, sonra `0x69/0x0F` (bugünkü dönüş test 8b'nin mod 0 adımı sayesinde doğruydu).

### Diğer

- **Tampon 12'de fan modu baytı yok** (mod değişince hiçbir bayt değişmedi) → modu geri okuyamıyoruz; duty ↔ sıcaklık ilişkisinden çıkarılır.
- **Bayt 21:** boşta 20, yükte **55–61** (01'de "bir kez 55, oyun") → sıcaklığa benzeyen ikinci bir sensör olabilir. ❓
- **Test 26 (kısmi ✅):** 10 dk boyunca Monster Klavye 15 kare/sn yazarken ikinci süreç 1 Hz okudu ve mod yazdı: 576 satır, boşluk 0, saçma değer 0, klavye uygulaması ayakta. Resmî protokol değil ama mutex ihtiyacına dair işaret yok.
- **Test 23 (sonuçsuz):** sonda 15 örnek, en çok 86°C; `Performance Limit Flags` 0, `% Performance Limit` 100. 90°C platosunda tekrar bakılacak (gölge kaydedici artık bu sayaçları yazıyor).
- **PDH `Processor Frequency` taban değeri verir** (2400). Gerçek frekans = 2400 × `% Processor Performance` / 100 (bu yükte 3,1–3,9 GHz).
- **NVML:** GPU uykudayken sıcaklık/kullanım "Unknown Error" (nvidia-smi 261/271 örnek), güç 4–9 W yine okunuyor; P8. Okuma GPU'yu uyandırmıyor gibi.
- **"Güvenli durum" serin değil:** firmware otomatiği (%35) gerçek yükte CPU'yu 90°C TCC platosunda tutuyor. İşlemci kendini koruyor ama sürekli 90°C.

### Kapı A + C (21:34–21:36, CCDCHUService durdurulmuş)

Betikler `prototip/testA_egri.py` + `testA_geri.py` (geri yükleme ayrı süreçte), kayıt `prototip/kayit/testA.log`. Yük artık hafifti (CPU 59–88°C, patlamalı).

| Adım | Sonuç |
|---|---|
| A1 servis kapalı, custom, eski eğri | CPU fanı %49–88, sıcaklığı izliyor → **eğriyi EC uyguluyor, servis değil (test 20 ✅)** |
| A2 aynı baytları yaz (`WriteAppSettings(4,16,12)`) | ret 1, sayfa değişmedi |
| A3 bayt 18: 99→55, mod 6 | ret 1, geri okumada yalnız (18, 99→55). **EC yeni eğriyi almadı:** 62°C'de %96, 87°C'de %99 (yeni eğriyle 80°C altı en çok %55, %99 için ~99°C gerekirdi) |
| A4 mod 0 → 6 yeniden tetik | Değişmedi: 61°C'de %89 |
| Geri yükleme | ret 1; taze süreçte sayfa 4 = yedek ✅; mod 0 + `0x69` → %35/%35 ✅ |

- **`WriteAppSettings` çalışıyor ve kısmi ofset yazıyor** (ret 1 = yazıldı; okuma 4096 döndürüyordu), ama sayfa 4 **EC'nin kullandığı eğri değil**: EC eğriyi kendi RAM'inde tutuyor. Sayfa 4 ya açılışta EC'ye yükleniyor (kapı B: yaz → yeniden başlat) ya da CC eğriyi başka bir SetDCHU komutuyla gönderiyor (CC'nin IL'inde mod 6'dan önceki komutlar aranmalı).
- **GPU fanı:** servis kapalı + custom'da %0 / 0 RPM (bayt 17 = 50–51°C); servis açıkken (8) 53–55°C'de %52–55 idi. Fark sıcaklık eşiği de olabilir, servis de. ❓
- Profil ve güç planı bu testte değişmedi (Eğlence, Dengeli).

### CC IL incelemesi (iki katlı çağrı taraması, salt okuma)

ControlCenter30.exe'nin donanıma giden tüm yolları yalnız InsydeDCHU.dll üzerinden (WMI yalnız okuma için, servis/boru yok):

| Sarmalayıcı | İmza | Kullanım |
|---|---|---|
| `60000CA` | `(cmd, alt, uint değer)` → `SetDCHU_Data(cmd, GetBytes(değer) + [3]=alt)` | `121,25,p` profil; `121,1,m` fan modu; `121,45,x` bir anahtar |
| `60000C7` | `(cmd, byte[] giriş, ref byte[] çıkış)` → `SetDCHU_DataEx(cmd, giriş, 256, çıkış)` | Hep `cmd 4`, `giriş[0]` = alt komut: 7, 8 (açılışta yetenek sorgusu), 21, 22, 24, 25, 26, 30, 31 |
| `60000CD` / `60000C5` | `WriteAppSettings(sayfa, ofset, uzunluk, buf)` | Sayfa 4 ofset 9 (fan anahtarı), 28; sayfa 1 ofset 18, 22, 30, 34 … |

- **Fan modunu değiştiren tek yer** bir açma/kapama düğmesi: açık → `121,1,8` (**mod 8**, bilinmiyor) + sayfa 4 ofset 9 = 1; kapalı → mod 0 + ofset 9 = 0.
- **Eğri noktası yazan kod yok:** ne sayfa 4 ofset 16–27'ye yazan, ne `SetDCHU_DataEx`'e eğri gönderen bir yol bulundu. Bu CC sürümü (7.68.1.0) custom eğri düzenlemiyor gibi; sayfa 4'teki 35/50/99 @ 40/60/80 büyük olasılıkla **fabrika varsayılanı** ve EC'nin eğrisiyle aynı olması da bundan.
- **Mod 8 denendi (21:54, `prototip/test_mod8.py`, kayıt `kayit/test_mod8.log`; kullanıcı: CC'deki düğme "Turbo fan"):** ret 121; 60 sn boyunca CPU fanı **değişmedi** (%35 düz, 57–81°C), GPU fanı çoğunlukla %0 (b17 49–51°C, ara sıra tekme). Turbo etkisi yok. Mod 0 + `0x69` sonrası %35/%35'e döndü. GPU fanının %0'a düşmesi otomatikte de b17 ≈ 50°C'de ara sıra görülüyor → muhtemelen sıcaklık eşiği (kapı A'daki GPU %0 gözlemi de büyük olasılıkla bu). Turbo için CC'nin ayrıca yazdığı sayfa 4 ofset 9 = 1 (NVRAM) gerekiyor olabilir — denenmedi.
- `SetDCHU_DataEx(4, [alt…])` ailesi keşfedilmemiş bir komut alanı: eğri yükleme orada olabilir ama anlamları bilinmiyor; körlemesine denemek yok.

### CC'nin custom eğrisi: sayfa 4 düzeni çözüldü (22:00)

Kullanıcı CC'de custom eğriyi değiştirdi (CPU 53°C → %98, GPU 61°C → %79). Ardından EC **yeni eğriyi izliyor**: CPU 64–68°C'de %98–99 (eski eğriyle ~%55–62 olurdu). Kayıt `prototip/kayit/custom_cc.log`, sayfa yedeği `prototip/kayit/sayfa4_cc_ozel_2026-11-10.json`.

| Ofset | CPU | GPU | Anlam |
|---|---|---|---|
| duty % (4 nokta, sonuncusu 100) | 16–19 | 34–37 | |
| sıcaklık °C (4 nokta, sonuncusu 100) | 22–25 | 40–43 | |
| **eğim** (3 parça, 16 bit LE) | 28, 30, 32 | 46, 48, 50 | `round((d₂−d₁) × 2,55 × 16 / (t₂−t₁))` = duty(0–255)/°C × 16 |
| fan modu | 5 | | CC custom'da 6, turbo'da 1 |

- Eğim formülü eski (fabrika) ve yeni eğrinin 12 değerinin **hepsini** tutturuyor. EC ara değeri büyük olasılıkla bu eğimlerle hesaplıyor.
- **Kapı A bu yüzden geçersizdi:** yalnız bayt 18'i (nokta) değiştirdik, eğimleri (30, 32) güncellemedik; EC 60–80°C'yi eski eğimle hesapladı. Ayrıca yük patlamalıydı.
- 20–21 / 26–27 (50,99 / 60,80) değişmedi — anlamı bilinmiyor. CC ayrıca 53–60, 64, 68, 81–82'yi değiştirdi (başka ayarlar olabilir).
- **Kapı A' (2026-11-10) ❌ — EC sayfa 4'ü çalışırken okumuyor.** CC kapalı, servis açık, hafif yük. Tutarlı test eğrisi (35/45/60/100 @ 40/60/80/100, eğimler 20/31/82) sayfa 4'e yazıldı (geri okumada doğru), sonra mod 6. CPU 67–75°C'de fan 60 sn boyunca **%98–99** kaldı: test eğrisi ~%52 verirdi, kullanıcının CC eğrisi %98. EC, CC'nin en son ittiği eğriyi tutuyor; sayfa 4 yalnız CC'nin kaydı. Eğriyi EC'ye CC başka bir çağrıyla gönderiyor (aday: `SetDCHU_DataEx(4, …)` alt komutları). Sayfa yedekten geri yazıldı, taze okumayla birebir doğrulandı; fan mod 0 + `0x69` → %35. Kayıt `prototip/kayit/testA2.log`.
- **Gönderen bulundu: `FanSpeedSetting.exe`** (CC'nin ayrı .NET uygulaması, 7.87, `WindowsApps\CLEVOCO.504814C03D814_…\FanSpeedSetting\`). Açılışta sayfa 4'ü okur, eğriyi **`SetDCHU_Data(14, 256 bayt)`** ile EC'ye gönderir (metot 600001C; düzen doc 01). EC'nin etkin eğrisi **`GetDCHU_Data_Buffer(13)`** ile okunur (bayt 16–31). Açılışta sayfa 4 ofset 81–82'ye de yazdı (0,0 → 1,255; anlamı bilinmiyor).
- **Kapı A'' (2026-11-10) ✅ — eğriyi biz yazabiliyoruz, NVRAM'a dokunmadan.** `prototip/testE_egri14.py`: sayfa 4'ten hesaplanan komut 14 tamponu EC'dekiyle birebir tuttu (kendini sınama). CPU'ya fabrika eğrisi gönderildi → tampon 13 hemen (60,128) gösterdi → custom modda 60–65°C'de fan %50–66 (kullanıcı eğrisiyle %98), 80°C'de %94–99. Kullanıcı eğrisi geri gönderildi, tampon 13 nokta nokta aynı; fan otomatik. Kayıt `prototip/kayit/testE_egri14.log`.

## 2. Mimari sonuç (03'teki "test 8'e bağlı" kararı)

**EC fan denetçisi olabilir.** Bu, 03'teki servis/bekçi gerekçesinin çoğunu kaldırıyor: uygulama ölse, Görev Yöneticisi'nden öldürülse, oturum kapansa da fan EC eğrisini izler.

| Katman | Kim | Ne yapar | Yönetici |
|---|---|---|---|
| 0 — Sigorta | **EC**, custom mod + bizim eğri | Her koşulda sıcaklığa göre fan | — |
| 1 — Uygulama | Monster Performans (tepsi, kullanıcı modu) | Sensör + kayıt; profil uygulama = CC güç profili + Windows planı (PROCFREQMAX/EPP/boost) + EC eğrisi; acil durum (önce frekans tavanı); uyku/açılışta yeniden uygulama + geri okuma (duty ↔ eğri) | Hayır |
| (2 — isteğe bağlı) | PawnIO | Gerçek watt (öğrenme için) | Evet |

- **Yazılım fan döngüsü (`0x68`) varsayılan değil.** Ezdiği için uygulama öldürülürse sabit hız asılı kalır. Yalnız ileride "eğri gürültüsünü yumuşatma" gerekirse, çıkışta `0x69` (→ eğriye döner) ile.
- **Servis yok** (P4 "fan bekçisi" büyük olasılıkla gereksiz) — §3 A ve B olumluysa kesinleşir.
- Uygulamanın donanıma yazdıkları seyrek ve olay güdümlü olur: profil değişimi, açılış, uyanma. 1 Hz döngü yalnız okur + acil durum.

## 3. Açık kapılar (P1'den önce)

| # | Soru | Yöntem | Onay |
|---|---|---|---|
| A | ✅ **Eğriyi biz yazabiliyor muyuz?** Evet: `SetDCHU_Data(14)` (A''), doğrulama `GetDCHU_Data_Buffer(13)`. Sayfa 4 yalnız CC'nin kaydı (A' ❌). Açık: EC belleği geçici → uyku/yeniden başlatmada eğri kalıyor mu (B) | Komut 14 gönder → tampon 13 oku → mod 6 → geri gönder | Fan yazması, kullanıcı onayı |
| B | **Custom mod kalıcı mı?** Kullanıcı gözlemi (ölçülmedi): CC'de custom eğri seçiliyken bilgisayar kapanıp açılınca otomatik moda dönüyor → uygulama açılışta (ve büyük olasılıkla uyanmada) mod 6 + komut 14'ü yeniden göndermeli | Uyku → uyanma, yeniden başlatma sonrası duty sıcaklığı izliyor mu; sayfa 4 ofset 5 (CC'nin mod kaydı) açılışta uygulanıyor mu | Kullanıcı (yeniden başlatma) |
| C | ✅ **EC** — servis kapalıyken de eğri izleniyor | Test 20: servis durdurulmuşken custom eğri | 🔐 |
| D | **Eğri tasarımı** | EC histerezissiz ve ham bayt 18'i izliyor → 70–88°C bandında yatık eğri, taban yüksek; test 21 (dB↔RPM) ile rezonans aralıkları | — |
| E | **GPU yükünde** | Test 17: GPU fanı neyi izliyor, CPU fanı GPU ısısına tepki veriyor mu (ortak boru) | — |

**Kapı A olumlu (A''):** uygulama profil değişiminde, açılışta ve uyanmada kendi eğrisini komut 14 ile gönderir, tampon 13 ile geri okur; NVRAM'a yazmaz. Aşağıdaki "olumsuz" seçeneği yedek olarak duruyor.

**Kapı A olumsuz çıkarsa mimari:** katman 0 yine EC custom + **fabrika eğrisi** (35/50/99 @ 40/60/80 — mod 0'ın düz %35'inden çok daha iyi). Eğriyi biçimlendirmek isteyen profiller uygulama çalışırken `0x68` kullanır; uygulama kapanırken/çökünce `0x69` → custom eğriye düşer (8b). Çökmede `0x68` asılı kalır → o profillerde küçük bekçi gerekir.

**Kullanıcı kararı bekleyen:** güvenli çıkış durumu "firmware otomatik (%35)" yerine "custom + güvenli eğri" olsun mu? (CLAUDE.md kuralı değişir.)

## 4. P0 kod temeli (dal `performans/p0`)

| Proje | İçerik |
|---|---|
| `performans/MonsterPerformans.Cekirdek` | `DchuDurumu` (tampon 12 çözümü, sensör hatası), `PdhSayaclari` (İngilizce adlarla 6 sayaç), `Nvml` (uykuda null), `Olcum` (+ gerçek MHz), `Ornekleyici` (salt okuma), `CsvKayitci` (günlük CSV, her satır diske, gün dönümü) |
| `performans/MonsterPerformans.Golge` | Konsol gölge kaydedici: 1 Hz → `%LocalAppData%\MonsterPerformans\kayit\yyyy-MM-dd.csv`, Ctrl+C |
| `performans/MonsterPerformans.Cekirdek.Tests` | 8 test (gerçek tampon örneği, sensör sınırları, CSV biçimi, gün dönümü) |

Gerçek donanımda 25 sn denendi: DLL, PDH, NVML okunuyor; CSV doğru. Sıradaki: gölge modda birkaç saat (oyun dahil) çalıştırmak.
