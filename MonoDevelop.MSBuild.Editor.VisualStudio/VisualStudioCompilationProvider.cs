// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.LanguageServices;

using MonoDevelop.MSBuild.Editor.Roslyn;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Export(typeof(IRoslynCompilationProvider))]
	class VisualStudioCompilationProvider : IRoslynCompilationProvider
	{
		[Import]
		public VisualStudioWorkspace Workspace { get; set; }

		public MetadataReference CreateReference (string assemblyPath)
			=> Workspace.CreatePortableExecutableReference (assemblyPath, MetadataReferenceProperties.Assembly);
	}
}
