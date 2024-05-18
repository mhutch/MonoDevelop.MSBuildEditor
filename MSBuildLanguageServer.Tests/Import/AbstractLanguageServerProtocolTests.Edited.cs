// heavily edited methods from 
// https://github.com/dotnet/roslyn/blob/1a4c3f429fe13a2e928c800cebbf93154447095a/src/EditorFeatures/TestUtilities/LanguageServer/AbstractLanguageServerProtocolTests.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.Test.Utilities;

using MSBuildLanguageServer.Tests;

namespace Roslyn.Test.Utilities
{
    partial class AbstractLanguageServerProtocolTests
    {
		protected static readonly TestComposition EditorFeaturesLspComposition = EditorTestCompositions.LanguageServerProtocolEditorFeatures;

		protected static readonly TestComposition FeaturesLspComposition = EditorTestCompositions.LanguageServerProtocol;
		protected virtual TestComposition Composition => EditorFeaturesLspComposition;
    }
}