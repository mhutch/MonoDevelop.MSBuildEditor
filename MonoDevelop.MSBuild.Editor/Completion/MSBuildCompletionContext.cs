// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

using static MonoDevelop.MSBuild.Language.ExpressionCompletion;

namespace MonoDevelop.MSBuild.Editor.Completion;

class MSBuildCompletionContext : XmlCompletionTriggerContext
{
	readonly IFunctionTypeProvider functionTypeProvider;
	readonly MSBuildBackgroundParser parser;

	public MSBuildCompletionContext (MSBuildBackgroundParser parser, MSBuildCompletionSourceProvider provider, IAsyncCompletionSession session, SnapshotPoint triggerLocation, XmlSpineParser spineParser, CompletionTrigger trigger, SnapshotSpan applicableToSpan)
		: base (session, triggerLocation, spineParser, trigger, applicableToSpan)
	{
		functionTypeProvider = provider.FunctionTypeProvider;
		this.parser = parser;
		DocumentationProvider = new MSBuildCompletionDocumentationProvider (this, provider.PackageSearchManager, provider.DisplayElementFactory);

		ExpressionTriggerReason = ConvertTriggerReason (trigger.Reason, trigger.Character);
	}

	public override bool IsSupportedTriggerReason => base.IsSupportedTriggerReason || ExpressionTriggerReason != ExpressionTriggerReason.Unknown;

	public override async Task InitializeNodePath (ILogger logger, CancellationToken cancellationToken)
	{
		await base.InitializeNodePath (logger, cancellationToken);

		MSBuildParseResult parseResult = parser.LastOutput ?? await parser.GetOrProcessAsync (TriggerLocation.Snapshot, cancellationToken);
		Document = parseResult.MSBuildDocument ?? MSBuildRootDocument.Empty;

		// clone the spine because the resolver alters it
		ResolveResult = MSBuildResolver.Resolve (SpineParser.Clone (), TriggerLocation.Snapshot.GetTextSource (), Document, functionTypeProvider, logger, cancellationToken);
	}

	public ExpressionTriggerReason ExpressionTriggerReason { get; private set; }
	public MSBuildResolveResult ResolveResult { get; private set; }
	public MSBuildRootDocument Document { get; private set; }
	public MSBuildCompletionDocumentationProvider DocumentationProvider { get; }

	internal static ExpressionTriggerReason ConvertTriggerReason (CompletionTriggerReason reason, char typedChar)
	{
		switch (reason) {
		case CompletionTriggerReason.Insertion:
			if (typedChar != '\0')
				return ExpressionTriggerReason.TypedChar;
			break;
		case CompletionTriggerReason.Backspace:
			return ExpressionTriggerReason.Backspace;
		case CompletionTriggerReason.Invoke:
		case CompletionTriggerReason.InvokeAndCommitIfUnique:
			return ExpressionTriggerReason.Invocation;
		}
		return ExpressionTriggerReason.Unknown;
	}
}
