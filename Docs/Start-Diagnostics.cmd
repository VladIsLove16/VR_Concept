@echo off
setlocal
pushd "%~dp0"
start "" /wait "%~dp0ConveyorTrainer.exe" -interaction-diagnostics -logFile "%~dp0Player-Diagnostics.log"
if exist "%USERPROFILE%\AppData\LocalLow\DefaultCompany\VR_Concept\Diagnostics" start "" "%USERPROFILE%\AppData\LocalLow\DefaultCompany\VR_Concept\Diagnostics"
popd
endlocal
