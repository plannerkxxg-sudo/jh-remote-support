@echo off
setlocal
cd /d "%~dp0"
call "%~dp0Build.cmd"
if errorlevel 1 exit /b 1
:run
start "" "%~dp0JHRemoteSupport.exe"
