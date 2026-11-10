param($exe)
$a=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($exe))
try{$t=$a.GetTypes()}catch{$t=$_.Exception.InnerException.Types|?{$_}}
$bf=[Reflection.BindingFlags]'Static,Instance,Public,NonPublic,DeclaredOnly'
$pin=@{}
foreach($ty in $t){foreach($m in $ty.GetMethods($bf)){ if($m.Attributes -band [Reflection.MethodAttributes]::PinvokeImpl){
  $d=$m.GetCustomAttributes([Runtime.InteropServices.DllImportAttribute],$false)|select -First 1
  if($d){$pin[$m.MetadataToken]="$($d.Value)!$($d.EntryPoint)"} }}}
$pin.Values | sort -u | % {"PINVOKE $_"}
foreach($ty in $t){foreach($m in @($ty.GetMethods($bf))+@($ty.GetConstructors($bf))){
  try{$il=$m.GetMethodBody().GetILAsByteArray()}catch{continue}; if(!$il){continue}
  $hist=New-Object Collections.Generic.List[string]
  for($i=0;$i -lt $il.Length;$i++){ $op=$il[$i]
    if($op -ge 0x16 -and $op -le 0x1E){$hist.Add([string]($op-0x16))}
    elseif($op -eq 0x1F){$hist.Add([string][int]$il[$i+1]);$i++}
    elseif($op -eq 0x20){$hist.Add('0x{0:X}' -f [BitConverter]::ToInt32($il,$i+1));$i+=4}
    elseif($op -eq 0x72){try{$hist.Add('"'+$m.Module.ResolveString([BitConverter]::ToInt32($il,$i+1))+'"')}catch{};$i+=4}
    elseif($op -eq 0x28 -or $op -eq 0x6F){ $tok=[BitConverter]::ToInt32($il,$i+1); $i+=4
      try{$cm=$m.Module.ResolveMethod($tok)}catch{continue}
      if($pin.ContainsKey($cm.MetadataToken) -and $pin[$cm.MetadataToken] -match 'DCHU'){
        $n=[Math]::Min(6,$hist.Count); "{0}.{1}: {2}  args<- {3}" -f $ty.Name,$m.Name,$pin[$cm.MetadataToken],($hist.GetRange($hist.Count-$n,$n) -join ' ') } }
  }}}
