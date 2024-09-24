// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;

using MonoDevelop.Xml.Tests;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests;

partial class MSBuildDocumentTest
{
	public static MSBuildRootDocument ParseDocumentWithDiagnostics (
		string source,
		IEnumerable<MSBuildAnalyzer>? analyzers = null,
		bool includeCoreDiagnostics = false,
		ILogger? logger = null,
		MSBuildSchema? schema = null,
		MSBuildRootDocument? previousDocument = null,
		IEnumerable<MSBuildDiagnosticDescriptor>? ignoreDiagnostics = null,
		CancellationToken cancellationToken = default
		)
	{
		// internal errors should cause test failure
		logger ??= TestLoggerFactory.CreateTestMethodLogger ().RethrowExceptions ();

		// suppress LogDidNotFindSdk messages as they are super noisy
		// and not relevant when testing diagnostics, as we will get diagnostics for them anyways
		logger = logger.WithFilter (["LogDidNotFindSdk"]);

		var parsedDocument = ParseDocument (source, logger, schema, previousDocument, cancellationToken);

		var analyzerDriver = new MSBuildAnalyzerDriver (logger);

		if (analyzers != null && analyzers.Any ()) {
			analyzerDriver.AddAnalyzers (analyzers);
		} else if (!includeCoreDiagnostics) {
			throw new ArgumentException ("Analyzers can only be null or empty if core diagnostics are included", nameof (analyzers));
		}

		var actualDiagnostics = analyzerDriver.Analyze (parsedDocument, includeCoreDiagnostics, cancellationToken);

		if (actualDiagnostics is not null && ignoreDiagnostics is not null) {
			var ignoredDiagnosticIds = ignoreDiagnostics.Select (d => d.Id).ToHashSet ();
			actualDiagnostics = actualDiagnostics.Where (a => !ignoredDiagnosticIds.Contains (a.Descriptor.Id)).ToList ();
		}

		parsedDocument.Diagnostics.Clear ();
		parsedDocument.Diagnostics.AddRange (actualDiagnostics ?? []);

		return parsedDocument;
	}

	public static void VerifyDiagnostics (string source, MSBuildAnalyzer analyzer, params MSBuildDiagnostic[] expectedDiagnostics)
		=> VerifyDiagnostics (source, out _, [analyzer], expectedDiagnostics: expectedDiagnostics);

	public static void VerifyDiagnostics (string source, ICollection<MSBuildAnalyzer> analyzers, bool includeCoreDiagnostics, params MSBuildDiagnostic[] expectedDiagnostics)
		=> VerifyDiagnostics (source, out _, analyzers, includeCoreDiagnostics, expectedDiagnostics: expectedDiagnostics);

	public static void VerifyDiagnostics (
		string source,
		ICollection<MSBuildAnalyzer>? analyzers = null,
		bool includeCoreDiagnostics = false,
		bool ignoreUnexpectedDiagnostics = false,
		MSBuildSchema? schema = null,
		MSBuildDiagnostic[]? expectedDiagnostics = null,
		ILogger? logger = null,
		MSBuildRootDocument? previousDocument = null,
		bool includeNoTargetsWarning = false,
		IEnumerable<MSBuildDiagnosticDescriptor>? ignoreDiagnostics = null
		)
		=> VerifyDiagnostics (source, out _, analyzers, includeCoreDiagnostics, ignoreUnexpectedDiagnostics, schema, expectedDiagnostics, logger, previousDocument, includeNoTargetsWarning, ignoreDiagnostics);

	public static void VerifyDiagnostics (
		string source,
		out MSBuildRootDocument parsedDocument,
		ICollection<MSBuildAnalyzer>? analyzers = null,
		bool includeCoreDiagnostics = false,
		bool ignoreUnexpectedDiagnostics = false,
		MSBuildSchema? schema = null,
		MSBuildDiagnostic[]? expectedDiagnostics = null,
		ILogger? logger = null,
		MSBuildRootDocument? previousDocument = null,
		bool includeNoTargetsWarning = false,
		IEnumerable<MSBuildDiagnosticDescriptor>? ignoreDiagnostics = null
		)
	{
		parsedDocument = ParseDocumentWithDiagnostics (source, analyzers, includeCoreDiagnostics, logger, schema, previousDocument, ignoreDiagnostics);
		var actualDiagnostics = parsedDocument.Diagnostics;

		var missingDiags = new List<MSBuildDiagnostic> ();

		foreach (var expectedDiag in expectedDiagnostics ?? []) {
			bool found = false;
			for (int i = 0; i < actualDiagnostics.Count; i++) {
				var actualDiag = actualDiagnostics[i];
				if (actualDiag.Descriptor == expectedDiag.Descriptor && actualDiag.Span.Equals (expectedDiag.Span)) {
					Assert.That (actualDiag.Properties ?? Enumerable.Empty<KeyValuePair<string, object>> (),
						Is.EquivalentTo (expectedDiag.Properties ?? Enumerable.Empty<KeyValuePair<string, object>> ())
						.UsingDictionaryComparer<string, object> ());
					// checks messageArgs
					Assert.That (actualDiag.GetFormattedMessageWithTitle (), Is.EqualTo (expectedDiag.GetFormattedMessageWithTitle ()));
					found = true;
					actualDiagnostics.RemoveAt (i);
					break;
				}
			}
			if (!found) {
				missingDiags.Add (expectedDiag);
			}
		}

		if (!includeNoTargetsWarning) {
			for (int i = 0; i < actualDiagnostics.Count; i++) {
				if (actualDiagnostics[i].Descriptor.Id == CoreDiagnostics.NoTargets_Id) {
					actualDiagnostics.RemoveAt (i);
					i--;
				}
			}
		}

		if (!ignoreUnexpectedDiagnostics && actualDiagnostics.Count > 0) {
			Assert.Fail ($"Found unexpected diagnostics: {string.Join (", ", actualDiagnostics.Select (diag => $"\n\t{diag.Descriptor.Id}@{diag.Span.Start}-{diag.Span.End}"))}");
		}

		if (missingDiags.Count > 0) {
			Assert.Fail ($"Did not find expected diagnostics: {string.Join (", ", missingDiags.Select (diag => $"{diag.Descriptor.Id}@{diag.Span.Start}-{diag.Span.End}"))}");
		}
	}
}
