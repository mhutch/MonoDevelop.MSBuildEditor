// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;

namespace MonoDevelop.MSBuild.Analysis
{
	class MSBuildDiagnostic
	{
		public MSBuildDiagnosticDescriptor Descriptor { get; }
		public ImmutableDictionary<string, object> Properties { get; }
		public MSBuildDiagnosticSeverity Severity { get; }
		public int Offset { get; }
		public int Length { get; }
		readonly object [] messageArgs;

		public MSBuildDiagnostic(MSBuildDiagnosticDescriptor descriptor, int offset, int length, MSBuildDiagnosticSeverity severity, ImmutableDictionary<string, object> properties, object [] messageArgs)
		{
			Descriptor = descriptor;
			Offset = offset;
			Length = length;
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