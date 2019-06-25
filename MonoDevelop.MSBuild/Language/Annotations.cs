// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Language
{
	interface IRegionAnnotation
	{
		TextSpan Span { get; }
	}

	public class NavigationAnnotation : IRegionAnnotation
	{
		public NavigationAnnotation (string path, TextSpan span)
		{
			Path = path;
			Span = span;
		}

		public TextSpan Span { get; }
		public string Path { get; }
	}
}
