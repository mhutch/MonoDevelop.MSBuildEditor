// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Composition;
using MonoDevelop.MSBuild.Editor.Roslyn;
using BF = System.Reflection.BindingFlags;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Export (typeof (IRoslynCompilationProvider))]
	class MonoDevelopCompilationProvider : IRoslynCompilationProvider
	{
		readonly MethodInfo getReferenceMethod;
		readonly object metadataService;

		public MonoDevelopCompilationProvider ()
		{
			var metadataServiceType = typeof (Workspace).Assembly.GetType ("Microsoft.CodeAnalysis.Host.IMetadataService");
			getReferenceMethod = metadataServiceType.GetMethod ("GetReference", BF.Instance | BF.NonPublic | BF.Public);
			var services = Ide.IdeServices.TypeSystemService.Workspace.Services;
			var getServiceMeth = services.GetType ().GetMethod ("GetService", BF.Instance | BF.NonPublic | BF.Public);
			getServiceMeth = getServiceMeth.MakeGenericMethod (metadataServiceType);
			metadataService = getServiceMeth.Invoke (services, null);
		}

		public MetadataReference CreateReference (string assemblyPath) =>
			(PortableExecutableReference) getReferenceMethod.Invoke (
				metadataService,
				new object[] { assemblyPath, MetadataReferenceProperties.Assembly }
			);
	}
}
