rem SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
rem SPDX-License-Identifier: CC0-1.0
if not exist "%~dp0\real.sign" goto noSign
if not exist "%CDE_CODE_SIGN%\azuresecrets.bat" goto noSecrets
echo Setting Azure Vault secrets
call ""%CDE_CODE_SIGN%\azuresecrets.bat"
:noSecrets
echo Signing with Azure Vault Key
rem Multi-targeted projects sign one binary per target framework in parallel. The timestamp server
rem occasionally throttles such requests (HTTP 429 / error 801901AD): retry before failing the build.
set signAttempt=1
:sign
AzureSignTool.exe sign -du "https://c-labs.com" -fd sha384 -kvu %CDE_KVU% -kvi %CDE_KVI% -kvt %CDE_KVT% -kvs %CDE_KVS% -kvc %CDE_KVC% -tr http://rfc3161timestamp.globalsign.com/advanced -td sha384 %1
set signResult=%ERRORLEVEL%
if %signResult% EQU 0 goto exit
if %signAttempt% GEQ 5 exit /b %signResult%
set /a signAttempt+=1
set /a signDelay=10 + %RANDOM% %% 10
echo Signing failed with exit code %signResult%: starting attempt %signAttempt% of 5 in about %signDelay% seconds
ping -n %signDelay% 127.0.0.1 >nul
goto sign
rem AzureSignTool.exe sign -du "https://c-labs.com" -fd sha384 -kvu %CDE_KVU% -kvi %CDE_KVI% -kvt %CDE_KVT% -kvs %CDE_KVS% -kvc %CDE_KVC% -td sha384 %1
rem AzureSignTool.exe sign -du "https://c-labs.com" -fd sha1 -kvu %CDE_KVU% -kvi %CDE_KVI% -kvt %CDE_KVT% -kvs %CDE_KVS% -kvc %CDE_KVC% -td sha1 %1
goto exit
:noSign
echo !!!!!!! Skipped signing: No "%~dp0\real.sign" file found - expected if not an official build.
:exit
