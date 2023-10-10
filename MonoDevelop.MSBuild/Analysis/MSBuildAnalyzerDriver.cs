// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Analysis
{
	partial class MSBuildAnalyzerDriver
	{
		readonly MSBuildAnalysisContextImpl context;
		private readonly ILogger logger;

		public MSBuildAnalyzerDriver (ILogger logger)
		{
			context = new MSBuildAnalysisContextImpl (logger);
			this.logger = logger;
		}

		public void AddBuiltInAnalyzers ()
		{
			AddAnalyzersFromAssembly (typeof (MSBuildAnalyzerDriver).Assembly);
		}

		public void AddAnalyzersFromAssembly (Assembly assembly)
		{
			Type abstractType = typeof (MSBuildAnalyzer);
			foreach (var type in assembly.GetTypes ()) {
				if (abstractType.IsAssignableFrom (type) && type.GetCustomAttribute<MSBuildAnalyzerAttribute> () != null) {
					MSBuildAnalyzer analyzer;
					try {
						analyzer = (MSBuildAnalyzer)Activator.CreateInstance (type);
					} catch (Exception ex) {
						LogAnalyzerCreationError(logger, ex, type.FullName);
						continue;
					}
					context.RegisterAnalyzer (analyzer);
				}
			}
		}

		[LoggerMessage (Level = LogLevel.Error, Message = "Failed to create instance of analyzer {analyzer}")]
		static partial void LogAnalyzerCreationError (ILogger logger, Exception ex, string analyzer);

		public void AddAnalyzers (IEnumerable<MSBuildAnalyzer> analyzers)
		{
			foreach (var analyzer in analyzers) {
				context.RegisterAnalyzer (analyzer);
			}
		}

		public List<MSBuildDiagnostic> Analyze (MSBuildRootDocument doc, bool includeFilteredCoreDiagnostics, CancellationToken token)
		{
			doc = doc ?? throw new ArgumentNullException (nameof (doc));

			if (doc.ProjectElement is not MSBuildProjectElement projectElement) {
				return new List<MSBuildDiagnostic> ();
			}

			var session = new MSBuildAnalysisSession (context, doc, token);

			AnalyzeElement (projectElement, session, token);

			if (includeFilteredCoreDiagnostics) {
				session.AddCoreDiagnostics (doc.Diagnostics);
			}

			return session.Diagnostics;
		}


		static void AnalyzeElement (MSBuildElement element, MSBuildAnalysisSession session, CancellationToken token)
		{
			token.ThrowIfCancellationRequested ();

			session.ExecuteElementActions (element);

			foreach (var child in element.Elements) {
				AnalyzeElement (child, session, token);
			}

			foreach (var att in element.Attributes) {
				session.ExecuteAttributeActions (att);
			}

			if (element is MSBuildPropertyElement propertyElement) {
				session.ExecutePropertyWriteActions (propertyElement);
			}
		}
	}
}
