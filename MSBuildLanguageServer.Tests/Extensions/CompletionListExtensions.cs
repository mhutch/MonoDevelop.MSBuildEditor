// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.LanguageServer.Tests;

static class CompletionListExtensions
{
    public static void AssertContains(this LSP.CompletionList list, string name)
    {
        var item = list.Items.FirstOrDefault(i => i.Label == name);
        Assert.NotNull(item); // "Completion result is missing item '{0}'", name);
    }

    public static void AssertNonEmpty([NotNull] this LSP.CompletionList? list)
    {
        Assert.NotNull(list);
        Assert.NotEmpty(list.Items);
    }

    public static void AssertItemCount([NotNull] this LSP.CompletionList? list, int expectedCount)
    {
        Assert.NotNull(list);
        Assert.Equal(expectedCount, list.Items.Length);
    }

    public static void AssertDoesNotContain(this LSP.CompletionList list, string name)
    {
        var item = list.Items.FirstOrDefault(i => i.Label == name);
        Assert.Null(item); //, "Completion result has unexpected item '{0}'", name);
    }
}
