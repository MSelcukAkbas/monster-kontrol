# Klavye parlaklığı: argümansız sadece okur (DCHU 61); argümanla yazar (DCHU 39, payload [seviye,0,0,0]).
import ctypes, os, sys
D = r"C:\Windows\System32\DriverStore\FileRepository\acpibridge1.inf_amd64_cedafa39846f03cf"
os.add_dll_directory(D)
dll = ctypes.CDLL(os.path.join(D, "InsydeDCHU.dll"))
dll.GetDCHU_Data_Integer.argtypes = [ctypes.c_int, ctypes.POINTER(ctypes.c_int)]
dll.SetDCHU_Data.argtypes = [ctypes.c_int, ctypes.POINTER(ctypes.c_ubyte), ctypes.c_int]

def oku():
    v = ctypes.c_int()
    r = dll.GetDCHU_Data_Integer(61, ctypes.byref(v))
    return r, v.value, v.value & 0xFF

print("Önce:", oku())
if len(sys.argv) > 1:
    lvl = int(sys.argv[1])
    assert 0 <= lvl <= 4, "parlaklık 0..4 olmalı"
    p = (ctypes.c_ubyte * 4)(lvl, 0, 0, 0)
    print("SetDCHU_Data(39) ret:", dll.SetDCHU_Data(39, p, 4))
    print("Sonra:", oku())
