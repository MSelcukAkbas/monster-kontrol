param($exe)
# Bir .NET exe'deki InsydeDCHU P/Invoke imzalarını listeler.
$a = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($exe))
try { $t = $a.GetTypes() } catch { $t = $_.Exception.InnerException.Types | ? { $_ } }
$bf = [Reflection.BindingFlags]'Static,Instance,Public,NonPublic,DeclaredOnly'
$out = foreach ($ty in $t) {
  foreach ($m in $ty.GetMethods($bf)) {
    if ($m.Attributes -band [Reflection.MethodAttributes]::PinvokeImpl) {
      $d = $m.GetCustomAttributes([Runtime.InteropServices.DllImportAttribute], $false) | select -First 1
      if ($d.Value -match 'Insyde') {
        $p = ($m.GetParameters() | % { $_.ParameterType.Name + $(if ($_.IsOut) { ' [out]' }) }) -join ', '
        "{0}({1}) -> {2}" -f $d.EntryPoint, $p, $m.ReturnType.Name
      }
    }
  }
}
$out | sort -Unique
