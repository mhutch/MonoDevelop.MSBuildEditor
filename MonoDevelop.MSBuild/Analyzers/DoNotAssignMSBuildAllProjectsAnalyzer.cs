// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using MonoDevelop.MSBuild.Analysis;

namespace MonoDevelop.MSBuild.Analyzers
{
	// see http://www.panopticoncentral.net/2019/07/12/msbuildallprojects-considered-harmful/
	[MSBuildAnalyzer]
	class DoNotAssignMSBuildAllProjectsAnalyzer : MSBuildAnalyzer
	{
		public override ImmutableArray<MSBuildDiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create (
			new MSBuildDiagnosticDescriptor (
				"DoNotAssignMSBuildAllProjects",
				"Do not assign MSBuildAllProjects",
				"MSBuild 16 and later automatically adds the last modified project/target/props to MSBuildAllProjects, " +
				"so they no longer need add themselves to ensure incremental builds work correctly. " +
				"Remove the assignment to improve performance.",
				MSBuildDiagnosticSeverity.Warning
			)
		);

		public override void Initialize (MSBuildAnalysisContext context)
		{
			context.RegisterPropertyWriteAction (AnalyzeProperty, "MSBuildAllProjects");
		}

		void AnalyzeProperty (PropertyWriteDiagnosticContext ctx) => ctx.ReportDiagnostic (new MSBuildDiagnostic (SupportedDiagnostics[0], ctx.Element.Span));
	}
}
