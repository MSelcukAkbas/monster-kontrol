# Toplam CPU zamanları (boşta, toplam) — iki ölçüm arasındaki farktan yük yüzdesi hesaplanır.
import ctypes

class _FT(ctypes.Structure):
    _fields_ = [("lo", ctypes.c_uint32), ("hi", ctypes.c_uint32)]

def zamanlar():
    i, k, u = _FT(), _FT(), _FT()
    ctypes.windll.kernel32.GetSystemTimes(ctypes.byref(i), ctypes.byref(k), ctypes.byref(u))
    f = lambda x: (x.hi << 32) | x.lo
    return f(i), f(k) + f(u)
