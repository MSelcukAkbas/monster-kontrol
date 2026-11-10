# 1 Hz kayıt (gölge mod prototipi): tampon 12 + CPU yükü + frekans (PDH) → CSV. Donanıma yalnız acil durumda yazar.
# Kullanım: kayit.py <cikti.csv> [saniye]   — etiket: aynı klasörde etiket.txt (her saniye okunur)
# Acil durum: CPU ≥ 95°C 3 örnek üst üste → fan.mod("max") ve kayıt sürer (05 güvenlik kuralı).
import ctypes, ctypes.wintypes as w, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan

pdh = ctypes.windll.pdh
sorgu, sayac = w.HANDLE(), w.HANDLE()
assert pdh.PdhOpenQueryW(None, 0, ctypes.byref(sorgu)) == 0
assert pdh.PdhAddEnglishCounterW(sorgu, r"\Processor Information(_Total)\% Processor Performance", 0, ctypes.byref(sayac)) == 0

class PDH_DEGER(ctypes.Structure):
    _fields_ = [("durum", w.DWORD), ("_pad", w.DWORD), ("deger", ctypes.c_double)]

def performans():
    d = PDH_DEGER()
    pdh.PdhCollectQueryData(sorgu)
    return d.deger if pdh.PdhGetFormattedCounterValue(sayac, 0x200, None, ctypes.byref(d)) == 0 else -1

class FT(ctypes.Structure):
    _fields_ = [("lo", ctypes.c_uint32), ("hi", ctypes.c_uint32)]
def zamanlar():
    i, k, u = FT(), FT(), FT()
    ctypes.windll.kernel32.GetSystemTimes(ctypes.byref(i), ctypes.byref(k), ctypes.byref(u))
    f = lambda x: (x.hi << 32) | x.lo
    return f(i), f(k) + f(u)

def ham():
    b = (ctypes.c_ubyte * 512)()
    fan.dll.GetDCHU_Data_Buffer(12, b)
    return list(b[:32])

cikti, sure = sys.argv[1], float(sys.argv[2]) if len(sys.argv) > 2 else 1e9
etiket_yolu = os.path.join(os.path.dirname(os.path.abspath(cikti)), "etiket.txt")
yeni = not os.path.exists(cikti)
f = open(cikti, "a", encoding="utf-8", buffering=1)
if yeni:
    f.write("zaman,etiket,cpu_C,yuk,perf,cpu_duty,cpu_rpm,gpu_duty,gpu_rpm,b17,acil," + ",".join(f"b{i}" for i in range(32)) + "\n")
t0, onceki, sicak, acil = time.time(), zamanlar(), 0, 0
performans()
while time.time() - t0 < sure:
    time.sleep(1)
    simdi = zamanlar()
    yuk = 100 * (1 - (simdi[0] - onceki[0]) / max(simdi[1] - onceki[1], 1))
    onceki = simdi
    b = ham()
    d = {"cpu_C": b[18], "cpu_%": round(b[16] / 2.55), "cpu_rpm": fan._rpm(b[2], b[3]),
         "gpu_%": round(b[19] / 2.55), "gpu_rpm": fan._rpm(b[4], b[5])}
    sicak = sicak + 1 if d["cpu_C"] >= 95 else 0
    if sicak >= 3 and not acil:
        fan.mod("max"); acil = 1
        print("!!! ACİL: CPU >= 95°C, fanlar MAX", flush=True)
    try:
        etiket = open(etiket_yolu, encoding="utf-8").read().strip()
    except OSError:
        etiket = ""
    f.write(f"{time.strftime('%H:%M:%S')},{etiket},{d['cpu_C']},{yuk:.1f},{performans():.1f},{d['cpu_%']},{d['cpu_rpm']},"
            f"{d['gpu_%']},{d['gpu_rpm']},{b[17]},{acil}," + ",".join(map(str, b)) + "\n")
