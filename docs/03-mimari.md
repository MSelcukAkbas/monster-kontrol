# 03 — C# uygulama mimarisi

## ⭐ Güncel karar (2026-11-10 16:00, kullanıcı): iki ayrı uygulama

| Uygulama | Görev | Yönetici | Bağımlılık |
|---|---|---|---|
| **Monster Klavye** (`MonsterKlavye.exe`) | Klavye ışığı: renk, parlaklık, efektler, uyku zamanlayıcısı, açılışta/uyanmada yeniden uygulama | **Hayır** | Yalnız `InsydeDCHU.dll` (sürücüyle gelir) |
| **Monster Performans** (`MonsterPerformans.exe` + fan bekçisi) | Fan eğrisi, koruma, güç profili, frekans tavanı, izleme/kayıt, kendini geliştiren optimizasyon ([10](10-veri-envanteri-ve-ogrenen-model.md)) | Bekçi: evet (servis) | DLL + NVML + PDH + (PawnIO) |

- **Çalışma zamanında birbirinden bağımsız:** biri çökerse ya da kaldırılırsa diğeri çalışır. Ortak süreç, ortak ayar dosyası, aralarında iletişim **yok**.
- **Ortak kod yalnız derleme zamanında:** küçük `MonsterKontrol.Dchu` kütüphanesi (P/Invoke, DLL bulma, 512 baytlık tampon, tek iş parçacıklı kuyruk). Her exe'ye gömülür (tek dosya yayın). Paylaşılan bir DLL dağıtılmaz.
- **İki süreçten aynı DLL:** klavye ve performans uygulaması `InsydeDCHU.dll`'i aynı anda çağıracak. Süreçler arası eşzamanlı çağrının güvenli olup olmadığı **ölçülmedi** → test 26 (iki süreç 10 dk boyunca biri tampon 12 okur, diğeri 15 kare/sn renk yazar; çökme / bozuk okuma var mı). Sorun çıkarsa adlandırılmış bir sistem mutex'i (`Global\MonsterKontrolDchu`) ile çağrılar sıraya alınır — iki uygulamanın tek ortak noktası bu olur.
- Aşağıdaki bölümlerin çoğu **performans uygulaması** içindir; klavye uygulamasının planı [11](11-uygulama-plani.md)'de.

## Karar özeti (performans uygulaması)

| Konu | Öneri | Neden |
|---|---|---|
| Dil / çatı | C# / **.NET 10 LTS**, x64 | DLL x64; LTS uzun destek. Kurulumda sürümü doğrula |
| Arayüz | **WPF + [WPF UI](https://github.com/lepoco/wpfui)** (Fluent, Mica, koyu/açık tema, `Wpf.Ui.Tray`) | Windows 11 görünümü hazır gelir; WinUI 3'ten kurulumu ve tepsi desteği daha basit |
| Arka plan | **Küçük "fan bekçisi" servisi** (LocalSystem) — yalnız fan döngüsü + koruma + çıkışta otomatik. **Test 8'e bağlı:** EC custom eğriyi kendisi uyguluyorsa servis yerine 20 satırlık bekçi süreç yeter ([09](09-ikinci-gorus.md)) | "Kaçış yolları" şartı |
| UI süreci | Klavye, efektler, güç planı, profiller, otomatik profil — DLL'i **kendisi** yükler (yönetici gerekmez; oturum 0'dan ön plan penceresi vb. görülemez) | |
| UI ↔ servis | **`ayarlar.json` izleme** (FileSystemWatcher); named pipe yok. UI durum için tampon 12'yi kendisi okur | Protokol kodu yok |
| Ayarlar | `%ProgramData%\MonsterKontrol\ayarlar.json` | Servis ve UI aynı dosyayı görür |
| GPU sıcaklığı | **NVML** (`nvml.dll`, NVIDIA sürücüsüyle gelir) | DCHU bayt 17 GPU çekirdeği değil |
| CPU frekans sınırı | `powrprof.dll` (`PowerWriteACValueIndex`, `PowerSetActiveScheme`) ile kendi güç planımız | `powercfg`'ye süreç açmadan, yönetici gerekmeden |

✅ **Test 8 (2026-11-10): custom eğriyi EC/firmware uyguluyor** (kullanıcı modunda yazar bulunamadı; CCDCHUService ancak test 20 ile kesin dışlanır) → servis/bekçi büyük olasılıkla gereksiz; yeni katmanlı mimari ve açık kapılar: [12](12-performans-arka-plan.md).

~~Karar test 8'den sonra:~~ EC custom eğriyi kendisi uyguluyorsa uygulama "eğri noktalarını yazan arayüz"e iner ve bekçi süreç yeter; uygulamıyorsa küçük fan bekçisi servisi. Her iki durumda Aşama 1 tek exe ile başlar; düşük sabit fan profilleri **bekçi gelmeden varsayılan olmaz**.

