param($exe, $hedef = 'SetDCHU')
# İki kat: P/Invoke'u doğrudan çağıran sarmalayıcıları bulur, sonra sarmalayıcıları çağıran metotların sabitlerini döker.
$a=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($exe))
try{$t=$a.GetTypes()}catch{$t=$_.Exception.InnerException.Types|?{$_}}
$bf=[Reflection.BindingFlags]'Static,Instance,Public,NonPublic,DeclaredOnly'
$tum=foreach($ty in $t){ @($ty.GetMethods($bf))+@($ty.GetConstructors($bf)) }
$hedefler=@{}
foreach($m in $tum){ if($m.Attributes -band [Reflection.MethodAttributes]::PinvokeImpl){
  $d=$m.GetCustomAttributes([Runtime.InteropServices.DllImportAttribute],$false)|select -First 1
  if($d -and $d.EntryPoint -match $hedef){$hedefler[$m.MetadataToken]=$d.EntryPoint} }}
function Tara($m,$tablo){
  try{$il=$m.GetMethodBody().GetILAsByteArray()}catch{return $null}; if(!$il){return $null}
  $sabit=New-Object Collections.Generic.List[string]; $cagri=@()
  for($i=0;$i -lt $il.Length;$i++){ $op=$il[$i]
    if($op -ge 0x16 -and $op -le 0x1E){$sabit.Add([string]($op-0x16))}
    elseif($op -eq 0x1F){$sabit.Add([string][int]$il[$i+1]);$i++}
    elseif($op -eq 0x20){$sabit.Add('0x{0:X}' -f [BitConverter]::ToInt32($il,$i+1));$i+=4}
    elseif($op -eq 0x72){$i+=4}
    elseif($op -in 0x28,0x6F,0x73){ $tok=[BitConverter]::ToInt32($il,$i+1); $i+=4
      try{$cm=$m.Module.ResolveMethod($tok); if($tablo.ContainsKey($cm.MetadataToken)){$cagri+=$tablo[$cm.MetadataToken]; $sabit.Add('<'+$tablo[$cm.MetadataToken]+'>')}}catch{} }
  }
  if($cagri){ ,@($cagri,$sabit) } else { $null }
}
$sarmal=@{}
foreach($m in $tum){ $r=Tara $m $hedefler; if($r){ $sarmal[$m.MetadataToken]=('S{0:X}:{1}' -f ($m.MetadataToken -band 0xFFFF),(($r[0]|sort -u) -join '+')) } }
"## sarmalayıcılar: " + ($sarmal.Values -join '  ')
foreach($m in $tum){ $r=Tara $m $sarmal; if($r){ '### {0:X} -> {1}' -f $m.MetadataToken,(($r[0]|sort -u) -join ','); '    ' + ($r[1] -join ' ') } }
