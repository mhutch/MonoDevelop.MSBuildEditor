// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Xml.Editor.Parsing;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Parser;

class NullBackgroundParseService : IBackgroundParseService
{
    public static NullBackgroundParseService Instance { get; } = new();

    public bool IsRunning => throw new NotSupportedException();

    public event EventHandler RunningStateChanged
    {
        add => throw new NotSupportedException();
        remove => throw new NotSupportedException();
    }

    public void RegisterBackgroundOperation(Task task)
    {
    }
}
