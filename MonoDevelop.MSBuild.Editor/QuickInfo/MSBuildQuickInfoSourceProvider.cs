// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using ProjectFileTools.NuGetSearch.Contracts;

namespace MonoDevelop.MSBuild.Editor.QuickInfo
{
	[Export (typeof (IAsyncQuickInfoSourceProvider))]
	[Name ("MSBuild Quick Info Provider")]
	[ContentType (MSBuildContentType.Name)]
	[Order]
	class MSBuildQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
	{
		readonly IPackageSearchManager packageSearchManager;

		[ImportingConstructor]
		public MSBuildQuickInfoSourceProvider (IPackageSearchManager packageSearchManager)
		{
			this.packageSearchManager = packageSearchManager;
		}

		public IAsyncQuickInfoSource TryCreateQuickInfoSource (ITextBuffer textBuffer)
			=> textBuffer.Properties.GetOrCreateSingletonProperty (() => new MSBuildQuickInfoSource (textBuffer, packageSearchManager));
	}
}