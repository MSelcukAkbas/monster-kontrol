# YÖNETİCİ: güç tahmin modeli için kalibrasyon. Dengeli plan, frekans sınırı × iş parçacığı sayısı.
# Her adım: N python meşgul döngüsü, LHM ile paket gücü + yük + ortalama çekirdek frekansı + sıcaklık.
# Kullanım: kalibrasyon.ps1 <lhm_klasoru> <cikti_csv> <adim_sn>
param($lhm, $csv, [int]$adim = 25, [string]$adimlar = '2900:0,2,4,6,8;3400:0,2,4,6,8;0:0,2,4,6,8')  # "sınır:işçiler;..." CSV'ye eklenir
$ErrorActionPreference = 'Stop'
$proto = $PSScriptRoot
$DENGELI = '381b4222-f694-41f0-9685-ff5bb260df2e'
# Bağımlılıkları önceden yükle (AssemblyResolve olay işleyicisi runspace dışı iş parçacığında pwsh'i çökertti)
Get-ChildItem $lhm -Filter *.dll | % { try { [void][Reflection.Assembly]::LoadFrom($_.FullName) } catch {} }
Add-Type -Path (Join-Path $lhm 'LibreHardwareMonitorLib.dll')
$pc = [LibreHardwareMonitor.Hardware.Computer]::new(); $pc.IsCpuEnabled = $true; $pc.Open()
$cpu = $pc.Hardware | ? { $_.HardwareType -eq 'Cpu' } | select -First 1
function S($tip, $ad) { ($cpu.Sensors | ? { $_.SensorType -eq $tip -and $_.Name -eq $ad }).Value }

python -I "$proto\fan.py" sabit 60 60 | Out-Null
try {
  foreach ($grup in $adimlar.Split(';')) {
    $mhz = [int]$grup.Split(':')[0]
    powercfg /setacvalueindex $DENGELI SUB_PROCESSOR PROCFREQMAX $mhz; powercfg /setactive $DENGELI
    foreach ($n in ($grup.Split(':')[1].Split(',') | % { [int]$_ })) {
      $bitis = (Get-Date).AddSeconds($adim)
      $isciler = @(1..$n | ? { $n -gt 0 } | % {
        Start-Process python -ArgumentList '-I', "`"$proto\mesgul.py`"", $adim -WindowStyle Hidden -PassThru })
      Start-Sleep 5  # oturma
      while ((Get-Date) -lt $bitis) {
        $cpu.Update()
        $saat = ($cpu.Sensors | ? { $_.SensorType -eq 'Clock' -and $_.Name -match '^CPU Core #' } | measure Value -Average).Average
        [pscustomobject]@{ sinir = $mhz; isci = $n; yuk = [math]::Round((S Load 'CPU Total'), 1); mhz = [math]::Round($saat); watt = [math]::Round((S Power 'CPU Package'), 2); cekirdek_w = [math]::Round((S Power 'CPU Cores'), 2); C = (S Temperature 'CPU Package') } |
          Export-Csv $csv -Append -NoTypeInformation -Encoding utf8
        Start-Sleep -Milliseconds 1500
      }
      $isciler | % { Stop-Process -Id $_.Id -EA 0 }
      "sınır $mhz, işçi $n bitti" | Out-Host
    }
  }
} finally {
  powercfg /setacvalueindex $DENGELI SUB_PROCESSOR PROCFREQMAX 0; powercfg /setactive $DENGELI
  python -I "$proto\fan.py" otomatik | Out-Null
  $pc.Close()
  "BİTTİ (Dengeli PROCFREQMAX=0, fanlar otomatik)"
}
