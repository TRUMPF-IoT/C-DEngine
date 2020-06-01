# C-DEngine and Tooling

The [C-DEngine Solution](C-DEngine.sln) ist used to build the C-DEngine DLL and Nuget Package as well as all the foundational tooling.

You can open it in Visual Studio 2019 or higher, or simply use the dotNet SDK[^1]:

```bash
dotnet build './src/C-DEngine.sln'
```

This solution builds all flavors of the C-DEngine, from .Net 3.5 (for reach all the way to Windows XP) over .Net 4.0, .Net 4.5 to .Net Standard 2.0 (for full cross-platform support on Windows, Linux, Raspberry Pi, Docker etc.).

There are a few tricks in the GitHub Action build integration that are described [here](/BuildTools/BuildReadme.md).

The solution also contains:

- cdeIIS: Adapter to run the C-DEngine inside the IIS web server ("Cloud Gate" or "Cloud Relay"). It is recommended for production deployments on internet-facing nodes, as the (optional) mini-web server in the C-DEngine is intended for local/intranet access.
- cdeAspNet: Adapter to run the C-DEngine inside an ASP.Net Core 3.x Web Server.
- cdePackager: a tool that creates deployment packages (.CDEX files) for the C-DEngine plug-in management system, as well as Mesh Deployment packages (.CDEP) for the C-DEngine Mesh Manager / Provisioning Service. Refer to the [Plug-ins section](http://docs.c-labs.com/plugins/UsingPlugins.html) for more info on how to use the cdePackager tool.
- cdeUpdater: a tool that gets embedded into the C-DEngine.dll to allow for self-updating of the C-DEngine and it's plug-ins via .CDEX deployment packages.

The C-DEngine, the web server adapters (cdeIIS, cdeAspnet) and the cdePackager are available as signed nuget packages.

[^1] dotNet SDK 2.2 or higher, we currently use 3.1.200. Building requires Windows right now due to some custom build steps that use Windows batch files.

<!--
 SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
 SPDX-License-Identifier: MPL-2.0
 -->
