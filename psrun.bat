@echo off
setlocal
powershell.exe -ExecutionPolicy Unrestricted -Command %*
rem echo PS Warpper exiting with code %ERRORLEVEL%
endlocal
exit /b %ERRORLEVEL%
