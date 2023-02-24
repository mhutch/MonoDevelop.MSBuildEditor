// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.ComponentModel.Composition;

using Microsoft.Extensions.Logging;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Editor.HighlightReferences;

namespace MonoDevelop.Xml.Editor.Tests;

static class TestLoggers
{
	static readonly ILoggerFactory loggerFactory = LoggerFactory.Create (builder => builder
		.AddConsole ()
		.SetMinimumLevel (LogLevel.Debug)
	);

	public static ILogger CreateLogger (string categoryName) => loggerFactory.CreateLogger (categoryName);
	public static ILogger<T> CreateLogger<T> () => loggerFactory.CreateLogger<T> ();

	[Export]
	public static ILogger<MSBuildCompletionSource> MSBuildCompletionSource => CreateLogger<MSBuildCompletionSource> ();

	[Export]
	public static ILogger<MSBuildHighlightReferencesTagger> MSBuildHighlightReferencesTagger => CreateLogger<MSBuildHighlightReferencesTagger> ();
}