// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using MonoDevelop.Xml.Parser;

using BF = System.Reflection.BindingFlags;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Parser;

static class LspTextExtensions
{
    public static int CompareTo(this VersionStamp version, VersionStamp other)
    {
        if(version == other)
        {
            return 0;
        }

        // FIXME: is there a better way to do this?
        var versionStampType = typeof(VersionStamp);
        var testAccessorType = versionStampType.GetNestedType("TestAccessor", BF.NonPublic)
            ?? throw new InvalidOperationException("Could not find VersionStamp.TestAccessor type");
        var getTestAccessor = versionStampType.GetMethod("GetTestAccessor", BF.NonPublic | BF.Instance)
            ?? throw new InvalidOperationException("Could not find VersionStamp.GetTestAccessor method");
        var isNewerThanMethod = testAccessorType.GetMethod("IsNewerThan", BF.NonPublic | BF.Instance)
            ?? throw new InvalidOperationException("Could not find VersionStamp.TestAccessor.IsNewerThan method");
        var testAccessor = getTestAccessor.Invoke(version, null)
            ?? throw new InvalidOperationException("Could not get test accessor for version");
        var result = (bool?)isNewerThanMethod.Invoke(testAccessor, [other])
            ?? throw new InvalidOperationException("Could not compare versions");

        return result ? 1 : -1;
    }

    // TODO: convert users of this to use SourceText instead
    public static ITextSource GetTextSource(this SourceText text)
        => new SourceTextTextSource(text);

    public static string GetText(this SourceText text, int start, int length)
        => text.ToString(new TextSpan(start, length));

    class SourceTextTextSource(SourceText text) : ITextSource
    {
        readonly SourceText text = text;

        public string GetText(int start, int length)
            => text.ToString(new TextSpan(start, length));

        public int Length => text.Length;

        public char this[int index] => text[index];

        public TextReader CreateReader()
            => new SourceTextTextReader(text);
    }

    class SourceTextTextReader(SourceText text) : TextReader
    {
        int position;

        public override int Peek()
        {
            if(position + 1 < text.Length)
            {
                return text[position + 1];
            }
            return -1;
        }

        public override int Read()
        {
            if(position < text.Length)
            {
                return text[position++];
            }
            return -1;
        }

        public override string ReadToEnd()
            => text.ToString(new TextSpan(position, text.Length - position));
    }
}
