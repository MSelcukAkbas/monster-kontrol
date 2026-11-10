# 09 — İkinci görüş (Fable 5.1) ve kararlar

2026-11-10 15:25. Tüm belgeler, prototipler ve ham CSV'ler bağımsız bir modele (Fable 5.1) eleştirel inceleme için verildi; yalnız okudu. Aşağıda her itiraz, **doğrulama** (varsa) ve **karar**.

## Salt-okuma ile hemen doğrulananlar

| İddia | Sonuç |
|---|---|
| Windows "Energy Meter" sayacı paket gücünü sürücüsüz verebilir | ❌ Sayaç seti **yok** (iki kez denendi) |
| Clevo WMI (`CLEVO_GET`) ile DLL atlanabilir | ❌ Sınıf **yok** → DLL tek yol |
| Modern Standby riski | ✅ **Yok**: `powercfg /a` → yalnız S3 + hazırda bekletme; S0 düşük güç desteklenmiyor |
| Hızlı başlangıç riski | ⚠️ **Gerçek**: `HiberbootEnabled = 1` (açık) |
| Geri alma için paket aile adları | ✅ Kaydedildi (bkz. 06) |

## Kabul edilenler (planı değiştirenler)

| # | İtiraz | Karar |
|---|---|---|
| 1 | **Sıcaklık tabloları denge değeri değil.** Aynı güçte (2,9 GHz ~26 W, fan %60) iki tur 63,6°C ve 72–74°C; fark kasanın ısınmışlığından | ✅ Kabul. 02'deki sayılar "soğuk kasa, 40 sn" diye etiketlendi. Profil kalibrasyonu için **10–15 dk/nokta, karışık sıra, 2 tekrar, oda sıcaklığı notu** (test 16). Arayüzde sabit "~91°C" ipuçları kaldırıldı. Eğri **kötü koşula** göre tasarlanacak |
| 2 | **GPU yüklü hiç ölçüm yok** | ✅ Kabul. Test 17: oyun / GPU yükü. "Oyunda CPU'yu 3,2'ye indir" kuralı ölçüm sonrasına ertelendi (oyunlar 1–3 ağır iş parçacığı kullanır, tek çekirdek hızını kesmek kare hızını düşürebilir) |
| 3 | **Test 8 (custom mod EC'de mi?) mimariyi belirler, 1. sıraya** | ✅ Kabul. EC eğriyi kendisi uyguluyorsa yazılım ölse de fan sıcaklığı izler → servis gereksinimi büyük ölçüde düşer |
| 4 | **Eğim (dT/dt) terimi gürültü üretir** — fan çip sıçramasını etkileyemez (%60→%100 yalnız −3°C), bayt 18 tamsayı | ✅ Kabul, **çıkarıldı**. Yerine τ = 20–30 sn ortalama + eğride 3–5°C histerezis |
| 5 | **Acil durum 92°C, Maksimum profilin 90°C platosunda limit döngüsü yapar; %100 fan koruma değil** | ✅ Kabul. Acil durumun ilk eylemi **`PROCFREQMAX` düşür** (doğrulanmış kaldıraç), fan ikincil. Giriş 95°C/3 sn, çıkış 85°C altı 60 sn. Eğri 88–90°C'de zaten %100. Sınırın geri alınma kuralı tanımlı |
| 6 | **Profil 0x79/0x19'un yalnız Eğlence↔Performans farkı doğrulandı** (bayt 30: 3 vs 1) | ✅ Kabul edildi, sonra **çözüldü**: MSR 0x610 dört profilin PL1'ini 15/30/45/60 W gösterdi ([10](10-veri-envanteri-ve-ogrenen-model.md)). Profil indirme koruma kaldıracı olarak geri geldi (üçüncü eylem) |
| 7 | **"Firmware otomatik %35'te kalıyor" tek gözlem**; bizim 0x68/0x69 yazmalarımız EC'yi etkilemiş olabilir | ✅ Kabul. Test 19: soğuk açılış, hiçbir yazma yapmadan, 5–10 dk tam yük, 4 profil |
| 8 | **Servis küçük olsun: yalnız "fan bekçisi"** (fan döngüsü + koruma + çıkışta otomatik). Klavye/efekt/otomatik profil UI sürecinde (oturum 0'dan zor). Named pipe yerine `ayarlar.json` izleme | ✅ Kabul. 03 güncellendi |
| 9 | **Her turda duty geri okuma ↔ komut karşılaştırması**; tutmuyorsa yeniden uygula. "Değişim < %2 ise gönderme" tek başına tehlikeli | ✅ Kabul. Uyku, hızlı başlangıç, Fn tuşu, CCDCHUService müdahalesini yakalar |
| 10 | **Hızlı başlangıç**: servis kapanışı görmez, açılışta EC sıfırlanmış olur | ✅ Kabul. Açılış/uyanmada tüm durumu geçersiz say + 9. madde. Hızlı başlangıcı kapatma seçeneği kullanıcıya sorulacak (pil yok, kayıp küçük; 🔐) |
| 11 | **CCDCHUService pasif değil** (LaunchFnKey, WakePanelUp_Hook, CRPS) — ikinci yazar | ✅ Kabul. Test 20: servis durdurulmuşken DLL çalışıyor mu, Fn+ışık sonrası renk |
| 12 | DLL yolu için yedek arama (`acpibridge1.inf_amd64_*`) | ✅ Kabul |
| 13 | Klavye efekti 30 kare/sn → 10–15 ile başla | ✅ Kabul (artık tek F0 komutu; yine de kuyruk payı) |
| 14 | **Güç modeli karara bilgi katmıyor** (yalnız u, f'nin fonksiyonu), yapısı da içbükey veriye doğrusal | ✅ Kabul. Formül yerine **ölçülen adım tablosundan ara değer**, yalnız gösterim için ("~W"); ya da hiç gösterme. Karar mantığı u ve f'yi doğrudan kullanır |
| 15 | **"Gereksiz turbo −%39" abartılı** (meşgul döngü sabit iş değil); asıl kazanç **sessizlik**: 2 iş parçacıklı turbo fan %60'ta 82–89°C, 8 iş parçacıklı 3,4 GHz'ten sıcak | ✅ Kabul. 02 düzeltildi |
| 16 | Kendi turbo dedektörü yerine Windows ayarları (`PERFEPP`, `PERFBOOSTMODE` verimli modları, `PROCFREQMAX`) profile konsun | ✅ Kabul |
| 17 | "Eğlence güç tavanı 42 W" yanlış ifade → **tavan 90°C**, güç ısınmaya göre 38–42 W | ✅ Kabul |
| 18 | Bayt 18 "✅✅" fazla güçlü; uyum 90°C platosunda, 65–88°C aralığı karşılaştırılmadı | ✅ Kabul, "✅ (65–88°C aralığı ek kıyas bekliyor)" |
| 19 | Fan gürültüsü ölçülmeli (telefonla dB↔RPM, rezonans aralıkları) | ✅ Kabul. Test 21 |
| 20 | Fiziksel bakım (macun/pad/temizlik) yazılımdan önce yapılırsa kalibrasyon bir kez yapılır | ✅ Kabul — kullanıcı kararı |
| 21 | GPU kare hızı sınırı (NVIDIA Max Frame Rate) oyunda en büyük sessizlik kazancı olabilir | ✅ Profil notu olarak |
| 22 | Monster'ı kaldırmadan önce bir hafta "açma, başlangıçtan kapat"; test 13 ve bilinmeyen 0x79 alt komutları (0x18, 0x1E, 0x22) kaldırmadan önce kapanmalı; `Get-AppxProvisionedPackage` kontrolü | ✅ Kabul. 06 güncellendi |
| 23 | Kısma nedeni sürücüsüz kısmen var (`Performance Limit Flags`, termal bölge `Throttle Reasons`) | ✅ Kabul (sayaçlar okunuyor; firmware 90°C kısmasını gösterip göstermediği test edilecek) |
| 24 | Undervolt "araştırılmadı" ret gerekçesi değil | ✅ Kabul. Tek seferlik kilit kontrolü (ThrottleStop, uygulama dışı, kullanıcı onayıyla) — test 22 |

## Kısmen kabul / itiraz

| İtiraz | Görüşüm |
|---|---|
| Gölge mod denetleyiciyi doğrulayamaz | Kısmen katılıyorum: firmware %35'teyken oluşan sıcaklık bizim eğriyle oluşacak olan değil. Ama **eğrinin ne zaman ve ne kadar tetikleneceğini** (osilasyon, acil durum sıklığı) ve **DLL'in saatlerce çökmeden çalıştığını** gösterir. "1–2 gün kapı" yerine **bir oyun oturumu dahil birkaç saat**; kayıt kalıcı özellik. |
| Servis yerine 20 satırlık "bekçi süreç" | Görev Yöneticisi senaryosunu karşılar, oturum kapatma/uyku yeniden uygulamayı karşılamaz. Test 8 olumsuz çıkarsa küçük servis tercih; olumlu çıkarsa bekçi süreç yeterli. |

## Güncellenmiş öncelik sırası (05'e işlendi)

1. **Test 8** — custom mod EC'de mi (mimariyi belirler)
2. **Test 3 + 20** — servis bağlamı, CCDCHUService durdurulunca
3. **Test 19** — firmware otomatiğini temiz koşulda yeniden doğrula
4. **Test 17** — GPU/oyun yükü
5. **Test 16** — uzun denge ölçümleri (profil sayıları)
6. Diğerleri
