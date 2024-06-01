// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable annotations

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Editor;

enum MSBuildGlyph
{
    MSBuildKeyword,
    MSBuildItem,
    MSBuildProperty,
    MSBuildTarget,
    MSBuildMetadata,
    MSBuildTask,
    MSBuildTaskParameter,
    MSBuildConstant,
    MSBuildSdk,
    MSBuildFrameworkId,
    Deprecated,
    Information,
    Warning,
    Error,
    Folder,
    File,
    DotNetProperty,
    DotNetMethod,
    DotNetClass,
}

static class MSBuildGlyphExtensions
{
    public static MSBuildGlyph? GetGlyph(this ISymbol info, bool isPrivate)
    {
        switch(info)
        {
        case MSBuildElementSyntax el:
            if(!el.IsAbstract)
                return MSBuildGlyph.MSBuildKeyword;
            break;
        case MSBuildAttributeSyntax att:
            if(!att.IsAbstract)
            {
                return MSBuildGlyph.MSBuildKeyword;
            }
            break;
        case ItemInfo:
            return MSBuildGlyph.MSBuildItem;
        case PropertyInfo:
            return MSBuildGlyph.MSBuildProperty;
        case TargetInfo:
            return MSBuildGlyph.MSBuildTarget;
        case MetadataInfo:
            return MSBuildGlyph.MSBuildMetadata;
        case TaskInfo:
            return MSBuildGlyph.MSBuildTask;
        case ConstantSymbol:
            return MSBuildGlyph.MSBuildConstant;
        case FileOrFolderInfo value:
            return value.IsFolder ? MSBuildGlyph.Folder : MSBuildGlyph.File;
        case FrameworkInfo:
            return MSBuildGlyph.MSBuildFrameworkId;
        case TaskParameterInfo:
            return MSBuildGlyph.MSBuildTaskParameter;
        case FunctionInfo fi:
            if(fi.IsProperty)
            {
                //FIXME: can we resolve the msbuild / .net property terminology overloading?
                return MSBuildGlyph.DotNetProperty;
            }
            return MSBuildGlyph.DotNetMethod;
        case ClassInfo _:
            return MSBuildGlyph.DotNetClass;
        }
        return null;
    }

    public static MSBuildGlyph ToGlyph(this MSBuildDiagnosticSeverity severity)
        => severity switch {
            MSBuildDiagnosticSeverity.Error => MSBuildGlyph.Error,
            MSBuildDiagnosticSeverity.Warning => MSBuildGlyph.Warning,
            _ => MSBuildGlyph.Information,
        };
}


