// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.ComponentModel.Composition;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.Xml.Editor.Logging;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.Logging;

[Export (typeof (IEditorLoggerProvider))]
[Name ("MSBuild Editor Logger Provider")]
[ContentType (MSBuildContentType.Name)]
class MSBuildEditorLoggerProvider : IEditorLoggerProvider
{
	readonly MSBuildExtensionLogger? loggerFactory;

	[ImportingConstructor]
	public MSBuildEditorLoggerProvider (SVsServiceProvider serviceProvider)
	{
		loggerFactory = (MSBuildExtensionLogger) serviceProvider.GetService (typeof (MSBuildExtensionLogger));
	}

	public ILogger CreateLogger (string categoryName) => loggerFactory?.CreateEditorLogger (categoryName) ?? NullLogger.Instance;

	public void Dispose () { }
}
