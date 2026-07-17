// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.ComponentModel.Composition;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Editor.Classification;
using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Editor.Parsing;

namespace MonoDevelop.MSBuild.Editor
{
	/// <summary>
	/// Provides classification and structure taggers for MSBuild buffers, delegating to the host's
	/// TextMate service when it is available, and falling back to <see cref="MSBuildClassificationTagger"/>
	/// for classification when it is not (e.g. VS 2026, issue #279).
	/// </summary>
	[Export (typeof (ITaggerProvider))]
	[TagType (typeof (IClassificationTag))]
	[TagType (typeof (IStructureTag))]
	[ContentType (MSBuildContentType.Name)]
	sealed partial class MSBuildTextMateTagger : ITaggerProvider
	{
		readonly ICommonEditorAssetServiceFactory? assetServiceFactory;
		readonly XmlParserProvider parserProvider;
		readonly MSBuildClassificationTypeMap typeMap;
		readonly JoinableTaskContext joinableTaskContext;
		readonly IEditorLoggerFactory loggerFactory;

		/// <summary>
		/// Creates the tagger provider.
		/// </summary>
		/// <param name="assetServiceFactory">The host's TextMate asset service factory. May be missing in hosts that do not provide it.</param>
		/// <param name="parserProvider">Provider used to obtain per-buffer XML background parsers for the fallback tagger.</param>
		/// <param name="typeMap">Shared map from MSBuild syntax constructs to classification tags for the fallback tagger.</param>
		/// <param name="joinableTaskContext">The host's joinable task context.</param>
		/// <param name="loggerFactory">Factory for per-buffer loggers.</param>
		[ImportingConstructor]
		public MSBuildTextMateTagger (
			[Import (AllowDefault = true)] ICommonEditorAssetServiceFactory? assetServiceFactory,
			XmlParserProvider parserProvider,
			MSBuildClassificationTypeMap typeMap,
			JoinableTaskContext joinableTaskContext,
			IEditorLoggerFactory loggerFactory)
		{
			this.assetServiceFactory = assetServiceFactory;
			this.parserProvider = parserProvider;
			this.typeMap = typeMap;
			this.joinableTaskContext = joinableTaskContext;
			this.loggerFactory = loggerFactory;
		}

		/// <summary>
		/// Creates a tagger for the buffer, preferring the host's TextMate tagger and falling back
		/// to <see cref="MSBuildClassificationTagger"/> for classification tags.
		/// </summary>
		/// <param name="buffer">The text buffer to tag.</param>
		/// <returns>The tagger, or null if no tagger is available for the requested tag type.</returns>
		public ITagger<T>? CreateTagger<T> (ITextBuffer buffer) where T : ITag
		{
			if (TextMateSupport.IsAvailable && assetServiceFactory is not null) {
				ITagger<T>? textMateTagger = assetServiceFactory.GetOrCreate (buffer)
					.FindAsset<ITaggerProvider> (
						(metadata) => metadata.TagTypes.Any (tagType => typeof (T).IsAssignableFrom (tagType))
					)
					?.CreateTagger<T> (buffer);
				if (textMateTagger is not null) {
					return textMateTagger;
				}
			}

			// The host's TextMate service is unavailable, so fall back to classification based on our
			// own parsers. Structure tags need no fallback, as MonoDevelop.Xml's StructureTaggerProvider
			// independently handles xmlcore-derived content types.
			if (typeof (T).IsAssignableFrom (typeof (ClassificationTag))) {
				return (ITagger<T>)(object)buffer.Properties.GetOrCreateSingletonProperty (() => {
					ILogger logger = loggerFactory.GetLogger<MSBuildClassificationTagger> (buffer);
					LogUsingFallbackClassifier (logger, TextMateSupport.HostDescription, typeMap.ResolvedTypeNames);
					return new MSBuildClassificationTagger (buffer, parserProvider, typeMap, joinableTaskContext, logger);
				});
			}

			return null;
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Information, Message = "TextMate classification unavailable ({hostDescription}), using built-in MSBuild classification tagger with classification types: {resolvedTypeNames}")]
		static partial void LogUsingFallbackClassifier (ILogger logger, string hostDescription, string resolvedTypeNames);
	}
}
