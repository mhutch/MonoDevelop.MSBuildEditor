// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


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
			Title = title;
			Id = id;
			Message = message;
			Severity = severity;
		}
	}
}