# Mod 8 (CC "Turbo fan" düğmesi: 0x79 alt 1 değer 8). Firmware ayarına (sayfa 4 ofset 9) yazmaz.
# Sıra: otomatik 15 sn → mod 8 60 sn → mod 0 + 0x69/0x0F, 30 sn iniş izlenir.
import ctypes, os, struct, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan

def izle(ad, sure):
    print(time.strftime("%H:%M:%S"), "==", ad, flush=True)
    for _ in range(int(sure // 3)):
        time.sleep(3)
        print("  ", time.strftime("%H:%M:%S"), fan.durum(), flush=True)
try:
    izle("otomatik (taban)", 15)
    p = (ctypes.c_ubyte * 4)(8, 0, 0, 1)
    print("mod 8 ret:", fan.dll.SetDCHU_Data(121, p, 4), flush=True)
    izle("mod 8", 60)
finally:
    fan.mod("otomatik"); fan.otomatik()
    izle("güvenli dönüş (mod 0 + 0x69)", 30)
