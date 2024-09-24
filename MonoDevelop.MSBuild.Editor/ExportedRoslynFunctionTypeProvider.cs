// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Editor.Roslyn;

[Export (typeof (IFunctionTypeProvider))]
class ExportedRoslynFunctionTypeProvider : RoslynFunctionTypeProvider
{
	[ImportingConstructor]
	public ExportedRoslynFunctionTypeProvider (IRoslynCompilationProvider assemblyLoader, MSBuildEnvironmentLogger environmentLogger)
		: base (assemblyLoader, environmentLogger.Logger)
	{
	}
}
