rem this script should be located inside %NHM_ROOT_PATH%\tools
rem set current directory in PATH
set PATH=%cd%;%PATH%
cd ..\
echo %cd%
rem pause
.\tools\CreateSnapshot.exe -push %cd%
pause
