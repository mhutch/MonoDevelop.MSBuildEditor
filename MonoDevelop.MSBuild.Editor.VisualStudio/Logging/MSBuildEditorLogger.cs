// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using MonoDevelop.Xml.Logging;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.Logging;

class MSBuildEditorLogger : ILogger
{
	readonly string categoryName;
	readonly MSBuildExtensionLogger extensionLogger;
	readonly ConcurrentDictionary<object, KeyValuePair<string, string>> scopes = new ();

	public MSBuildEditorLogger (string categoryName, MSBuildExtensionLogger extensionLogger)
	{
		this.categoryName = categoryName;
		this.extensionLogger = extensionLogger;
	}

	readonly record struct LoggerScope (MSBuildEditorLogger parent, object state) : IDisposable
	{
		public void Dispose () => parent.scopes.TryRemove (state, out _);
	}

	public IDisposable? BeginScope<TState> (TState state) where TState : notnull
	{
		if (!scopes.TryAdd (state, new KeyValuePair<string, string> (typeof (TState).Name, state.ToString ()))) {
			extensionLogger.Log (LogLevel.Error, (EventId) 0, this, null, couldNotAddScopeMessageFormatter);
		};
		return new LoggerScope (this, state);
	}

	static readonly Func<MSBuildEditorLogger, Exception?, string> couldNotAddScopeMessageFormatter = (_, _) =>"Could not add scope to logger";

	public void Log<TState> (LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		=> extensionLogger.Log(categoryName, scopes.Values, logLevel, eventId, state, exception, formatter);

	public bool IsEnabled (LogLevel logLevel) => extensionLogger.IsEnabled (logLevel);
}