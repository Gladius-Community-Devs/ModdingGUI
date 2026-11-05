# Remove the mark-of-the-web from designer resource files.
#
# Why this exists: Windows tags files that arrive from a zip or a network share with a
# Zone.Identifier stream. Visual Studio's MSBuild refuses to process a marked .resx
# ("MSB3821 ... Internet or Restricted zone") even when the SDK's MSBuild tolerates it, which
# means a copied repo can fail to build on one machine and not another. Clearing the stream before
# the build removes that difference.
#
# Usage:  powershell -File build/unblock-resources.ps1 -Directory <project folder>
# Quiet by design: prints only when it actually clears something.

param(
    [Parameter(Mandatory = $true)]
    [string]$Directory
)

if (-not (Test-Path -LiteralPath $Directory)) { exit 0 }

$extensions = @('.resx', '.ico')
$cleared = 0

Get-ChildItem -LiteralPath $Directory -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $extensions -contains $_.Extension.ToLowerInvariant() } |
    ForEach-Object {
        $zone = Get-Item -LiteralPath $_.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue
        if ($null -eq $zone) { return }

        Remove-Item -LiteralPath $_.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue
        if ($null -eq (Get-Item -LiteralPath $_.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue)) {
            $cleared++
            Write-Output "unblocked: $($_.FullName)"
        }
    }

if ($cleared -gt 0) { Write-Output "Cleared the mark-of-the-web from $cleared file(s)." }
exit 0
