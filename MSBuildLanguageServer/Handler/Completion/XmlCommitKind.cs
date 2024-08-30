// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

/// <summary>
/// Controls how XML completion items are committed
/// </summary>
enum XmlCommitKind
{
    Element,
    SelfClosingElement,
    Attribute,
    AttributeValue,
    CData,
    Comment,
    Prolog,
    Entity,
    ClosingTag,
    MultipleClosingTags
}
