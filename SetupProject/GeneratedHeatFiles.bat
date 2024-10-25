@echo off
echo Checking heat command...

where heat
if %ERRORLEVEL% NEQ 0 (
    echo Heat command not found in PATH
    echo Please ensure WiX toolset is installed and the path is correctly set
    echo Current PATH: %PATH%
    pause
    exit /b 1
)

echo Heat command found. Generating WiX source files...
pause

:: MainApp Files
heat dir "..\S1FileSync\bin\Release\net8.0-windows" -gg -g1 -dr MainAppFolder -cg HeatGenerated_MainAppComponents -sfrag -srd -template:fragment -out "GeneratedFiles\MainAppFiles.wxs"
if %ERRORLEVEL% NEQ 0 (
    echo Error generating MainAppFiles.wxs
    pause
    exit /b 1
)

:: Service Files
heat dir "..\S1FileSyncService\bin\Release\net8.0-windows" -gg -g1 -dr ServiceFolder -cg HeatGenerated_ServiceComponents -sfrag -srd -template:fragment -out "GeneratedFiles\ServiceFiles.wxs"
if %ERRORLEVEL% NEQ 0 (
    echo Error generating ServiceFiles.wxs
    pause
    exit /b 1
)

echo WiX source files generated successfully!
pause