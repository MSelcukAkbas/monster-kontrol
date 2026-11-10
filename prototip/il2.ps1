param($exe, $hedef = 'SetDCHU')
# Hedef P/Invoke'u çağıran (ya da onu çağıran sarmalayıcıyı çağıran) metotlardaki tüm sabitleri ve stringleri döker.
$a=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($exe))
try{$t=$a.GetTypes()}catch{$t=$_.Exception.InnerException.Types|?{$_}}
$bf=[Reflection.BindingFlags]'Static,Instance,Public,NonPublic,DeclaredOnly'
$hedefler=@{}
foreach($ty in $t){foreach($m in $ty.GetMethods($bf)){ if($m.Attributes -band [Reflection.MethodAttributes]::PinvokeImpl){
  $d=$m.GetCustomAttributes([Runtime.InteropServices.DllImportAttribute],$false)|select -First 1
  if($d -and $d.EntryPoint -match $hedef){$hedefler[$m.MetadataToken]=$d.EntryPoint} }}}
function Tara($m){
  try{$il=$m.GetMethodBody().GetILAsByteArray()}catch{return}; if(!$il){return}
  $sabit=New-Object Collections.Generic.List[string]; $cagri=@()
  for($i=0;$i -lt $il.Length;$i++){ $op=$il[$i]
    if($op -ge 0x16 -and $op -le 0x1E){$sabit.Add([string]($op-0x16))}
    elseif($op -eq 0x1F){$sabit.Add([string][int]$il[$i+1]);$i++}
    elseif($op -eq 0x20){$sabit.Add('0x{0:X}' -f [BitConverter]::ToInt32($il,$i+1));$i+=4}
    elseif($op -eq 0x72){try{$sabit.Add('"'+$m.Module.ResolveString([BitConverter]::ToInt32($il,$i+1))+'"')}catch{};$i+=4}
    elseif($op -in 0x28,0x6F,0x73){ $tok=[BitConverter]::ToInt32($il,$i+1); $i+=4
      try{$cm=$m.Module.ResolveMethod($tok); if($hedefler.ContainsKey($cm.MetadataToken)){$cagri+=$hedefler[$cm.MetadataToken]}}catch{} }
  }
  if($cagri){ "### {0}.{1} -> {2}`n    {3}" -f $m.DeclaringType.FullName,$m.Name,(($cagri|sort -u) -join ','),($sabit -join ' ') }
}
foreach($ty in $t){foreach($m in @($ty.GetMethods($bf))+@($ty.GetConstructors($bf))){ Tara $m }}
