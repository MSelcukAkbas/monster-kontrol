# Yük testi: tüm iş parçacıklarını meşgul eder, sıcaklık/frekans/fanı izler. CPU >= 95°C olursa durur.
# Kullanım: yuk.py <saniye>
import sys, time, os, ctypes, multiprocessing as mp
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan

def _mesgul(bitis):
    # Ortak kilit yok: her işlem kendi saatine bakar (Event kilidi işlemleri sıraya sokuyordu).
    x = 0
    while time.time() < bitis:
        for i in range(100000):
            x = (x * 1103515245 + i) & 0xFFFFFFFF

class _Pdh:  # gerçek anlık frekans: taban MHz * % Processor Performance / 100
    def __init__(self):
        p = ctypes.windll.pdh
        self.p, self.q = p, ctypes.c_void_p()
        p.PdhOpenQueryW(None, 0, ctypes.byref(self.q))
        self.c = {}
        for ad in ("Processor Frequency", "% Processor Performance"):
            h = ctypes.c_void_p()
            p.PdhAddEnglishCounterW(self.q, f"\\Processor Information(_Total)\\{ad}", 0, ctypes.byref(h))
            self.c[ad] = h
        p.PdhCollectQueryData(self.q)
    def mhz(self):
        class V(ctypes.Structure):
            _fields_ = [("status", ctypes.c_ulong), ("pad", ctypes.c_ulong), ("val", ctypes.c_double)]
        self.p.PdhCollectQueryData(self.q)
        out = {}
        for ad, h in self.c.items():
            v = V()
            self.p.PdhGetFormattedCounterValue(h, 0x200, None, ctypes.byref(v))  # PDH_FMT_DOUBLE
            out[ad] = v.val
        return out["Processor Frequency"] * out["% Processor Performance"] / 100

if __name__ == "__main__":
    sure = float(sys.argv[1])
    bitis = time.time() + sure + 3
    ps = [mp.Process(target=_mesgul, args=(bitis,)) for _ in range(os.cpu_count())]
    pdh = _Pdh()
    for p in ps:
        p.start()
    t0, sic, frek = time.time(), [], []
    import izle_cpu
    onceki = izle_cpu.zamanlar()
    try:
        while time.time() - t0 < sure:
            time.sleep(3)
            d, f = fan.durum(), pdh.mhz()
            simdi = izle_cpu.zamanlar()
            yuk = 100 * (1 - (simdi[0] - onceki[0]) / max(simdi[1] - onceki[1], 1)); onceki = simdi
            sic.append(d["cpu_C"]); frek.append(f)
            print(f"{time.time()-t0:4.0f}s  yük %{yuk:5.1f}  CPU {d['cpu_C']}°C  {f:5.0f} MHz  fan CPU {d['cpu_rpm']} ({d['cpu_%']}%)  GPU {d['gpu_rpm']} ({d['gpu_%']}%)", flush=True)
            if d["cpu_C"] >= 95:
                print("!!! 95°C, test durduruluyor"); break
    finally:
        for p in ps:
            p.terminate()
            p.join()
    print(f"ÖZET: CPU ort {sum(sic)/len(sic):.1f}°C (max {max(sic)}), frekans ort {sum(frek)/len(frek):.0f} MHz (min {min(frek):.0f} / max {max(frek):.0f})")
