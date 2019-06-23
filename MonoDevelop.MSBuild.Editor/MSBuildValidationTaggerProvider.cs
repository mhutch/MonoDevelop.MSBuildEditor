// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace MonoDevelop.MSBuild.Editor
{
	[Export (typeof (ITaggerProvider))]
	[ContentType (MSBuildContentType.Name)]
	[TagType (typeof (IErrorTag))]
	[TextViewRole (PredefinedTextViewRoles.Analyzable)]

	class MSBuildValidationTaggerProvider : ITaggerProvider
	{
		JoinableTaskContext joinableTaskContext;

		[ImportingConstructor]
		public MSBuildValidationTaggerProvider (JoinableTaskContext joinableTaskContext)
		{
			this.joinableTaskContext = joinableTaskContext;
		}

		public ITagger<T> CreateTagger<T> (ITextBuffer buffer) where T : ITag
			=> (ITagger<T>)buffer.Properties.GetOrCreateSingletonProperty (() => new MSBuildValidationTagger (buffer, joinableTaskContext));
	}
}
