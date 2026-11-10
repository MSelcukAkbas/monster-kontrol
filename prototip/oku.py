# Sadece okuma: InsydeDCHU.dll'den fan/sıcaklık/klavye durumunu okur, hiçbir şey yazmaz.
import ctypes, os
D = r"C:\Windows\System32\DriverStore\FileRepository\acpibridge1.inf_amd64_cedafa39846f03cf"
os.add_dll_directory(D)
dll = ctypes.CDLL(os.path.join(D, "InsydeDCHU.dll"))
dll.ReadAppSettings.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.POINTER(ctypes.c_ubyte)]
dll.GetDCHU_Data_Buffer.argtypes = [ctypes.c_int, ctypes.POINTER(ctypes.c_ubyte)]

def ras(page, off, n=1):
    b = (ctypes.c_ubyte * n)()
    r = dll.ReadAppSettings(page, off, n, b)
    return r, list(b)

buf = (ctypes.c_ubyte * 512)()  # DLL 257. bayta yazıyor
r = dll.GetDCHU_Data_Buffer(12, buf)
raw = (buf[2] << 8) | buf[3]
rpm = int(60.0 / (5.56521739130435e-05 * raw) * 2) if raw else 0
print(f"GetDCHU_Data_Buffer(12) ret={r} ilk32={list(buf[:32])}")
print(f"FAN: {rpm} RPM, %{int(buf[16]/255*100)}, CPU {buf[18]}°C")
raw2 = (buf[4] << 8) | buf[5]
rpm2 = int(60.0 / (5.56521739130435e-05 * raw2) * 2) if raw2 else 0
print(f"FAN2 (GPU): {rpm2} RPM, %{int(buf[19]/255*100)}, GPU? {buf[17]}°C")
print("Fan modu (4,5):", ras(4, 5, 4), " offset (4,7):", ras(4, 7, 4))
print("Güç modu (1,1):", ras(1, 1))
print("Klavye: parlaklık", ras(2, 35), "renk", ras(2, 81, 3), "mod", ras(2, 32), "açık mı", ras(2, 84))
