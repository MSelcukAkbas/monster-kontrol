# 10 — Veri envanteri ve öğrenen model

2026-11-10 15:40. Kullanıcı sordu: "Sürekli öğrenen, tüm sistem verilerine ağırlık verip performansı en üste çıkaran bir model?" ve "işlemciyle ilgili ne kadar veri toplayabiliyoruz?". ChatGPT'nin ikinci cevabı da değerlendirildi.

> ⚠️ İşlemci **i5-10200H, Comet Lake** (Skylake ailesi, 10. nesil) — sistemden okundu. ChatGPT iki kez "i5-11200H / Tiger Lake" yazdı; MSR ve PMU olayları nesile göre değiştiği için önerilerindeki adresler ancak Comet Lake'e göre doğrulanarak kullanılır.

## 1. Veri envanteri (✅ bu makinede okundu, ❓ okunamadı/doğrulanmadı)

### A) Yönetici gerekmez (~300+ sinyal)

| Kaynak | Veriler | Durum |
|---|---|---|
| DCHU tampon 12 | CPU °C (bayt 18), kart/GPU sensörü (17), 2 fan RPM + duty, etkin profil (30), bilinmeyen 10–13 / 21 | ✅ |
| DCHU tamsayı | BIOS özellik bayrakları, webcam/touchpad/uçak modu, FANINFO | ✅ |
| CC ayar sayfaları | fan modu/eğri, klavye, güç modu kaydı | ✅ (yedek: appsettings-yedek.json) |
| **İşlemci Bilgileri** (Türkçe sayaç seti, 8 mantıksal işlemci × 29) | **Gerçek Frekans** (APERF/MPERF tabanlı, çekirdek başına), İşlemci Performansı %, % İşlemci Zamanı (≈ C0), **% C1/C2/C3 süresi + geçiş/sn**, **Performans Sınırı % / Bayrakları**, Park Durumu, kesme/DPC, ortalama boşta kalma süresi, İşlemci Kullanılabilirliği | ✅ (Gerçek Frekans 3710 MHz okundu) |
| **Sistem** (17) | **İşlemci Sırası Uzunluğu** (karşılanmayan talep), bağlam geçişi/sn, sistem çağrısı/sn, iş parçacığı/işlem sayısı, dosya G/Ç | ✅ |
| ACPI termal bölge | TZ0 (= bayt 18 + 0,1), kısma nedenleri | ✅ |
| **Güç Ölçer** (Power Meter) | Güç, Güç Bütçesi | ❌ **hep 0** (pil yok) |
| NVML | GPU °C, **W**, çekirdek/bellek MHz, kullanım, P-state, kısma nedenleri, VRAM | ✅ (nvidia-smi ile) |
| İşlem düzeyi | süreç başına CPU, ön plan penceresi, tam ekran | — (standart API) |
| Ayar durumu | Windows planı, PROCFREQMAX, EPP, turbo modu, CC profili | ✅ |

> Not: Windows Türkçe olduğu için PDH sayaç **adları Türkçe** (`\İşlemci Bilgileri(_Total)\Gerçek Frekans`). Uygulamada `PdhAddEnglishCounter` kullan (İngilizce adla dile bağımsız).

### B) Yönetici + PawnIO (LibreHardwareMonitorLib, `IntelMsr.ReadMsr`) — salt okuma

