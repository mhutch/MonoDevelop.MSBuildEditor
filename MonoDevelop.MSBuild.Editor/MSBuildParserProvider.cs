// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Editor;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	[Export]
	class MSBuildParserProvider
	{
		[Import (typeof (ITaskMetadataBuilder))]
		public ITaskMetadataBuilder TaskMetadataBuilder { get; set; }

		[Import (typeof (MSBuildSchemaProvider), AllowDefault = true)]
		public MSBuildSchemaProvider SchemaProvider { get; set; }

		[Import (typeof (IRuntimeInformation), AllowDefault = true)]
		public IRuntimeInformation RuntimeInformation { get; set; }

		public MSBuildBackgroundParser GetParser (ITextBuffer buffer)
		{
			buffer = GetSubjectBuffer (buffer);
			return buffer.Properties.GetOrCreateSingletonProperty (typeof (MSBuildBackgroundParser), () => CreateParser (buffer));
		}

		MSBuildBackgroundParser CreateParser (ITextBuffer buffer)
		{
			Debug.Assert (buffer.ContentType.IsOfType (MSBuildContentType.Name));
			buffer.ContentTypeChanged += ContentTypeChanged;

			var runtimeInfo = RuntimeInformation;
			if (runtimeInfo == null) {
				try {
					runtimeInfo = new MSBuildEnvironmentRuntimeInformation ();
				} catch (Exception ex) {
					LoggingService.LogError ("Failed to initialize runtime info for parser", ex);
					runtimeInfo = new NullRuntimeInformation ();
				}
			}
			return new MSBuildBackgroundParser (
				buffer,
				runtimeInfo,
				SchemaProvider ?? new MSBuildSchemaProvider (),
				TaskMetadataBuilder ?? new NoopTaskMetadataBuilder ()
			);
		}

		ITextBuffer GetSubjectBuffer (ITextBuffer textBuffer, string expectedContentType = XmlContentTypeNames.Xml)
		{
			if (textBuffer is IProjectionBuffer projectionBuffer) {
				textBuffer = projectionBuffer.SourceBuffers.FirstOrDefault (b => b.ContentType.IsOfType (expectedContentType));
				if (textBuffer == null) {
					throw new InvalidOperationException (
						$"Couldn't find a source buffer with content type {expectedContentType} in buffer {projectionBuffer}");
				}
			}

			return textBuffer;
		}

		void ContentTypeChanged (object sender, ContentTypeChangedEventArgs e)
		{
			if (!e.AfterContentType.IsOfType (MSBuildContentType.Name)) {
				var buffer = (ITextBuffer)sender;
				buffer.ContentTypeChanged -= ContentTypeChanged;
				if (buffer.Properties.TryGetProperty (typeof (MSBuildBackgroundParser), out MSBuildBackgroundParser parser)) {
					parser.Dispose ();
				}
				buffer.Properties.RemoveProperty (typeof (MSBuildBackgroundParser));
			}
		}
	}
}