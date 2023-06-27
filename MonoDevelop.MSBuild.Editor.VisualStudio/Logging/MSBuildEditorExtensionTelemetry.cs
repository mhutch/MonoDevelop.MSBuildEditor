// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Editor.VisualStudio.Options;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.Logging;

sealed partial class MSBuildEditorExtensionTelemetry : IDisposable
{
	const string ConnectionString = "InstrumentationKey=80591d65-3815-4ee1-9572-d1b3691a83b1;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/";

	object locker = new object();
	TelemetryClient? client;
	bool isRunning;
	ILogger logger;

	public MSBuildEditorTelemetrySettings Settings { get; }

	static string ExtensionVersion => ThisAssembly.AssemblyInformationalVersion;

	readonly string VisualStudioVersion;

	public MSBuildEditorExtensionTelemetry (ILogger logger, MSBuildEditorSettingsStorage settingsStorage)
	{
		VisualStudioVersion = GetAssemblyVersion(typeof (Microsoft.VisualStudio.Shell.Package).Assembly);
		this.logger = logger;
		Settings = MSBuildEditorTelemetrySettings.Load (logger, settingsStorage);
	}

	[MemberNotNullWhen (true, nameof (client))]
	public bool IsRunning => client is not null && isRunning;

	public UserIdentifiableValueSanitizer Sanitizer { get; } = new ();

	static string GetAssemblyVersion(Assembly assembly)
		=> assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute> ()?.InformationalVersion
		?? assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version
		?? assembly.GetName ().Version.ToString ();

	public void UpdateRunningState (bool isOptedIn)
	{
		isRunning = isOptedIn;
		if (!isRunning || client is not null) {
			return;
		}

		lock (locker) {
			if (client is not null) {
				return;
			}
			try {
				client = CreateClient ();
			} catch (Exception ex) {
				logger.LogError (ex, "Failed to start telemetry");
			}
		}
	}

	TelemetryClient? CreateClient ()
	{
		var config = new TelemetryConfiguration { ConnectionString = ConnectionString };

		var channel = new ServerTelemetryChannel {
			DeveloperMode = true //Debugger.IsAttached
		};

		if (Settings.TelemetryStorageDir is string storageDir) {
			channel.StorageFolder = storageDir;
		}

		channel.Initialize (config);
		config.TelemetryChannel = channel;

		var client = new TelemetryClient (config);

		client.Context.User.Id = Settings.UserId.ToString ();
		client.Context.Device.Id = Settings.MachineId.ToString ();
		client.Context.Session.Id = Guid.NewGuid ().ToString ();
		client.Context.Session.IsFirst = Settings.IsFirstSession;

		client.Context.Component.Version = ExtensionVersion;
		client.Context.GlobalProperties.Add ("VisualStudioVersion", VisualStudioVersion);
		client.Context.GlobalProperties.Add ("ExtensionVersion", ExtensionVersion);

		// stop app insights autopopulating this with the machine name
		client.Context.Cloud.RoleInstance = "VS";

		var sessionStart = new EventTelemetry ("SessionStart");
		client.TrackEvent (sessionStart);
		return client;
	}

	public void TrackException (Exception ex, LogLevel logLevel = LogLevel.Error, string? sanitizedMessage = null, string? category = null, EventId? eventId = default, IEnumerable<KeyValuePair<string,string>>? properties = null)
	{
		if (!IsRunning) {
			return;
		}

		var exceptionTelemetry = new ExceptionTelemetry (ex);
		if (sanitizedMessage is not null) {
			exceptionTelemetry.Message = sanitizedMessage;
		}
		AddSanitizedProperties (exceptionTelemetry, properties, category, eventId);

		foreach (var detail in exceptionTelemetry.ExceptionDetailsInfoList) {
			detail.Message = Sanitizer.HashUserString (detail.Message);
		}

		exceptionTelemetry.SeverityLevel = MapSeverity (logLevel);

		client.TrackException (exceptionTelemetry);
	}

	public void TrackTrace (LogLevel logLevel, string sanitizedMessage, string? category = null, EventId? eventId = default, IEnumerable<KeyValuePair<string, string>>? properties = null)
	{
		if (!IsRunning) {
			return;
		}

		var telemetry = new TraceTelemetry (sanitizedMessage, MapSeverity (logLevel));
		AddSanitizedProperties (telemetry, properties, category, eventId);

		client.TrackTrace (telemetry);
	}

	void AddSanitizedProperties (ISupportProperties telemetry, IEnumerable<KeyValuePair<string, string>>? properties, string? category = null, EventId? eventId = default)
	{
		if (properties is not null) {
			foreach (var property in properties) {
				telemetry.Properties[property.Key] = Sanitizer.Sanitize (property.Value)?.ToString ();
			}
		}
		if (category is string c) {
			telemetry.Properties.Add ("Category", c);
		}
		if (eventId is EventId ei) {
			if (ei.Id is int eventIdVal) {
				telemetry.Properties.Add ("EventId", eventIdVal.ToString ());
			}
			if (ei.Name is string eventName) {
				telemetry.Properties.Add ("EventName", eventName.ToString ());
			}
		}
	}

	SeverityLevel MapSeverity (LogLevel logLevel) => logLevel switch {
		LogLevel.Critical => SeverityLevel.Critical,
		LogLevel.Error => SeverityLevel.Error,
		LogLevel.Warning => SeverityLevel.Warning,
		LogLevel.Information => SeverityLevel.Information,
		LogLevel.Debug => SeverityLevel.Verbose,
		LogLevel.Trace => SeverityLevel.Verbose,
		_ => SeverityLevel.Information
	};

	public void Dispose ()
	{
		if (!isRunning) {
			return;
		}
		isRunning = false;

		try {
			client?.Flush ();
			client = null;
		} catch (Exception ex) {
			logger.LogError (ex, "Telemetry did not shut down cleanly");
		}
	}
}
