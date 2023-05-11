// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Editor.Logging;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	[Export]
	partial class MSBuildParserProvider
	{
		[ImportingConstructor]
		public MSBuildParserProvider (
			XmlParserProvider xmlParserProvider,
			MSBuildEnvironmentLogger environmentLogger,
			[Import (AllowDefault = true)] ITaskMetadataBuilder taskMetadataBuilder,
			[Import (AllowDefault = true)] MSBuildSchemaProvider schemaProvider,
			[Import (AllowDefault = true)] IMSBuildEnvironment msbuildEnvironment
			)
		{
			if (msbuildEnvironment == null) {
				try {
					msbuildEnvironment = new CurrentProcessMSBuildEnvironment (environmentLogger.Logger);
				} catch (Exception ex) {
					LogFailedRuntimeInitialization (environmentLogger.Logger, ex);
					msbuildEnvironment = new NullMSBuildEnvironment ();
				}
			}

			XmlParserProvider = xmlParserProvider;
			LoggerFactory = environmentLogger.Factory;
			TaskMetadataBuilder = taskMetadataBuilder ?? new NoopTaskMetadataBuilder ();
			SchemaProvider = schemaProvider ?? new MSBuildSchemaProvider ();
			MSBuildEnvironment = msbuildEnvironment;
		}

		public XmlParserProvider XmlParserProvider { get; }
		public ITaskMetadataBuilder TaskMetadataBuilder { get; }
		public MSBuildSchemaProvider SchemaProvider { get; }
		public IMSBuildEnvironment MSBuildEnvironment { get; }
		public IEditorLoggerFactory LoggerFactory { get; internal set; }

		public MSBuildBackgroundParser GetParser (ITextBuffer buffer)
		{
			buffer = GetSubjectBuffer (buffer);
			return buffer.Properties.GetOrCreateSingletonProperty (typeof (MSBuildBackgroundParser), () => CreateParser (buffer));
		}

		MSBuildBackgroundParser CreateParser (ITextBuffer buffer)
		{
			Debug.Assert (buffer.ContentType.IsOfType (MSBuildContentType.Name));
			buffer.ContentTypeChanged += ContentTypeChanged;

			return new (buffer, this);
		}

		ITextBuffer GetSubjectBuffer (ITextBuffer textBuffer, string expectedContentType = XmlContentTypeNames.XmlCore)
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

		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Failed to initialize MSBuild runtime info")]
		static partial void LogFailedRuntimeInitialization (ILogger logger, Exception ex);
	}
}