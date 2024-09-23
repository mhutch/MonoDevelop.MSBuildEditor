// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Editor.CodeActions;

static class MSBuildCodeActionKindExtensions
{
	/// <summary>
	/// Whether this kind is a valid response for the kinds requested by the IDE
	/// </summary>
	public static bool MatchesRequest(this MSBuildCodeActionKind producedKind, ISet<MSBuildCodeActionKind> requestedKinds)
	{
		if (requestedKinds.Count == 0) {
			return true;
		}

		if (requestedKinds.Contains (producedKind)) {
			return true;
		}
		switch (producedKind) {
		case MSBuildCodeActionKind.StyleFix:
		case MSBuildCodeActionKind.ErrorFix:
			if (requestedKinds.Contains (MSBuildCodeActionKind.CodeFix)) {
				return true;
			}
			break;
		case MSBuildCodeActionKind.RefactoringExtract:
		case MSBuildCodeActionKind.RefactoringInline:
			if (requestedKinds.Contains (MSBuildCodeActionKind.Refactoring)) {
				return true;
			}
			break;
		}
		return false;
	}

	public static bool MatchesRequest (this MSBuildCodeAction action, ISet<MSBuildCodeActionKind> requestedKinds)
	{
		if (action.Kind.MatchesRequest (requestedKinds)) {
			return true;
		}

		if (requestedKinds.Contains (MSBuildCodeActionKind.ErrorFix)) {
			if (action.GetFixesErrorDiagnostics ()) {
				return true;
			}
		}

		return false;
	}
}


