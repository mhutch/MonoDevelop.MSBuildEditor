// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;

using MonoDevelop.MSBuild.Editor;
using MonoDevelop.MSBuild.Editor.Analysis;
using MonoDevelop.MSBuild.Editor.Host;

namespace MonoDevelop.MSBuild.Tests.Editor.Mocks
{
	// this just needs to compose, it doesn't need to work
	[Export (typeof (IMSBuildEditorHost))]
	class TestMSBuildEditorHost : IMSBuildEditorHost
	{
		public Dictionary<string, ITextBuffer> GetOpenDocuments ()
		{
			throw new System.NotImplementedException ();
		}

		public bool OpenFile (string destFile, int destOffset, bool isPreview = false)
		{
			throw new System.NotImplementedException ();
		}

		public void ShowGoToDefinitionResults (string[] paths)
		{
			throw new System.NotImplementedException ();
		}

		public void ShowStatusBarMessage (string v)
		{
			throw new System.NotImplementedException ();
		}
	}

	[Export (typeof (IStreamingFindReferencesPresenter))]
	class TestStreamingFindReferencesPresenter : IStreamingFindReferencesPresenter
	{
		public void ClearAll () => throw new System.NotImplementedException ();

		public FindReferencesContext StartSearch (string title, string referenceName, bool showUsage)
			=> throw new System.NotImplementedException ();
	}

	[Export (typeof (IDifferenceBufferFactoryService))]
	class MockDifferenceBufferFactoryService : IDifferenceBufferFactoryService
	{
		public IDifferenceBuffer CreateDifferenceBuffer (ITextBuffer leftBaseBuffer, ITextBuffer rightBaseBuffer)
		{
			throw new System.NotImplementedException ();
		}

		public IDifferenceBuffer CreateDifferenceBuffer (ITextBuffer leftBaseBuffer, ITextBuffer rightBaseBuffer, StringDifferenceOptions options, bool disableEditing = false, bool wrapLeftBuffer = true, bool wrapRightBuffer = true) => throw new System.NotImplementedException ();

		public IDifferenceBuffer TryGetDifferenceBuffer (IProjectionBufferBase projectionBuffer) => throw new System.NotImplementedException ();
	}

	[Export (typeof (IDifferenceViewElementFactory))]
	class MockDifferenceViewElementFactoryService : IDifferenceViewElementFactory
	{
		public Task<object> CreateDiffViewElementAsync (IDifferenceBuffer diffBuffer) => throw new System.NotImplementedException ();
	}

	[Export (typeof (IMSBuildSuggestedActionFactory))]
	class MockSuggestedActionFactory : IMSBuildSuggestedActionFactory
	{
		public ISuggestedAction CreateSuggestedAction (PreviewChangesService previewService, ITextView textView, ITextBuffer buffer, MSBuildCodeFix fix) => throw new System.NotImplementedException ();
	}
}
