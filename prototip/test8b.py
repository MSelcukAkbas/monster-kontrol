# Test 8b: custom moddayken sabit hız (0x68) eğriyi ezer mi, ardından 0x69/0x0F custom eğriye mi firmware otomatiğine mi döndürür?
# Sıra: custom 40 sn → sabit %40/%40 30 sn → 0x69/0x0F 40 sn → otomatik. Sonunda her durumda güvenli durum.
import os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan

etiket = os.path.join(os.path.dirname(os.path.abspath(__file__)), "kayit", "etiket.txt")
def adim(ad, sure, islem):
    open(etiket, "w", encoding="utf-8").write(ad)
    islem()
    print(time.strftime("%H:%M:%S"), ad, flush=True)
    for _ in range(int(sure // 5)):
        time.sleep(5)
        print("  ", fan.durum(), flush=True)
try:
    adim("test8b_custom", 40, lambda: fan.mod("custom"))
    adim("test8b_sabit40", 30, lambda: fan.sabit(40, 40))
    adim("test8b_0x69_sonrasi", 40, fan.otomatik)
finally:
    fan.mod("otomatik")
    fan.otomatik()
    open(etiket, "w", encoding="utf-8").write("firmware_otomatik_sonra")
    print(time.strftime("%H:%M:%S"), "güvenli durum:", fan.durum(), flush=True)
