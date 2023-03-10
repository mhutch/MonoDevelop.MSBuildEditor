// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.ComponentModel.Composition;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Editor.HighlightReferences;
using MonoDevelop.Xml.Editor.Logging;

namespace MonoDevelop.Xml.Editor.Tests;

static class TestLoggers
{
	static readonly ILoggerFactory loggerFactory = LoggerFactory.Create (builder => builder
		.AddConsole ()
		.SetMinimumLevel (LogLevel.Debug)
	);

	public static ILogger<T> CreateLogger<T> () => loggerFactory.CreateLogger<T> ();
}

[Export (typeof (IEditorLoggerFactory))]
[ContentType (XmlContentTypeNames.XmlCore)]
class TestEditorLoggerFactory : IEditorLoggerFactory
{
	public ILogger<T> CreateLogger<T> () => TestLoggers.CreateLogger<T> ();

	public ILogger<T> CreateLogger<T> (ITextBuffer buffer) => CreateLogger<T> ();

	public ILogger<T> CreateLogger<T> (ITextView textView) => CreateLogger<T> ();
}