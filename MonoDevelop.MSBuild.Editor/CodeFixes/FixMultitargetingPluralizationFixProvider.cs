// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Analyzers;
using MonoDevelop.MSBuild.Editor.Analysis;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.CodeFixes
{
	[Export (typeof (MSBuildFixProvider))]
	class FixMultitargetingPluralizationFixProvider : MSBuildFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; }
			= ImmutableArray.Create (
				TargetFrameworksOrTargetFrameworkAnalyzer.UseTargetFrameworksForMultipleFrameworksDiagnosticId,
				TargetFrameworksOrTargetFrameworkAnalyzer.UseTargetFrameworkForSingleFrameworkDiagnosticId,
				RuntimeIdentifierOrRuntimeIdentifiersAnalyzer.UseRuntimeIdentifiersForMultipleRIDsDiagnosticId,
				RuntimeIdentifierOrRuntimeIdentifiersAnalyzer.UseRuntimeIdentifierForSingleRIDDiagnosticId
			);

		public override Task RegisterCodeFixesAsync (MSBuildFixContext context)
		{
			foreach (var diag in context.Diagnostics) {
				var prop = context.XDocument.FindAtOffset (diag.Span.Start) as XElement;
				if (prop == null || prop.ClosingTag == null || prop.IsSelfClosing) {
					//FIXME log error?
					continue;
				}

				string newName = diag.Descriptor.Id switch
				{
					TargetFrameworksOrTargetFrameworkAnalyzer.UseTargetFrameworksForMultipleFrameworksDiagnosticId => "TargetFrameworks",
					TargetFrameworksOrTargetFrameworkAnalyzer.UseTargetFrameworkForSingleFrameworkDiagnosticId => "TargetFramework",
					RuntimeIdentifierOrRuntimeIdentifiersAnalyzer.UseRuntimeIdentifiersForMultipleRIDsDiagnosticId => "RuntimeIdentifiers",
					RuntimeIdentifierOrRuntimeIdentifiersAnalyzer.UseRuntimeIdentifierForSingleRIDDiagnosticId => "RuntimeIdentifier",
					_ => throw new InvalidOperationException ()
				};

				context.RegisterCodeFix (new ChangePropertyNameAction (prop, newName), diag);
			}

			return Task.CompletedTask;
		}

		class ChangePropertyNameAction : SimpleMSBuildCodeAction
		{
			readonly XElement prop;
			readonly string newName;

			public ChangePropertyNameAction (XElement prop, string newName)
			{
				this.prop = prop;
				this.newName = newName;
			}

			public override string Title => $"Change '{prop.Name}' to '{newName}'";

			protected override MSBuildCodeActionOperation CreateOperation ()
				=> new EditTextActionOperation ().RenameElement (prop, newName);
		}
	}
}