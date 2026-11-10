# YÖNETİCİ: frekans sınırı başına paket gücü (LHM/PawnIO) + iş puanı (olcum.py). Fanlar sabit.
# Kullanım: perf_watt.ps1 <lhm_klasoru> <cikti_klasoru> <fan_yuzde> <yuk_sn> <sogu_sn> <MHz...>
param($lhm, $cikti, [int]$fanY = 60, [int]$yuk = 45, [int]$sogu = 20, [int[]]$mhzler = @(2900, 3200, 3400, 3600, 0))
$ErrorActionPreference = 'Stop'
$proto = $PSScriptRoot
# Bağımlılıkları önceden yükle (AssemblyResolve olay işleyicisi runspace dışı iş parçacığında pwsh'i çökertti)
Get-ChildItem $lhm -Filter *.dll | % { try { [void][Reflection.Assembly]::LoadFrom($_.FullName) } catch {} }
Add-Type -Path (Join-Path $lhm 'LibreHardwareMonitorLib.dll')
$pc = [LibreHardwareMonitor.Hardware.Computer]::new(); $pc.IsCpuEnabled = $true; $pc.Open()
$cpu = $pc.Hardware | ? { $_.HardwareType -eq 'Cpu' } | select -First 1
$cpu.Update()
$cpu.Sensors | sort SensorType, Name | % { "{0,-12} {1,-35} {2}" -f $_.SensorType, $_.Name, $_.Value } | Set-Content (Join-Path $cikti 'lhm_sensorler.txt') -Encoding utf8

$ozet = @()
foreach ($mhz in $mhzler) {
  python -I "$proto\fan.py" sabit $fanY $fanY | Out-Null
  $log = Join-Path $cikti "perf_watt_$mhz.log"
  $p = Start-Process python -ArgumentList '-I', "`"$proto\olcum.py`"", $yuk, $sogu, $mhz -RedirectStandardOutput $log -WindowStyle Hidden -PassThru
  $orn = @()
  while (Get-Process -Id $p.Id -EA 0) {  # $p.HasExited yönetici oturumunda güncellenmedi (takıldı)
    $cpu.Update()
    $g = ($cpu.Sensors | ? { $_.SensorType -eq 'Power' -and $_.Name -eq 'CPU Package' }).Value
    $y = ($cpu.Sensors | ? { $_.SensorType -eq 'Load' -and $_.Name -eq 'CPU Total' }).Value
    $t = ($cpu.Sensors | ? { $_.SensorType -eq 'Temperature' -and $_.Name -eq 'CPU Package' }).Value
    if ($y -ge 95) { $orn += [pscustomobject]@{ W = $g; C = $t } }
    Start-Sleep -Milliseconds 1500
  }
  $puan = (Get-Content $log | sls "'puan': ([\d.]+)").Matches | select -Last 1 | % { [double]$_.Groups[1].Value }
  $mhzOlc = (Get-Content $log | sls "'mhz': (\d+)").Matches | select -Last 1 | % { [int]$_.Groups[1].Value }
  $W = ($orn.W | measure -Average).Average
  $ozet += [pscustomobject]@{ sinir = $(if ($mhz) { $mhz } else { 'serbest' }); mhz = $mhzOlc; puan = $puan; watt = [math]::Round($W, 1); C_ort = [math]::Round(($orn.C | measure -Average).Average, 1); puan_watt = $(if ($W) { [math]::Round($puan / $W, 2) }); ornek = $orn.Count }
  $ozet[-1] | Out-Host
}
python -I "$proto\fan.py" otomatik | Out-Null
$pc.Close()
$ozet | Export-Csv (Join-Path $cikti 'perf_watt.csv') -NoTypeInformation -Encoding utf8
"BİTTİ"
