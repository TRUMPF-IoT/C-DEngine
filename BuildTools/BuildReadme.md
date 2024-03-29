<!--
 SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
 SPDX-License-Identifier: MPL-2.0
 -->

# Build Tools

All artifacts are built using the SLN file, either in Visual Studio or using the dotnet SDK's command line tools.

## Code Signing
Most binaries are signed using the Azure SignTool through a set of batch files (in the [/BuildTools/](/BuildTools/) directory). These are invoked as custom build steps from each of the .csproj files (and the only reason the C-DEngine can currently only be built on Windows). Signing is off by default and is enabled by creating an (empty) /BuildTools/real-sign file.

To install the Azure SignTool run the following command line once:
```ps
dotnet tool install --global AzureSignTool
```

You can use a file azuresecrets.bat to set the CDE_* signing secrets on your dev machine only for the signing action to avoid setting it globally.
 
```
set CDE_KVU=XXX
set CDE_KVI=XXX
set CDE_KVT=XXX
set CDE_KVS=XXX
set CDE_KVC=XXX
rem AzureSignTool.exe sign -du "https://c-labs.com" -fd sha1 -kvu %CDE_KVU% -kvi %CDE_KVI% -kvt %CDE_KVT% -kvs %CDE_KVS% -kvc %CDE_KVC% -tr http://timestamp.digicert.com -td sha1 %1
```
Set the CDE_CODE_SIGN variable to point to the directory with the azuresecret.bat file.

## Global MSBuild targets and properties
A set of global msbuild files ([Directory.Build.props](/Directory.Build.props) and [Directory.Build.targets](/Directory.Build.targets) ) are used to inject common functionality into all projects:

1. GitVersion (GitVersionTask NuGet package) is used to override version information (if present) in each of the .csproj files. 
    > To build non-pre-release versions, the top-most commit must have a git tag of a compatible form including the version number. For offical builds this is handled through the GitHub Release mechanism. Developer builds should usually be marked as pre-release builds (automatic if you make additional local commits).
2. Package references are dynamically replaced with a project reference if a project with a matching name is found in the solution (filtered to C-DEngine and CDMyNMIHtml5RT in the `<PackageReferenceFilter>` declaration in Directory.Build.targets for performance reason). This allows for easier development and debugging across or outside of the Git repros.
3. All binaries are placed into a central /bin/$(Configuration)/{projectname} directory instead of the default bin/$(Configuration) directory under each .csproj. This makes it easier to extract the artifacts.
4. Package references to Microsoft.SourceLink.GitHub are injected into each project to enable source-level debugging.

## GitHub Action/Workflow
[.github/workflows/dotnetcore.yml](/.github/workflows/dotnetcore.yml) contains a single build sequence that is triggered on any pull request or commit/tag push to the master branch. The action creates the following outputs:

1. GitHub Artifacts containing NuGet packages, CDEX deployment packages etc.
2. For tagged builds (top-level commit has a version tag recognized by GitVersion), NuGet Packages are pushed to NuGet.org.

GitHub Releases are created through the standard GitHub Release mechanism (required owner/administrator permissions) by adding a Git tag with the desired version during the Release Draft creation. This automatically starts build workflow and publishes the NuGet packages.
> TODO: Artifacts don't get automatically added to the Release yet, but are available in the Action workflow run.

## Copyright and License declarations

We rely on the [REUSE tool](https://reuse.software/) to ensure that each and every file is covered by a valid license. The tool runs during PR builds and the PR build will fail if an undeclared file is added.

License texts are centrally listed in [/LICENSES](/LICENSES). Each file must contain an SPDX statement listing one of the license identifiers for a license in that directory.

External License declarations for files that can not be modified are captured in [/.reuse/dep5](/.reuse/dep5).

## SHA1 Usage (Deprecated Crypto)

For Windows code signing, we use continue to use SHA1 because Windows XP can not verify SHA256 code signatures. SHA1 is still supported for code signing (<https://social.technet.microsoft.com/wiki/contents/articles/32288.windows-enforcement-of-sha1-certificates.aspx>). At some point we were generating two signatures (SHA1 / SHA256), but the additional complexity wasn’t worth the effort and risk at the time. We may revisit this in the future. **signtool.exe** is called from /buildtools/signandpackage.cmd and used for our signing.

We also use SHA1 for plugin activation/licensing purposes, where the bar is proving intention when somebody hacks it rather than hardened anti-piracy protection (which tends to be futile anyway). We can’t use SHA256 here because of the size of the digest: a user needs to be able to type in the activation key, so we are limiting ourselves it to 6 time 6 characters, which is about 170 bits.
