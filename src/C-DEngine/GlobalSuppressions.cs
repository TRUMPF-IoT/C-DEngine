// SPDX-FileCopyrightText: Copyright (c) 2009-2022 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "C-Labs and C-DEngine has a custom naming covention", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine")]
[assembly: SuppressMessage("Minor Code Smell", "S1104:Fields should not have public accessibility", Justification = "API of C-DEngine is published", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine")]
[assembly: SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "API of C-DEngine is published and has different custom convention", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine")]
[assembly: SuppressMessage("Critical Code Smell", "S927:Parameter names should match base declaration and other partial definitions", Justification = "API of C - DEngine is published", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine)")]
