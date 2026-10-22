# Lists the built DLL's references to non-public game members, which compile against the publicized lib/ DLLs but throw at run time.
param([string]$Dll, [string]$Managed = "C:\Program Files (x86)\Steam\steamapps\common\SpiderHeck\SpiderHeckApp_Data\Managed")
Add-Type -Path "$env:USERPROFILE\.nuget\packages\mono.cecil\0.11.6\lib\netstandard2.0\Mono.Cecil.dll"
$res = New-Object Mono.Cecil.DefaultAssemblyResolver
foreach ($d in $res.GetSearchDirectories()) { $res.RemoveSearchDirectory($d) }
$res.AddSearchDirectory($Managed)
$rp = New-Object Mono.Cecil.ReaderParameters
$rp.AssemblyResolver = $res
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Dll, $rp)
$mod = $asm.MainModule

function Get-Outers($t) { $l = @(); while ($t) { $l += $t; $t = $t.DeclaringType }; $l }
function Test-Derives($caller, $baseDef) {
  foreach ($c in (Get-Outers $caller)) {
    $b = $c.BaseType
    while ($b) {
      try { $bd = $b.Resolve() } catch { $bd = $null }
      if (-not $bd) { break }
      if ($bd.FullName -eq $baseDef.FullName -and $bd.Module.Assembly.Name.Name -eq $baseDef.Module.Assembly.Name.Name) { return $true }
      $b = $bd.BaseType
    }
  }
  $false
}
function Test-TypeVisible($td, $caller) {
  $t = $td
  while ($t) {
    if ($t.IsNested) {
      if ($t.IsNestedPrivate -or $t.IsNestedAssembly -or $t.IsNestedFamilyAndAssembly) { return $false }
      if (($t.IsNestedFamily -or $t.IsNestedFamilyOrAssembly) -and -not (Test-Derives $caller $t.DeclaringType)) { return $false }
    } elseif (-not $t.IsPublic) { return $false }
    $t = $t.DeclaringType
  }
  $true
}

$found = @{}
$unresolved = @{}
foreach ($type in $mod.GetTypes()) {
  if ($type.Namespace -match '^(YamlDotNet|Newtonsoft|MinVer)' ) { continue }
  foreach ($m in $type.Methods) {
    if (-not $m.HasBody) { continue }
    foreach ($ins in $m.Body.Instructions) {
      $op = $ins.Operand
      if ($op -isnot [Mono.Cecil.MemberReference]) { continue }
      $scopeName = $null
      if ($op -is [Mono.Cecil.TypeReference]) { $scopeName = $op.Scope.Name } else { $scopeName = $op.DeclaringType.Scope.Name }
      if ($scopeName -eq $mod.Name -or $scopeName -eq $asm.Name.Name) { continue }
      try { $def = $op.Resolve() } catch { $def = $null }
      if (-not $def) { $unresolved[$scopeName] = 1 + [int]$unresolved[$scopeName]; continue }
      if ($def.Module.Assembly.Name.Name -eq $asm.Name.Name) { continue }
      $bad = $null
      if ($def -is [Mono.Cecil.TypeDefinition]) {
        if (-not (Test-TypeVisible $def $type)) { $bad = "type" }
      } else {
        $dt = $def.DeclaringType
        if (-not (Test-TypeVisible $dt $type)) { $bad = "declaring type" }
        elseif ($def -is [Mono.Cecil.FieldDefinition] -or $def -is [Mono.Cecil.MethodDefinition]) {
          if ($def.IsPrivate -or $def.IsAssembly -or $def.IsFamilyAndAssembly) { $bad = "member" }
          elseif (($def.IsFamily -or $def.IsFamilyOrAssembly) -and -not (Test-Derives $type $dt)) { $bad = "protected member" }
        }
      }
      if ($bad) {
        $key = "$($def.FullName)  <-  $($type.FullName)::$($m.Name)  [$bad]"
        $found[$key] = 1 + [int]$found[$key]
      }
    }
  }
}
"Non-public references: $($found.Count)"
$found.Keys | Sort-Object
"Unresolved scopes:"
$unresolved.GetEnumerator() | Sort-Object Name | ForEach-Object { "  $($_.Name): $($_.Value)" }
