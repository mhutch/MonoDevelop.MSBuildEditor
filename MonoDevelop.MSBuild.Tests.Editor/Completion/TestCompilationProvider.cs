// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Composition;

using MonoDevelop.MSBuild.Editor.Roslyn;

namespace MonoDevelop.MSBuild.Tests.Editor.Completion
{
	[Export (typeof (IRoslynCompilationProvider))]
	class TestCompilationProvider : IRoslynCompilationProvider
	{
		public MetadataReference CreateReference (string assemblyPath)
			=> MetadataReference.CreateFromFile (assemblyPath);
	}
}
