// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;

using MonoDevelop.MSBuild.Analysis;

namespace MonoDevelop.MSBuild.Editor
{
	class MSBuildDiagnosticTag : ErrorTag
	{
		public MSBuildDiagnosticTag (MSBuildDiagnostic diagnostic)
			: base (GetErrorTypeName (diagnostic.Descriptor.Severity), null)
		{
			Diagnostic = diagnostic;
		}

		public MSBuildDiagnostic Diagnostic { get; }

		static string GetErrorTypeName (MSBuildDiagnosticSeverity severity)
		{
			switch (severity) {
			case MSBuildDiagnosticSeverity.Error:
				return PredefinedErrorTypeNames.SyntaxError;
			case MSBuildDiagnosticSeverity.Warning:
				return PredefinedErrorTypeNames.Warning;
			case MSBuildDiagnosticSeverity.Suggestion:
				return PredefinedErrorTypeNames.HintedSuggestion;
			case MSBuildDiagnosticSeverity.None:
				return PredefinedErrorTypeNames.Suggestion;
			}
			throw new ArgumentException ($"Unknown DiagnosticSeverity value {severity}", nameof (severity));
		}
	}
}
