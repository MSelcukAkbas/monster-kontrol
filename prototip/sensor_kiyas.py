# Sensör karşılaştırma: DCHU bayt 18/17, ACPI \_TZ.TZ0 (PDH), nvidia-smi. Yönetici gerekmez.
# Kullanım: sensor_kiyas.py <bosta_sn> <yuk_sn>
import sys, os, time, ctypes, subprocess, multiprocessing as mp
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan
from olcum import _isci

class _V(ctypes.Structure):
    _fields_ = [("status", ctypes.c_ulong), ("pad", ctypes.c_ulong), ("val", ctypes.c_double)]

def tz_okuyucu():
    pdh, q, h = ctypes.windll.pdh, ctypes.c_void_p(), ctypes.c_void_p()
    pdh.PdhOpenQueryW(None, 0, ctypes.byref(q))
    pdh.PdhAddEnglishCounterW(q, "\\Thermal Zone Information(\\_TZ.TZ0)\\High Precision Temperature", 0, ctypes.byref(h))
    def oku():
        pdh.PdhCollectQueryData(q)
        v = _V()
        pdh.PdhGetFormattedCounterValue(h, 0x200, None, ctypes.byref(v))
        return v.val / 10 - 273.15  # onda bir Kelvin
    return oku

def gpu_smi():
    r = subprocess.run(["nvidia-smi", "--query-gpu=temperature.gpu", "--format=csv,noheader"], capture_output=True, text=True)
    return int(r.stdout.strip())

if __name__ == "__main__":
    bosta, yuk = map(int, sys.argv[1:])
    tz = tz_okuyucu()
    satir = []
    def ornek(evre):
        d = fan.durum()
        s = (evre, d["cpu_C"], round(tz(), 1), d["gpu_C"], gpu_smi())
        satir.append(s)
        print(f"{evre:5} DCHU-CPU {s[1]:3}  ACPI-TZ0 {s[2]:5}  |  DCHU-b17 {s[3]:3}  nvidia {s[4]:3}", flush=True)
    t0 = time.time()
    while time.time() - t0 < bosta:
        ornek("bosta"); time.sleep(2)
    adet = os.cpu_count()
    sayac = mp.Array("q", adet, lock=False)
    bitis = time.time() + yuk
    ps = [mp.Process(target=_isci, args=(bitis, sayac, i)) for i in range(adet)]
    for p in ps: p.start()
    try:
        while time.time() < bitis:
            ornek("yuk"); time.sleep(2)
            if satir[-1][1] >= 95:
                print("!!! 95°C, durduruluyor"); break
    finally:
        for p in ps: p.terminate(); p.join()
    for evre in ("bosta", "yuk"):
        s = [x for x in satir if x[0] == evre]
        if s:
            fark = [x[1] - x[2] for x in s]; gfark = [x[4] - x[3] for x in s]
            print(f"ÖZET {evre}: DCHU-CPU − TZ0 ort {sum(fark)/len(fark):+.1f} (min {min(fark):+.1f} / max {max(fark):+.1f});  nvidia − b17 ort {sum(gfark)/len(gfark):+.1f}")
