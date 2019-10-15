// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.MiniEditor;

using MonoDevelop.MSBuild.Editor;
using MonoDevelop.MSBuild.Editor.Roslyn;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Projects.MSBuild.Conditions;
using MonoDevelop.Xml.Tests.Completion;
using MonoDevelop.Xml.Tests.EditorTestHelpers;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
    [TestFixture]
    public class MSBuildConditionTests : CompletionTestBase
    {
        [OneTimeSetUp]
        public void LoadMSBuild() => MSBuildTestHelpers.RegisterMSBuildAssemblies();

        protected override string ContentTypeName => MSBuildContentType.Name;

        protected override (EditorEnvironment, EditorCatalog) InitializeEnvironment() => MSBuildTestEnvironment.EnsureInitialized();

        [Test]
        [Ignore("https://github.com/mhutch/MonoDevelop.MSBuildEditor/issues/23")]
        public void TestCondition1()
        {
            var condition = @"@(AssemblyAttribute->WithMetadataValue('A', 'B')->Count()) == 0";
            var expression = ConditionParser.ParseCondition(condition);
        }
    }
}