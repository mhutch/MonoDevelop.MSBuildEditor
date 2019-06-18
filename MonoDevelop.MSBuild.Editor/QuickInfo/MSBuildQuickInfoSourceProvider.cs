// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace MonoDevelop.MSBuild.Editor.QuickInfo
{
	[Export (typeof (IAsyncQuickInfoSourceProvider))]
	[Name ("MSBuild Quick Info Provider")]
	[ContentType (MSBuildContentType.Name)]
	[Order]
	class MSBuildQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
	{
		public IAsyncQuickInfoSource TryCreateQuickInfoSource (ITextBuffer textBuffer)
		{
			return new MSBuildQuickInfoSource ();
		}
	}
}