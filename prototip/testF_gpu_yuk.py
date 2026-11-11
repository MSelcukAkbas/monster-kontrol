# Kapı E: GPU yük altında fan davranışı. Fan modu otomatik, hiçbir şey yazılmaz; yalnız okunur.
# gpu_yuk.exe çalışır, 3 sn'de bir EC (fan.durum) + nvidia-smi okunur. GPU >= 88°C veya CPU >= 95°C → yük durdurulur.
# Kullanım: testF_gpu_yuk.py <saniye>
import os, subprocess, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan

SURE = int(sys.argv[1]) if len(sys.argv) > 1 else 90
EXE = os.path.join(os.path.dirname(os.path.abspath(__file__)), r"gpu_yuk\bin\Release\net10.0-windows\gpu_yuk.exe")

def smi():
    o = subprocess.run(["nvidia-smi", "--query-gpu=temperature.gpu,utilization.gpu,power.draw,clocks.gr,pstate",
                        "--format=csv,noheader,nounits"], capture_output=True, text=True).stdout.strip()
    t, u, w, mhz, ps = [x.strip() for x in o.split(",")]
    return {"smi_C": int(t), "util_%": int(u), "W": float(w), "MHz": int(mhz), "ps": ps}

def satir(et):
    d = {**fan.durum(), **smi()}
    print(time.strftime("%H:%M:%S"), et, d, flush=True)
    return d

for _ in range(3): satir("önce"); time.sleep(3)
p = subprocess.Popen([EXE, str(SURE)], stdout=subprocess.DEVNULL)
bas = time.time()
try:
    while p.poll() is None:
        time.sleep(3)
        d = satir(f"{time.time() - bas:5.0f}s")
        if d["smi_C"] >= 88 or d["gpu_C"] >= 88 or d["cpu_C"] >= 95:
            print("KORUMA: sıcaklık sınırı, yük durduruluyor", flush=True)
            break
finally:
    if p.poll() is None: p.kill()
for _ in range(10): time.sleep(3); satir("sonra")
print("çıkış", p.wait())
