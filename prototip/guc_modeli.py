# Kalibrasyon CSV'sinden sürücüsüz güç tahmin modeli uydurur ve hatasını ölçer.
# Girdiler (uygulamanın sürücüsüz bildiği): yük % (u), ortalama frekans MHz (f). Hedef: paket gücü W.
# Kullanım: guc_modeli.py <kalibrasyon.csv>
import csv, sys, itertools

satir = [r for r in csv.DictReader(open(sys.argv[1], encoding="utf-8-sig"))]
v = [(float(r["yuk"].replace(",", ".")) / 100, float(r["mhz"]) / 1000, float(r["watt"].replace(",", "."))) for r in satir]
v = [x for x in v if x[1] > 0]

def en_kucuk_kareler(X, y):
    # Normal denklemler (X'X)b = X'y, küçük boyutlu Gauss eleme
    n = len(X[0])
    A = [[sum(X[k][i] * X[k][j] for k in range(len(X))) for j in range(n)] + [sum(X[k][i] * y[k] for k in range(len(X)))] for i in range(n)]
    for i in range(n):
        p = max(range(i, n), key=lambda r: abs(A[r][i])); A[i], A[p] = A[p], A[i]
        for r in range(n):
            if r != i:
                c = A[r][i] / A[i][i]
                A[r] = [a - c * b for a, b in zip(A[r], A[i])]
    return [A[i][n] / A[i][i] for i in range(n)]

def degerlendir(ad, tahmin):
    hata = [abs(tahmin(u, f) - w) for u, f, w in v]
    yuzde = [abs(tahmin(u, f) - w) / w * 100 for u, f, w in v if w > 3]
    print(f"{ad:42} ort. mutlak hata {sum(hata)/len(hata):5.2f} W   ort. %{sum(yuzde)/len(yuzde):5.1f}   en kötü {max(hata):5.2f} W")

y = [w for _, _, w in v]
# M1: P = a + b·u·f³   (dinamik güç ~ V²f, V ~ f)
a, b = en_kucuk_kareler([[1, u * f ** 3] for u, f, _ in v], y)
degerlendir(f"M1: {a:.2f} + {b:.3f}·u·f³", lambda u, f: a + b * u * f ** 3)
# M2: P = a + b·u + c·u·f³
a2, b2, c2 = en_kucuk_kareler([[1, u, u * f ** 3] for u, f, _ in v], y)
degerlendir(f"M2: {a2:.2f} + {b2:.2f}·u + {c2:.3f}·u·f³", lambda u, f: a2 + b2 * u + c2 * u * f ** 3)
# M3: P = a + b·f + c·u·f³  (boşta da frekansa bağlı taban)
a3, b3, c3 = en_kucuk_kareler([[1, f, u * f ** 3] for u, f, _ in v], y)
degerlendir(f"M3: {a3:.2f} + {b3:.2f}·f + {c3:.3f}·u·f³", lambda u, f: a3 + b3 * f + c3 * u * f ** 3)

# M4: P = a + b·f + c·u·f^k, k veriden (ızgara arama)
def m4(k):
    a, b, c = en_kucuk_kareler([[1, f, u * f ** k] for u, f, _ in v], y)
    return sum((a + b * f + c * u * f ** k - w) ** 2 for u, f, w in v), (a, b, c)
k4 = min((k / 10 for k in range(20, 81)), key=lambda k: m4(k)[0])
a4, b4, c4 = m4(k4)[1]
degerlendir(f"M4: {a4:.2f} + {b4:.2f}·f + {c4:.4f}·u·f^{k4:.1f}", lambda u, f: a4 + b4 * f + c4 * u * f ** k4)

print(f"\nAdım ortalamaları (sınır, işçi → yük %, MHz, ölçülen W, M4 tahmini W):")
for (s, n), grup in itertools.groupby(sorted(satir, key=lambda r: (r["sinir"], int(r["isci"]))), key=lambda r: (r["sinir"], int(r["isci"]))):
    g = list(grup)
    u = sum(float(r["yuk"].replace(",", ".")) for r in g) / len(g) / 100
    f = sum(float(r["mhz"]) for r in g) / len(g) / 1000
    w = sum(float(r["watt"].replace(",", ".")) for r in g) / len(g)
    print(f"  {s:>5} {n}  →  %{u*100:5.1f}  {f*1000:5.0f} MHz  {w:5.1f} W   tahmin {a4 + b4*f + c4*u*f**k4:5.1f} W")
