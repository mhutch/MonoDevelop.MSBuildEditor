// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.MSBuild.Language;
using ProjectFileTools.NuGetSearch.Contracts;

namespace MonoDevelop.MSBuild.Editor.QuickInfo
{
	[Export (typeof (IAsyncQuickInfoSourceProvider))]
	[Name ("MSBuild Quick Info Provider")]
	[ContentType (MSBuildContentType.Name)]
	[Order]
	class MSBuildQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
	{
		[Import (typeof (IFunctionTypeProvider))]
		public IFunctionTypeProvider FunctionTypeProvider { get; set; }


		[Import (typeof (IPackageSearchManager))]
		public IPackageSearchManager PackageSearchManager { get; set; }

		public IAsyncQuickInfoSource TryCreateQuickInfoSource (ITextBuffer textBuffer)
			=> textBuffer.Properties.GetOrCreateSingletonProperty (() => new MSBuildQuickInfoSource (textBuffer, this));
	}
}