## Neden servis? — "kaçış yolları" şartı

Firmware sabit fan hızında koruma yapmıyor (90°C'de fan %10'da kaldı). Bu yüzden:

> **Şart:** Fan kontrolümüz hangi yoldan biterse bitsin, sonunda ya `0x69 / 0x0F` (otomatik) gönderilmeli ya da kontrol aynı ayarla hemen yeniden başlamalı.

| Kaçış yolu | Tek exe (tepsi) | Servis |
|---|---|---|
| Normal kapatma | ✅ | ✅ `OnStop` → otomatik |
| Çökme (yakalanmamış hata) | ⚠️ `AppDomain.UnhandledException` çoğu zaman | ✅ SCM "hata olursa yeniden başlat" |
| Görev Yöneticisi'nden sonlandırma | ❌ hiçbir kod çalışmaz → fan sabit kalır | ✅ SCM yeniden başlatır (kullanıcı servis süreçlerini kolay öldüremez) |
| Oturum kapatma | ❌ uygulama kapanır | ✅ servis çalışmaya devam eder |
| Uyku / uykudan dönüş | ⚠️ `SystemEvents.PowerModeChanged` | ✅ `OnPowerEvent` |
| Güncelleme | ⚠️ | ✅ dur → otomatik → güncelle → başlat |
| Windows kapanışı | ✅ | ✅ `OnShutdown` |

Not: Fan sabit kalsa bile CPU ve GPU **kendini kısar** (PROCHOT 100°C, GPU 97°C). Ama VRM/SSD için fanın dolaylı etkisi var ve servisin de çözemediği durumlar kalıyor (sistem donması, mavi ekran). **Asıl sigorta:** taban duty koşula bağlı (GPU etkinken en az %30–35) + mümkünse EC tarafı eğri (test 8).

**Bu makinede:** Modern Standby **yok** (`powercfg /a`: yalnız S3 + hazırda bekletme) → uyku olayları güvenilir. **Hızlı başlangıç açık** (`HiberbootEnabled=1`) → servis kapanışı görmez, açılışta EC sıfırlanmış olabilir → aşağıdaki "geri okuma karşılaştırması" bunu yakalar; hızlı başlangıcı kapatmak kullanıcıya sorulacak (🔐, pil yok, kayıp küçük).

## Proje yapısı

```
MonsterKontrol.sln
├─ src/MonsterKontrol.Dchu            ORTAK (yalnız derleme zamanı): P/Invoke, DLL bulma, 512 B tampon,
│                                      tek iş parçacıklı kuyruk, komut sabitleri, IDchu arayüzü
├─ src/MonsterKlavye                   WPF + WPF UI uygulaması (tepsi), yönetici gerekmez
│   ├─ Klavye/KeyboardDevice.cs        F0 renk + parlaklık (0x67)
│   ├─ Klavye/Efektler/*.cs            efekt motoru + efektler (prototip: klavye.py)
│   ├─ Ayarlar/                        %AppData%\MonsterKlavye\ayarlar.json
│   └─ Arayuz/                         sayfalar, renk tekerleği, efekt galerisi
├─ src/MonsterPerformans               WPF + WPF UI uygulaması (tepsi)
│   ├─ Sensorler/                      tampon 12 + NVML + PDH (+ PawnIO/LHM)
│   ├─ Fan/FanCurve.cs, FanController.cs
│   ├─ Guc/PowerController.cs          CC profili (0x79/0x19) + Windows planı/PROCFREQMAX
│   ├─ Ogrenme/                        kayıt + çevrimiçi optimizasyon (10)
│   └─ Arayuz/
├─ src/MonsterPerformans.Bekci         küçük fan bekçisi (servis ya da bekçi süreç — test 8'e göre)
└─ tests/
    ├─ MonsterKontrol.Dchu.Tests       sahte DLL ile: tampon boyu, komut kodlama
    ├─ MonsterKlavye.Tests             efekt kareleri geçerli aralıkta, renk kodlama (B<<16|R<<8|G)
    └─ MonsterPerformans.Tests         eğri, yumuşatma, acil durum durum makinesi (sahte cihazla)
```

Her uygulama `IDchu` arayüzü üzerinden çalışır → testlerde sahte cihaz, gerçekte `DchuDevice`.

## DchuDevice

