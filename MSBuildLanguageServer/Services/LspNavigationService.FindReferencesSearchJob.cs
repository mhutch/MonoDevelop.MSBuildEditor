// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Services;

partial class LspNavigationService
{
    class FindReferencesSearchJob
    {
        public FindReferencesSearchJob(string filename, XDocument? document, SourceText? sourceText)
        {
            Filename = filename;
            Document = document;
            SourceText = sourceText;
        }

        public string Filename { get; }
        public XDocument? Document { get; set; }
        public SourceText? SourceText { get; set; }
    }
}
