// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.ComponentModel.Composition;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Editor;
using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Tests;

namespace MonoDevelop.MSBuild.Tests.Editor;

[Export (typeof (IEditorLoggerProvider))]
[ContentType (MSBuildContentType.Name)]
class MSBuildTestEditorLoggerProvider : IEditorLoggerProvider
{
	public ILogger CreateLogger (string categoryName) => TestLoggerFactory.CreateLogger (categoryName);

	public void Dispose () { }
}