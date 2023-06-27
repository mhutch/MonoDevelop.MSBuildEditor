// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.IO;

using Microsoft.Extensions.Logging;

using PropertyChanged.SourceGenerator;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.Logging;

// NOTE: this does not use the VS options API so that machine/user IDs can be shared with VS Code
// in future we could be smarter and try to unify with VS and VS Code roaming settings
partial class MSBuildEditorTelemetrySettings
{
	readonly ILogger logger;
	JsonSettingsStorage<LocalTelemetrySettings> localSettingsStorage;
	JsonSettingsStorage<RoamingTelemetrySettings> roamingSettingsStorage;

	public string? TelemetryStorageDir { get; private set; }

	public Guid UserId => roamingSettingsStorage.Settings.UserId;
	public Guid MachineId => localSettingsStorage.Settings.MachineId;

	public bool IsFirstSession { get; private set; }

	MSBuildEditorTelemetrySettings (ILogger logger, MSBuildEditorSettingsStorage settingsStorage)
	{
		const string settingsFilename = "TelemetrySettings.json";
		this.logger = logger;

		TelemetryStorageDir = Path.Combine (settingsStorage.LocalDataDir, "TelemetryStorage");

		localSettingsStorage = new (
			logger,
			Path.Combine (settingsStorage.LocalDataDir, settingsFilename),
			() => new LocalTelemetrySettings {
				MachineId = Guid.NewGuid (),
			});

		roamingSettingsStorage = new (
			logger,
			Path.Combine (settingsStorage.RoamingDataDir, settingsFilename),
			() => new RoamingTelemetrySettings {
				UserId = Guid.NewGuid (),
			});
	}

	void Load ()
	{
		TryCreateStorageDir ();

		roamingSettingsStorage.LoadOrCreate (out bool wasCreated);
		localSettingsStorage.LoadOrCreate ();

		IsFirstSession = wasCreated;
	}

	public static MSBuildEditorTelemetrySettings Load (ILogger logger, MSBuildEditorSettingsStorage settingsStorage)
	{
		var instance = new MSBuildEditorTelemetrySettings (logger, settingsStorage);
		instance.Load ();
		return instance;
	}

	void TryCreateStorageDir ()
	{
		try {
			Directory.CreateDirectory (TelemetryStorageDir);
		} catch (Exception ex) {
			logger.LogError (ex, "Error creating telemetry storage directory");
			TelemetryStorageDir = null;
		}
	}

	partial class LocalTelemetrySettings : IEquatable<LocalTelemetrySettings>
	{
		[Notify] Guid machineId;

		public bool Equals (LocalTelemetrySettings other) => machineId == other.machineId;
	}

	partial class RoamingTelemetrySettings : IEquatable<RoamingTelemetrySettings>
	{
		[Notify] Guid userId;

		public bool Equals (RoamingTelemetrySettings other) => userId == other.userId;
	}
}