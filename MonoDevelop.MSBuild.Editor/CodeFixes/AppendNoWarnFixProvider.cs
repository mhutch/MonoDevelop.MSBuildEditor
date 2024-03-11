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
	class AppendNoWarnFixProvider : MSBuildFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create (
			AppendNoWarnAnalyzer.DiagnosticId
		);

		public override Task RegisterCodeFixesAsync (MSBuildFixContext context)
		{
			foreach (var diag in context.Diagnostics) {
				if (context.XDocument.FindAtOffset (diag.Span.Start) is XElement prop && prop.InnerSpan is TextSpan valueSpan) {
					context.RegisterCodeFix (new PrependListValueAction (valueSpan, "$(NoWarn)"), diag);
				}
			}
			return Task.CompletedTask;
		}

		class PrependListValueAction (TextSpan valueSpan, string valueToPrepend) : SimpleMSBuildCodeAction
		{
			public override string Title => $"Prepend '{valueToPrepend}' to list";

			protected override MSBuildCodeActionOperation CreateOperation ()
			{
				var op = new EditTextActionOperation ();
				op.Insert (valueSpan.Start, valueToPrepend + ";");
				return op;
			}
		}
	}
}
