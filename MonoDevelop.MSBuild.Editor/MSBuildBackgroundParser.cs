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
		object initLocker = new object ();
		IRuntimeInformation runtimeInformation;
		MSBuildSchemaProvider schemaProvider;
		ITaskMetadataBuilder taskMetadataBuilder;

		internal void Initialize (IRuntimeInformation runtimeInformation, MSBuildSchemaProvider schemaProvider, ITaskMetadataBuilder taskMetadataBuilder)
		{
			this.runtimeInformation = runtimeInformation ?? throw new ArgumentNullException (nameof (runtimeInformation));
			this.schemaProvider = schemaProvider ?? throw new ArgumentNullException (nameof (schemaProvider));
			this.taskMetadataBuilder = taskMetadataBuilder ?? throw new ArgumentNullException (nameof (taskMetadataBuilder));
		}

		void EnsureInitialized ()
		{
			lock (initLocker) {
				if (runtimeInformation != null) {
					return;
				}
				try {
					runtimeInformation = new MSBuildEnvironmentRuntimeInformation ();
				} catch (Exception ex) {
					LoggingService.LogError ("Failed to initialize runtime info for parser", ex);
					runtimeInformation = new NullRuntimeInformation ();
				}
				schemaProvider = new MSBuildSchemaProvider ();
				taskMetadataBuilder = new NoopTaskMetadataBuilder ();
			}
		}

		public static MSBuildBackgroundParser GetParser (ITextBuffer buffer)
			=> GetParser<MSBuildBackgroundParser>((ITextBuffer2)buffer);

		protected override Task<MSBuildParseResult> StartParseAsync (
			ITextSnapshot2 snapshot, MSBuildParseResult previousParse,
			ITextSnapshot2 previousSnapshot, CancellationToken token)
		{
			if (runtimeInformation == null) {
				EnsureInitialized ();
			}

			return Task.Run (() => {
				var oldDoc = previousParse?.MSBuildDocument;

				var filepath = TryGetFilePath ();

				MSBuildRootDocument doc = MSBuildRootDocument.Empty;
				try {
					doc = MSBuildRootDocument.Parse (snapshot.GetTextSource (filepath), oldDoc, schemaProvider, runtimeInformation, taskMetadataBuilder, token);
				} catch (Exception ex) when (!(ex is OperationCanceledException && token.IsCancellationRequested)) {
					LoggingService.LogError ("Unhandled error in MSBuild parser", ex);
					doc = MSBuildRootDocument.Empty;
				}

				return new MSBuildParseResult (doc, doc.XDocument, doc.Errors);
			}, token);
		}
	}
}