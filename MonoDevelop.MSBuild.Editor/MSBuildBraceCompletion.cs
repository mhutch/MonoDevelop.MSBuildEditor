// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.BraceCompletion;
using Microsoft.VisualStudio.Utilities;

namespace MonoDevelop.MSBuild.Editor
{
	[Export (typeof (IBraceCompletionDefaultProvider))]
	[ContentType (MSBuildContentType.Name)]
	[BracePair ('\'', '\'')]
	[BracePair ('"', '"')]
	[BracePair ('(', ')')]
	[BracePair ('[', ']')]
	[BracePair ('{', '}')]
	[BracePair ('<', '>')]
	class MSBuildBraceCompletionProvider : IBraceCompletionDefaultProvider
	{
	}
}
