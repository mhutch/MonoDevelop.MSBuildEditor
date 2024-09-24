// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Editor.Roslyn;
using MonoDevelop.MSBuild.Tests.Helpers;
using MonoDevelop.Xml.Editor.Tests;

namespace MonoDevelop.MSBuild.Tests
{
	class MSBuildTestEnvironment : XmlTestEnvironment
	{
		public static new MSBuildEditorCatalog CreateEditorCatalog () => new (GetInitialized<MSBuildTestEnvironment> ().GetEditorHost ());

		protected override Task OnInitialize ()
		{
			MSBuildTestHelpers.RegisterMSBuildAssemblies ();
			return base.OnInitialize ();
		}

		protected override IEnumerable<string> GetAssembliesToCompose ()
			=> base.GetAssembliesToCompose ().Concat (new[] {
				typeof (MSBuildCompletionSource).Assembly.Location,
				typeof (TaskMetadataBuilder).Assembly.Location,
				typeof (MSBuildTestEnvironment).Assembly.Location
			});

		protected override bool ShouldIgnoreCompositionError (string error)
			=> error.Contains ("Microsoft.VisualStudio.Editor.ICommonEditorAssetServiceFactory")
				|| error.Contains ("MonoDevelop.MSBuild.Editor.Host.IStreamingFindReferencesPresenter")
				|| error.Contains ("Microsoft.VisualStudio.Language.Intellisense.ISuggestedActionCategoryRegistryService2")
				|| base.ShouldIgnoreCompositionError (error);
	}
}
