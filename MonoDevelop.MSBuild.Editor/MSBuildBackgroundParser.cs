// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Text;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Editor.Completion;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class MSBuildBackgroundParser : XmlBackgroundParser<MSBuildParseResult>
	{
		readonly IRuntimeInformation runtimeInformation;
		readonly MSBuildSchemaProvider schemaProvider = new MSBuildSchemaProvider ();

		public MSBuildBackgroundParser ()
		{
			try {
				runtimeInformation = new MSBuildEnvironmentRuntimeInformation ();
			} catch (Exception ex) {
				LoggingService.LogError ("Failed to initialize runtime info for parser", ex);
				runtimeInformation = new NullRuntimeInformation ();
			}
		}

		protected override Task<MSBuildParseResult> StartParseAsync (
			ITextSnapshot2 snapshot, MSBuildParseResult previousParse,
			ITextSnapshot2 previousSnapshot, CancellationToken token)
		{
			return Task.Run (() => {
				var oldDoc = previousParse?.MSBuildDocument;

				var filepath = TryGetFilePath ();

				MSBuildRootDocument doc = MSBuildRootDocument.Empty;
				try {
					doc = MSBuildRootDocument.Parse (snapshot.GetTextSource (filepath), oldDoc, schemaProvider, runtimeInformation, token);
				} catch (Exception ex) {
					LoggingService.LogError ("Unhandled error in MSBuild parser", ex);
					doc = MSBuildRootDocument.Empty;
				}

				return new MSBuildParseResult (doc, doc.XDocument, doc.Errors);
			});
		}
	}
}