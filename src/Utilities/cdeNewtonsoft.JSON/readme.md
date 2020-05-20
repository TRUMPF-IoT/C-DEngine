<!--
 SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
 SPDX-License-Identifier: MPL-2.0
 -->
# Newtonsoft.Json, internalized

In general, the C-DEngine avoids any dependencies outside of the base runtimes, to streamline deployment and avoid DLL conflicts.

This shared project is a snapshot of Newtonsoft.JSON sources, with two modifications:
1. All types are marked internal
2. All namespaces are changed to cdeNewtonsoft.

When ILMerge supports portable PDBs, we are planning to use it to internalize the Newtonsoft.Json library and elimiate this clone.
For .Net Core 3.0 and higher, we are looking into switch to the native Json serializer.


