// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Extensions.Logging;

namespace MonoDevelop.MSBuild.Tests;

/// <summary>
/// Logger that rethrows exceptions logged to it
/// </summary>
class ExceptionRethrowingLogger : ILogger
{
	ILogger innerLogger;

	public ExceptionRethrowingLogger (ILogger innerLogger)
	{
		this.innerLogger = innerLogger;
	}

	public IDisposable BeginScope<TState> (TState state) where TState : notnull => innerLogger.BeginScope (state);

	public bool IsEnabled (LogLevel logLevel) => innerLogger.IsEnabled (logLevel);

	public void Log<TState> (LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
	{
		innerLogger.Log (logLevel, eventId, state, exception, formatter);
		if (exception is not null) {
			throw exception;
		}
	}
}
