# 02 — Ölçümler ve profil önerileri

Koşullar: prizde, oda sıcaklığı ölçülmedi, arka planda video + disk/CPU işleri vardı (gürültü kaynağı). Yük testi = 8 iş parçacığı %100 (`prototip/yuk.py`, `olcum.py`, `arama.py`). Frekans sınırı = Windows `PROCFREQMAX` (MHz). Sıcaklık = DCHU bayt 18.

> ⚠️ **İkinci görüş uyarısı ([09](09-ikinci-gorus.md)):** Aşağıdaki sıcaklıklar **denge değeri değil**, 35–45 sn'lik adımlardan. Aynı güçte (2,9 GHz ~26 W, fan %60) iki tur 63,6°C ve 72–74°C verdi; fark kasanın ısınmışlığından. Sayılar soğuk kasa için iyimser uç. Profil sayıları için test 16 (10–15 dk/nokta) bekleniyor. Fan eğrisi kötü koşula göre tasarlanmalı.

## ⭐ Temiz ölçüm (2026-11-10 13:25, yeniden başlatma sonrası, arka plan yükü yok, CC Eğlence)

`prototip/temiz_olcum.ps1`. Adım 40 sn, son 15 sn ortalaması. Boşta (otomatik fan): **46,6°C** (43–60).

| Frekans sınırı | Fan | Sıcaklık ort/max | İş puanı |
|---|---|---|---|
| 2,9 GHz | %20 / %15 / **%10** | 64,8 / 68,2 / **71,8°C** | 23,3 |
| 3,2 GHz | %35 / %25 / **%20** | 76,5 / 78,5 / **80,6°C** | 25,7 |
| 3,4 GHz | %60 / %50 / **%40** | 83,0 / 84,0 / **85,9°C** | 27,3 |
| Sınırsız (~3,6 GHz) | %100 / %70 | 90,1 / 90,1°C | 28,2 |

- Temiz sistemde sıcaklıklar dünküden **~15°C düşük** (2,9 GHz @ %10: 88 → 72°C). Dünkü sayılar oyun sonrası ısınmış kasa + arka plan yükü altındaydı → gerçek hayatın kötü senaryosu olarak saklanıyor, aşağıda.
- Sınırsız + Eğlence: firmware **90°C'de tavan** yapıyor, frekansı ~3,6 GHz'e ayarlıyor; fan %70 ile %100 arasında fark yok (−50 MHz).
- Puan frekansla doğrusal (~8/GHz). 3,4 → 3,6 GHz: **+%3 performans**, +4–5°C ve çok daha fazla fan.
- Sınır: adımlar sırayla koştu, 2,9 GHz ilk sırada → soğuk kasadan bir miktar avantaj almış olabilir. Hiçbir adım ≥ 92°C'ye yaklaşmadı.

**Güncellenmiş profil önerisi (temiz koşul / kötü koşul):**

| Profil | Frekans | Fan | Temiz | Dünkü kötü koşul |
|---|---|---|---|---|
| 🌙 Sessiz | 2,9 GHz | %10–15 | 68–72°C | 85–88°C |
| ⚖️ Dengeli | 3,2 GHz | %20–25 | 79–81°C | ~87°C (%35 ile) |
| 🚀 Güçlü | 3,4 GHz | %40–50 | 84–86°C | 91°C (%50 ile) |
| Maksimum | sınırsız | %70 | 90°C (firmware tavanı) | 90°C+ |

→ Sabit fan değil **eğri** şart: aynı profil koşula göre 15°C oynuyor. Eğri, temiz koşulda sessiz kalır, kötü koşulda kendiliğinden hızlanır.

## ⭐ Performans / watt (2026-11-10 14:25, PawnIO + LHM, fanlar %60 sabit, CC Eğlence)

`prototip/perf_watt.ps1`, ham veri `prototip/perf_watt/`. Tam yük 45 sn, güç = LHM "CPU Package", yalnız yük ≥ %95 örnekleri.

| Frekans | Paket gücü | CPU °C ort | MHz / W | Güç artışı | Hız artışı |
|---|---|---|---|---|---|
| 2,9 GHz | **25,4 W** | 63,6 | **114** | — | — |
| 3,2 GHz | 30,7 W | 70,8 | 104 | +%21 | +%10 |
| 3,4 GHz | 35,9 W | 76,5 | 95 | +%41 | +%17 |
| 3,6 GHz | 42,4 W | 88,1 | 85 | +%67 | +%24 |
| Sınırsız | 42,1 W | 90,0 | 86 | ≈ 3,6 GHz | firmware ~42 W / 90°C'de tavan |

