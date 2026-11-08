@echo off
setlocal
cd /d "%~dp0"
set "JH_COMPILER=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%JH_COMPILER%" set "JH_COMPILER=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%JH_COMPILER%" (
  echo .NET Framework compiler not found. Windows 7 SP1/8.1/10/11 with .NET Framework 4.8 is required.
  pause
  exit /b 1
)
"%JH_COMPILER%" /nologo /target:winexe /win32manifest:JHRemoteSupport.manifest /platform:anycpu /optimize+ /codepage:65001 /out:JHRemoteSupport.exe /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll RemoteSupport.cs ManagedWebSocket.cs RuntimeRequirements.cs AutoUpdater.cs UpdateTrust.cs
if errorlevel 1 (
  echo Build failed. Please keep the message above for troubleshooting.
  pause
  exit /b 1
)
start "" /wait "%~dp0JHRemoteSupport.exe" --verify-network-policy
if errorlevel 1 (
  echo Modern TLS configuration check failed. Please send this message for troubleshooting.
  pause
  exit /b 1
)
start "" /wait "%~dp0JHRemoteSupport.exe" --verify-setup
if errorlevel 1 (
  echo Connection setup or teacher branding validation failed. Please send this message for troubleshooting.
  pause
  exit /b 1
)
set "JH_CHECK_DIR=%TEMP%\JHRemoteSupport-check-%RANDOM%-%RANDOM%"
mkdir "%JH_CHECK_DIR%"
copy /y "JHRemoteSupport.exe" "%JH_CHECK_DIR%\JHSupport-000007-tls-build-check.invalid.exe" >nul
start "" /wait "%JH_CHECK_DIR%\JHSupport-000007-tls-build-check.invalid.exe" --verify-profile
if errorlevel 1 (
  rmdir /s /q "%JH_CHECK_DIR%"
  echo Stable download filename validation failed.
  pause
  exit /b 1
)
rmdir /s /q "%JH_CHECK_DIR%"
if exist "..\relay\downloads\" copy /y "JHRemoteSupport.exe" "..\relay\downloads\JHRemoteSupport.exe" >nul
powershell -NoProfile -NonInteractive -Command "$jhHasher=[Security.Cryptography.SHA256]::Create(); try { [BitConverter]::ToString($jhHasher.ComputeHash([IO.File]::ReadAllBytes('JHRemoteSupport.exe'))).Replace('-','').ToLowerInvariant() } finally { $jhHasher.Dispose() }" > JHRemoteSupport.sha256.txt
if errorlevel 1 exit /b 1
if exist "..\relay\downloads\" copy /y "JHRemoteSupport.sha256.txt" "..\relay\downloads\JHRemoteSupport.sha256.txt" >nul
echo Build complete: JHRemoteSupport.exe
exit /b 0
