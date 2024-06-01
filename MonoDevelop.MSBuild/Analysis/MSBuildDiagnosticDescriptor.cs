// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace MonoDevelop.MSBuild.Analysis
{
	public class MSBuildDiagnosticDescriptor
	{
		public string Title { get; }
		public string Id { get; }

		[StringSyntax (StringSyntaxAttribute.CompositeFormat)]
		public string? MessageFormat { get; }
		public MSBuildDiagnosticSeverity Severity { get; }

		public MSBuildDiagnosticDescriptor (string id, string title, [StringSyntax (StringSyntaxAttribute.CompositeFormat)] string? messageFormat, MSBuildDiagnosticSeverity severity)
		{
			Title = title ?? throw new ArgumentNullException (nameof (title));
			Id = id ?? throw new ArgumentNullException (nameof (id));
			MessageFormat = messageFormat;
			Severity = severity;
		}

		public MSBuildDiagnosticDescriptor (string id, string title, MSBuildDiagnosticSeverity severity)
			: this (id, title, null, severity) { }

		internal string GetFormattedMessageAndTitle (object[]? messageArgs)
		{
			try {
				string? message = messageArgs != null && messageArgs.Length > 0 && MessageFormat is string format
					? string.Format (MessageFormat, messageArgs)
					: MessageFormat;
				return string.IsNullOrEmpty (message)
					? Title
					: Title + Environment.NewLine + message;
			} catch (FormatException ex) {
				// this is likely to be called from somewhere other than where the diagnostic was constructed
				// so ensure the error has enough info to track it down
				throw new FormatException ($"Error formatting message for diagnostic {Id}", ex);
			}
		}
	}
}