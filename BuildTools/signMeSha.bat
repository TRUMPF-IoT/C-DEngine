rem SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
rem SPDX-License-Identifier: CC0-1.0
if not exist "%~dp0\real.sign" goto noSign
if not exist "%CDE_CODE_SIGN%\azuresecrets.bat" goto noSecrets
echo Setting Azure Vault secrets
call ""%CDE_CODE_SIGN%\azuresecrets.bat"
:noSecrets
echo Signing with Azure Vault Key
AzureSignTool.exe sign -du "https://c-labs.com" -fd sha1 -kvu %CDE_KVU% -kvi %CDE_KVI% -kvt %CDE_KVT% -kvs %CDE_KVS% -kvc %CDE_KVC% -tr http://timestamp.digicert.com -td sha1 %1
:noSign
echo !!!!!!! Skipped signing: No "%~dp0\real.sign" file found - expected if not an official build.
:exit
