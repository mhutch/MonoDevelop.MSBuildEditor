// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Options;

using Roslyn.Utilities;

using TextSpan = MonoDevelop.Xml.Dom.TextSpan;

namespace MonoDevelop.MSBuild.Editor.CodeActions;

[Export]
class MSBuildCodeActionService
{
	readonly MSBuildCodeActionProvider[] codeActionProviders;

	[ImportingConstructor]
	public MSBuildCodeActionService (
		[ImportMany (typeof (MSBuildCodeActionProvider))] MSBuildCodeActionProvider[] codeActionProviders
		)
	{
		this.codeActionProviders = codeActionProviders;
	}

	public async Task<List<MSBuildCodeAction>?> GetCodeActions (
		SourceText sourceText,
		MSBuildRootDocument msbuildDocument,
		TextSpan range,
		IEnumerable<MSBuildCodeActionKind>? requestedKinds,
		IOptionsReader options,
		CancellationToken cancellationToken)
	{
		var fixes = new List<MSBuildCodeAction> ();

		if (requestedKinds is not ISet<MSBuildCodeActionKind> requestedKindsSet) {
			if (requestedKinds is not null) {
				requestedKindsSet = requestedKinds.ToSet ();
			} else {
				requestedKindsSet = new HashSet<MSBuildCodeActionKind> ();
			}
		}

		void ReportFix (MSBuildCodeAction a)
		{
			if (!a.MatchesRequest (requestedKindsSet)) {
				return;
			}

			lock (fixes) {
				fixes.Add (a);
			}
		}

		await RunCodeActionProviders (sourceText, msbuildDocument, range, requestedKindsSet, options, ReportFix, cancellationToken);

		return fixes;
	}

	async Task RunCodeActionProviders (
		SourceText sourceText,
		MSBuildRootDocument msbuildDocument,
		TextSpan range,
		ISet<MSBuildCodeActionKind> requestedKinds,
		IOptionsReader options,
		Action<MSBuildCodeAction> reportAction,
		CancellationToken cancellationToken)
	{
		var ctx = new MSBuildCodeActionContext (
			sourceText,
			msbuildDocument,
			new TextSpan (range.Start, range.Length),
			requestedKinds,
			options,
			reportAction,
			cancellationToken
		);

		IEnumerable<MSBuildCodeActionProvider> activeProviders = GetActiveProviders (requestedKinds, ctx.AllDiagnosticsInSpan);

		// TODO: parallelize this
		foreach (var provider in activeProviders) {
			await provider.RegisterCodeActionsAsync (ctx);
		}
	}

	/// <summary>
	/// Determines which providers may produces code actions of the requested kinds
	/// </summary>
	private IEnumerable<MSBuildCodeActionProvider> GetActiveProviders (ISet<MSBuildCodeActionKind> requestedKinds, IEnumerable<MSBuildDiagnostic> diagnosticsInSpan)
	{
		var diagnosticIdsInSpan = diagnosticsInSpan.Select (d => d.Descriptor.Id).ToSet ();

		// only populate this if we actually need it
		ISet<string>? diagnosticErrorIdsInSpan = null;

		var activeProviders = codeActionProviders.Where (provider => {
			// we used to use a map to find providers based on the IDs in the span, but it meant the same provider could be invoked multiple times.
			// while that's solvable, change to a simpler approach for now unless we determine it's a bottleneck.
			if (provider.FixableDiagnosticIds.Length > 0 && !provider.FixableDiagnosticIds.Any (diagnosticIdsInSpan.Contains)) {
				return false;
			}

			bool requestedErrorFixKind = false;

			foreach (var producedKind in provider.ProducedCodeActionKinds) {
				if (producedKind.MatchesRequest (requestedKinds)) {
					return true;
				}
				requestedErrorFixKind |= producedKind == MSBuildCodeActionKind.ErrorFix;
			}

			// MSBuildCodeActionKind.ErrorFix is a VS Editor kind that needs special handling, so we only check it if no earlier check already returned.
			// It means a fix that fixes a diagnostic that is an error. However, this cannot be statically determined as a diagnostic could
			// be upgraded to error severity via configuration e.g. editorconfig.
			if (requestedErrorFixKind && provider.FixableDiagnosticIds.Length > 0) {
				diagnosticErrorIdsInSpan ??= diagnosticsInSpan.Where (d => d.Descriptor.Severity == MSBuildDiagnosticSeverity.Error).Select (d => d.Descriptor.Id).ToSet ();
				return provider.FixableDiagnosticIds.Any (diagnosticErrorIdsInSpan.Contains);
			}

			return true;
		});

		return activeProviders;
	}
}

