# Her frekans sınırı için, tam yükte sıcaklığı hedefte tutan en düşük sabit fan hızını arar.
# Kullanım: arama.py <adim_sn> <MHz> [MHz ...]
# Fanlar %100'den başlar, her adımda %10 azalır; adımın son 15 sn'si: ort <= 92 ve max <= 95 ise geçer.
import sys, os, time, json, multiprocessing as mp
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fan
from yuk import _Pdh
from olcum import frekans_siniri, temizle, _isci

ORT_SINIR, MAX_SINIR, ACIL = 92, 95, 97
FANLAR = [int(x) for x in os.environ.get("FANLAR", "100,90,80,70,60,50,40,35").split(",")]

def adim(yuzde, adim_sn, pdh, mhz):
    fan.sabit(yuzde, yuzde)
    t0, pencere = time.time(), []
    while time.time() - t0 < adim_sn:
        time.sleep(2)
        d, f = fan.durum(), pdh.mhz()
        if time.time() - t0 >= adim_sn - 15:
            pencere.append((d["cpu_C"], f))
        print(f"  [{mhz}] fan %{yuzde:3}  {d['cpu_C']}°C  {f:5.0f} MHz  rpm {d['cpu_rpm']}/{d['gpu_rpm']}", flush=True)
        if d["cpu_C"] >= ACIL:
            print(f"  !!! {ACIL}°C: adım başarısız", flush=True)
            fan.mod("max")
            return False, d["cpu_C"], d["cpu_C"], f
    ort = sum(c for c, _ in pencere) / len(pencere)
    mx = max(c for c, _ in pencere)
    frek = sum(f for _, f in pencere) / len(pencere)
    return ort <= ORT_SINIR and mx <= MAX_SINIR, round(ort, 1), mx, round(frek)

def frekans_ara(mhz, adim_sn, pdh):
    frekans_siniri(mhz)
    fan.sabit(100, 100)
    time.sleep(10)  # önceki testten kalan ısıyı at
    adet = os.cpu_count()
    sayac = mp.Array("q", adet, lock=False)
    bitis = time.time() + adim_sn * len(FANLAR) + 30
    ps = [mp.Process(target=_isci, args=(bitis, sayac, i)) for i in range(adet)]
    for p in ps:
        p.start()
    time.sleep(2)
    t0, n0 = time.time(), sum(sayac)
    en_iyi, kayit = None, []
    try:
        for y in FANLAR:
            gecti, ort, mx, frek = adim(y, adim_sn, pdh, mhz)
            kayit.append({"fan": y, "gecti": gecti, "C_ort": ort, "C_max": mx, "mhz": frek})
            print(f"  -> fan %{y}: {'GEÇTİ' if gecti else 'KALDI'} ort {ort}°C max {mx}°C {frek} MHz", flush=True)
            if not gecti:
                break
            en_iyi = kayit[-1]
    finally:
        puan = (sum(sayac) - n0) * 20000 / (time.time() - t0) / 1e6
        for p in ps:
            p.terminate(); p.join()
        fan.mod("max")  # bir sonraki frekansa güvenli geç
    return {"sinir": mhz, "en_dusuk_fan": en_iyi["fan"] if en_iyi else None, "en_iyi": en_iyi,
            "puan": round(puan, 1), "adimlar": kayit}

if __name__ == "__main__":
    adim_sn, *siniri = map(int, sys.argv[1:])
    pdh, sonuc = _Pdh(), []
    try:
        for mhz in siniri:
            print(f"=== {mhz} MHz", flush=True)
            sonuc.append(frekans_ara(mhz, adim_sn, pdh))
            print("  SONUÇ:", {k: v for k, v in sonuc[-1].items() if k != "adimlar"}, flush=True)
    finally:
        temizle()
        fan.otomatik()
        print("Frekans sınırı kaldırıldı, Dengeli plan, fanlar otomatik", flush=True)
    print("SONUC_JSON " + json.dumps(sonuc, ensure_ascii=False))
