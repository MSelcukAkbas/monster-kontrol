# 08 — ChatGPT konuşmasındaki fikirlerin değerlendirmesi

Kaynak: kullanıcının 2026-11-09 ChatGPT konuşması. Her fikir **bu makinede ölçtüklerimize** göre değerlendirildi.

> ⚠️ Konuşmada işlemci **i5-11200H** yazıyor; doğrusu **i5-10200H** (10. nesil, Comet Lake). Ayrıca örnek tablolardaki sayılar (4,1 GHz 45 W vb.) ChatGPT'nin kendisinin de dediği gibi temsili — bizim gerçek ölçümlerimiz [02](02-olcumler.md)'de.

## Özet karar

| Fikir | Karar | Neden |
|---|---|---|
| CPU+GPU ortak termal eğri | ✅ **zaten planda** | Fan sürücüsü = max(CPU, GPU) ([03](03-mimari.md)) |
| Termal eğim (°C/sn) ile erken tepki | ❌ **çıkarıldı** ([09](09-ikinci-gorus.md)) | Önce eklenmişti; ikinci görüş haklı: fan çip sıçramasını etkileyemiyor (%60→%100 yalnız −3°C), bayt 18 tamsayı → türev gürültü üretir. Yerine τ 20–30 sn ortalama + histerezis |
| **Fan kombinasyon verimi haritası** | ✅ **ekle (test 14)** | CPU fanı %70 + GPU %40 vs tersi vs eşit — ölçmeden bilmiyoruz. `arama.py` altyapısı hazır |
| **Shadow mode (gölge mod)** | ✅ **ekle — Aşama 1'in ilk hali** | Algoritma hesaplar, günlüğe yazar, donanıma dokunmaz. Firmware otomatiği çalışırken "ben olsam %X verirdim" → risksiz doğrulama |
| **Önce kayıt, sonra kontrol** | ✅ **ekle** | Aşama 0: 1–2 gün CSV kaydı (sıcaklık, fan, frekans, yük, GPU, profil). Shadow mode ile birleşir |
| **Karar açıklaması** | ✅ **ekle** | Arayüzde tek satır: "Fan %45: CPU 82°C, +1,5°C/sn yükseliyor". Ucuz, güven verir |
| Otomatik deney laboratuvarı | ✅ **zaten planda** | = kalibrasyon sihirbazı ([07](07-ekstra-fikirler.md)); GPU yüklü varyant eklenecek |
| İş yükü sınıflandırma | ✅ **zaten planda** | = otomatik profil (07). Basit kural: GPU kullanımı / tam ekran / exe listesi |
| Fan arızası / soğutma verimi düşüşü | ✅ **zaten planda** | RPM nöbetçisi (03) + termal macun takibi (07) |
| Asimetrik fan tepkisi + histerezis | ✅ **zaten planda** | Artış %10/sn, azalış %2/sn (03) |
| **GPU yükünde CPU tavanını düşür** (ısı bütçesi) | ✅ **ekle, basit hali** | Oyunda (NVML GPU > %50) CPU frekans sınırı 3,2 GHz → ortak boruda GPU'ya pay kalır. CPU tavanı `PROCFREQMAX` ile mümkün; GPU güç sınırı laptopta ayarlanamıyor |
| Gereksiz turbo tespiti | ✅ **önceliği yükseldi** | Kalibrasyon: hafif yükte (2 iş parçacığı) turbo 4,0 GHz **31 W**, 2,9 GHz tavanıyla **19 W**. Profil başına sabit tavan çoğunu verir; dinamik tavan sonra (`powercfg` 10 sn'den sık yazılmaz) |
| Performans/watt optimizasyonu | ✅ **sürücüsüz tahminle** | CPU gücü sürücüsüz okunamıyor; PawnIO ile kalibre edilen model `P ≈ 5,37 + 1,68·f + 0,2652·u·f^3,8` (ort. hata %12,7) uygulamada "tahmini W" olarak kullanılır. Bkz. [02](02-olcumler.md). GPU gücü NVML'den gerçek |
| Termal hafıza / öğrenme | ⏳ **sonra** | Aşama 0 kaydı bunun verisini toplar; makine öğrenmesi gerekmez |
| Öngörülü kontrol (5–10 sn tahmin) | ❌ **gereksiz** | Eğim terimi + hızlı artış aynı işi çok daha basit görür |
| Çekirdek bazında frekans | ❌ **mümkün değil** | Windows'ta çekirdek başına frekans tavanı yok; 10. nesil HWP çekirdek ayarı MSR (sürücü) ister. Yapabildiğimiz: çekirdek park (yapıldı, çalışıyor) |
| PL1/PL2 doğrudan ayarı | ❌ **gerek yok** | MSR/MMIO yazımı (sürücü + risk). CC güç profili (0x79/0x19) güç sınırını zaten değiştiriyor (ölçüldü: 3,5 → 3,76 GHz) |
| Effective clock / IPC / C0 / kısma nedeni | ❌ **sürücüsüz yok** | MSR ister. Yerine: Windows "% Processor Performance" + bayt 18'in 90°C tavanı kısmayı gösteriyor |
| Undervolt | ❌ | 10. nesil Intel'de çoğu BIOS kilitli (Plundervolt). Risk/kazanç düşük |

## Mimariye yansıyanlar

- **Aşama 0 (yeni): Kayıt + gölge mod.** Tek exe, yalnız okur: CSV kaydı + algoritmanın "yapardım" kararları. 1–2 gün. Donanıma yazmaz.
- **Fan algoritmasına eğim terimi:** `hedef = eğri(T_ort) + k × max(0, dT/dt)` (ör. k = %5 / (°C/sn), en fazla +%25). Azalırken eğim kullanılmaz (yavaş iniş zaten var).
- **Oyun kuralı:** NVML GPU kullanımı > %50 iken CPU tavanı profilin bir kademe altı.
- **Arayüz:** pano altında "karar satırı".

## Yeni testler (05'e eklendi)

- **14 — Fan kombinasyon haritası:** 3,2 GHz tam yük; (CPU fan, GPU fan) ∈ {(50,50), (70,30), (30,70), (60,40), (40,60)} her biri 60 sn; CPU sıcaklığı + sesi kullanıcı değerlendirir. Ortak boruda hangi fanın daha etkili olduğunu söyler.
- **15 — (opsiyonel) Performans/watt:** PawnIO + LHM ile 2,9 / 3,2 / 3,4 / 3,6 GHz'te paket gücü + puan. Sonra PawnIO kaldırılır.
