# Sıcaklık/fan/CPU izleme. Kullanım: izle.py <saniye> [aralik]
import sys, time, ctypes, statistics
sys.path.insert(0, __file__.rsplit("\\", 1)[0])
import fan

class FT(ctypes.Structure):
    _fields_ = [("lo", ctypes.c_uint32), ("hi", ctypes.c_uint32)]
def _cpu():
    i, k, u = FT(), FT(), FT()
    ctypes.windll.kernel32.GetSystemTimes(ctypes.byref(i), ctypes.byref(k), ctypes.byref(u))
    f = lambda x: (x.hi << 32) | x.lo
    return f(i), f(k) + f(u)

sure, aralik = float(sys.argv[1]), float(sys.argv[2]) if len(sys.argv) > 2 else 5
t0, onceki, cpuC = time.time(), _cpu(), []
while time.time() - t0 < sure:
    time.sleep(aralik)
    simdi = _cpu()
    yuk = 100 * (1 - (simdi[0] - onceki[0]) / max(simdi[1] - onceki[1], 1))
    onceki = simdi
    d = fan.durum()
    cpuC.append(d["cpu_C"])
    print(f"{time.time()-t0:5.0f}s  CPU {d['cpu_C']}°C  yük %{yuk:4.1f}  fan CPU {d['cpu_rpm']} ({d['cpu_%']}%)  GPU {d['gpu_rpm']} ({d['gpu_%']}%)", flush=True)
print(f"ORTALAMA CPU {statistics.mean(cpuC):.1f}°C  (min {min(cpuC)} / max {max(cpuC)})")
