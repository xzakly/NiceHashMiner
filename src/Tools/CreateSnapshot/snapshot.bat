rem this script should be located inside %NHM_ROOT_PATH%\tools
rem set current directory in PATH
set PATH=%cd%;%PATH%
cd ..\
echo %cd%
@echo off

for %%x in (
		"reg query "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography" /v "MachineGuid""
		"reg query "HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v "NiceHashMiner""
		"reg query "HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\Windows Error Reporting" /v "DontShowUI""
		"reg query "HKEY_CURRENT_USER\SOFTWARE\06003e5b-46fa-4c91-8fa8-27c184bdc001""
		"reg query "HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\06003e5b-46fa-4c91-8fa8-27c184bdc001""

	) do (
		for /f "tokens=1-3" %%a in ('%%~x') do (
			if errorlevel 0 (
				echo %%a %%b %%c >> registrySnapshot.txt
			)
		)
	)
@echo on

.\tools\CreateSnapshot.exe -push %cd%

del registrySnapshot.txt

pause
