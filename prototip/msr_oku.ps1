# YÖNETİCİ: PawnIO (LHM IntelMsr) ile i5-10200H (Comet Lake) güç/termal MSR'larını YALNIZCA OKUR.
# Her CC güç profili için PL1/PL2 ve kısma nedenlerini döker. Sonunda profili eğlence'ye döndürür.
# Kullanım: msr_oku.ps1 <lhm_klasoru> <cikti_txt>
param($lhm, $cikti)
$ErrorActionPreference = 'Stop'
$proto = $PSScriptRoot
Get-ChildItem $lhm -Filter *.dll | % { try { [void][Reflection.Assembly]::LoadFrom($_.FullName) } catch {} }
Add-Type -Path (Join-Path $lhm 'LibreHardwareMonitorLib.dll')
$msr = [LibreHardwareMonitor.PawnIo.IntelMsr]::new()
function M([uint32]$i) { [uint64]$v = 0; if ($msr.ReadMsr($i, [ref]$v)) { $v } else { $null } }
function Bit($v, $b) { [int](($v -shr $b) -band 1) }
function Alan($v, $lo, $n) { ($v -shr $lo) -band ([uint64][math]::Pow(2, $n) - 1) }

$out = New-Object Collections.Generic.List[string]
function Yaz($s) { $out.Add($s); $s | Out-Host }

$birim = M 0x606
$pu = 1 / [math]::Pow(2, (Alan $birim 0 4)); $tu = 1 / [math]::Pow(2, (Alan $birim 16 4))
Yaz ("0x606 RAPL birimleri: ham {0:X}  güç birimi {1} W, zaman birimi {2} s" -f $birim, $pu, $tu)
$plat = M 0xCE; Yaz ("0xCE PLATFORM_INFO: taban oran {0} (= {1} MHz)" -f (Alan $plat 8 8), ((Alan $plat 8 8) * 100))
$tr = M 0x1AD; Yaz ("0x1AD TURBO oranları 1/2/3/4 çekirdek: {0}/{1}/{2}/{3} (x100 MHz)" -f (Alan $tr 0 8), (Alan $tr 8 8), (Alan $tr 16 8), (Alan $tr 24 8))
$tt = M 0x1A2; Yaz ("0x1A2 TEMPERATURE_TARGET: TjMax {0}°C, TCC ofset {1} → etkin sınır {2}°C (ham {3:X})" -f (Alan $tt 16 8), (Alan $tt 24 6), ((Alan $tt 16 8) - (Alan $tt 24 6)), $tt)
$cl = M 0x601; Yaz ("0x601 VR akım sınırı: {0} A (kilit {1})" -f ((Alan $cl 0 13) / 8), (Bit $cl 31))

function Sinirlar($etiket) {
  $pl = M 0x610
  $pl1 = (Alan $pl 0 15) * $pu; $pl2 = (Alan $pl 32 15) * $pu
  $y = Alan $pl 17 5; $z = Alan $pl 22 2; $tw = [math]::Pow(2, $y) * (1 + $z / 4) * $tu
  $lr = M 0x64F; $pt = M 0x1B1
  $ad = @{0='PROCHOT';1='Termal';4='Residency';5='RATL';6='VR termal';7='VR TDC';8='Diğer/EDP';10='PL1';11='PL2';12='Max turbo';13='Turbo geçiş'}
  $simdi = ($ad.Keys | sort | ? { Bit $lr $_ } | % { $ad[$_] }) -join ','
  $gecmis = ($ad.Keys | sort | ? { Bit $lr ($_ + 16) } | % { $ad[$_] }) -join ','
  Yaz ("[{0}] PL1 {1} W (açık {2}, pencere {3:N1} s)  PL2 {4} W (açık {5})  kilit {6}" -f $etiket, $pl1, (Bit $pl 15), $tw, $pl2, (Bit $pl 47), (Bit $pl 63))
  Yaz ("      0x64F kısma ŞİMDİ: [{0}]  GEÇMİŞ: [{1}]   0x1B1 paket termal: durum {2} geçmiş {3}, PROCHOT {4}, sınıra {5}°C" -f $simdi, $gecmis, (Bit $pt 0), (Bit $pt 1), (Bit $pt 2), (Alan $pt 16 7))
}

try {
  Sinirlar 'mevcut'
  foreach ($p in 'sessiz', 'tasarruf', 'eglence', 'performans') {
    python -I "$proto\fan.py" profil $p | Out-Null
    Start-Sleep 3
    Sinirlar $p
  }
} finally {
  python -I "$proto\fan.py" profil eglence | Out-Null
  $msr.Close()
  $out | Set-Content $cikti -Encoding utf8
  "BİTTİ (profil: eğlence)" | Out-Host
}
