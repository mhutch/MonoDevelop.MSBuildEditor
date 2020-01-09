// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

using Microsoft.VisualStudio.MiniEditor;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.Xml.Tests.Completion;
using MonoDevelop.Xml.Tests.EditorTestHelpers;

namespace MonoDevelop.MSBuild.Tests
{
	class MSBuildTestEnvironment : XmlTestEnvironment
	{
		public static new(EditorEnvironment, EditorCatalog) EnsureInitialized ()
			=> EnsureInitialized<MSBuildTestEnvironment> ();

		protected override IEnumerable<string> GetAssembliesToCompose ()
		{
			return base.GetAssembliesToCompose ().Concat (new[] {
				typeof (MSBuildCompletionSource).Assembly.Location,
				typeof (MSBuildTestEnvironment).Assembly.Location
			});
		}

		protected override bool ShouldIgnoreCompositionError (string error)
			=> error.Contains ("MonoDevelop.MSBuild.Editor.Navigation.MSBuildNavigationService")
				|| error.Contains ("Microsoft.VisualStudio.Editor.ICommonEditorAssetServiceFactory")
				|| error.Contains ("MonoDevelop.MSBuild.Editor.Host.IStreamingFindReferencesPresenter")
				|| error.Contains ("Microsoft.VisualStudio.Language.Intellisense.ISuggestedActionCategoryRegistryService2")
			;
	}

	[Export (typeof (IRuntimeInformation))]
	class TestRuntimeInformation : NullRuntimeInformation
	{
	}
}