- DLL yolu: `HKLM\SYSTEM\CurrentControlSet\Services\CCDCHUService` → `ImagePath`'in dizini + `InsydeDCHU.dll`; yedek: `DriverStore\FileRepository\acpibridge1.inf_amd64_*\InsydeDCHU.dll` araması. Bulunamazsa açık hata ("Clevo sürücüsü kurulu değil"), sessizce devam etme. (Clevo WMI `CLEVO_GET` bu makinede **yok** — DLL tek yol.)
- `NativeLibrary.Load` + `SetDllDirectory`/`AddDllDirectory` o dizin için.
- Tüm çağrılar **tek bir adanmış iş parçacığında**, `BlockingCollection` kuyruğu ile.
- `ReadAppSettings` yalnızca bilinen sayfa/ofsetlerle (çökme riski).
- **`GetDCHU_Data_Buffer` için 512 baytlık tampon** (DLL 257. bayta yazıyor; 256'lık tampon yığını bozar). Yönetilen kodda `byte[512]` + `ref buf[0]` ile çağır.
- DLL'in kendi hatası süreci düşürebilir → servis mimarisi bunu da karşılar (SCM yeniden başlatır). Tek exe aşamasında DLL çağrılarını ayrı bir yardımcı süreçte çalıştırmak isteğe bağlı ek güvence.
- Her `SetFanDuty` sonrası 100 ms bekle, tampon 12'den duty geri oku; uyuşmazsa günlüğe yaz ve tekrar dene (1 kez).

## Fan kontrol algoritması (FanController, 1 Hz)

1. **Oku:** CPU °C (bayt 18), GPU °C (NVML; yoksa bayt 17), RPM'ler, duty'ler.
2. **Acil durum (yumuşatmasız, ham değer, histerezisli):** ([09](09-ikinci-gorus.md))
   - **Giriş:** CPU ≥ 95°C 3 sn **veya** GPU ≥ 87°C (GTX 1650 Ti: en yüksek çalışma 87°C, yavaşlama 97°C, kapanma 102°C — `nvidia-smi -q -d TEMPERATURE`).
   - **İlk eylem: `PROCFREQMAX`'ı bir kademe indir** (doğrulanmış kaldıraç; %100 fan yalnız −3°C getiriyor). İkinci: fanlar %100.
   - **Çıkış:** 85°C altında 60 sn → frekans sınırını kademe kademe geri al (her kademe arası 60 sn).
   - Üçüncü eylem (frekans sınırı yetmezse): CC profilini bir kademe indir — **artık doğrulandı** (MSR 0x610): Sessiz PL1 15 W, Tasarruf 30 W, Eğlence 45 W, Performans 60 W ([10](10-veri-envanteri-ve-ogrenen-model.md)). Sessiz en güçlü soğutma kaldıracı.
   - Not: 90°C'deki plato işlemcinin kendi TCC sınırı (TjMax 100 − ofset 10); 95°C eşiği yalnız TCC'nin yetmediği (ör. GPU ısısı ortak borudan) durumlar için.
   - Eğri 88–90°C'de zaten %100 olduğu için Maksimum profilin 90°C platosunda limit döngüsü oluşmaz.
3. **Yumuşat:** üstel ortalama, **τ ≈ 20–30 sn** (soğutucunun zaman ölçeği; çip sıçramalarını fan zaten etkileyemez).
4. **Eğri:** `hedefCPU = eğri(ort. sıcaklık)`, doğrusal ara değer, **3–5°C histerezis** (yükselirken ve inerken farklı eşik). ~~Eğim terimi~~ çıkarıldı (gürültü üretiyordu, 09). Ortak ısı borusu nedeniyle **tek sürücü sıcaklık = max(CPU, GPU)**. `hedefGPU = max(gpuEğri(GPU), hedefCPU × k)`, k test 14'ten (şimdilik 0,8).
5. **Hız sınırla:** artış ≤ %10/sn, azalış ≤ %2/sn.
6. **Geri okuma karşılaştırması (her tur):** tampon 12'deki duty ≠ komut edilen (±2) → yeniden uygula, günlüğe yaz. Uyku, hızlı başlangıç, Fn tuşu, CCDCHUService müdahalesini yakalar. ("Değişim < %2 ise gönderme" yalnız bununla birlikte güvenli.)
7. **Taban:** ayarlanabilir taban (varsayılan %15), **GPU etkinken (NVML kullanım > %10) en az %30**. **Fanı durdurma yok** (bu makinede CPU 55°C'nin altına nadiren iniyor).
8. **Kick-start:** fan 0 RPM iken hedef > 0 → önce %30 / 2 sn.
9. **RPM nöbetçisi:** duty > %15 ama RPM = 0 süresi > 5 sn → duty %40; hâlâ 0 ise diğer fan %100 + frekans sınırını indir + kalıcı uyarı. (Firmware GPU fanını soğukta kendisi durduruyor; sabit modda aynısı olursa yanlış alarm — test 6 ile ayır.)
10. **Sensör hatası** (okuma başarısız / 0°C / 127°C) 3 kez üst üste → `0x69/0x0F` otomatik + uyarı.

Mod seçenekleri: `Otomatik (firmware)` · `Eğri (bizim)` · `Sabit %` · `Max`. Varsayılan: **Eğri**.

## Klavye

- Renk: **yalnız F0** (`0xF0000000 | B<<16 | R<<8 | G`) — tek bölge, test edildi. Renk + parlaklık = 2 komut/kare.
- Donanım efekti yok → tüm efektler yazılımla (Monster uygulamasında hiç efekt yok; bu bizim artımız).
- Efekt motoru **15 kare/sn ile başla** (fan döngüsüyle aynı DLL kuyruğu; 30'a çıkmadan önce gecikmeyi ölç), **yalnızca değişen değeri gönder** (komut ~3 ms).
- Uyanmada ve Fn+ışık tuşundan sonra rengi yeniden uygula (klavye yazmalarının geri okuması yok; CCDCHUService/FnKey CC kaydını uygulayabilir).
- Efektler (prototipte hazır, `prototip/klavye.py`): nefes, gökkuşağı, renkli nefes, kalp, polis, ateş (kullanıcı beğendi), okyanus (geniş aralık), gün batımı, flaş, **sıcaklık rengi**.
- Kilit ekranı / ekran kapalı / uyku → efekti duraklat, son sabit rengi uygula.
- Firmware ayar sayfalarına (`WriteAppSettings`) **efekt döngüsünden asla yazma**.

## Güç

- CC profili: `0x79`, `(0x19<<24)|p`. Uygulamamız açılışta ve uykudan dönüşte istenen profili yeniden yazar (CC kaldırılınca kimse yazmayacak; açılıştaki varsayılan ❓ test).
- Windows: uygulama kendi planlarını yönetir ("MK Sessiz/Dengeli/Maksimum"), Dengeli'nin kopyası olarak oluşturulur. Frekans sınırı = `PROCFREQMAX`; turbo davranışı `PERFBOOSTMODE` (verimli modlar) + `PERFEPP` — kendi "gereksiz turbo dedektörü" yazılmaz, Windows bunu ms ölçeğinde zaten yapar. Çıkışta kullanıcının önceki planına döner.
- Güç (W) gösterimi: sürücüsüz gerçek ölçüm yok (Energy Meter sayacı yok). Kalibrasyon tablosundan ara değerle "~W (tahmini)" ya da hiç gösterme; karar mantığı yük ve frekansı doğrudan kullanır.

## Uyku / açılış / kapanış

| Olay | Yapılacak |
|---|---|
| Servis başlangıcı | DLL'i bul, sensörleri doğrula, profili uygula, kontrol döngüsünü başlat |
| Uykuya geçiş | Efekti durdur, `0x69/0x0F` |
| Uykudan dönüş / açılış (hızlı başlangıç dahil) | 3 sn bekle (EC hazır olsun), **tüm önbelleği geçersiz say**, her şeyi yeniden uygula |
| Ekran kapandı | Fanı otomatiğe ver (isteğe bağlı), efekti durdur |
| Kapanış / servis durdurma | `0x69/0x0F`, efekt durdur, son sabit klavye rengi |

## Kurulum / dağıtım

- `dotnet publish -c Release -r win-x64 --self-contained` → .NET kurulu olmayan makinede de çalışır.
- Servis kaydı yönetici ister (🔐, tek seferlik): `sc create MonsterKontrol binPath= ... start= auto` + `sc failure MonsterKontrol reset= 60 actions= restart/2000/restart/5000/restart/10000`.
- UI: `HKCU\...\Run` ile oturum açılışında tepside başlar.
- Geliştirme aracı: .NET SDK kurulu değil. Seçenek: `dotnet-install.ps1 -Channel 10.0` **kullanıcı kapsamında, UAC'siz**; ya da `winget install Microsoft.DotNet.SDK.10` (🔐). Kullanıcı onayıyla yapılacak.

## Aşamalar

0. **Kayıt + gölge mod** (bkz. [08](08-chatgpt-fikirleri.md), [09](09-ikinci-gorus.md)): Core'un okuma tarafı + fan algoritması "kuru" çalışır, kararlarını CSV'ye yazar, donanıma dokunmaz. **Bir oyun oturumu dahil birkaç saat** — amaç DLL'in uzun süre çökmeden çalışması, gerçek sıcaklık/yük dağılımı, acil durumun ne sıklıkla tetikleneceği. Kayıt özelliği kalıcı kalır.
1. **Core + tek exe:** DchuDevice, sensörler, fan eğrisi, klavye, güç profili, WPF arayüz. Testler yeşil. Koruma ve `ProcessExit`/`UnhandledException` → otomatik.
2. **Fan bekçisi:** test 8'e göre küçük servis ya da bekçi süreç; SCM kurtarma. Düşük fan profilleri ancak bundan sonra varsayılan olabilir.
3. **Monster uygulamalarını kaldır** (bkz. 06), bir hafta günlük kullanım, sorunları topla.
4. **Ekstralar** (bkz. 07).
