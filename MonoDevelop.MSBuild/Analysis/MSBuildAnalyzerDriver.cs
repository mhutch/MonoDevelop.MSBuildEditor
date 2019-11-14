// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Analysis
{
	class MSBuildAnalyzerDriver
	{
		readonly MSBuildAnalysisContextImpl context;

		public MSBuildAnalyzerDriver ()
		{
			context = new MSBuildAnalysisContextImpl ();
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
						LoggingService.LogError ($"Failed to create instance of analyzer {type.FullName}", ex);
						continue;
					}
					context.RegisterAnalyzer (analyzer);
				}
			}
		}

		public void AddAnalyzers (IEnumerable<MSBuildAnalyzer> analyzers)
		{
			foreach (var analzyer in analyzers) {
				context.RegisterAnalyzer (analzyer);
			}
		}

		public List<MSBuildDiagnostic> Analyze (MSBuildRootDocument doc, CancellationToken token)
		{
			var session = new MSBuildAnalysisSession (context, doc, token);

			AnalyzeElement (doc.ProjectElement, session, token);

			session.AddCoreDiagnostics (doc.Diagnostics);

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