| MSR | Değer (bu makine) | Durum |
|---|---|---|
| RAPL paket/çekirdek/bellek/platform gücü | 7–42 W ölçüldü | ✅ |
| Çekirdek başına °C, TjMax'a uzaklık, VID, frekans | | ✅ |
| `0x606` RAPL birimleri | güç 1/8 W, zaman 1/1024 s | ✅ |
| `0xCE` taban oran | 24 → 2400 MHz | ✅ |
| `0x1AD` turbo oranları 1/2/3/4 çekirdek | **41/41/40/40** (×100 MHz) | ✅ |
| `0x1A2` TjMax / TCC ofseti | **TjMax 100°C, ofset 10 → etkin sınır 90°C** — 90°C platosunun sebebi işlemcinin kendi TCC'si | ✅ |
| `0x610` PL1/PL2 | aşağıdaki tablo; **kilit = 0** (yazılabilir — biz yazmıyoruz) | ✅ |
| `0x601` VR akım sınırı | 0 (kullanılmıyor) | ✅ |
| `0x1B1` paket termal durum | geçmiş biti 1; yükte "sınıra 9–13°C" | ✅ |
| `0x64F` kısma nedenleri + geçmiş | **hep 0**, 90°C tam yükte bile | ❓ büyük olasılıkla PawnIO modülü izin vermiyor (beyaz liste) |

**Güç profillerinin gerçek etkisi (0x610) — test 18 çözüldü:**

| CC profili | PL1 | PL2 | Pencere |
|---|---|---|---|
| Sessiz (0) | **15 W** | 30 W | 8 sn |
| Tasarruf (1) | **30 W** | 30 W | 8 sn |
| Eğlence (3) | **45 W** | 107 W | 28 sn |
| Performans (2) | **60 W** | 135 W | 48 sn |