- **En verimli nokta 2,9 GHz.** 3,6 GHz, %24 hız için %67 fazla güç ve +25°C.
- Eğlence profilinde sınırlayan **sıcaklık (90°C)**, güç değil: aynı fanla bir tur 42 W, diğeri 38 W'ta 90°C'ye oturdu; kasa ısındıkça sürdürülebilir güç düşüyor.
- İş puanları bu turda gürültülü çıktı (3,4 GHz 25,9 < 3,2 GHz 26,0; LHM örneklemesi de CPU kullanıyordu). Saf hesaplama yükünde performans ≈ frekans orantılı kabul edildi (sabahki temiz puanlar bunu destekliyor: 23,3 / 25,7 / 27,3 / 28,2).
- 2,9 GHz testinin ilk denemesi (takılan betik) 20,3 puan vermişti; geçersiz sayıldı.

## ⭐ Sürücüsüz güç tahmin modeli (2026-11-10 15:00, kalibrasyon)

`prototip/kalibrasyon.ps1` (Dengeli plan, fan %60, LHM+PawnIO), model: `prototip/guc_modeli.py`, veri `prototip/perf_watt/kalibrasyon.csv` (15 adım × ~13 örnek).

**Model (M4):** `P ≈ 5,37 + 1,68·f + 0,2652·u·f^3,8` — f = ortalama çekirdek frekansı (GHz, PDH'den), u = toplam yük (0–1). Sürücü gerektirmez.

| Hata | Değer |
|---|---|
| Örnek başına ortalama | 2,15 W (%12,7) |
| Adım ortalamasında | çoğu ≤ 2,5 W; en kötü: boşta 7,4 W ölçülen ↔ 11,0 W tahmin, turbo 2 iş parçacığı 31,1 ↔ 28,1 W |

→ Arayüzde **"~28 W (tahmini)"** olarak gösterilir; karar mantığında kaba kıyas için yeterli, kesin değer değil.

**Ölçülen adımlar (Dengeli plan):**

| Sınır | İş parçacığı | Yük | MHz | Güç |
|---|---|---|---|---|
| Sınırsız | 0 (boşta) | %11 | 2660 | 7,4 W |
| Sınırsız | 2 | %31 | **4000** | **31,1 W** |
| Sınırsız | 4 | %62 | 3787 | 40,8 W |
| Sınırsız | 8 | %99 | 3450 | 38,3 W (güç tavanı) |
| 2,9 GHz | 2 | %51 | 2900 | **19,0 W** |
| 2,9 GHz | 8 | %99 | 2900 | 26,1 W |
| 3,4 GHz | 2 | %46 | 3400 | 24,4 W |
| 3,4 GHz | 8 | %100 | 3400 | 36,3 W |

**Önemli bulgu — "gereksiz turbo":** 2 iş parçacıklı hafif yükte turbo 4,0 GHz'e çıkıp **31 W** yakıyor; 2,9 GHz sınırıyla aynı yük **19 W**. ⚠️ Düzeltme (09): meşgul döngü sonsuz iş olduğu için 4,0 GHz'te %38 daha çok iş yapılıyor; **sabit iş için gerçek kazanç ~%16–32**. Asıl kazanç **sessizlik**: 2 iş parçacıklı turbo, fan %60'ta **82–89°C** — 8 iş parçacıklı 3,4 GHz'ten (36 W) daha sıcak. Hafif yükte fanı bağırtan şey turbo sıcak noktası; frekans tavanı bunu önlüyor.

**Güç modeli için not (09):** Model karar mantığına bilgi katmıyor (yalnız u ve f'nin fonksiyonu) ve yük terimi doğrusal, veri içbükey. Uygulamada formül yerine **bu tablodan ara değer** ("~W", yalnız gösterim) ya da hiç gösterme.

## Dünkü ölçüm (ısınmış kasa + arka plan yükü): frekans × en düşük fan

Hedef: tam yükte **ortalama ≤ 92°C, en yüksek ≤ 95°C** (son 15 sn penceresi, adım başına 35–40 sn).

| Frekans sınırı | CC güç modu | En düşük güvenli fan | Sıcaklık ort/max | Not |
|---|---|---|---|---|
| 2,9 GHz | Eğlence | **%10** | 87,9 / 88°C | %15 → 85°C, %20 → 83°C, %25 → 81°C, %30 → 79°C. Kullanıcı: "%20 altı inanılmaz sessiz" |
| 3,2 GHz | Performans | **%35** (denenen en düşük) | 86,7 / 87°C | |
| 3,4 GHz | Performans | **%50** | 91 / 91°C | %40 → 93/94°C kaldı |
| 3,6 GHz | Performans | yok (%100'de ort 93) | 93 / 94°C | Kullanıcının 95 sınırına sığar, çok gürültülü |
| 3,8 GHz | Performans | **imkânsız** | %100 fanla 98°C | |
| Serbest | Performans | — | 3 sn'de 98°C | Tam yükte ~3,76–3,88 GHz |
| Serbest | Eğlence | Max fanla | 90°C sabit | Tam yükte ~3,5 GHz, firmware 90°C'de tutuyor |

### Fan verimi (3,4 GHz, tam yük)

| Fan | 100 | 90 | 80 | 70 | 60 | 50 | 40 |
|---|---|---|---|---|---|---|---|
| °C ort | 86,3 | 88,0 | 88,2 | 88,3 | 89,3 | 91,0 | 92,9 ❌ |

→ **%60 üstü neredeyse boşa gürültü** (%60→%100 sadece −3°C). Darboğaz fan değil, işlemciden soğutucuya ısı geçişi. Termal macun yenilemek gerçek kazanç getirebilir (tahmin).

### İş puanı (milyon döngü/sn, aynı koşul)

| 3,0 GHz | 3,4 GHz | 
|---|---|
| 23,2 | 26,4 (+%14) |

Diğer turların puanları arka plan yükü yüzünden tutarsızdı; kullanma.

## Windows güç planları (oluşturuldu, makinede duruyor)

| Plan | GUID | Ayarlar | Sonuç |
|---|---|---|---|
| Dengeli (varsayılan) | `381b4222-f694-41f0-9685-ff5bb260df2e` | — | Video + arka plan: ~79°C |
| Serin | `24841f8c-4d4f-4527-a29b-a6d1a1553b03` | PERFBOOSTMODE=0, PROCTHROTTLEMAX=50, CPMAXCORES=50, **CPMINCORES=25** | 52,5°C, 1,2 GHz, 4/8 iş parçacığı park, video sorunsuz |
| Performans | `cfda36e9-41fd-4b93-b905-94299a13d052` | PERFBOOSTMODE=2, PROCTHROTTLEMIN=100, PROCTHROTTLEMAX=100, CPMIN/MAXCORES=100, PERFEPP=0 | Boşta bile ısıtır (Max fanla 90°C) |

Dersler:
- Çekirdek park etmek için **CPMINCORES ≤ CPMAXCORES** olmalı; aksi halde CPMINCORES kazanır (ilk denemede park olmadı).
- Turbo/park/frekans ayarları **gizli**: `powercfg /qh` ile gör, `/q` göstermez.
- Ayar değişince `powercfg /setactive <guid>` ile yeniden uygula.
- Yönetici izni gerekmedi.
- Görev Yöneticisi park edilen çekirdeği de çizer; doğrulama için `Processor Information` sayacındaki `ParkingStatus` veya `resmon`.

## Firmware otomatik fan davranışı (önemli)

- Otomatik modda tabanda iki fan **%35 (~2650 / ~2280 RPM)**.
- **CC Performans + otomatik fan + 91–94°C tam yükte 60 sn: fan %35'te kaldı, hiç hızlanmadı.** Eğlence modunda boşta 82°C'de de %35'ti.
- CPU < ~60°C iken GPU fanını kendisi kapatıyor (0 RPM).
- Sonuç: **kendi fan eğrimiz konfor değil, gereklilik.**

## Önerilen hazır profiller (C# uygulaması için başlangıç)

| Profil | CC güç | Windows | Frekans sınırı | Fan | Beklenen tam yük |
|---|---|---|---|---|---|
| 🌙 Sessiz | Eğlence | özel plan | 2,9 GHz | eğri, taban %15–20 | ~83–85°C, neredeyse sessiz |
| ⚖️ Dengeli | Eğlence | özel plan | 3,4 GHz | eğri, taban %25, tam yükte %50–60 | ~89–91°C |
| 🚀 Maksimum | Performans | özel plan | yok | eğri agresif, tam yükte %100 | 3,6+ GHz, 93–98°C → **frekansı 3,6'da sınırla** |
| ❄️ Serin (video) | Sessiz | Serin planı | %50 + yarım çekirdek | taban | ~52°C |

Bu sayılar 35–40 sn'lik adımlara dayanıyor; saatlerce süren yükte 1–3°C daha yüksek olabilir → koruma her zaman açık.
