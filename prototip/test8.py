# Test 8: fan modu custom (6) ve maxQ (5) — EC eğriyi kendisi uyguluyor mu? kayit.py arka planda çalışırken.
# Sıra: custom 150 sn → otomatik 90 sn → maxq 150 sn → otomatik. Her durumda sonunda 0x69/0x0F (güvenli durum).
import os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan

etiket = os.path.join(os.path.dirname(os.path.abspath(__file__)), "kayit", "etiket.txt")
def adim(ad, sure, mod):
    open(etiket, "w", encoding="utf-8").write(ad)
    print(time.strftime("%H:%M:%S"), ad, "ret", fan.mod(mod), flush=True)
    for _ in range(int(sure // 5)):
        time.sleep(5)
        print("  ", fan.durum(), flush=True)
try:
    adim("test8_custom", 150, "custom")
    adim("test8_otomatik_ara", 90, "otomatik")
    adim("test8_maxq", 150, "maxq")
finally:
    fan.mod("otomatik")
    fan.otomatik()
    open(etiket, "w", encoding="utf-8").write("firmware_otomatik_sonra")
    print(time.strftime("%H:%M:%S"), "güvenli durum:", fan.durum(), flush=True)
