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
using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Editor;

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

		public object CreateDiffViewElement (IDifferenceBuffer diffBuffer)
		{
			var diffView = _diffFactory.CreateDifferenceView (diffBuffer, _previewRoleSet);

			diffView.ViewMode = DifferenceViewMode.Inline;
			diffView.InlineView.ZoomLevel *= .75;
			diffView.InlineView.VisualElement.Focusable = false;
			diffView.InlineHost.GetTextViewMargin ("deltadifferenceViewerOverview").VisualElement.Visibility = System.Windows.Visibility.Collapsed;

			// Reduce the size of the buffer once it's ready
			diffView.DifferenceBuffer.SnapshotDifferenceChanged += (sender, args) => {
				diffView.InlineView.DisplayTextLineContainingBufferPosition (
					new SnapshotPoint (diffView.DifferenceBuffer.CurrentInlineBufferSnapshot, 0),
					0.0, ViewRelativePosition.Top, double.MaxValue, double.MaxValue
				);

				var width = Math.Max (diffView.InlineView.MaxTextRightCoordinate * (diffView.InlineView.ZoomLevel / 100), 400); // Width of the widest line.
				var height = diffView.InlineView.LineHeight * (diffView.InlineView.ZoomLevel / 100) * // Height of each line.
					diffView.DifferenceBuffer.CurrentInlineBufferSnapshot.LineCount;

				diffView.VisualElement.Width = width;
				diffView.VisualElement.Height = height;
			};

			return diffView.VisualElement;
		}
	}
}