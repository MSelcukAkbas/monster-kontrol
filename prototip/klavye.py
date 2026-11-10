# Klavye ışığı (DCHU 103). Kullanım: klavye.py parlaklik 0-255 | klavye.py renk R G B
import ctypes, os, struct, sys, time, math, colorsys, random
D = r"C:\Windows\System32\DriverStore\FileRepository\acpibridge1.inf_amd64_cedafa39846f03cf"
os.add_dll_directory(D)
dll = ctypes.CDLL(os.path.join(D, "InsydeDCHU.dll"))
dll.SetDCHU_Data.argtypes = [ctypes.c_int, ctypes.POINTER(ctypes.c_ubyte), ctypes.c_int]

def gonder(v, yaz=True):
    p = (ctypes.c_ubyte * 4)(*struct.pack("<I", v))
    r = dll.SetDCHU_Data(103, p, 4)
    if yaz:
        print(f"SetDCHU_Data(103, {v:#010x}) ret:", r)

def parlaklik(n):
    assert 0 <= n <= 255
    gonder(0xF4000000 | n)

def renk(r, g, b, yaz=True):
    for c in (r, g, b):
        assert 0 <= c <= 255
    gonder(0xF0000000 | (b << 16) | (r << 8) | g, yaz)  # tek bölge: yalnız F0 etkili (gözle test edildi)

# ---- Yazılım efektleri ----
# Her efekt t (saniye) alır, (4 bölgenin rengi listesi ya da None, parlaklık 0..1) döner.
def hsv(h, s=1, v=1):
    return tuple(int(255 * c) for c in colorsys.hsv_to_rgb(h % 1, s, v))

def dalga01(t, periyot):
    return 0.5 - 0.5 * math.cos(2 * math.pi * t / periyot)

def cpu_sicaklik():
    dll.GetDCHU_Data_Buffer.argtypes = [ctypes.c_int, ctypes.POINTER(ctypes.c_ubyte)]
    b = (ctypes.c_ubyte * 512)()  # DLL 257. bayta yazıyor
    dll.GetDCHU_Data_Buffer(12, b)
    return b[18]

_sic = {"t": -9, "v": 50}
def _sicaklik(t):
    if t - _sic["t"] >= 1:  # sıcaklığı saniyede bir oku
        _sic.update(t=t, v=cpu_sicaklik())
    x = min(max((_sic["v"] - 50) / 40, 0), 1)  # 50°C yeşil -> 90°C kırmızı
    return [hsv(0.33 * (1 - x))] * 4, 1

_ates = {"t": -1, "c": None}
def _atesf(t):
    if t - _ates["t"] >= 0.08:
        _ates.update(t=t, c=[(255, random.randint(40, 110), 0) for _ in range(4)], b=random.uniform(0.45, 1))
    return _ates["c"], _ates["b"]

def _kalp(t):
    x = t % 1.2
    b = max(math.exp(-((x - 0.10) / 0.06) ** 2), 0.7 * math.exp(-((x - 0.38) / 0.07) ** 2))
    return [(255, 0, 0)] * 4, 0.08 + 0.92 * b

EFEKTLER = {
    "nefes":        lambda t: (None, dalga01(t, 4)),
    "gokkusagi":    lambda t: ([hsv(t / 6)] * 4, 1),
    "renkli_nefes": lambda t: ([hsv(math.floor(t / 4) * 0.13)] * 4, dalga01(t, 4)),
    "kalp":         _kalp,
    "polis":        lambda t: ([(255, 0, 0) if int(t / 0.4) % 2 else (0, 0, 255)] * 4, 1),
    "ates":         _atesf,
    "okyanus":      lambda t: ([hsv(0.56 + 0.14 * math.sin(t * 0.6))] * 4, 0.55 + 0.45 * dalga01(t + 0.7, 5)),
    "gun_batimi":   lambda t: ([hsv(0.95 + 0.12 * dalga01(t, 8))] * 4, 1),  # kırmızı-mor-turuncu arası salınım
    "flas":         lambda t: ([(255, 255, 255)] * 4, 1 if (t % 0.5) < 0.12 else 0),
    "sicaklik":     _sicaklik,
}

def calistir(ad, sure=0):  # sure <= 0: durdurulana kadar
    f, t0, son_c, son_b = EFEKTLER[ad], time.time(), None, None
    while sure <= 0 or time.time() - t0 < sure:
        c, b = f(time.time() - t0)
        if c is not None and c != son_c:  # değişmeyeni tekrar gönderme
            renk(*c[0], yaz=False)
            son_c = c
        b = int(255 * b)
        if b != son_b:
            gonder(0xF4000000 | b, False)
            son_b = b
        time.sleep(1 / 30)

def demo(her_biri=10, *adlar):
    for ad in adlar or [a for a in EFEKTLER if a != "flas"]:
        calistir(ad, her_biri)

# Donanım efektleri: LedKeyboardSetting.exe'deki sabitler, tuxedo Clevo listesiyle aynı.
EFEKT = {"nefes": 0x1002A000, "dongu": 0x33010000, "dans": 0x80000000, "tempo": 0x90000000,
         "flas": 0xA0000000, "dalga": 0xB0000000, "rastgele": 0x70000000}

def efekt(ad):
    gonder(EFEKT[ad])

if __name__ == "__main__":
    k, *a = sys.argv[1:]
    if k == "efekt":
        efekt(a[0])
    elif k in EFEKTLER:
        calistir(k, *map(float, a))
    elif k == "demo":
        demo(float(a[0]) if a else 10, *a[1:])
    else:
        {"parlaklik": parlaklik, "renk": renk}[k](*map(int, a))
