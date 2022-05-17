rem Script to create a CDEX installation package for the C-DEngine. This script gets invoked in a post build action of the cdePackager project
rem SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
rem SPDX-License-Identifier: MPL-2.0
rem "%~dp0\..\..\BuildTools\cdePackager\cdePackager" "%~dp0\..\..\bin\%1\C-DEngine\net45\C-DEngine.CDES" "%~dp0\..\..\bin\%1\C-DEngine\net45\." "%~dp0\..\..\bin\%1\C-DEngine\net45\." "net45"
"%~dp0\..\..\BuildTools\cdePackager\cdePackager" "%~dp0\..\..\bin\%1\C-DEngine\netstandard2.0\C-DEngine.CDES" "%~dp0\..\..\bin\%1\C-DEngine\netstandard2.0\." "%~dp0\..\..\bin\%1\C-DEngine\netstandard2.0\." "netstandard2.0"
rem "%~dp0\..\..\BuildTools\cdePackager\cdePackager" "%~dp0\..\..\bin\%1\C-DEngine\net40\C-DEngine.CDES" "%~dp0\..\..\bin\%1\C-DEngine\net40\." "%~dp0\..\..\bin\%1\C-DEngine\net40\." "net40"
rem "%~dp0\..\..\BuildTools\cdePackager\cdePackager" "%~dp0\..\..\bin\%1\C-DEngine\net35\C-DEngine.CDES" "%~dp0\..\..\bin\%1\C-DEngine\net35\." "%~dp0\..\..\bin\%1\C-DEngine\net35\." "net35"
