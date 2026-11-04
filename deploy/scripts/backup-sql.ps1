param(
    [Parameter(Mandatory = $true)]
    [string] $ServerInstance,

    [Parameter(Mandatory = $true)]
    [string] $Database,

    [Parameter(Mandatory = $true)]
    [string] $BackupDirectory,

    [switch] $TrustedConnection
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $BackupDirectory | Out-Null
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$target = Join-Path $BackupDirectory "${Database}_$stamp.bak"

if ($TrustedConnection) {
    $auth = "-E"
} else {
    throw "Provide -TrustedConnection for Windows auth, or extend this script for SQL auth via secure secret injection."
}

$sql = "BACKUP DATABASE [$Database] TO DISK=N'$target' WITH INIT, COMPRESSION, CHECKSUM"
Write-Host "Backing up $Database on $ServerInstance to $target"
sqlcmd -S $ServerInstance $auth -Q $sql
Write-Host "Backup completed: $target"
