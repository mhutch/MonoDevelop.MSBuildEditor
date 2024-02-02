// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.Commanding;
using Microsoft.VisualStudio.Threading;

using MonoDevelop.Xml.Editor.Completion;

using TriggerState = MonoDevelop.MSBuild.Language.ExpressionCompletion.TriggerState;

namespace MonoDevelop.MSBuild.Editor.Completion;

partial class MSBuildCompletionCommitManager (ILogger logger, JoinableTaskContext joinableTaskContext, IEditorCommandHandlerServiceFactory commandServiceFactory)
	: AbstractCompletionCommitManager<TriggerState, MSBuildCommitItemKind> (logger, joinableTaskContext, commandServiceFactory)
{
	public override IEnumerable<char> PotentialCommitCharacters => commitChars;

	static readonly char[] commitChars = [' ', '(', ')', '.', '-', ';', '[', '\n'];

	protected override bool IsCommitCharForTriggerKind (TriggerState trigger, IAsyncCompletionSession session, ITextSnapshot snapshot, char typedChar)
	{
		switch (trigger) {
		case TriggerState.Value:
			// Suppress committing on period as some value completions may contain periods and we need to be able to match them w/o committing.
			if (typedChar == '.') {
				return false;
			}
			goto default;
		default:
			return Array.IndexOf (commitChars, typedChar) > -1;
		}
	}

	protected override CommitResult TryCommitItemKind (MSBuildCommitItemKind itemKind, IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token)
	{
		switch (itemKind) {
		case MSBuildCommitItemKind.NewGuid:
			InsertGuid (session, buffer);
			return CommitResult.Handled;

		case MSBuildCommitItemKind.ItemReference:
		case MSBuildCommitItemKind.PropertyReference:
		case MSBuildCommitItemKind.MetadataReference:
			var str = item.InsertText;
			//avoid the double paren
			if (typedChar == '(') {
				str = str.Substring (0, str.Length - 1);
			}
			ReplaceApplicableToSpan (session, buffer, str);
			RetriggerCompletion (session.TextView);
			//TODO: insert overtypeable closing paren
			return CommitResult.Handled;
		default:
			throw new InvalidOperationException ($"Unhandled MSBuildCommitItemKind value '{itemKind}'");
		}
	}

	static void InsertGuid (IAsyncCompletionSession session, ITextBuffer buffer)
		=> ReplaceApplicableToSpan (session, buffer, Guid.NewGuid ().ToString ("B").ToUpper ());
}