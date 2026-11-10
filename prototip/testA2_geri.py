# Kapı A' geri yükleme (ayrı süreç): sayfa 4 bayt 16–33 kayit/sayfa4_A2_oncesi.json'dan, taze okumayla doğrulama, fan güvenli duruma.
import ctypes, json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan

KOK = os.path.dirname(os.path.abspath(__file__))
ONCE = json.load(open(os.path.join(KOK, "kayit", "sayfa4_A2_oncesi.json")))["bytes"]
for f in (fan.dll.ReadAppSettings, fan.dll.WriteAppSettings):
    f.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.POINTER(ctypes.c_ubyte)]
def oku():
    b = (ctypes.c_ubyte * 512)(); fan.dll.ReadAppSettings(4, 0, 256, b); return list(b[:256])

f = [(i, ONCE[i], v) for i, v in enumerate(oku()) if v != ONCE[i]]
print("geri öncesi fark:", f, flush=True)
if f:
    b = (ctypes.c_ubyte * 512)(*ONCE[16:34])
    print("geri yazma ret:", fan.dll.WriteAppSettings(4, 16, 18, b), flush=True)
    time.sleep(0.2)
if os.path.exists(os.path.join(KOK, "kayit", "acil.txt")):
    fan.mod("max"); print("ACİL: fan max'ta bırakıldı", flush=True)
else:
    fan.mod("otomatik"); fan.otomatik()
print(time.strftime("%H:%M:%S"), "güvenli durum:", fan.durum(), flush=True)
