param
(
    [Parameter(Mandatory=$true)]
    [string]$version = $null,
    [Parameter(Mandatory=$true)]
    [string]$codePath = $null
)

# Setup the script
$ErrorActionPreference = "Stop"

# Parse the version out
$versionParts = $version.Split(".", 4)
if ($versionParts.Length -ne 4)
{
    Write-Error "Invalid version string"
}
foreach ($versionPart in $versionParts)
{
    if ($versionPart -as [int] -eq $null)
    {
        Write-Error ("Invalid version - non numeric component: " + $versionPart)
    }
}

# Next let us update the SetupBootstrap assembly info
$existingContents = Get-Content ($codePath + "\Guytp.Networking\Properties\AssemblyInfo.cs")
for ($i = 0; $i -lt $existingContents.Length; $i++)
{
    $line = $existingContents[$i]
    if ($line.StartsWith("[assembly: AssemblyVersion"))
    {
        $existingContents[$i] = "[assembly: AssemblyVersion(`"" +$version + "`")]"
        continue
    }
    if ($line.StartsWith("[assembly: AssemblyFileVersion"))
    {
        $existingContents[$i] = "[assembly: AssemblyFileVersion(`"" +$version + "`")]"
        continue
    }
}
$existingContents | Set-Content ($codePath + "\Guytp.Networking\Properties\AssemblyInfo.cs")
Write-Output ("Updated version in AssemblyInfo: " + ($codePath + "\Guytp.Networking\Properties\AssemblyInfo.cs"))
