@ECHO off

REM Requires "Run As Administrator" 

set /P c=If this file has admin rights press Y, if not, press N and RunAs Administrator[Y/N]?
if /I "%c%" EQU "Y" goto :Admin
if /I "%c%" EQU "N" goto :NoAdmin

:Admin

ECHO Installing Service...
ECHO....................

	%Windir%\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe "%~dp0ABPMilkDryfeHiberSrvMilk.exe"

pause

:NoADmin

Exit