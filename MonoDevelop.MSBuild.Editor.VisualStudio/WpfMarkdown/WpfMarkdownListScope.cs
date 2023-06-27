// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Markup;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.WpfMarkdown;

class WpfMarkdownListScope : List<FrameworkElement>, IAddChild
{
	public void AddChild (object value) => Add ((FrameworkElement)value);

	public void AddText (string text) => throw new NotSupportedException ();
}
