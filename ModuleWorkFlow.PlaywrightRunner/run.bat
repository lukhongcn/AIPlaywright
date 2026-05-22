@echo off
cd /d %~dp0

set MES_LOGIN_URL=http://localhost:5008/Login.aspx
set MES_USER=admin
set MES_PASSWORD=123456

.\bin\Debug\ModuleWorkFlow.PlaywrightRunner.exe request.json

pause