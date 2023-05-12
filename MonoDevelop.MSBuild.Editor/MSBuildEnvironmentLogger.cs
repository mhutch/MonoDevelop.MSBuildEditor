// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.Extensions.Logging;

using MonoDevelop.Xml.Editor.Logging;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	/// <summary>
	/// Shared logger any component can import
	/// </summary>
	[Export]
	class MSBuildEnvironmentLogger
	{
		[ImportingConstructor]
		public MSBuildEnvironmentLogger (IEditorLoggerFactory loggerFactory)
		{
			Factory = loggerFactory;
			Logger = loggerFactory.CreateLogger<MSBuildEnvironmentLogger> (MSBuildContentType.Name);
		}

		public ILogger Logger { get; }
		public IEditorLoggerFactory Factory { get; }
	}
}