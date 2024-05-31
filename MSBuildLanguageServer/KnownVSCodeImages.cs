// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable annotations

using System.Text;

namespace MonoDevelop.MSBuild.Editor;

enum KnownVSCodeImages
{
    // VS Code misc
    Error,
    Warning,
    Info,
    Target,
    Run,

    // VS Code symbols
    SymbolBoolean,
    SymbolClass,
    SymbolColor,
    SymbolConstant,
    SymbolConstructor,
    SymbolEnum,
    SymbolEnumMember,
    SymbolEvent,
    SymbolField,
    SymbolFile,
    SymbolFolder,
    SymbolFunction,
    SymbolInterface,
    SymbolKey,
    SymbolKeyword,
    SymbolMethod,
    SymbolMisc,
    SymbolModule,
    SymbolNamespace,
    SymbolNull,
    SymbolNumber,
    SymbolNumeric,
    SymbolObject,
    SymbolOperator,
    SymbolPackage,
    SymbolParameter,
    SymbolProperty,
    SymbolReference,
    SymbolRuler,
    SymbolSnippet,
    SymbolString,
    SymbolStruct,
    SymbolStructure,
    SymbolText,
    SymbolTypeParameter,
    SymbolUnit,
    SymbolValue,
    SymbolVariable,
}

static class KnownImagesExtensions
{
    public static string ToVSCodeImageId(this KnownVSCodeImages knownImage)
    {
        var name = knownImage.ToString();
        var sb = new StringBuilder(name.Length + 3);
        for(int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if(char.IsAsciiLetterUpper(c))
            {
                c = (char)(c + ('a' - 'A'));
                if(i > 0)
                {
                    sb.Append('-');
                }
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    public static KnownVSCodeImages ToVSCodeImage(this MSBuildGlyph glyph)
        => glyph switch {
            MSBuildGlyph.MSBuildKeyword => KnownVSCodeImages.SymbolKeyword,
            MSBuildGlyph.MSBuildItem => KnownVSCodeImages.SymbolObject,
            MSBuildGlyph.MSBuildProperty => KnownVSCodeImages.SymbolProperty,
            MSBuildGlyph.MSBuildTarget => KnownVSCodeImages.Target,
            MSBuildGlyph.MSBuildMetadata => KnownVSCodeImages.SymbolField,
            MSBuildGlyph.MSBuildTask => KnownVSCodeImages.Run,
            MSBuildGlyph.MSBuildTaskParameter => KnownVSCodeImages.SymbolParameter,
            MSBuildGlyph.MSBuildConstant => KnownVSCodeImages.SymbolConstant,
            MSBuildGlyph.MSBuildSdk => KnownVSCodeImages.SymbolFolder,
            MSBuildGlyph.MSBuildFrameworkId => KnownVSCodeImages.SymbolEnum,
            MSBuildGlyph.Deprecated => KnownVSCodeImages.Warning,
            MSBuildGlyph.Warning => KnownVSCodeImages.Warning,
            MSBuildGlyph.Information => KnownVSCodeImages.Info,
            MSBuildGlyph.Error => KnownVSCodeImages.Error,
            MSBuildGlyph.Folder => KnownVSCodeImages.SymbolFolder,
            MSBuildGlyph.File => KnownVSCodeImages.SymbolFile,
            MSBuildGlyph.DotNetProperty => KnownVSCodeImages.SymbolProperty,
            MSBuildGlyph.DotNetMethod => KnownVSCodeImages.SymbolMethod,
            MSBuildGlyph.DotNetClass => KnownVSCodeImages.SymbolClass,
            _ => throw new ArgumentException($"Unknown glyph '{glyph}'")
        };
}