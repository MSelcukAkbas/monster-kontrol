# Kapı A' (doc 12): CC kapalıyken tutarlı bir CPU eğrisi (noktalar + eğimler) yaz → mod 6 → EC izliyor mu?
# Test eğrisi (40,35) (60,45) (80,60) (100,100): 90°C'de %80, 70°C'de %52. Kullanıcının CC eğrisi aynı yerlerde %98–99.
# Önce: kayit/sayfa4_A2_oncesi.json (ayrı süreç) alınmış olmalı. Geri yükleme ayrı süreçte: testA2_geri.py.
import ctypes, json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan

KOK = os.path.dirname(os.path.abspath(__file__))
ONCE = json.load(open(os.path.join(KOK, "kayit", "sayfa4_A2_oncesi.json")))["bytes"]
for f in (fan.dll.ReadAppSettings, fan.dll.WriteAppSettings):
    f.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.POINTER(ctypes.c_ubyte)]

def oku():
    b = (ctypes.c_ubyte * 512)(); fan.dll.ReadAppSettings(4, 0, 256, b); return list(b[:256])

def egim(d1, d2, t1, t2):
    return round((d2 - d1) * 2.55 * 16 / (t2 - t1))

def blok(d, t, kaynak):
    # 16–33: duty 16–19, (20–21 aynen), sıcaklık 22–25, (26–27 aynen), eğimler 28/30/32 (16 bit LE)
    b = kaynak[16:34]
    b[0:4] = d; b[6:10] = t
    for i in range(3):
        e = egim(d[i], d[i + 1], t[i], t[i + 1]); b[12 + 2 * i] = e & 0xFF; b[13 + 2 * i] = e >> 8
    return b

# Kendini sına: formül fabrika ve kullanıcı eğrisinin kayıtlı eğimlerini vermeli.
assert blok(ONCE[16:20], ONCE[22:26], ONCE) == ONCE[16:34], "eğim formülü mevcut sayfayla tutmuyor"
assert oku() == ONCE, "sayfa 4 önceki yedekten farklı"

ACIL = os.path.join(KOK, "kayit", "acil.txt")
if os.path.exists(ACIL): os.remove(ACIL)

def izle(ad, sure):
    print(time.strftime("%H:%M:%S"), "==", ad, flush=True)
    for _ in range(int(sure // 3)):
        time.sleep(3)
        d = fan.durum(); print("  ", time.strftime("%H:%M:%S"), d, flush=True)
        if d["cpu_C"] >= 95:
            open(ACIL, "w").write(str(d)); fan.mod("max"); raise SystemExit("ACİL ≥95°C → fan max")

izle("otomatik (taban)", 15)
yeni = blok([35, 45, 60, 100], [40, 60, 80, 100], ONCE)
b = (ctypes.c_ubyte * 512)(*yeni)
print("yazma ret:", fan.dll.WriteAppSettings(4, 16, len(yeni), b), flush=True)
time.sleep(0.2)
s = oku()
print("fark:", [(i, ONCE[i], s[i]) for i in range(256) if s[i] != ONCE[i]], flush=True)
fan.mod("custom")
izle("custom, test eğrisi: 80°C üstü %60 + 2/°C (90°C → %80)", 60)
