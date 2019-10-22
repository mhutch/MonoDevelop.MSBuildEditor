// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	public class MSBuildDiagnostic
	{
		public MSBuildDiagnosticDescriptor Descriptor { get; }
		public ImmutableDictionary<string, object> Properties { get; }
		public TextSpan Span { get; }

		readonly object [] messageArgs;

		public MSBuildDiagnostic (MSBuildDiagnosticDescriptor descriptor, TextSpan span, ImmutableDictionary<string, object> properties = null, object[] messageArgs = null)
		{
			Descriptor = descriptor;
			Span = span;
			Properties = properties;
			this.messageArgs = messageArgs;
		}

		public MSBuildDiagnostic (MSBuildDiagnosticDescriptor descriptor, TextSpan span, params object [] messageArgs)
			: this (descriptor, span, null, messageArgs)
		{
		}

		public string GetFormattedMessage () => Descriptor.GetFormattedMessage (args);
	}
}