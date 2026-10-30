param([switch]$SkipRuntimeCheck, [string]$OutputDir = 'outputs/KwekDrop')
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskOutput = Join-Path $taskRoot $OutputDir

if (-not $SkipRuntimeCheck) {
    foreach ($taskFile in @('runtime/python/python.exe', 'runtime/ffmpeg/ffmpeg.exe', 'runtime/ffmpeg/ffprobe.exe', 'runtime/7zip/7z.exe', 'runtime/7zip/7z.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $taskOutput $taskFile))) {
            throw "Missing $taskFile. Run python tools/prepare-runtime.py first, or copy runtime from a complete build."
        }
    }
}

# The .NET SDK: the one on PATH (CI and most machines), else DOTNET_ROOT, else this author's install folder.
$taskDotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $taskDotnet -and $env:DOTNET_ROOT) { $taskDotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe' }
if (-not $taskDotnet -or -not (Test-Path -LiteralPath $taskDotnet)) { $taskDotnet = 'D:\INSTALLED PROGRAMS\dotnet\dotnet.exe' }
& $taskDotnet publish (Join-Path $taskRoot 'src/KwekDrop/KwekDrop.csproj') -c Release -r win-x64 --self-contained true -o $taskOutput --nologo
if ($LASTEXITCODE -ne 0) { throw 'publish failed.' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'src/KwekDrop/backend') -Destination $taskOutput -Recurse -Force
Get-ChildItem -LiteralPath (Join-Path $taskOutput 'backend') -Recurse -Directory -Filter '__pycache__' | Remove-Item -Recurse -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/usage.md') -Destination (Join-Path $taskOutput 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/dependencies.json') -Destination (Join-Path $taskOutput 'dependencies.json') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSE') -Destination (Join-Path $taskOutput 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSES') -Destination $taskOutput -Recurse -Force
# Third-party notices, opened from the tray menu; generated from docs/dependencies.json.
$taskPython = Join-Path $taskOutput 'runtime/python/python.exe'
if (Test-Path -LiteralPath $taskPython) {
    & $taskPython (Join-Path $taskRoot 'tools/make-notices.py') (Join-Path $taskOutput 'THIRD-PARTY-NOTICES.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Generating third-party notices failed.' }
} elseif (-not $SkipRuntimeCheck) { throw 'Missing runtime/python; cannot generate third-party notices.' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/validation') -Destination $taskOutput -Recurse -Force
$taskSource = Join-Path $taskOutput 'source'
New-Item -ItemType Directory -Force -Path $taskSource | Out-Null
$appSrc = (Join-Path $taskRoot 'src/KwekDrop')
Get-ChildItem -LiteralPath $appSrc -Recurse -File | Where-Object {
    $_.Extension -in @('.cs', '.csproj', '.manifest') -and
    $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
} | ForEach-Object {
    $rel = $_.FullName.Substring($appSrc.Length).TrimStart('\', '/')
    $dest = Join-Path $taskSource $rel
    $destDir = Split-Path $dest -Parent
    if (-not (Test-Path -LiteralPath $destDir)) {
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    }
    Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
}
Copy-Item -LiteralPath (Join-Path $taskRoot 'src/KwekDrop/backend') -Destination $taskSource -Recurse -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'src/KwekDrop/assets') -Destination $taskSource -Recurse -Force
Get-ChildItem -LiteralPath $taskSource -Recurse -Include '*.otf', '__pycache__' | Remove-Item -Recurse -Force
Write-Output "Built: $taskOutput/KwekDrop.exe"
