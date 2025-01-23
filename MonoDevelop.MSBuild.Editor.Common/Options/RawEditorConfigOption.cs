// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.EditorConfig.Parsing;
using Microsoft.CodeAnalysis.Text;

namespace MonoDevelop.MSBuild.Editor.Options;


//  msbuild scope/lifetime: not valid before this this point, after this point. editor can squiggle, build check can warn, eventually engine can enforce
// another scope: env var, editor uses this to stop marking thing ans unused, build checks hard enforces it
// plenty of points people use variables where they are no longer valid

record class RawEditorConfigOption (Section Section, string name, string value, TextSpan? Span)
	: EditorConfigOption (Section, Span)
{
}
