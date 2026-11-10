# 07 — Ekstra fikirler (öncelik sırasıyla)

Hepsi isteğe bağlı. ⭐ = değeri yüksek / maliyeti düşük.

| Öncelik | Fikir | Nasıl | Not |
|---|---|---|---|
| ⭐⭐⭐ | **Otomatik profil** | Oyun (NVML GPU kullanımı > %50 veya exe listesi) → Maksimum; tam ekran video → Sessiz; 10 dk boşta → Serin | En çok fark yaratacak özellik |
| ⭐⭐⭐ | **Kalibrasyon sihirbazı** | `arama.py` mantığı uygulama içinde: "bu makine için en sessiz güvenli eğriyi bul" | Termal macun değişince yeniden çalıştırılır |
| ⭐⭐⭐ | **Sıcaklık rengi klavye** | Prototipte var (`sicaklik` efekti) | Sıcaklığı bakmadan görürsün |
| ⭐⭐ | **Geçmiş ve günlük** | Son 24 saat sıcaklık/fan/frekans CSV + grafik | Sorun ayıklama ve termal macun öncesi/sonrası kıyas |
| ⭐⭐ | **Komut satırı** | `mk profil sessiz`, `mk klavye renk ff8800` | Kısayol/otomasyon (ör. Stream Deck, görev zamanlayıcı) |
| ⭐⭐ | **Sessiz saatler** | 23:00–08:00 arası fan tavanı %30, klavye kapalı | |
| ⭐⭐ | **Webcam / touchpad anahtarı** | 0x06/0x22, 0x09/0x2A (test 11) | Gizlilik için webcam kapatma |
| ⭐ | **Müziğe göre klavye** | WASAPI loopback ses seviyesi → parlaklık/renk | Eğlenceli, CPU'yu az kullanır |
| ⭐ | **Ekran rengine göre klavye** | Ekran ortalama rengi (DXGI ekran yakalama, 10 fps) | Pil yok, enerji sorunu değil |
| ⭐ | **Termal macun takibi** | Aynı kalibrasyonu ayda bir kaydet, sıcaklık artarsa uyar | Fan %60 üstünün işe yaramaması macunu işaret edebilir |
| ❌ | Undervolt | 10. nesil Intel'de çoğu BIOS kilitli (Plundervolt yaması) | Araştırılmadı, risk/kazanç düşük |
| ❌ | GPU saat kilidi | `nvidia-smi -lgc` yönetici ister, laptop GPU'larında sınırlı | Gerekirse sonra |

## Bilinçli olarak yapılmayacaklar

- Fanı tamamen durduran "sıfır RPM" modu: bu makinede CPU 55°C'nin altına inmiyor.
- Firmware ayar sayfalarına sürekli yazma (flash aşınması).
- InsydeDCHU.dll'i uygulamayla birlikte dağıtmak: Insyde'nin dosyası; sistemdeki sürücü kopyasını kullanıyoruz.
