// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

namespace MonoDevelop.MSBuild;

// consider moving some of these down to the XML layer?
static partial class MSBuildLoggerExtensions
{
	/// <summary>
	/// Helper for switch expressions to log a message about missing cases when they can gracefully return a default value instead of throwing
	/// </summary>
	/// <remarks>This must be kept internal so that analyzers and fixes don't use it, as it does not sanitize the callsite for PII.</remarks>
	internal static TReturn LogUnhandledCaseAndReturnDefaultValue<TReturn,TSwitchValue> (this ILogger logger, TReturn valueToReturn, TSwitchValue missingValue, [CallerMemberName] string? methodName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0) where TSwitchValue : notnull
	{
		LogUnhandledCase(logger, missingValue, methodName!, filePath!, lineNumber);
		return valueToReturn;
	}

	/// <remarks>This must be kept internal so that analyzers and fixes don't use it, as it does not sanitize the callsite for PII.</remarks>
	[LoggerMessage (EventId = 0, Level = LogLevel.Warning, Message = "Unhandled case '{missingValue}' in method {methodName} at {filePath}:{lineNumber}'")]
	static partial void LogUnhandledCase (ILogger logger, object missingValue, string methodName, string filePath, int lineNumber);
}
