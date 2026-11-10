# Kapı A'' (doc 12): eğri EC'ye SetDCHU_Data(14, 256 bayt) ile gider (FanSpeedSetting.exe 7.87 IL, metot 600001C);
# EC'nin etkin eğrisi GetDCHU_Data_Buffer(13) ile okunur. NVRAM'a yazmaz; EC belleği geçicidir.
# Sıra: tampon 13 oku → CPU'ya fabrika eğrisini gönder → tampon 13 doğrula → mod 6, 45 sn izle → sayfa 4'teki eğriyi geri gönder → doğrula → mod 0 + 0x69.
import ctypes, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan

fan.dll.ReadAppSettings.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.POINTER(ctypes.c_ubyte)]
def sayfa4():
    b = (ctypes.c_ubyte * 512)(); fan.dll.ReadAppSettings(4, 0, 256, b); return list(b[:256])
def tampon13():
    b = (ctypes.c_ubyte * 512)(); fan.dll.GetDCHU_Data_Buffer(13, b); return list(b[:64])
def egim(d1, d2, t1, t2):
    return round((d2 - d1) * 2.55 * 16 / (t2 - t1))

def fanlar(s):
    # Sayfa 4'te her fan 18 bayt (16 / 34 / 52): duty 0–3, sıcaklık 6–9, eğimler 12/14/16 (16 bit LE).
    return [dict(d=s[o:o + 4], t=s[o + 6:o + 10], e=[s[o + 12 + 2 * i] | s[o + 13 + 2 * i] << 8 for i in range(3)]) for o in (16, 34, 52)]

def komut14(fs):
    b = [0] * 256
    for i, f in enumerate(fs):  # FanSpeedSetting: 2. ve 3. nokta (sıcaklık, duty×2,55), eğimler büyük endian
        b[2 + 4 * i:6 + 4 * i] = [f["t"][1], round(f["d"][1] / 100 * 255), f["t"][2], round(f["d"][2] / 100 * 255)]
        for j, e in enumerate(f["e"]):
            b[14 + 6 * i + 2 * j] = e >> 8; b[15 + 6 * i + 2 * j] = e & 0xFF
    return b

def gonder(b):
    p = (ctypes.c_ubyte * 512)(*b); r = fan.dll.SetDCHU_Data(14, p, 256); time.sleep(0.5); return r

def ec_noktalari(t):  # tampon 13: fan başına 8 bayt (16 / 24 / 32): 4 × (sıcaklık, duty 0–255)
    return [[(t[o + 2 * k], t[o + 2 * k + 1]) for k in range(4)] for o in (16, 24, 32)]

def izle(ad, sure):
    print(time.strftime("%H:%M:%S"), "==", ad, flush=True)
    for _ in range(int(sure // 3)):
        time.sleep(3)
        d = fan.durum(); print("  ", time.strftime("%H:%M:%S"), d, flush=True)
        if d["cpu_C"] >= 95:
            fan.mod("max"); raise SystemExit("ACİL ≥95°C → fan max")

S = sayfa4(); F = fanlar(S); ONCE = tampon13()
print("tampon 13 önce:", ec_noktalari(ONCE), flush=True)
# Kendini sına: sayfa 4'ten hesaplanan 2./3. noktalar EC'dekiyle aynı olmalı (eğri sayfa 4'ten gönderilmiş).
for f, n in zip(F, ec_noktalari(ONCE)):
    assert [n[1], n[2]] == [(f["t"][1], round(f["d"][1] * 2.55)), (f["t"][2], round(f["d"][2] * 2.55))], (f, n)
for f in F[:2]:
    assert f["e"] == [egim(f["d"][k], f["d"][k + 1], f["t"][k], f["t"][k + 1]) for k in range(3)], f

fab = dict(d=[35, 50, 99, 100], t=[40, 60, 80, 100]); fab["e"] = [egim(fab["d"][k], fab["d"][k + 1], fab["t"][k], fab["t"][k + 1]) for k in range(3)]
assert fab["e"] == [31, 100, 2]
try:
    print("gönder (CPU fabrika) ret:", gonder(komut14([fab, F[1], F[2]])), flush=True)
    print("tampon 13 sonra:", ec_noktalari(tampon13()), flush=True)
    fan.mod("custom")
    izle("custom, CPU fabrika eğrisi: 70°C → ~%75 (senin eğrin %98)", 45)
finally:
    print("geri gönder ret:", gonder(komut14(F)), flush=True)
    son = tampon13()
    print("tampon 13 geri:", ec_noktalari(son), "ÖNCEKİYLE AYNI" if son == ONCE else "FARKLI!", flush=True)
    fan.mod("otomatik"); fan.otomatik()
    time.sleep(3); print(time.strftime("%H:%M:%S"), "güvenli durum:", fan.durum(), flush=True)
