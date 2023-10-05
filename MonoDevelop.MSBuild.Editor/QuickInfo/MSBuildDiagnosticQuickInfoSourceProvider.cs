// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.Xml.Editor.Logging;

namespace MonoDevelop.MSBuild.Editor.QuickInfo;

[Export (typeof (IAsyncQuickInfoSourceProvider))]
[Name (ProviderName)]
[ContentType (MSBuildContentType.Name)]
[Order (After = MSBuildQuickInfoSourceProvider.ProviderName)]
class MSBuildDiagnosticQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
{
	public const string ProviderName = "MSBuild Diagnostic Quick Info Provider";

	[ImportingConstructor]
	public MSBuildDiagnosticQuickInfoSourceProvider (
		IBufferTagAggregatorFactoryService tagAggregatorFactoryService,
		DisplayElementFactory displayElementFactory,
		IEditorLoggerFactory loggerFactory)
	{
		TagAggregatorFactoryService = tagAggregatorFactoryService;
		DisplayElementFactory = displayElementFactory;
		LoggerFactory = loggerFactory;
	}

	public IBufferTagAggregatorFactoryService TagAggregatorFactoryService { get; }
	public DisplayElementFactory DisplayElementFactory { get; }
	public IEditorLoggerFactory LoggerFactory { get; }

	public IAsyncQuickInfoSource TryCreateQuickInfoSource (ITextBuffer textBuffer)
		=> textBuffer.Properties.GetOrCreateSingletonProperty (() => {
			var logger = LoggerFactory.CreateLogger<MSBuildDiagnosticQuickInfoSource> (textBuffer);
			var tagAggregator = TagAggregatorFactoryService.CreateTagAggregator<MSBuildDiagnosticTag> (textBuffer);

			return new MSBuildDiagnosticQuickInfoSource (textBuffer, logger, tagAggregator, DisplayElementFactory);
		});
}