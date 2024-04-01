// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

using MonoDevelop.MSBuild.Editor;
using MonoDevelop.Xml.Editor.Tests;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests;

[TestFixture]
class MSBuildExpandSelection 
	//subclass the XML tests to make sure we didn't break XML expand selection
	: Xml.Editor.Tests.Commands.ExpandSelectionTests
{
	protected override EditorCatalog CreateCatalog () => MSBuildTestEnvironment.CreateEditorCatalog ();
	protected override string ContentTypeName => MSBuildContentType.Name;

	//args are document, line, col, then the expected sequence of expansions
	[Test]
	[TestCase (
		"<Project><PropertyGroup><Foo Condition=\"true and ((false or $(foo)=='bar') and !('$(baz.Length)'==5)) and 'thing' != 'other thing'\">True</Foo></PropertyGroup></Project>",
		1, 91,
		"Length",
		"baz.Length",
		"$(baz.Length)",
		"'$(baz.Length)'",
		"'$(baz.Length)'==5",
		"('$(baz.Length)'==5)",
		"!('$(baz.Length)'==5)",
		"(false or $(foo)=='bar') and !('$(baz.Length)'==5)",
		"((false or $(foo)=='bar') and !('$(baz.Length)'==5))",
		"true and ((false or $(foo)=='bar') and !('$(baz.Length)'==5))",
		"true and ((false or $(foo)=='bar') and !('$(baz.Length)'==5)) and 'thing' != 'other thing'",
		"Condition=\"true and ((false or $(foo)=='bar') and !('$(baz.Length)'==5)) and 'thing' != 'other thing'\"",
		"<Foo Condition=\"true and ((false or $(foo)=='bar') and !('$(baz.Length)'==5)) and 'thing' != 'other thing'\">",
		"<Foo Condition=\"true and ((false or $(foo)=='bar') and !('$(baz.Length)'==5)) and 'thing' != 'other thing'\">True</Foo>",
		"<PropertyGroup><Foo Condition=\"true and ((false or $(foo)=='bar') and !('$(baz.Length)'==5)) and 'thing' != 'other thing'\">True</Foo></PropertyGroup>"
		)]
	public Task MSBuildTestExpandShrink (object[] args) => TestExpandShrink (args);
}
