# YÖNETİCİ olarak çalışır: LibreHardwareMonitor (PawnIO) ile Intel DTS sıcaklıkları ↔ DCHU bayt 18.
# Kullanım: dts_kiyas.ps1 <lhm_klasoru> <cikti_csv> <bosta_sn> <yuk_sn>
param($lhm, $csv, [int]$bosta = 16, [int]$yuk = 30)
$ErrorActionPreference = 'Stop'
$proto = $PSScriptRoot
# Bağımlılıklar pwsh dizininden değil LHM klasöründen çözülsün
# Bağımlılıkları önceden yükle (AssemblyResolve olay işleyicisi runspace dışı iş parçacığında pwsh'i çökertti)
Get-ChildItem $lhm -Filter *.dll | % { try { [void][Reflection.Assembly]::LoadFrom($_.FullName) } catch {} }
Add-Type -Path (Join-Path $lhm 'LibreHardwareMonitorLib.dll')
$pc = [LibreHardwareMonitor.Hardware.Computer]::new(); $pc.IsCpuEnabled = $true; $pc.Open()
$cpu = $pc.Hardware | ? { $_.HardwareType -eq 'Cpu' } | select -First 1

function Ornek($evre) {
  $cpu.Update()
  $t = $cpu.Sensors | ? { $_.SensorType -eq 'Temperature' -and $_.Value -ne $null }
  $pkg = ($t | ? Name -eq 'CPU Package').Value
  $max = ($t | ? Name -match '^Core Max$').Value
  $cores = ($t | ? Name -match '^CPU Core #\d+$' | % { [math]::Round($_.Value) }) -join '/'
  $d = (python -I "$proto\fan.py" durum) -replace "'", '"' | ConvertFrom-Json
  $o = [pscustomobject]@{ zaman = (Get-Date -f 'HH:mm:ss'); evre = $evre; dchu_b18 = $d.cpu_C; dts_paket = [math]::Round($pkg, 1); dts_core_max = [math]::Round($max, 1); cekirdekler = $cores; fan_cpu = $d.cpu_rpm }
  $o | Export-Csv $csv -Append -NoTypeInformation -Encoding utf8
  $o
}

Remove-Item $csv -EA 0
"Sensörler: " + (($cpu.Sensors | ? SensorType -eq 'Temperature' | % Name) -join ', ')
$t0 = Get-Date
while (((Get-Date) - $t0).TotalSeconds -lt $bosta) { Ornek 'bosta' | Out-Host; Start-Sleep 2 }
$yukP = Start-Process python -ArgumentList '-I', "`"$proto\yuk.py`"", $yuk -WindowStyle Hidden -PassThru
$t0 = Get-Date
while (((Get-Date) - $t0).TotalSeconds -lt $yuk -and -not $yukP.HasExited) { Ornek 'yuk' | Out-Host; Start-Sleep 2 }
$yukP.WaitForExit()
$pc.Close()
"BİTTİ"
