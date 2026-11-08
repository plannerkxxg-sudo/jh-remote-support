@echo off
setlocal
echo JH Remote Support v0.9 - environment information
echo.
ver
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v ProductName
reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v CurrentBuildNumber
reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release
echo.
echo Requires Windows 7 SP1 / 8.1 / 10 / 11 and .NET Framework 4.8 or newer.
echo Windows 7 SP1 requires .NET 4.8, not 4.8.1.
echo Please send a photo of this window to your support teacher.
pause
