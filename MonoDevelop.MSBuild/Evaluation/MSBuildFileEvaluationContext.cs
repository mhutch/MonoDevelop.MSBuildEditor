// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using Microsoft.Build.Shared;
using Microsoft.Extensions.Logging;

using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;

namespace MonoDevelop.MSBuild.Evaluation
{
	/// <summary>
	/// Provides MSBuild properties specific to the current file
	/// </summary>
	class MSBuildFileEvaluationContext : IMSBuildEvaluationContext
	{
		readonly IMSBuildEvaluationContext projectContext;
		readonly string absoluteFilePath;

		public ILogger Logger { get; }

		MSBuildFileEvaluationContext (IMSBuildEvaluationContext projectContext, ILogger logger, string thisFilePath)
		{
			Logger = logger;

			this.projectContext = projectContext ?? throw new ArgumentNullException (nameof (projectContext));

			// the path arguments should already be absolute but may not be for tests etc
			absoluteFilePath = Path.GetFullPath (thisFilePath ?? throw new ArgumentNullException (nameof (thisFilePath)));
		}

		public static IMSBuildEvaluationContext Create (IMSBuildEvaluationContext projectContext, ILogger logger, string? thisFilePath)
			=> thisFilePath is null
				? projectContext
				: new MSBuildFileEvaluationContext (projectContext, logger, thisFilePath);


		public bool TryGetProperty (string name, [NotNullWhen (true)] out EvaluatedValue? value)
		{
			if (projectContext.TryGetProperty (name, out value)) {
				return true;
			}

			if (ReservedPropertyNames.IsReservedProperty (name)) {
				if (absoluteFilePath is not null && GetReservedFileProperty (name, absoluteFilePath) is EvaluatedValue filePropertyValue) {
					value = filePropertyValue;
					return true;
				}
			}

			return false;
		}

		public bool TryGetMultivaluedProperty (string name, [NotNullWhen (true)] out OneOrMany<EvaluatedValue>? value, bool isProjectImportStart = false)
		{
			if (projectContext.TryGetMultivaluedProperty (name, out value, isProjectImportStart)) {
				return true;
			}

			if (ReservedPropertyNames.IsReservedProperty (name)) {
				if (absoluteFilePath is not null && GetReservedFileProperty (name, absoluteFilePath) is EvaluatedValue filePropertyValue) {
					value = filePropertyValue;
					return true;
				}
			}

			return false;
		}

		// see https://github.com/dotnet/msbuild/blob/d074c1250646c338f7eacb1ff8d9cbe5cf8ef3c6/src/Build/Evaluation/Expander.cs#L1559
		static EvaluatedValue? GetReservedFileProperty (string propertyName, string absoluteFilePath)
		{
			if (string.Equals (propertyName, ReservedPropertyNames.thisFile, StringComparison.OrdinalIgnoreCase)) {
				return EvaluatedValue.FromUnescaped (Path.GetFileName (absoluteFilePath));
			}
			if (string.Equals (propertyName, ReservedPropertyNames.thisFileName, StringComparison.OrdinalIgnoreCase)) {
				return EvaluatedValue.FromUnescaped (Path.GetFileNameWithoutExtension (absoluteFilePath));
			}
			if (string.Equals (propertyName, ReservedPropertyNames.thisFileFullPath, StringComparison.OrdinalIgnoreCase)) {
				return EvaluatedValue.FromNativePath (FileUtilities.NormalizePath (absoluteFilePath));
			}
			if (string.Equals (propertyName, ReservedPropertyNames.thisFileExtension, StringComparison.OrdinalIgnoreCase)) {
				return EvaluatedValue.FromUnescaped (Path.GetExtension (absoluteFilePath));
			}
			if (string.Equals (propertyName, ReservedPropertyNames.thisFileDirectory, StringComparison.OrdinalIgnoreCase)) {
				return EvaluatedValue.FromNativePath (FileUtilities.EnsureTrailingSlash (Path.GetDirectoryName (absoluteFilePath)));
			}
			if (string.Equals (propertyName, ReservedPropertyNames.thisFileDirectoryNoRoot, StringComparison.OrdinalIgnoreCase)) {
				string directory = Path.GetDirectoryName (absoluteFilePath);
				int rootLength = Path.GetPathRoot (directory).Length;
				return EvaluatedValue.FromNativePath (FileUtilities.EnsureTrailingNoLeadingSlash (directory, rootLength));
			}
			return null;
		}
	}
}