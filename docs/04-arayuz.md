# 04 — Arayüz tasarımı

Hedef: Monster'ın neon/ızgaralı görünümü yerine **sade, Windows 11'e uyan, koyu temalı** bir uygulama. Kullanıcı "arayüzün güzel olmasını istiyorum" dedi → görsel kalite birinci sınıf gereksinim.

## Görsel dil

- **WPF UI `FluentWindow`**, Mica arka plan, sistem temasını takip eder (koyu varsayılan).
- **Vurgu rengi = klavye rengi.** Klavye turuncuysa uygulamanın vurgusu da turuncu. Gökkuşağı efektinde vurgu yavaşça döner (ayar: kapatılabilir).
- Yazı: Segoe UI Variable. İkonlar: Fluent System Icons (WPF UI ile gelir).
- Sayılar büyük ve okunur: sıcaklık 32 pt, birim küçük.
- Renk kodlu sıcaklık: < 70 nötr, 70–85 sarı, 85–92 turuncu, ≥ 92 kırmızı.
- Animasyonlar kısa (150–250 ms), göz yormayan.
- Pencere en küçük 900×600, yeniden boyutlanabilir.

## Sayfalar (sol gezinti çubuğu)

### 1. Ana sayfa (pano)

```
┌──────────────────────────────────────────────────────────────┐
│  [🌙 Sessiz] [⚖️ Dengeli] [🚀 Maksimum] [❄️ Serin]   ← profil  │
├───────────────┬───────────────┬───────────────┬──────────────┤
│  CPU          │  GPU          │  CPU fanı     │  GPU fanı    │
│   83°C        │   57°C        │   1545 RPM    │   1466 RPM   │
│  2,9 GHz      │  300 MHz      │   %20         │   %20        │
├───────────────┴───────────────┴───────────────┴──────────────┤
│  Son 5 dakika: sıcaklık + fan çizgi grafiği (kayan)           │
│  ───────────────/\────────/──────────                         │
├──────────────────────────────────────────────────────────────┤
│  Klavye: ● Ateş efekti   Güç: Eğlence   Plan: MK Sessiz       │
└──────────────────────────────────────────────────────────────┘
```

### 2. Fan

- Mod seçici: `Eğri` · `Sabit` · `Max` · `Firmware otomatik`.
- **Eğri düzenleyici:** x = sıcaklık (40–100°C), y = fan % (0–100). Sürüklenebilir 4–6 nokta. Canlı nokta: şu anki sıcaklık → şu anki duty. Kırmızı bölge 92°C+ (koruma, düzenlenemez).
- CPU ve GPU fanı için ayrı eğri; "GPU fanı CPU'yu izlesin (%80)" anahtarı varsayılan açık.
- Taban (%), yükselme/düşme hızı (gelişmiş, katlanır).
- Sabit modda: iki kaydırıcı, kilit simgesiyle bağlanabilir.
- Uyarı bandı: sabit ve %25'in altı seçilirse "Koruma açık: 92°C'de fanlar tam hıza çıkar".

Eğri kontrolü için hazır grafik kütüphanesi yerine **özel `Canvas` kontrolü** öneriyorum: 6 nokta + çizgi + canlı işaret basit, tamamen tema uyumlu, bağımlılık yok. Pano grafiği için de aynı hafif çizim (son 300 örnek).

### 3. Klavye

- Açık/kapalı anahtarı, parlaklık kaydırıcısı (0–100 → 0–255).
- Renk: renk tekerleği + 8 hazır renk + HEX girişi. Değişiklik **anında** klavyeye gider.
- **Efekt galerisi:** kartlar (nefes, gökkuşağı, renkli nefes, kalp, polis, ateş, okyanus, gün batımı, flaş, sıcaklık). Her kartta küçük canlı önizleme (kart üzerinde efektin renk animasyonu). Seçili efekt için: hız ve renk ayarı.
- Uyku zamanlayıcısı (CC'deki gibi): N dakika hareketsizlikte ışığı kapat.
- Flaş efekti için ışığa duyarlılık uyarısı.

### 4. Performans

- CC güç modu: 4 kart (Sessiz / Tasarruf / Eğlence / Performans), açıklamalarıyla.
- CPU frekans sınırı kaydırıcısı (serbest, 2,4–4,0 GHz). Sabit sıcaklık ipucu **yok** — aynı ayar koşula göre 15–25°C oynuyor (09); yerine canlı ölçüm ve son 10 dakikanın en yüksek değeri.
- Turbo açık/kapalı, çekirdek park (%).
- "Ölçüm yap" düğmesi: `arama.py` mantığını uygulama içinde çalıştırır, sonucu tablo olarak gösterir (Aşama 4).

### 5. Profiller

- Profil = {CC güç, frekans sınırı, turbo, fan eğrisi/modu, klavye ayarı}.
- Hazır 4 profil (bkz. 02) + kullanıcı profilleri. Kopyala / düzenle / sil / dışa aktar.
- Profil için kısayol tuşu (ör. Ctrl+Alt+1..4).

### 6. Ayarlar

- Windows ile başlat, tepsiye küçült, tema, vurgu = klavye rengi.
- Koruma eşikleri (en fazla 95°C'ye kadar yükseltilebilir, kilitli varsayılan 92).
- Günlükler, "Firmware otomatiğe dön" acil düğmesi, sürüm bilgisi.

## Tepsi simgesi

- Simge içinde **CPU sıcaklığı rakamla** (ör. "83"), renk sıcaklığa göre.
- Sol tık: küçük açılır panel (profil düğmeleri + 4 sayı). Sağ tık: menü (profiller, klavye aç/kapa, fan max, uygulamayı aç, çıkış).
- Profil değişince küçük bildirim (Windows toast) — kapatılabilir.

## Erişilebilirlik / kalite

- Tüm kontroller klavyeyle gezilebilir, ekran okuyucu adları Türkçe.
- Renk tek bilgi taşıyıcısı değil (sıcaklıkta sayı + renk).
- Yüksek DPI ve %150 ölçek test edilmeli (laptop ekranı).
