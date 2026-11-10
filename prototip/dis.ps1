param($exe, [int[]]$tokens)
# Küçük IL sökücü: verilen metot belirteçlerini işlem kodu + çözülmüş çağrı/alan/string ile döker.
$a=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($exe))
$mod=$a.ManifestModule
$tek=@{}; $cift=@{}
foreach($f in [Reflection.Emit.OpCodes].GetFields('Public,Static')){ $o=$f.GetValue($null); if($o.Size -eq 1){$tek[[int]$o.Value]=$o}else{$cift[[int]($o.Value -band 0xFF)]=$o} }
foreach($tok in $tokens){
  $m=$mod.ResolveMethod($tok)
  "===== {0:X} {1}.{2}({3})" -f $tok,$m.DeclaringType.Name,$m.Name,(($m.GetParameters()|%{$_.ParameterType.Name}) -join ',')
  $il=$m.GetMethodBody().GetILAsByteArray(); $i=0; $out=New-Object Collections.Generic.List[string]
  while($i -lt $il.Length){
    $at=$i; $b=$il[$i++]; if($b -eq 0xFE){$op=$cift[[int]$il[$i++]]}else{$op=$tek[[int]$b]}
    $arg=''
    switch($op.OperandType){
      'InlineNone' {}
      {$_ -in 'ShortInlineI','ShortInlineVar','ShortInlineBrTarget'} { $v=$il[$i]; if($_ -eq 'ShortInlineBrTarget'){$v=$i+1+[sbyte]$il[$i]}; $arg=$v; $i+=1 }
      {$_ -in 'InlineVar'} { $arg=[BitConverter]::ToUInt16($il,$i); $i+=2 }
      'InlineI' { $arg=[BitConverter]::ToInt32($il,$i); $i+=4 }
      'InlineBrTarget' { $arg=$i+4+[BitConverter]::ToInt32($il,$i); $i+=4 }
      'InlineI8' { $i+=8 } 'InlineR' { $i+=8 } 'ShortInlineR' { $i+=4 }
      'InlineString' { $arg='"'+$mod.ResolveString([BitConverter]::ToInt32($il,$i))+'"'; $i+=4 }
      'InlineSwitch' { $n=[BitConverter]::ToInt32($il,$i); $i+=4+4*$n; $arg="switch[$n]" }
      default { $t2=[BitConverter]::ToInt32($il,$i); $i+=4
        try{ $r=$mod.ResolveMember($t2); $arg=('{0:X}:' -f $t2)+$r.DeclaringType.Name+'.'+$r.Name }catch{ $arg='{0:X}' -f $t2 } }
    }
    $out.Add(("{0:X}:{1} {2}" -f $at,$op.Name,$arg).Trim())
  }
  $out -join ' | '
}