→ Dört profil gerçekten farklı. **"Sessiz" (PL1 15 W) en güçlü soğutma kaldıracı** → acil durumda profili indirmek geçerli (09'daki itiraz kapandı). Eğlence'de sürekli tavanı 45 W'ın altında tutan şey TCC 90°C.

### C) Yönetici + ETW donanım sayaçları (PawnIO gerekmez)

`wpr -pmcsources` bu makinede listeliyor (VBS çalışırken bile): `InstructionRetired`, `UnhaltedCoreCycles` (→ **IPC**), `UnhaltedReferenceCycles`, `LLCReference` / `LLCMisses`, `BranchMispredictions`, `BranchInstructions`, `CacheMisses`, sabit sayaçlar.

Deneme (`prototip/pmc.wprp`, `yuk_msr_pmc.ps1`): 10 sn kayıt, `PmcCtrConfig` ✅, 377 bin CSwitch olayı ✅, **8 `PmcCtrConrruption` uyarısı** ⚠️ (sayaçları başka biri de kullandı: Hyper-V/VBS ya da aynı anda çalışan LHM). **Sayaç değerleri henüz çözülmedi** — `tracerpt` ek veriyi göstermiyor; C# tarafında Microsoft **TraceEvent** kütüphanesiyle çözülecek (ETL `prototip/perf_watt/pmc.etl` saklandı). Doğrulanana kadar "IPC okunabiliyor" denmez.

### D) Okunamayanlar

- Top-down (TMA) analizi: Windows'un sabit sayaç listesinde yok.
- Ring/önbellek frekansı, bellek denetleyicisi yükü: uncore sayaçları; değer/zahmet oranı düşük.
- Ortam sıcaklığı: sensör yok → ölçümlere kullanıcı not eder; uzun boşta kalma sonrası EC değeri kaba vekil.

## 2. Örnekleme planı

| Veri | Aralık | Not |
|---|---|---|
| DCHU (tampon 12) | **1 sn** | bir okuma ~18 ms, tek iş parçacığı |
| PDH (frekans, yük, C-state, sınır, sıra) | 1 sn | |
| NVML | 1 sn | |
| PawnIO MSR (varsa) | 1–2 sn | |
| ETW PMC | sürekli oturum, 1 sn'de toplanır | yalnız teşhis modunda (ek yük) |

ChatGPT'nin 100–250 ms önerisi reddedildi: ısıl zaman sabiti saniyeler-dakikalar; hızlı örnekleme ölçüm sistemini yük haline getirir. Kısa teşhis oturumlarında 250 ms açılabilir. **Ham veri her zaman kaydedilir** (sıkıştırılmış günlük CSV/Parquet), modele seçilmiş özellikler girer.

## 3. Öğrenen model tasarımı

**İlke:** "Performansı en üste çıkar" tek başına anlamsız (cevap: her şey maksimum). Hedef bir **denge puanı**:

```
puan = iş − a·gürültü − b·max(0, T − T_hedef) − c·(kısma süresi)
```

- **İş vekilleri:** Gerçek Frekans × kullanım; İşlemci Sırası Uzunluğu (talep karşılanıyor mu); oyunda **FPS** (PresentMon/ETW — doğrulanacak); IPC (ETW çözülünce).
- **Gürültü vekili:** fan RPM (test 21'deki dB↔RPM tablosuyla).

**Katmanlar (sırayla):**

1. **Kayıt (Aşama 0):** tüm A + (varsa) B, C verisi 1 Hz, ham.
2. **Öğrenen ısı modeli (sistem tanımlama):** iki düğümlü RC modeli
   `dT_cpu/dt = α·P_cpu + κ·(T_gpu − T_cpu) − β·g(fan)·(T_cpu − T_ortam)` (GPU için simetrik).
   P_cpu: RAPL (PawnIO) ya da tahmin; P_gpu: NVML. Parametreler **özyinelemeli en küçük kareler** ile her saniye güncellenir → kasanın ısınmışlığını kendiliğinden öğrenir (bugünkü 10–15°C'lik "aynı ayar farklı sonuç" sorununun cevabı). Kullanım: "bu ayarla 5 dk sonra kaç °C'de dengelenir" tahmini → hedefi aşmayan en yüksek frekans / en düşük fan.
3. **Ayar keşfi (isteğe bağlı):** iş yükü sınıfı başına küçük bir ayar kümesi (PROCFREQMAX, EPP, profil, fan tabanı) üzerinde bağlamsal bandit; güvenlik sınırları (acil durum, taban) **öğrenmenin dışında**.

**Bilinçli sınırlar:** derin öğrenme yok (veri az, açıklanamaz, gereksiz); 300 sinyal modele girmez — birkaç günlük kayıttan sonra 10–15 özellik seçilir (aday: Gerçek Frekans, kullanım, sıra uzunluğu, C0/C-state, P_cpu, P_gpu, T_cpu, T_gpu, fan RPM'leri, profil, PROCFREQMAX, kısma bayrakları).

## 3b. ⭐ Kendini geliştiren sistem: çevrimiçi optimizasyon (kullanıcı kararı, 2026-11-10 15:55)

**Karar:** Kullanıcıdan geri bildirim **istenmez** (oyundan çıkıp geri bildirim verene kadar durum geçmiş olur). Sistem yalnız ölçülen metriklerden öğrenir ve **gerçek kullanım sırasında sürekli küçük denemeler** yapar (NVIDIA otomatik ayar mantığına benzer, ama kendi test yükü yerine gerçek kullanımdan). Kısa testlerin "ısınmamış kasa" sorunu gerçek uzun kullanım verisiyle kendiliğinden çözülür.

**Döngü (performans uygulamasında, fan bekçisinden sonra devreye girer):**

1. **Bağlam:** yük sınıfı (boşta / hafif / orta / ağır / oyun = NVML GPU etkin) + ön plandaki program.
2. **Ayar seçimi:** çoğunlukla bu bağlamın bilinen en iyi ayarı; arada bir **komşu kademe** (±1 adım) denemesi (ε-açgözlü / tepe tırmanma; deneme oranı zamanla azalır).
3. **Ölçüm penceresi (2–5 dk, bağlam değişmediyse):** iş = Σ(Gerçek Frekans × kullanım); güç = RAPL (PawnIO) — yoksa tahmin; °C; fan RPM; **"yetişemiyor" sinyali** = İşlemci Sırası Uzunluğu yüksek ya da kullanım ≥ %95 oranı.
4. **Puan:** `iş / W`; ceza: sıcaklık hedefi aşımı, fan ses bütçesi aşımı, yetişemiyor oranının artması (= gerçek performans kaybı).
5. **Kayıt:** bağlam × ayar tablosu (koşan ortalama, örnek sayısı), **unutma faktörü** (yaz/kış, macun kuruması); ani değişim → uyarı (soğutma sağlığı).

**Neden işe yarar:** İşlemci yetişiyorsa (tavanı düşürünce iş yine zamanında bitiyorsa) düşük tavan **bedava kazanç**tır — ölçüm: hafif yükte turbo 4,0 GHz 31 W, 2,9 GHz 19 W. Gerçekten güç isteyen yerde "yetişemiyor" sinyali tavanı otomatik yükseltir.

**Kapsam (aşamalı):** önce **tek düğme: frekans tavanı** (en büyük etki, net nedensellik). Oturunca: CC profili (Sessiz/Eğlence), EPP, fan eğrisi kaydırması.

**Güvenlik:** yalnız güvenli aralıkta (frekans tavanı, profil); **voltaj/overclock asla**. Acil durum, fan tabanı, sıcaklık sınırları öğrenmenin dışında. "Yetişemiyor" sinyali gelirse deneme anında kesilir. Yeni en iyi ayar ancak yeterli örnekle (ör. ≥ 5 pencere) kabul edilir.

**Gereksinim:** verimlilik için gerçek watt → **PawnIO kurulu kalsın** (performans uygulamasının fan bekçisi zaten yönetici; tahmin modeli %13 hatalı).

**Veri saklama:** seçilmiş ~60 sinyal 1 Hz ≈ 10–30 MB/gün (tahmin); ham veri 30 gün, sonrası dakikalık özet, süresiz.

Literatür (özet düzeyinde incelendi): kullanıcı geri bildirimli DVFS — [Northwestern UDFS](https://users.cs.northwestern.edu/~pdinda/Papers/NWU-EECS-06-11.pdf); pekiştirmeli öğrenmeyle DVFS + çekirdek + fan (Iranfar vd., [2025 makalesinde özet](https://arxiv.org/html/2409.14178v2)). Fan sesini hedef alan çalışma bulunamadı.

## 4. ChatGPT ikinci tur — karar özeti

| Öneri | Karar |
|---|---|
| APERF/MPERF | ✅ zaten var: PDH "Gerçek Frekans" (sürücüsüz); ETW `UnhaltedReferenceCycles` ile bağımsız doğrulama |
| C0 süresi | ✅ PDH (% İşlemci Zamanı, C1/C2/C3); paket C-state'leri MSR ister |
| Kısma nedenleri + geçmiş (0x64F) | ❓ PawnIO ile okunuyor gibi ama hep 0 — güvenilmez; PDH "Performans Sınırı Bayrakları" yük altında denenecek |
| PL1/PL2/zaman penceresi | ✅ **okundu**, profil tablosu yukarıda |
| IPC / LLC / dal tahmini | ✅ kaynaklar mevcut (ETW), ⏳ değer çözümü TraceEvent ile |
| TMA, ring frekansı, bellek denetleyicisi, VR alarmı | ❌/⏳ düşük öncelik |
| CPU↔GPU sistem tanımlama | ✅ öğrenen ısı modelinin iki düğümlü hali (test 14, 17 verisi gerekir) |
| 100–250 ms örnekleme | ❌ 1 sn yeterli; teşhis modunda hızlı |
| Ham veriyi kaybetme | ✅ |

## 5. Yeni testler

- **23 — PDH kısma bayrakları yük altında:** "Performans Sınırı Bayrakları/Yüzdesi" 90°C'de ne gösteriyor (0x64F'nin sürücüsüz yedeği).
- **24 — ETW PMC çözümü:** TraceEvent ile `pmc.etl`'den IPC/LLC; `PmcCtrConrruption` LHM kapalıyken de çıkıyor mu.
- **25 — PresentMon FPS:** yönetici olmadan / ile oyun FPS'i okunabiliyor mu.
