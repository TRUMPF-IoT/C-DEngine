rem SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
rem SPDX-License-Identifier: CC0-1.0
echo Signing with Azure Vault Key
AzureSignTool.exe sign -du "https://c-labs.com" -fd sha1 -kvu %CDE_KVU% -kvi %CDE_KVI% -kvt %CDE_KVT% -kvs %CDE_KVS% -kvc %CDE_KVC% -tr http://timestamp.digicert.com -td sha1 %1
