// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using Microsoft.VisualStudio.Text;

namespace MonoDevelop.MSBuild.Analysis
{
	class MSBuildDiagnostic
	{
		public MSBuildDiagnosticDescriptor Descriptor { get; }
		public ImmutableDictionary<string, object> Properties { get; }
		public MSBuildDiagnosticSeverity Severity { get; }
		public Span Location { get; }
		readonly object [] messageArgs;

		public MSBuildDiagnostic(MSBuildDiagnosticDescriptor descriptor, Span location, MSBuildDiagnosticSeverity severity, ImmutableDictionary<string, object> properties, object [] messageArgs)
		{
			Descriptor = descriptor;
			Location = location;
			Properties = properties;
			Severity = severity;
			this.messageArgs = messageArgs;
		}

		public string GetMessage ()
		{
			if (messageArgs != null && messageArgs.Length > 0) {
				return string.Format (Descriptor.Message, messageArgs);
			}
			return Descriptor.Message;
		}
	}
}