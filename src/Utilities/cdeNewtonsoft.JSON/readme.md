<!--
 SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
 SPDX-License-Identifier: MPL-2.0
 -->
# Newtonsoft.Json, internalized

In general, the C-DEngine avoids any dependencies outside of the base runtimes, to streamline deployment and avoid DLL conflicts.

This shared project is a snapshot of Newtonsoft.Json sources, with two modifications:
1. All types are marked internal
2. All namespaces are changed to cdeNewtonsoft.

When [ILMerge](https://github.com/dotnet/ILMerge) supports portable PDBs, we are planning to use it to internalize the Newtonsoft.Json library and eliminate this clone.
For .Net Core 3.0 and higher, we are looking into switch to the native Json serializer.

If you want to avoid the internalized cdeNewtonsoft.Json, and instead rely on a regular Newtonsoft.Json NuGet dependency, you can do so by building your own C-DEngine.dll and:

- using a VS Solution of your own that does not have the cdeNewtonsoft.JSON shared project,
- OR removing cdeNewtonsoft.JsonN from the C-DEngine.SLN (just make sure you don't include it in a pull request),
- OR removing the define for CDE_INTNEWTON in src/C-DEngine/C-DEngine.csproj.
