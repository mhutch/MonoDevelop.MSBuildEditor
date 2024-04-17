// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable annotations

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Language;

/// <summary>
/// The result of resolving an MSBuild reference at an offset in a document
/// </summary>
class MSBuildResolveResult
{
	readonly MSBuildResolver.MSBuildMutableResolveResult inner;

	internal MSBuildResolveResult (MSBuildResolver.MSBuildMutableResolveResult inner)
	{
		if (inner.Reference is null && inner.ReferenceKind != MSBuildReferenceKind.None) {
			throw new ArgumentException ("inner.Reference must be non-null when ReferenceKind is not ReferenceKind.None", nameof (inner));
		}
		this.inner = inner;
	}

	object? Reference => inner.Reference;

	public MSBuildReferenceKind ReferenceKind => inner.ReferenceKind;
	public int ReferenceOffset => inner.ReferenceOffset;
	public int ReferenceLength => inner.ReferenceLength;

	public XElement? Element => inner.Element;
	public XAttribute? Attribute => inner.Attribute;

	public MSBuildElementSyntax? ElementSyntax => inner.ElementSyntax;
	public MSBuildAttributeSyntax? AttributeSyntax => inner.AttributeSyntax;

	public ITypedSymbol? ElementSymbol => inner.ElementSymbol;
	public ITypedSymbol? AttributeSymbol=> inner.AttributeSymbol;

	public string? AttributeName => Attribute?.Name.Name;
	public string? ElementName => Element?.Name.Name;
	public string? ParentName => (Element?.Parent as XElement)?.Name.Name;

	[DoesNotReturn]
	T AssertKind<T> (MSBuildReferenceKind kind, [CallerMemberName] string? caller = null)
	{
		if (ReferenceKind == kind && Reference is T value) {
			return value;
		}
		if (ReferenceKind != kind) {
			throw new InvalidOperationException ($"MSBuildResolveResult.{caller} can only be called when {ReferenceKind} is {kind}");
		}
		throw new InvalidOperationException ($"Internally inconsistent MSBuildResolveResult, {ReferenceKind} is '{kind}' but Reference is '{Reference?.GetType () ?? null}'");
	}

	// ************************
	// These getters isolate callers from having to know the correct cast for the untyped `Reference` object
	// This is deeply coupled with MSBuildResolveVisitor, as it places the values in the `Reference` object
	// ************************

	public (string itemName, string metaName) GetMetadataReference () => AssertKind<ValueTuple<string, string>> (MSBuildReferenceKind.Metadata);
	public (string taskName, string paramName) GetTaskParameterReference () => AssertKind<ValueTuple<string, string>> (MSBuildReferenceKind.TaskParameter);
	public (MSBuildValueKind type, string functionName) GetPropertyFunctionReference () => AssertKind<ValueTuple<MSBuildValueKind, string>> (MSBuildReferenceKind.PropertyFunction);
	public (string className, string functionName) GetStaticPropertyFunctionReference () => AssertKind<ValueTuple<string, string>> (MSBuildReferenceKind.StaticPropertyFunction);

	public string GetItemReference () => AssertKind<string> (MSBuildReferenceKind.Item);
	public string GetPropertyReference () => AssertKind<string> (MSBuildReferenceKind.Property);
	public string GetTaskReference () => AssertKind<string> (MSBuildReferenceKind.Task);
	public string GetTargetReference () => AssertKind<string> (MSBuildReferenceKind.Target);

	public ISymbol GetKeywordReference () => AssertKind<ISymbol> (MSBuildReferenceKind.Keyword);
	public ITypedSymbol GetKnownValueReference () => AssertKind<ITypedSymbol> (MSBuildReferenceKind.KnownValue);

	public string GetTargetFrameworkReference () => AssertKind<string> (MSBuildReferenceKind.TargetFramework);
	public string GetTargetFrameworkVersionReference () => AssertKind<string> (MSBuildReferenceKind.TargetFrameworkVersion);
	public string GetTargetFrameworkIdentifierReference () => AssertKind<string> (MSBuildReferenceKind.TargetFrameworkIdentifier);
	public string GetTargetFrameworkProfileReference () => AssertKind<string> (MSBuildReferenceKind.TargetFrameworkProfile);

	public string GetItemFunctionReference () => AssertKind<string> (MSBuildReferenceKind.ItemFunction);
	public string GetClassNameReference () => AssertKind<string> (MSBuildReferenceKind.ClassName);
	public string GetEnumReference () => AssertKind<string> (MSBuildReferenceKind.Enum);
	public string GetConditionFunctionReference () => AssertKind<string> (MSBuildReferenceKind.ConditionFunction);
	public string GetNuGetIDReference () => AssertKind<string> (MSBuildReferenceKind.NuGetID);

	public string[] GetFileOrFolderReference () => AssertKind<string[]> (MSBuildReferenceKind.FileOrFolder);

	/// <summary>
	/// Gets a name for the reference that can be displayed in the UI
	/// </summary>
	public string GetReferenceDisplayName ()
	{
		switch (ReferenceKind) {
		case MSBuildReferenceKind.TaskParameter:
			return GetTaskParameterReference ().paramName;
		case MSBuildReferenceKind.Metadata:
			return GetMetadataReference ().metaName;
		case MSBuildReferenceKind.PropertyFunction:
			return GetPropertyFunctionReference ().functionName;
		case MSBuildReferenceKind.StaticPropertyFunction:
			return GetStaticPropertyFunctionReference ().functionName;
		}
		return inner.Reference is ISymbol info ? info.Name : (string)Reference;
	}

	// Tests compare this object directly, let them get the reference without having to call all the getter methods
	internal object? GetReferenceForTest () => Reference;
}
