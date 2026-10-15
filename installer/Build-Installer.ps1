param([string]$Wix = 'wix', [switch]$SkipPublish)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $root 'publish'
$dist = Join-Path $root 'dist'
if (-not $SkipPublish) {
    dotnet publish (Join-Path $root 'src/UniversalSearch.App') -c Release -r win-x64 --self-contained true -o $publish -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE) { throw 'Publish failed' }
}
New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination $publish -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md'),(Join-Path $root 'THIRD-PARTY-NOTICES.md') -Destination $publish -Force
function Id([string]$path) { 'I' + [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($path.ToLowerInvariant()))).Substring(0,24) }
function ComponentGuid([string]$path) { ([Guid]::new([byte[]]([System.Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes('UniversalSearch:0:' + $path.ToLowerInvariant()))[0..15]))).ToString() }
function Xml([string]$value) { [System.Security.SecurityElement]::Escape($value) }
$directories = [Text.StringBuilder]::new()
$components = [Text.StringBuilder]::new()
$refs = [Text.StringBuilder]::new()
$folders = Get-ChildItem $publish -Directory -Recurse | Sort-Object FullName
foreach ($folder in $folders) {
    $relative = [IO.Path]::GetRelativePath($publish,$folder.FullName)
    $parentRelative = [IO.Path]::GetDirectoryName($relative)
    $parentId = if ($parentRelative) { Id $parentRelative } else { 'INSTALLFOLDER' }
    [void]$directories.AppendLine(('<DirectoryRef Id="{0}"><Directory Id="{1}" Name="{2}" /></DirectoryRef>' -f $parentId,(Id $relative),(Xml $folder.Name)))
}
foreach ($file in (Get-ChildItem $publish -File -Recurse)) {
    $relative = [IO.Path]::GetRelativePath($publish,$file.FullName)
    $parentRelative = [IO.Path]::GetDirectoryName($relative)
    $parentId = if ($parentRelative) { Id $parentRelative } else { 'INSTALLFOLDER' }
    $id = Id $relative
    $fileId = if ($relative -eq 'UniversalSearch.exe') { 'AppExe' } else { 'F' + $id }
    [void]$components.AppendLine(('<DirectoryRef Id="{0}"><Component Id="C{1}" Guid="{4}"><File Id="{2}" Source="{3}"/><RegistryValue Root="HKCU" Key="Software\UniversalSearch\Installer" Name="{1}" Type="integer" Value="1" KeyPath="yes"/></Component></DirectoryRef>' -f $parentId,$id,$fileId,(Xml $file.FullName),(ComponentGuid $relative)))
    [void]$refs.AppendLine(('<ComponentRef Id="C{0}"/>' -f $id))
}
$source = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="Universal Search" Manufacturer="Universal Search" Version="0.5.0" UpgradeCode="9FC2BE74-A058-4CBA-91BA-9001BDAAB87C" Scope="perUser" Language="1033">
    <MajorUpgrade DowngradeErrorMessage="A newer version of Universal Search is installed." Schedule="afterInstallInitialize"/>
    <MediaTemplate EmbedCab="yes" CompressionLevel="high"/>
    <Property Id="ARPNOMODIFY" Value="1"/>
    <Property Id="INSTALLFOLDER"><RegistrySearch Id="SavedInstallFolder" Root="HKCU" Key="Software\UniversalSearch\Installer" Name="InstallPath" Type="raw"/></Property>
    <StandardDirectory Id="LocalAppDataFolder"><Directory Id="ProgramsFolder" Name="Programs"><Directory Id="INSTALLFOLDER" Name="UniversalSearch"/></Directory></StandardDirectory>
    <StandardDirectory Id="ProgramMenuFolder"><Directory Id="AppMenu" Name="Universal Search">
      <Component Id="MenuShortcut" Guid="*">
        <Shortcut Id="StartShortcut" Name="Universal Search" Target="[INSTALLFOLDER]UniversalSearch.exe" WorkingDirectory="INSTALLFOLDER"/>
        <RemoveFolder Id="RemoveMenu" On="uninstall"/>
        <RegistryValue Root="HKCU" Key="Software\UniversalSearch\Installer" Name="Menu" Type="integer" Value="1" KeyPath="yes"/>
        <RegistryValue Root="HKCU" Key="Software\UniversalSearch\Installer" Name="InstallPath" Type="string" Value="[INSTALLFOLDER]"/>
        <RemoveFile Id="RemoveStartupShortcut" Directory="StartupFolder" Name="UniversalSearch.lnk" On="uninstall"/>
      </Component>
    </Directory></StandardDirectory>
    <StandardDirectory Id="StartupFolder"/>
    $directories
    $components
    <Feature Id="Main" Title="Universal Search"><ComponentRef Id="MenuShortcut"/>$refs</Feature>
  </Package>
</Wix>
"@
$wxs = Join-Path $dist 'UniversalSearch.wxs'
Set-Content -LiteralPath $wxs -Value $source -Encoding utf8
& $Wix build $wxs -arch x64 -o (Join-Path $dist 'UniversalSearch-0.5.0-win-x64.msi')
if ($LASTEXITCODE) { throw 'MSI build failed' }
Get-FileHash (Join-Path $dist '*.msi') -Algorithm SHA256 | Format-List















