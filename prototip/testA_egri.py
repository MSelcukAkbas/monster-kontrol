# Kapı A (doc 12): CC'nin custom eğrisini WriteAppSettings sayfa 4 ile biz yazabiliyor muyuz, EC yeni eğriyi izliyor mu?
# Sıra: custom (mevcut eğri) 30 sn → aynı baytları yaz (etkisiz) → 80°C noktası 99→55 (tek bayt) + mod 6 60 sn → mod 0 + mod 6 30 sn.
# Geri yükleme ayrı süreçte: testA_geri.py (DLL çökse de çalışsın diye). CPU ≥95°C → fan max, kayit/acil.txt.
import ctypes, json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan

KOK = os.path.dirname(os.path.abspath(__file__))
YEDEK = json.load(open(os.path.join(KOK, "..", "docs", "appsettings-yedek.json")))["4"]["bytes"]
for f in (fan.dll.ReadAppSettings, fan.dll.WriteAppSettings):
    f.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.POINTER(ctypes.c_ubyte)]

def oku_sayfa():
    b = (ctypes.c_ubyte * 512)()
    fan.dll.ReadAppSettings(4, 0, 256, b)
    return list(b[:256])

def yaz(ofset, baytlar):
    b = (ctypes.c_ubyte * 512)(*baytlar)
    r = fan.dll.WriteAppSettings(4, ofset, len(baytlar), b)
    time.sleep(0.2)
    return r

def fark(a):
    return [(i, YEDEK[i], a[i]) for i in range(256) if a[i] != YEDEK[i]]

class Acil(Exception): pass

def izle(ad, sure):
    print(time.strftime("%H:%M:%S"), "==", ad, flush=True)
    for _ in range(int(sure // 3)):
        time.sleep(3)
        d = fan.durum()
        print("  ", time.strftime("%H:%M:%S"), d, flush=True)
        if d["cpu_C"] >= 95:
            raise Acil(d)

EGRI = YEDEK[16:28]  # 16–18 duty, 19 = 100, 20–21, 22–24 sıcaklık, 25 = 100, 26–27
assert EGRI == [35, 50, 99, 100, 50, 99, 40, 60, 80, 100, 60, 80], EGRI
assert oku_sayfa() == YEDEK, "sayfa 4 yedekten farklı, test başlamaz"
ACIL = os.path.join(KOK, "kayit", "acil.txt")
if os.path.exists(ACIL): os.remove(ACIL)
try:
    fan.mod("custom")
    izle("A1 custom, mevcut eğri (35/50/99 @ 40/60/80)", 30)
    print("A2 etkisiz yazma ret:", yaz(16, EGRI), "fark:", fark(oku_sayfa()), flush=True)
    print("A3 80°C noktası 99→55 ret:", yaz(18, [55]), "fark:", fark(oku_sayfa()), flush=True)
    fan.mod("custom")
    izle("A3 custom, 80°C noktası %55 — 90°C'de ~%80 (100°C noktası kullanılıyorsa) ya da %55 beklenir", 60)
    fan.mod("otomatik"); time.sleep(1); fan.mod("custom")
    izle("A4 mod 0 → 6 yeniden tetik", 30)
except Acil as e:
    open(ACIL, "w").write(str(e))
    fan.mod("max")
    print("ACİL ≥95°C → fan max:", e, flush=True)
