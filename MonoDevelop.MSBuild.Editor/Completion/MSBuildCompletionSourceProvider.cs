// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Language;

using ProjectFileTools.NuGetSearch.Contracts;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	[Export (typeof (IAsyncCompletionSourceProvider))]
	[Name ("MSBuild Completion Source Provider")]
	[ContentType (MSBuildContentType.Name)]
	class MSBuildCompletionSourceProvider : IAsyncCompletionSourceProvider
	{
		[Import (typeof (IFunctionTypeProvider))]
		internal IFunctionTypeProvider FunctionTypeProvider { get; set; }


		[Import (typeof (IPackageSearchManager))]
		public IPackageSearchManager PackageSearchManager { get; set; }

		[Import]
		public DisplayElementFactory DisplayElementFactory { get; set; }

		public IAsyncCompletionSource GetOrCreate (ITextView textView) =>
			textView.Properties.GetOrCreateSingletonProperty (
				typeof (MSBuildCompletionSource),
				() => new MSBuildCompletionSource (textView, this)
			);
	}
}