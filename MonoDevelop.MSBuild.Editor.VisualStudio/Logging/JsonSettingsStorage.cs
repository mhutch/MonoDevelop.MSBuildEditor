// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.IO;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.Logging;

class JsonSettingsStorage<T> where T: class, IEquatable<T>
{
	readonly object locker = new object ();

	readonly ILogger logger;
	readonly string settingsFilePath;
	readonly Func<T> createDefault;

	T? value;
	T? savedValue;

	public JsonSettingsStorage (ILogger logger, string settingsFilePath, Func<T> createDefault)
	{
		this.logger = logger;
		this.settingsFilePath = settingsFilePath;
		this.createDefault = createDefault;
	}

	public void LoadOrCreate () => LoadOrCreate (out _);

	public void LoadOrCreate (out bool wasCreated)
	{
		lock (locker) {
			try {
				if (File.Exists (settingsFilePath)) {
					var serializer = JsonSerializer.CreateDefault ();
					using var textReader = File.OpenText (settingsFilePath);
					using var jsonReader = new JsonTextReader (textReader);
					if (serializer.Deserialize<T> (jsonReader) is T settings) {
						value = settings;
						savedValue = settings;
						wasCreated = false;
						return;
					}
				}
			} catch (Exception ex) {
				logger.LogError (ex, "Error loading {typeof(T)} settings file");
			}

			value = createDefault ();
			wasCreated = true;
			Save ();
		}
	}

	static StreamWriter CreateTextAndDirectory (string filePath)
	{
		try {
			return File.CreateText (filePath);
		} catch (IOException) {
		}

		Directory.CreateDirectory (Path.GetDirectoryName (filePath));
		return File.CreateText (filePath);
	}

	void Save ()
	{
		try {
			using var textWriter = CreateTextAndDirectory (settingsFilePath);
			using var jsonWriter = new JsonTextWriter (textWriter);

			var serializerSettings = new JsonSerializerSettings {
				Formatting = Formatting.Indented
			};

			var serializer = JsonSerializer.Create (serializerSettings);
			serializer.Serialize (jsonWriter, Settings);
			savedValue = Settings;
		} catch (Exception ex) {
			logger.LogError (ex, $"Error saving {typeof(T)} settings file");
		}
	}

	public void SaveIfNeeded ()
	{
		lock (locker) {
			if (value is null)
				return;
			if (savedValue is not null && value.Equals (savedValue))
				return;

			Save ();
		}
	}

	public T Settings => value ?? throw new InvalidOperationException ("Settings not loaded");
}