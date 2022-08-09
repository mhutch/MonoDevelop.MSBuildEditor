// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Analyzers;
using MonoDevelop.MSBuild.Editor.Analysis;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.CodeFixes
{
	[Export (typeof (MSBuildFixProvider))]
	class RemoveMSBuildAllProjectsAssignmentFixProvider : MSBuildFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; }
			= ImmutableArray.Create (DoNotAssignMSBuildAllProjectsAnalyzer.DiagnosticId);

		public override Task RegisterCodeFixesAsync (MSBuildFixContext context)
		{
			foreach (var diag in context.Diagnostics) {
				if (context.XDocument.FindAtOffset (diag.Span.Start) is XElement el) {
					context.RegisterCodeFix (new RemoveMSBuildAllProjectsAssignmentAction (el), diag);
				}
			}
			return Task.CompletedTask;
		}

		class RemoveMSBuildAllProjectsAssignmentAction : SimpleMSBuildCodeAction
		{
			readonly XElement element;
			public RemoveMSBuildAllProjectsAssignmentAction (XElement element) => this.element = element;
			public override string Title => $"Remove redundant assignment of 'MSBuildAllProjectsAssignment'";
			protected override MSBuildCodeActionOperation CreateOperation () => new EditTextActionOperation ().RemoveElement (element);
		}
	}
}
