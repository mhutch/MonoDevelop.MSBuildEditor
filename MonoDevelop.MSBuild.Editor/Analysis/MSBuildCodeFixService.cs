// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Editor.Completion;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	[Export]
	class MSBuildCodeFixService
	{
		readonly Dictionary<string, MSBuildFixProvider> diagnosticIdToFixProviderMap = new Dictionary<string, MSBuildFixProvider> ();
		readonly MSBuildFixProvider[] fixProviders;

		[ImportingConstructor]
		public MSBuildCodeFixService ([ImportMany(typeof(MSBuildFixProvider))] MSBuildFixProvider[] fixProviders)
		{
			this.fixProviders = fixProviders;

			foreach (var provider in fixProviders) {
				foreach (var fixableId in provider.FixableDiagnosticIds) {
					diagnosticIdToFixProviderMap.Add (fixableId, provider);
				}
			}
		}

		/// <remarks>
		/// FIXME: The buffer parameter is workaround for the spellchecker. It can be removed when
		/// there is a better concept of a durable context/scope to which information can be bound.
		/// </remarks>
		public async Task<bool> HasFixes (
			ITextBuffer buffer, MSBuildParseResult result, SnapshotSpan range,
			MSBuildDiagnosticSeverity requestedSeverities, CancellationToken cancellationToken)
		{
			var filteredDiags = result.Diagnostics.Where (d => range.IntersectsWith (new SnapshotSpan (range.Snapshot, d.Span.Start, d.Span.Length)));

			bool foundFix = false;
			void ReportFix (MSBuildCodeAction a, ImmutableArray<MSBuildDiagnostic> d) => foundFix = true;

			//TODO invoke the provider once for all the diagnostics it supports 
			foreach (var diagnostic in filteredDiags) {
				if ((diagnostic.Descriptor.Severity | requestedSeverities) == 0) {
					continue;
				}
				if (cancellationToken.IsCancellationRequested) {
					return false;
				}
				if (range.IntersectsWith (new SnapshotSpan (range.Snapshot, diagnostic.Span.Start, diagnostic.Span.Length))) {
					if (diagnosticIdToFixProviderMap.TryGetValue (diagnostic.Descriptor.Id, out var fixProvider)) {
						var ctx = new MSBuildFixContext (
							buffer,
							result.MSBuildDocument,
							result.MSBuildDocument.XDocument,
							new Xml.Dom.TextSpan (range.Start, range.Length),
							ImmutableArray.Create (diagnostic),
							ReportFix, cancellationToken);
						await fixProvider.RegisterCodeFixesAsync (ctx);
						if (foundFix) {
							return true;
						}
					}
				}
			}

			return false;
		}

		public async Task<MSBuildDiagnosticSeverity> GetFixSeverity (
			ITextBuffer buffer, MSBuildParseResult result, SnapshotSpan range,
			MSBuildDiagnosticSeverity severities, CancellationToken cancellationToken)
		{
			var filteredDiags = result.Diagnostics.Where (d => range.IntersectsWith (new SnapshotSpan (range.Snapshot, d.Span.Start, d.Span.Length)));

			var severity = MSBuildDiagnosticSeverity.None;

			void ReportFix (MSBuildCodeAction a, ImmutableArray<MSBuildDiagnostic> diags) {
				foreach (var d in diags) {
					severity |= d.Descriptor.Severity;
				}
			};

			//TODO invoke the provider once for all the diagnostics it supports
			foreach (var diagnostic in filteredDiags) {
				if (cancellationToken.IsCancellationRequested) {
					return severity;
				}
				if (range.IntersectsWith (new SnapshotSpan (range.Snapshot, diagnostic.Span.Start, diagnostic.Span.Length))) {
					if (diagnosticIdToFixProviderMap.TryGetValue (diagnostic.Descriptor.Id, out var fixProvider)) {
						var ctx = new MSBuildFixContext (
							buffer,
							result.MSBuildDocument,
							result.MSBuildDocument.XDocument,
							new Xml.Dom.TextSpan (range.Start, range.Length),
							ImmutableArray.Create (diagnostic),
							ReportFix, cancellationToken);
						await fixProvider.RegisterCodeFixesAsync (ctx);

						//callers only really care about the highest severity, so if we got error, we can early out
						if ((severity | MSBuildDiagnosticSeverity.Error) != 0) {
							return severity;
						}
					}
				}
			}

			return severity;
		}

		/// <remarks>
		/// FIXME: The buffer parameter is workaround for the spellchecker. It can be removed when
		/// there is a better concept of a durable context/scope to which information can be bound.
		/// </remarks>
		public async Task<List<MSBuildCodeFix>> GetFixes (
			ITextBuffer buffer, MSBuildParseResult result, SnapshotSpan range,
			MSBuildDiagnosticSeverity requestedSeverities, CancellationToken cancellationToken)
		{
			var filteredDiags = ImmutableArray.CreateRange (
				result.Diagnostics.Where (d => range.IntersectsWith (new SnapshotSpan (range.Snapshot, d.Span.Start, d.Span.Length))));

			var fixes = new List<MSBuildCodeFix> ();
			void ReportFix (MSBuildCodeAction a, ImmutableArray<MSBuildDiagnostic> d)
			{
				lock (fixes) {
					fixes.Add (new MSBuildCodeFix (a, d));
				}
			}

			//TODO invoke the provider once for all the diagnostics it supports
			foreach (var diagnostic in filteredDiags) {
				if ((diagnostic.Descriptor.Severity | requestedSeverities) == 0) {
					continue;
				}
				if (cancellationToken.IsCancellationRequested) {
					return null;
				}
				if (range.IntersectsWith (new SnapshotSpan (range.Snapshot, diagnostic.Span.Start, diagnostic.Span.Length))) {
					if (diagnosticIdToFixProviderMap.TryGetValue (diagnostic.Descriptor.Id, out var fixProvider)) {
						var ctx = new MSBuildFixContext (
							buffer,
							result.MSBuildDocument,
							result.MSBuildDocument.XDocument,
							new Xml.Dom.TextSpan (range.Start, range.Length),
							ImmutableArray.Create (diagnostic),
							ReportFix, cancellationToken);
						await fixProvider.RegisterCodeFixesAsync (ctx);
					}
				}
			}

			return fixes;
		}
	}

	class MSBuildCodeFix
	{
		public string Category { get; }
		public MSBuildCodeAction Action { get; }
		public ImmutableArray<MSBuildDiagnostic> Diagnostics { get; }

		public MSBuildCodeFix (MSBuildCodeAction action, ImmutableArray<MSBuildDiagnostic> diagnostics)
		{
			Action = action;
			Diagnostics = diagnostics;
			if (diagnostics == null || diagnostics.Length == 0) {
				Category = PredefinedSuggestedActionCategoryNames.Refactoring;
			} else {
				Category = PredefinedSuggestedActionCategoryNames.CodeFix;
				foreach (var d in diagnostics) {
					if (d.Descriptor.Severity == MSBuildDiagnosticSeverity.Error) {
						Category = PredefinedSuggestedActionCategoryNames.ErrorFix;
						break;
					}
				}
			}
		}
	}
}
