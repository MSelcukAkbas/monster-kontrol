# Fan: durum okuma (DCHU 12) ve mod yazma (DCHU 121, payload [mod,0,0,1]).
# Kullanım: fan.py durum | fan.py mod otomatik|max|maxq|custom
import ctypes, os, struct, sys, time
D = r"C:\Windows\System32\DriverStore\FileRepository\acpibridge1.inf_amd64_cedafa39846f03cf"
os.add_dll_directory(D)
dll = ctypes.CDLL(os.path.join(D, "InsydeDCHU.dll"))
dll.SetDCHU_Data.argtypes = [ctypes.c_int, ctypes.POINTER(ctypes.c_ubyte), ctypes.c_int]
dll.GetDCHU_Data_Buffer.argtypes = [ctypes.c_int, ctypes.POINTER(ctypes.c_ubyte)]

MOD = {"otomatik": 0, "max": 1, "maxq": 5, "custom": 6}

def _rpm(hi, lo):
    raw = (hi << 8) | lo
    return int(60.0 / (5.56521739130435e-05 * raw) * 2) if raw else 0

def durum():
    b = (ctypes.c_ubyte * 512)()  # DLL 256 baytlık veriden sonra 257. bayta 0 yazıyor (taşma → çökme)
    dll.GetDCHU_Data_Buffer(12, b)
    return {"cpu_rpm": _rpm(b[2], b[3]), "cpu_%": round(b[16] / 255 * 100), "cpu_C": b[18],
            "gpu_rpm": _rpm(b[4], b[5]), "gpu_%": round(b[19] / 255 * 100), "gpu_C": b[17]}

def mod(ad):
    p = (ctypes.c_ubyte * 4)(MOD[ad], 0, 0, 1)
    return dll.SetDCHU_Data(121, p, 4)

def _gonder(cmd, v):
    p = (ctypes.c_ubyte * 4)(*struct.pack("<I", v))
    return dll.SetDCHU_Data(cmd, p, 4)

# TUXEDO clevo komutları: 0x68 SET_FANSPEED_VALUE (fan1 | fan2<<8, 0-255), 0x69 SET_FANSPEED_AUTO.
def sabit(cpu_yuzde, gpu_yuzde):
    assert 0 <= cpu_yuzde <= 100 and 0 <= gpu_yuzde <= 100
    return _gonder(0x68, round(cpu_yuzde * 2.55) | (round(gpu_yuzde * 2.55) << 8))

def otomatik():
    # 0x69 argümanı fan başına bir bit (TUXEDO SetFansAuto: 0x0F). 0 göndermek etkisiz.
    return _gonder(0x69, 0x0F)

# 0x79 alt komut 0x19: güç profili (TUXEDO). CC'nin kendi kaydını değiştirmez.
PROFIL = {"sessiz": 0, "tasarruf": 1, "performans": 2, "eglence": 3}
def profil(ad):
    return _gonder(0x79, (0x19 << 24) | PROFIL[ad])

if __name__ == "__main__":
    k, *a = sys.argv[1:]
    if k == "mod":
        print("SetDCHU_Data(121) ret:", mod(a[0]))
    elif k == "sabit":
        print("SetDCHU_Data(104) ret:", sabit(int(a[0]), int(a[1] if len(a) > 1 else a[0])))
    elif k == "otomatik":
        print("SetDCHU_Data(105) ret:", otomatik())
    elif k == "profil":
        print("SetDCHU_Data(121) ret:", profil(a[0]))
    print(durum())
