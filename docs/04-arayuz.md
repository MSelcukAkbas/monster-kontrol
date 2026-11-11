# 04 — Arayüz tasarımı

Hedef: Monster'ın neon/ızgaralı görünümü yerine **sade, Windows 11'e uyan, koyu temalı** iki uygulama. Kullanıcı "arayüzün güzel olmasını istiyorum" dedi → görsel kalite birinci sınıf gereksinim. Kullanıcının ana isteği: **"ne kadar düzenleyebilirsek o kadar iyi, ama aşırı karışık olmasın."**

İki bağımsız uygulama: **Monster Klavye** (yapıldı, K0–K4) ve **Monster Performans** (bu belgedeki ana tasarım). Performans'ın kendi uygulama simgesi ve tepsi simgesi var; klavye sayfası yok.

## Görsel dil (iki uygulamada ortak)

- **WPF UI `FluentWindow`**, Mica arka plan, sistem temasını takip eder (koyu varsayılan).
- Yazı: Segoe UI Variable. İkonlar: Fluent System Icons (WPF UI ile gelir).
- Sayılar büyük ve okunur: sıcaklık 32 pt, birim küçük.
- Renk kodlu sıcaklık: < 70 nötr, 70–85 sarı, 85–92 turuncu, ≥ 92 kırmızı.
- Animasyonlar kısa (150–250 ms), göz yormayan.
- Pencere en küçük 900×600, yeniden boyutlanabilir.

## Monster Klavye

Uygulandı (K0–K4): ışık sayfası (aç/kapa, parlaklık, renk), efekt galerisi, tepsi + hızlı panel, Windows ile başlat. Vurgu rengi = klavye rengi.

## Monster Performans

### Profiller (temel kavram)

- **Hazır 3 profil: Sessiz / Dengeli / Performans — kilitli.** Ayarları görünür ama gri; değiştirilemez.
- **Kullanıcı profilleri: sınırsız.** Yeni profil herhangi bir profilden (hazır ya da kendi) **kopyalanarak** başlar, sonra düzenlenir.
- Her profilde **"Tepside göster"** seçeneği. Tepside en fazla **bir** kullanıcı profili durur; yenisi işaretlenince eskisinin yerine geçer.

**Profil ayarları (5):**

| Ayar | Değerler |
|---|---|
| Güç seviyesi (CC, `0x79/0x19`) | Tasarruf / Sessiz / Eğlence / Performans |
| Windows güç planı | sistemdeki planlar |
| CPU frekans sınırı | % (en yüksek 4,1 GHz'in yüzdesi; yanında GHz karşılığı) |
| Turbo | açık / kapalı |
| Fan modu | **Otomatik / Maksimum / Özel eğri** (CPU ve GPU fanı için ayrı eğri) |

Diğer firmware modları (MaxQ, Sessiz, IFSC, Noiseless) bu modelde desteklenmiyor ([12](12-performans-arka-plan.md) §5) → listede yok.

**"Ölçüm yap" düğmesi (profil sayfasında):** profili geçici uygular, CPU + GPU'yu birlikte yükler (30 sn / 60 sn / 3 dk seçilir). Sıcaklık, güç, frekans ve fan değerlerini (GPU sütunları dahil) kaydeder. CPU 95°C'de durur. Bitince eski duruma döner. Sonucu saklar ve geçmiş ölçümlerle kıyaslayan tek satırlık özet gösterir.

### Sayfalar (sol gezinti çubuğu)

#### 1. Pano

```
┌──────────────────────────────────────────────────────────────┐
│  [Sessiz] [Dengeli] [Performans] [Profilim…]   ← tüm profiller│
├───────────────┬───────────────┬───────────────┬──────────────┤
│  CPU          │  GPU          │  CPU fanı     │  GPU fanı    │
│   83°C        │   57°C        │   2650 RPM    │   2250 RPM   │
│  2,9 GHz 28 W │  300 MHz 9 W  │   %35         │   %35        │
├───────────────┴───────────────┴───────────────┴──────────────┤
│  Grafik: CPU °C, GPU °C, fan %   [1 dk] [5 dk] [30 dk]        │
│  ───────────────/\────────/──────────                         │
├──────────────────────────────────────────────────────────────┤
│  Durum: Dengeli · Eğlence · Dengeli planı · eğri doğrulandı ✓ │
└──────────────────────────────────────────────────────────────┘
```

- GPU sıcaklığı **NVML'den** okunur; EC'nin GPU sıcaklığı dGPU çekirdeği değil ([12](12-performans-arka-plan.md) §6).

#### 2. Fan (eğri düzenleyici)

- EC eğrisi 4 noktalı. **1. nokta (40°C %35) ve 4. nokta (100°C %100) sabit**; yalnız 2. ve 3. nokta sürüklenir.
- Kurallar: t2 < t3, fan % azalmaz. Yanında komut 14 baytları ve eğimler görünür.
- **Hazır eğriler:** Sessiz / Dengeli / Serin. Seçince noktalar yerleşir, sonra elle ince ayar. Sessiz eğri boşta otomatikten sessiz olamaz (taban %35 sabit); fanın geç ve yavaş yükselmesini sağlar.
- "Uygula" düğmesi + durum satırı: tampon 13 geri okunur, "eğri doğrulandı" ya da hata.
- Koruma bandı ≥ 92°C (kırmızı, düzenlenemez).
- Kilitli profilde eğri yalnız görüntülenir.
- Eğri çizimi için özel `Canvas` kontrolü (bağımlılık yok); pano grafiği de aynı hafif çizim.

#### 3. Güç / Profil düzenleyici

- Profil listesi (hazır + kullanıcı), "Kopyala", "Sil", "Tepside göster".
- Seçili profilin 5 ayarı ve "Ölçüm yap".

#### 4. Ayarlar

- Windows ile başlat.
- Açılışta ve uykudan dönüşte profili yeniden uygula (kapı B: özel eğri yeniden başlatmada sıfırlanıyor).
- Acil eşik: 92°C kilitli varsayılan, en fazla 95.
- Eğri doğrulaması başarısız olursa ne yapılacağı.
- "Güvenli duruma dön" düğmesi (fan mod 0 + `0x69`, profil eğlence, Dengeli plan).
- Günlük klasörü.

### Tepsi

- Simgede **CPU sıcaklığı rakamla** (ör. "83"), renk sıcaklığa göre.
- **Sol tık:** küçük panel → CPU °C, GPU °C, Sessiz / Dengeli / Performans düğmeleri, varsa tek kullanıcı profili düğmesi (yoksa hiç yer kaplamaz).
- **Sağ tık:** Aç / Çıkış.

### Açık kalanlar

- Performans'ın vurgu rengi (sistem vurgusu varsayılan), profil değişiminde bildirim.

## Erişilebilirlik / kalite

- Tüm kontroller klavyeyle gezilebilir, ekran okuyucu adları Türkçe.
- Renk tek bilgi taşıyıcısı değil (sıcaklıkta sayı + renk).
- Yüksek DPI ve %150 ölçek test edilmeli (laptop ekranı).
