# Monster Kontrol

Monster ABRA A5 V16.6 (Clevo, i5-10200H, GTX 1650 Ti) için Control Center'ın yerine geçen iki bağımsız C# uygulaması: **Monster Klavye** (önce) ve **Monster Performans**.

**Her oturumda önce `docs/README.md` → "KALDIĞIMIZ YER" bölümünü oku.** Tüm donanım bilgisi, ölçümler, kararlar ve plan `docs/` altında (01–11).

Kritik kurallar (ayrıntı docs/01):
- `InsydeDCHU.dll` `GetDCHU_Data_Buffer` 257. bayta yazar → tampon her zaman ≥ 512 bayt.
- `SetDCHU_Data` dönüş değeri başarı kodu değildir.
- Klavye tek bölge: yalnız `0xF0000000 | B<<16 | R<<8 | G`; parlaklık `0xF4000000 | 0..255`.
- Fan/güç yazmaları donanımı etkiler: yalnız kullanıcı onayıyla ve sonunda güvenli duruma dön (fan `0x69/0x0F`, profil eğlence, Dengeli plan).
- .NET 10 SDK PATH'te değil: `$env:DOTNET_ROOT="$env:LOCALAPPDATA\Microsoft\dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"`.
