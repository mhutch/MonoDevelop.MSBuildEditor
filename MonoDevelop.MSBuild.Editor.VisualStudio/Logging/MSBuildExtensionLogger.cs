// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Shell;

using MonoDevelop.MSBuild.Editor.VisualStudio.Options;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.Logging;

class MSBuildExtensionLogger : ILogger
{
	MSBuildEditorExtensionTelemetry telemetry;
	MSBuildOutputPaneWriter outputPaneWriter;

	const LogLevel activityLogLogLevel = LogLevel.Warning;
	const LogLevel telemetryLogLevel = LogLevel.Warning;
	const LogLevel outputWindowLogLevel = LogLevel.Error;
	const LogLevel debuggerLogLogLevel = LogLevel.Information;

	static LogLevel GetCombinedLogLevel (bool telemetryEnabled)
	{
		LogLevel logLevel = activityLogLogLevel;
		logLevel = (LogLevel)Math.Min ((int)logLevel, (int)outputWindowLogLevel);
		if (telemetryEnabled) {
			logLevel = (LogLevel)Math.Min ((int)logLevel, (int)telemetryLogLevel);
		}
		if (Debugger.IsLogging ()) {
			logLevel = (LogLevel)Math.Min ((int)logLevel, (int)debuggerLogLogLevel);
		}
		return logLevel;
	}

	static bool ShouldLog (LogLevel threshold, LogLevel messageLevel) => messageLevel >= threshold;

	LogLevel combinedLogLevel;

	public MSBuildExtensionLogger (MSBuildEditorSettingsStorage settingsStorage, MSBuildTelemetryOptions options)
	{
		telemetry = new (this, settingsStorage);
		outputPaneWriter = new (this);

		UpdateTelemetryOptions (options);
	}

	public void UpdateTelemetryOptions (MSBuildTelemetryOptions options)
	{
		bool telemetryEnabled = Microsoft.VisualStudio.Telemetry.TelemetryService.DefaultSession.IsOptedIn && options.IsEnabled;
		combinedLogLevel = GetCombinedLogLevel (telemetryEnabled);
		telemetry.UpdateRunningState (telemetryEnabled);
	}

	public void ShutdownTelemetry () => telemetry.Dispose ();

	public ILogger CreateEditorLogger (string categoryName) => new MSBuildEditorLogger (categoryName, this);

	public bool IsEnabled (LogLevel logLevel) => ShouldLog (combinedLogLevel, logLevel);

	public void Log<TState> (LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		=> Log (null, null, logLevel, eventId, state, exception, formatter);

	public void Log<TState> (string? categoryName, IEnumerable<KeyValuePair<string, string>>? properties, LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		string? message = null;
		string GetMessage () => message ??= formatter (state, exception);

		// Community Toolkit, logs to VS output window
		// hacky to log a null exception but there doesn't seem to be an easy alternative
		// and it does handle null exceptions
		if (ShouldLog (outputWindowLogLevel, logLevel)) {
			outputPaneWriter.WriteMessage (exception, eventId, logLevel, GetMessage ());
		}

		if (ShouldLog (debuggerLogLogLevel, logLevel) && Debugger.IsLogging ()) {
			Debugger.Log ((int)logLevel, categoryName, GetMessage ());
		}

		if (ShouldLog (activityLogLogLevel, logLevel)) {
			switch (logLevel) {
			case LogLevel.Critical:
			case LogLevel.Error:
				ActivityLog.LogError ("MSBuildEditor", GetMessage ());
				break;
			case LogLevel.Warning:
				ActivityLog.LogWarning ("MSBuildEditor", GetMessage ());
				break;
			case LogLevel.Information:
				ActivityLog.LogInformation ("MSBuildEditor", GetMessage ());
				break;
			default:
				break;
			}
		}

		// telemetry may be null for log messages from telemetry instantiation
		if (telemetry is not null && ShouldLog (telemetryLogLevel, logLevel)) {
			var sanitizedMessage = telemetry.Sanitizer.TryGetSanitizedLogMessage (state, out bool sanitizationFailed) ?? GetMessage ();

			var telemetryProperties = properties;
			if (sanitizationFailed) {
				telemetryProperties = telemetryProperties.Append (new KeyValuePair<string, string> ("sanitizationFailed", "true"));
			}
			if (exception is not null) {
				telemetry.TrackException (exception, logLevel, sanitizedMessage, categoryName, eventId, telemetryProperties);
			} else {
				telemetry.TrackTrace (logLevel, sanitizedMessage, categoryName, eventId, telemetryProperties);
			}
		}
	}

	public IDisposable? BeginScope<TState> (TState state) where TState : notnull => null;
}
