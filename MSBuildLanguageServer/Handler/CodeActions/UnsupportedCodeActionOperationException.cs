// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Editor.CodeActions;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.CodeActions;

class UnsupportedCodeActionOperationException(MSBuildWorkspaceEditOperation? operation, bool isUnknown) : Exception
{
    public MSBuildWorkspaceEditOperation? Operation { get; } = operation;
    public bool IsUnknown { get; } = isUnknown;

    public override string Message => (IsUnknown || Operation is null)
            ? $"Code action returned unknown workspace edit operation '{Operation?.GetType().ToString() ?? "[null]"}'"
            : $"Code action returned unsupported workspace edit operation '{Operation.GetType()}'";
}