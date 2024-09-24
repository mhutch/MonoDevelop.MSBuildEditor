// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using MonoDevelop.MSBuild;

[assembly: InternalsVisibleTo ($"MonoDevelop.MSBuild.Tests, {IVT.PublicKeyAtt}")]
[assembly: InternalsVisibleTo ($"MonoDevelop.MSBuild.Tests.Editor, {IVT.PublicKeyAtt}")]
[assembly: InternalsVisibleTo ($"MSBuildLanguageServer.Tests, {IVT.PublicKeyAtt}")]

[assembly: InternalsVisibleTo ($"MSBuildLanguageServer, {IVT.PublicKeyAtt}")]
[assembly: InternalsVisibleTo ($"MonoDevelop.MSBuild.Editor, {IVT.PublicKeyAtt}")]
[assembly: InternalsVisibleTo ($"MonoDevelop.MSBuild.Editor.VisualStudio, {IVT.PublicKeyAtt}")]
[assembly: InternalsVisibleTo ($"MonoDevelop.MSBuildEditor, {IVT.PublicKeyAtt}")]
[assembly: InternalsVisibleTo ($"Microsoft.Ide.LanguageService.MSBuild, PublicKey=0024000004800000940000000602000000240000525341310004000001000100675da410943cdcf89a2bbd3716e451b3c35c0de9278a874e06d143dbc861f7b4d21771131177e413290078b98615421b2bb9ac25c14021c4e2c7b967407b5ea96417317ff8bdb1ef34e0d63f5965bdf92841bdaae505987af712a2e1951b2ff76a16d211e0d5ae2c444f55dbd0a3c0f5bed051af0cf7bae49114c4e0c527c4ed")]
