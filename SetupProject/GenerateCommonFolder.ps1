$combinedPublishDir = ".\Combined"
$outputWxsPath = ".\GeneratedFiles\CombinedFiles.wxs"

if (Test-Path $combinedPublishDir) {
    Remove-Item -Path $combinedPublishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $combinedPublishDir -Force

if (Test-Path $outputWxsPath) {
    Remove-Item -Path $outputWxsPath -Force
}

Copy-Item -Recurse "..\S1FileSync\bin\Release\publish\*" -Destination $combinedPublishDir -Force -Exclude *.pdb
Copy-Item -Recurse "..\S1FileSyncService\bin\Release\publish\*" -Destination $combinedPublishDir -Force -Exclude *.pdb

& "C:\Program Files (x86)\WiX Toolset v3.11\bin\heat.exe" `
    dir $combinedPublishDir `
    -cg HeatGenerated_CombinedComponents `
    -gg -scom -sreg -sfrag -srd `
    -dr INSTALLFOLDER `
    -out $outputWxsPath `
    -var var.CombinedSourceDir

(Get-Content $outputWxsPath) `
    -replace 'HeatGenerated_CombinedComponents', 'CombinedComponents' `
    -replace '<DirectoryRef Id="TARGETDIR".*?</DirectoryRef>', '' `
    -replace '(\\publish|publish\\|publish/)', '' | Set-Content $outputWxsPath