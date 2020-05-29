rem <projectdir> <targetdir> <targetfilename> <cdePlatform - X64_V3 NETSTD_V20 etc.>
rem SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
rem SPDX-License-Identifier: CC0-1.0
call "%~dp0\signMeSha.bat" "%~2%~3" "%~dp0"
"%~dp0cdePackager\cdePackager.exe" "%~2%~3" "%~2." "%~2." "%~4"
if not %errorlevel%==0 goto :EOF
