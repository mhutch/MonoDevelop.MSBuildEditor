// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using MonoDevelop.MSBuild;

[assembly: InternalsVisibleTo ($"MonoDevelop.MSBuild.Tests, {IVT.PublicKeyAtt}")]
[assembly: InternalsVisibleTo ($"MonoDevelop.MSBuild.Tests.Editor, {IVT.PublicKeyAtt}")]

[assembly: InternalsVisibleTo ($"MonoDevelop.MSBuild.Editor.VisualStudio, {IVT.PublicKeyAtt}")]
[assembly: InternalsVisibleTo ($"MonoDevelop.MSBuildEditor, {IVT.PublicKeyAtt}")]
[assembly: InternalsVisibleTo ($"Microsoft.Ide.LanguageService.MSBuild, {IVT.PublicKeyAtt}")]
