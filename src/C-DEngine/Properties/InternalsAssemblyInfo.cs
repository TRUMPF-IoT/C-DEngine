// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("1d24a210-756e-4c75-99b0-2c4aa5a1c4cd")]

[assembly: InternalsVisibleTo("C-DEngine.Tests")]
[assembly: InternalsVisibleTo("C-DEngine.Explorables")]
[assembly: InternalsVisibleTo("CDMyOpenLogin")] // temporary: functionality will move into core or special engine extension
