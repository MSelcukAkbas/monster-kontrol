# ReadAppSettings sayfalarını JSON'a yedekler (salt okuma). Her sayfa ayrı süreçte okunur:
# geçersiz sayfa InsydeDCHU.dll'i çökertebiliyor.
# Kullanım: ayar_yedek.py <cikti.json>   |  iç kullanım: ayar_yedek.py --sayfa N
import sys, os, json, subprocess, ctypes

if sys.argv[1] == "--sayfa":
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import fan
    fan.dll.ReadAppSettings.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.POINTER(ctypes.c_ubyte)]
    b = (ctypes.c_ubyte * 256)()
    r = fan.dll.ReadAppSettings(int(sys.argv[2]), 0, 256, b)
    print(json.dumps({"ret": r, "bytes": list(b)}))
    sys.exit(0)

sonuc = {"_not": "ReadAppSettings(sayfa,0,256) yedegi; Monster uygulamalari kaldirilmadan once (2026-11-10)"}
for p in range(9):
    r = subprocess.run([sys.executable, "-I", os.path.abspath(__file__), "--sayfa", str(p)], capture_output=True, text=True)
    if r.returncode == 0 and r.stdout.strip():
        d = json.loads(r.stdout)
        sonuc[str(p)] = d
        print(f"sayfa {p}: ret {d['ret']}, dolu bayt {sum(1 for x in d['bytes'] if x)}")
    else:
        print(f"sayfa {p}: okunamadı (çıkış {r.returncode})")
json.dump(sonuc, open(sys.argv[1], "w"), indent=1)
print("kaydedildi:", sys.argv[1])
