// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Editor.CodeActions;

// These are modeled on LSP CodeActionKind and VS ISuggestedActionCategorySet/PredefinedSuggestedActionCategoryNames.
// Both of those are actually strings and allow arbitrary values.
// LSP CodeActionKind also supports a form of inheritance i.e. 'refactor.extract' is implicitly also 'refactor'.
// For now, we only support a limited set of enum values to keep it simple.
public enum MSBuildCodeActionKind
{
	/// <summary>
	/// Fixes a problem identified by a diagnostic.
	/// </summary>
	CodeFix,

	/// <summary>
	/// Fixes a problem identified by an error diagnostic. This is implicitly also of type <see cref="CodeFix"/>.
	/// </summary>
	/// <remarks>
	/// Providers do not need to return this from <see cref="MSBuildCodeActionProvider.ProducedCodeActionKinds"/>
	/// as a provider of kind <see cref="CodeFix"/> is implicitly upgraded to <see cref="ErrorFix"/> if any of
	/// the <see cref="MSBuildCodeActionProvider.FixableDiagnosticIds"/> have error-level severity.
	/// <para>
	/// A diagnostic can be upgraded to error severity through configuration, so any <see cref="CodeFix"/> provider
	/// could be dynamically upgraded to <see cref="ErrorFix"/>.
	/// </para>
	/// </remarks>
	ErrorFix,

	/// <summary>
	/// Fixes a style problem identified by a diagnostic. This is implicitly also of type <see cref="CodeFix"/>.
	/// </summary>
	StyleFix,

	/// <summary>
	/// Provides some kind of transformation the user may choose to invoke, such as reorder parameters.
	/// </summary>
	Refactoring,

	/// <summary>
	/// Provides an extraction refactoring, such as extract expression. This is implicitly also of type <see cref="Refactoring"/>.
	/// </summary>
	RefactoringExtract,

	/// <summary>
	/// Provides an inline refactoring, such as inline expression. This is implicitly also of type <see cref="Refactoring"/>.
	/// </summary>
	RefactoringInline
}

