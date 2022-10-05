// SPDX-FileCopyrightText: Copyright (c) 2009-2022 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "C-Labs and C-DEngine has a custom naming covention", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine")]
[assembly: SuppressMessage("Info", "S1135:Remove TODO Tags", Justification = "We need to continue to track this", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine")]
[assembly: SuppressMessage("Minor Code Smell", "S1104:Fields should not have public accessibility", Justification = "API of C-DEngine is published", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine")]
[assembly: SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "API of C-DEngine is published and has different custom convention", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine")]
[assembly: SuppressMessage("Minor Code Smell", "S3358:Ternary operators should not be nested", Justification = "Performance", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine")]
[assembly: SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "API of C - DEngine is published", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine)")]
[assembly: SuppressMessage("Major Code Smell", "S3885:Use Load instead LoadFrom", Justification = "Required to dynamically load CryptoDLL", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine)")]
[assembly: SuppressMessage("Major Code Smell", "S125:Remove this commented out code", Justification = "Commented code is used for future use", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine)")]
[assembly: SuppressMessage("Critical Code Smell", "S927:Parameter names should match base declaration and other partial definitions", Justification = "API of C - DEngine is published", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine)")]
[assembly: SuppressMessage("Critical Code Smell", "S2696:Instance members should not write to \"static\" fields", Justification = "API of C - DEngine is published", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine)")]
[assembly: SuppressMessage("Critical Code Smell", "S2223:Change the visibility of 'xxx' or make it 'const' or 'readonly'.", Justification = "API of C - DEngine is published", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine)")]
[assembly: SuppressMessage("Critical Code Smell", "S3776:Refactor this method to reduce its Cognitive Complexity from...to...'.", Justification = "Performance over Beauty", Scope = "namespaceanddescendants", Target = "~N:nsCDEngine)")]

