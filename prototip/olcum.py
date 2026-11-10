# Frekans sınırlarına göre performans ölçümü.
# Kullanım: olcum.py <yuk_sn> <sogu_sn> <MHz> [MHz ...]   (0 = sınırsız)
# Her sınır için: PROCFREQMAX ayarla -> soğu -> tam yük; puan = saniyede yapılan iş (milyon döngü/sn).
import sys, os, time, json, subprocess, multiprocessing as mp
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan, izle_cpu
from yuk import _Pdh

PLAN = "cfda36e9-41fd-4b93-b905-94299a13d052"  # Performans planı

DENGELI = "381b4222-f694-41f0-9685-ff5bb260df2e"

def frekans_siniri(mhz):
    subprocess.run(["powercfg", "/setacvalueindex", PLAN, "SUB_PROCESSOR", "PROCFREQMAX", str(mhz)], check=True)
    subprocess.run(["powercfg", "/setactive", PLAN], check=True)

def temizle():
    # Ölçüm sonu: sınırı kaldır VE Dengeli'ye dön (eskiden Performans planı açık kalıyordu).
    frekans_siniri(0)
    subprocess.run(["powercfg", "/setactive", DENGELI], check=True)

def _isci(bitis, sayac, i):
    # Her işçi kendi sayaç hücresine yazar: ortak kilit yok.
    x, n = 0, 0
    while time.time() < bitis:
        for j in range(20000):
            x = (x * 1103515245 + j) & 0xFFFFFFFF
        n += 1
        sayac[i] = n

def olc(mhz, yuk_sn, sogu_sn, pdh):
    frekans_siniri(mhz)
    time.sleep(sogu_sn)
    adet = os.cpu_count()
    sayac = mp.Array("q", adet, lock=False)
    bitis = time.time() + yuk_sn
    ps = [mp.Process(target=_isci, args=(bitis, sayac, i)) for i in range(adet)]
    for p in ps:
        p.start()
    time.sleep(2)  # işçiler ısınsın
    t0, n0, onceki = time.time(), sum(sayac), izle_cpu.zamanlar()
    satir, durdu = [], False
    while time.time() < bitis - 1:
        time.sleep(3)
        d, f, simdi = fan.durum(), pdh.mhz(), izle_cpu.zamanlar()
        yuk = 100 * (1 - (simdi[0] - onceki[0]) / max(simdi[1] - onceki[1], 1)); onceki = simdi
        satir.append((d["cpu_C"], f, yuk, d["cpu_rpm"], d["gpu_rpm"]))
        print(f"  [{mhz or 'serbest'}] {time.time()-t0:4.0f}s yük %{yuk:5.1f}  {f:5.0f} MHz  {d['cpu_C']}°C  fan {d['cpu_rpm']}/{d['gpu_rpm']}", flush=True)
        if d["cpu_C"] >= 95:
            print("  !!! 95°C: test durduruldu, fanlar Max", flush=True)
            fan.mod("max"); durdu = True
            break
    t1, n1 = time.time(), sum(sayac)
    for p in ps:
        p.terminate(); p.join()
    o = lambda k: sum(s[k] for s in satir) / len(satir)
    return {"sinir": mhz or "serbest", "puan": round((n1 - n0) * 20000 / (t1 - t0) / 1e6, 1),
            "mhz": round(o(1)), "C_ort": round(o(0), 1), "C_max": max(s[0] for s in satir),
            "yuk": round(o(2), 1), "fan_cpu": round(o(3)), "fan_gpu": round(o(4)), "durdu": durdu}

if __name__ == "__main__":
    yuk_sn, sogu_sn, *siniri = map(int, sys.argv[1:])
    pdh, sonuc = _Pdh(), []
    try:
        for mhz in siniri:
            print(f"=== Sınır: {mhz or 'serbest'} ({sogu_sn}s soğuma)", flush=True)
            sonuc.append(olc(mhz, yuk_sn, sogu_sn, pdh))
            print("  ->", sonuc[-1], flush=True)
    finally:
        temizle()
        print("Frekans sınırı kaldırıldı, Dengeli plan açık", flush=True)
    print("SONUC_JSON " + json.dumps(sonuc, ensure_ascii=False))
