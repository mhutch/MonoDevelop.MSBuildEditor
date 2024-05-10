// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace MonoDevelop.MSBuild.Tests;

public static class MSBuildTestLogHelpers
{
	public static ILogger WithFilter (this ILogger logger, IEnumerable<string> ignoreEventNames)
	{
		return new FilteredLogger (logger, ignoreEventNames);
	}

	class FilteredLogger (ILogger inner, IEnumerable<string> ignoreEventNames) : ILogger
	{
		HashSet<string> ignoredEvents = new HashSet<string> (ignoreEventNames);

		public IDisposable? BeginScope<TState> (TState state) where TState : notnull => inner.BeginScope (state);

		public bool IsEnabled (LogLevel logLevel) => inner.IsEnabled (logLevel);

		public void Log<TState> (LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (eventId.Name is null || !ignoredEvents.Contains (eventId.Name)) {
				inner.Log (logLevel, eventId, state, exception, formatter);
			}
		}
	}

}
