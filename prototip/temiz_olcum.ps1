# Soğuk/temiz sistemde tekrar ölçüm (CC Eğlence). Her frekans kendi fan adımlarıyla.
$d = $PSScriptRoot
"=== BOŞTA 60 sn (fan otomatik)"; python -I "$d\izle.py" 60 5
$plan = @(
  @{ mhz = 2900; fanlar = '20,15,10' },
  @{ mhz = 3200; fanlar = '35,25,20' },
  @{ mhz = 3400; fanlar = '60,50,40' },
  @{ mhz = 0;    fanlar = '100,70' }   # 0 = sınırsız: Eğlence modunun tavanı
)
foreach ($p in $plan) {
  $env:FANLAR = $p.fanlar
  python -I "$d\arama.py" 40 $p.mhz
}
"=== BİTTİ"
