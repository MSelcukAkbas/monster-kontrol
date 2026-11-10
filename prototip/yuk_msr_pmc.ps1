# YÖNETİCİ: (1) tam yük altında kısma nedenleri (0x64F, salt okuma), (2) ETW PMC kaydı denemesi.
# Kullanım: yuk_msr_pmc.ps1 <lhm_klasoru> <cikti_klasoru>
param($lhm, $cikti)
$ErrorActionPreference = 'Stop'
$proto = $PSScriptRoot
Get-ChildItem $lhm -Filter *.dll | % { try { [void][Reflection.Assembly]::LoadFrom($_.FullName) } catch {} }
Add-Type -Path (Join-Path $lhm 'LibreHardwareMonitorLib.dll')
$msr = [LibreHardwareMonitor.PawnIo.IntelMsr]::new()
function M([uint32]$i) { [uint64]$v = 0; if ($msr.ReadMsr($i, [ref]$v)) { $v } else { $null } }
function Bit($v, $b) { [int](($v -shr $b) -band 1) }
$ad = @{0='PROCHOT';1='Termal';4='Residency';5='RATL';6='VR termal';7='VR TDC';8='Diğer/EDP';10='PL1';11='PL2';12='Max turbo';13='Turbo geçiş'}
$log = New-Object Collections.Generic.List[string]
function Yaz($s) { $log.Add($s); $s | Out-Host }
$isciler = @()
try {
  # --- 1) tam yükte kısma nedenleri (fan otomatik, eğlence) ---
  $isciler = 1..8 | % { Start-Process python -ArgumentList '-I', "`"$proto\mesgul.py`"", 40 -WindowStyle Hidden -PassThru }
  # --- 2) aynı yük sırasında ETW PMC kaydı (10 sn) ---
  Start-Sleep 3
  $etl = Join-Path $cikti 'pmc.etl'
  wpr -cancel 2>$null | Out-Null
  $r = wpr -start "$proto\pmc.wprp" -filemode 2>&1; Yaz "wpr start: $r (çıkış $LASTEXITCODE)"
  for ($i = 0; $i -lt 10; $i++) {
    Start-Sleep 3
    $lr = M 0x64F; $pt = M 0x1B1
    $simdi = ($ad.Keys | sort | ? { Bit $lr $_ } | % { $ad[$_] }) -join ','
    $gecmis = ($ad.Keys | sort | ? { Bit $lr ($_ + 16) } | % { $ad[$_] }) -join ','
    Yaz ("{0,2}. 0x64F ham {1:X8}  ŞİMDİ [{2}]  GEÇMİŞ [{3}]  | 0x1B1 sınıra {4}°C" -f $i, $lr, $simdi, $gecmis, (($pt -shr 16) -band 0x7F))
    if ($i -eq 3) { $r = wpr -stop $etl 2>&1; Yaz "wpr stop: $r (çıkış $LASTEXITCODE)" }
  }
} finally {
  $isciler | % { Stop-Process -Id $_.Id -EA 0 }
  wpr -cancel 2>$null | Out-Null
  $msr.Close()
  $log | Set-Content (Join-Path $cikti 'yuk_msr_pmc.txt') -Encoding utf8
  "BİTTİ" | Out-Host
}
