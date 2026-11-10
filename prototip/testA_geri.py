# Kapı A geri yükleme (ayrı süreç): sayfa 4 bayt 16–27'yi yedekten yazar, taze okumayla doğrular, fanı güvenli duruma alır.
# Acil (kayit/acil.txt) varsa fan max'ta kalır; yoksa mod 0 + 0x69/0x0F.
import ctypes, json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan

KOK = os.path.dirname(os.path.abspath(__file__))
YEDEK = json.load(open(os.path.join(KOK, "..", "docs", "appsettings-yedek.json")))["4"]["bytes"]
for f in (fan.dll.ReadAppSettings, fan.dll.WriteAppSettings):
    f.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.POINTER(ctypes.c_ubyte)]
def oku():
    b = (ctypes.c_ubyte * 512)(); fan.dll.ReadAppSettings(4, 0, 256, b); return list(b[:256])

f = [(i, YEDEK[i], v) for i, v in enumerate(oku()) if v != YEDEK[i]]
print("geri öncesi fark:", f, flush=True)
if f:
    b = (ctypes.c_ubyte * 512)(*YEDEK[16:28])
    print("geri yazma ret:", fan.dll.WriteAppSettings(4, 16, 12, b), flush=True)
    time.sleep(0.2)
if os.path.exists(os.path.join(KOK, "kayit", "acil.txt")):
    fan.mod("max"); print("ACİL: fan max'ta bırakıldı", flush=True)
else:
    fan.mod("otomatik"); fan.otomatik()
print(time.strftime("%H:%M:%S"), "güvenli durum:", fan.durum(), flush=True)
