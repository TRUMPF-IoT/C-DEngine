rem Script to create a CDEX installation package for the C-DEngine. This script gets invoked in a post build action of the cdePackager project
rem SPDX-FileCopyrightText: Copyright (c) 2009-2026 TRUMPF Laser GmbH, authors: C-Labs
rem SPDX-License-Identifier: MPL-2.0
"%~dp0\..\..\BuildTools\cdePackager\cdePackager" "%~dp0\..\..\bin\%1\C-DEngine\net10\C-DEngine.CDES" "%~dp0\..\..\bin\%1\C-DEngine\net10\." "%~dp0\..\..\bin\%1\C-DEngine\net10\." "net10"
