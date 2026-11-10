# "Çıkış kodu 5" tetikleyicisini ayırmak için: argv[1] = sadece_dchu | nvidia | pdh ; argv[2] = tekrar
import sys, os, time, subprocess
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan
mod, n = sys.argv[1], int(sys.argv[2])
if mod == "pdh":
    from sensor_kiyas import tz_okuyucu
    tz = tz_okuyucu()
for i in range(n):
    fan.durum()
    if mod == "nvidia":
        subprocess.run(["nvidia-smi", "--query-gpu=temperature.gpu", "--format=csv,noheader"], capture_output=True)
    elif mod == "pdh":
        tz()
    time.sleep(0.5)
    print(i, end=" ", flush=True)
print("\nTAMAM", mod)
