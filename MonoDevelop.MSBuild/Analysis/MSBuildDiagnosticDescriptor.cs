// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;

namespace MonoDevelop.MSBuild.Analysis
{
	public class MSBuildDiagnosticDescriptor
	{
		public string Title { get; }
		public string Id { get; }
		public string Message { get; }
		public MSBuildDiagnosticSeverity Severity { get; }

		public MSBuildDiagnosticDescriptor (string id, string title, string message, MSBuildDiagnosticSeverity severity)
		{
			Title = title ?? throw new ArgumentNullException (nameof (title));
			Id = id ?? throw new ArgumentNullException (nameof (id));
			Message = message;
			Severity = severity;
		}

		public MSBuildDiagnosticDescriptor (string id, string title, MSBuildDiagnosticSeverity severity)
			: this (id, title, null, severity) { }

		string combinedMsg;

		internal string GetFormattedMessage (object[] args)
		{
			combinedMsg = combinedMsg ?? (combinedMsg = Title + Environment.NewLine + Message);
			if (args != null && args.Length > 0) {
				return string.Format (combinedMsg, args);
			}
			return combinedMsg;
		}
	}
}