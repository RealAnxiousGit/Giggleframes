@echo off
rem Builds artifacts\native\version.dll (the launch helper) with the MSVC x64 build tools.
setlocal
"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath > "%TEMP%\giggleframes-vs.txt"
set /p VS=<"%TEMP%\giggleframes-vs.txt"
call "%VS%\VC\Auxiliary\Build\vcvars64.bat" >nul || exit /b 1
cd /d "%~dp0"
if not exist ..\artifacts\native mkdir ..\artifacts\native
cl /nologo /O1 /GS- /W3 /c version_proxy.c /Fo..\artifacts\native\version_proxy.obj || exit /b 1
link /nologo /DLL /NODEFAULTLIB /ENTRY:DllMain /OUT:..\artifacts\native\version.dll ..\artifacts\native\version_proxy.obj kernel32.lib || exit /b 1