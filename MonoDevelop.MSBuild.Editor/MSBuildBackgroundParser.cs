// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.Text;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Editor.Completion;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class MSBuildBackgroundParser : XmlBackgroundParser<MSBuildParseResult>
	{
		readonly IRuntimeInformation runtimeInformation = new ProjectCollectionRuntimeInformation (ProjectCollection.GlobalProjectCollection);

		protected override Task<MSBuildParseResult> StartParseAsync (
			ITextSnapshot2 snapshot, MSBuildParseResult previousParse,
			ITextSnapshot2 previousSnapshot, CancellationToken token)
		{
			return Task.Run (() => {
				var oldDoc = previousParse?.MSBuildDocument;

				//fixme
				string filename = "foo.csproj";
				var schemaProvider = new MSBuildSchemaProvider ();

				var doc = MSBuildRootDocument.Parse (filename, snapshot.GetTextSource (), oldDoc, schemaProvider, runtimeInformation, token);

				return new MSBuildParseResult (doc, doc.XDocument, doc.Errors);
			});
		}
	}
}