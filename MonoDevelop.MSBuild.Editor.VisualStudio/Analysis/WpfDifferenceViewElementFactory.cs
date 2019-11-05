// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;
using MonoDevelop.MSBuild.Editor.Analysis;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Export (typeof(IDifferenceViewElementFactory))]
	class WpfDifferenceViewElementFactory : IDifferenceViewElementFactory
	{
		private readonly IWpfDifferenceViewerFactoryService _diffFactory;
		private readonly ITextViewRoleSet _previewRoleSet;

		[ImportingConstructor]
		public WpfDifferenceViewElementFactory (IWpfDifferenceViewerFactoryService diffFactory, ITextEditorFactoryService textEditorFactoryService)
		{
			_diffFactory = diffFactory;
			_previewRoleSet = textEditorFactoryService.CreateTextViewRoleSet (PredefinedTextViewRoles.Analyzable);
		}

		public async Task<object> CreateDiffViewElementAsync (IDifferenceBuffer diffBuffer)
		{
			var diffView = _diffFactory.CreateDifferenceView (diffBuffer, _previewRoleSet);

			diffView.ViewMode = DifferenceViewMode.Inline;
			diffView.InlineView.ZoomLevel *= 1.0;
			diffView.InlineView.VisualElement.Focusable = false;
			diffView.InlineHost.GetTextViewMargin ("deltadifferenceViewerOverview").VisualElement.Visibility = System.Windows.Visibility.Collapsed;

			await diffView.SizeToFitAsync ().ConfigureAwait (true);

			return diffView.VisualElement;
		}
	}
}