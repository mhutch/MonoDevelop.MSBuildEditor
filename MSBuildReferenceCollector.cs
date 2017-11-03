//
// MSBuildReferenceCollector.cs
//
// Author:
//       Mikayla Hutchinson <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2016 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor
{
	abstract class MSBuildReferenceCollector : MSBuildVisitor
	{
		public List<(int Offset,int Length)> Results { get; } = new List<(int,int)> ();
		public string Name { get; }

		public MSBuildReferenceCollector (string name)
		{
			if (string.IsNullOrEmpty (name)) {
				throw new ArgumentException ("Name cannot be null or empty", name);
			}
			Name = name;
		}

		protected bool IsMatch (string name) => string.Equals (name, Name, StringComparison.OrdinalIgnoreCase);
		protected bool IsMatch (INamedXObject obj) => IsMatch (obj.Name.Name);

		protected void AddResult(DocumentRegion region)
		{
			var begin = ConvertLocation (region.Begin);
			var end = ConvertLocation (region.End);
			Results.Add ((begin, end - begin));
		}

		public static bool CanCreate (MSBuildKind? kind, string name)
		{
			switch (kind) {
			case MSBuildKind.Property:
			case MSBuildKind.Item:
			case MSBuildKind.ItemMetadata:
			case MSBuildKind.Task:
				return !string.IsNullOrEmpty (name);
			}
			return false;
		}

		public static MSBuildReferenceCollector Create (MSBuildKind kind, string name, string parentName)
		{
			switch (kind) {
			case MSBuildKind.Property:
				return new MSBuildPropertyReferenceCollector (name);
			case MSBuildKind.Item:
				return new MSBuildItemReferenceCollector (name);
			case MSBuildKind.ItemMetadata:
				return new MSBuildMetadataReferenceCollector (parentName, name);
			case MSBuildKind.Task:
				return new MSBuildTaskReferenceCollector (name);
			default:
				throw new ArgumentException ($"Cannot create collector for {kind}", nameof (kind));
			}
		}
	}

	class MSBuildItemReferenceCollector : MSBuildReferenceCollector
	{
		public MSBuildItemReferenceCollector (string itemName) : base (itemName)
		{
		}

		protected override void VisitItem (XElement element)
		{
			if (IsMatch (element)) {
				AddResult (element.Region);
			}
			base.VisitItem (element);
		}

		protected override void VisitItemReference (string itemName, int start, int length)
		{
			if (IsMatch (itemName)) {
				Results.Add ((start, length));
			}
			base.VisitItemReference (itemName, start, length);
		}
	}

	class MSBuildPropertyReferenceCollector : MSBuildReferenceCollector
	{
		public MSBuildPropertyReferenceCollector (string propertyName) : base (propertyName)
		{
		}

		protected override void VisitProperty (XElement element)
		{
			if (IsMatch (element)) {
				AddResult (element.Region);
			}
			base.VisitProperty (element);
		}

		protected override void VisitPropertyReference (string propertyName, int start, int length)
		{
			if (IsMatch (propertyName)) {
				Results.Add ((start, length));
			}
			base.VisitPropertyReference (propertyName, start, length);
		}
	}

	class MSBuildTaskReferenceCollector : MSBuildReferenceCollector
	{
		public MSBuildTaskReferenceCollector (string taskName) : base (taskName)
		{
		}

		protected override void VisitTask (XElement element)
		{
			if (IsMatch (element)) {
				AddResult (element.Region);
			}
			base.VisitTask (element);
		}
	}

	class MSBuildMetadataReferenceCollector : MSBuildReferenceCollector
	{
		readonly string itemName;

		public MSBuildMetadataReferenceCollector (string itemName, string metadataName) : base (metadataName)
		{
			this.itemName = itemName;
		}

		protected override void VisitMetadata (XElement element, string itemName, string metadataName)
		{
			if (IsMatch (element) && IsItemNameMatch (itemName)) {
				AddResult (element.Region);
			}
			base.VisitMetadata (element, itemName, metadataName);
		}

		protected override void VisitMetadataAttribute (XAttribute attribute, string itemName, string metadataName)
		{
			if (IsMatch (attribute) && IsItemNameMatch (itemName)) {
				AddResult (attribute.Region);
			}
			base.VisitMetadataAttribute (attribute, itemName, metadataName);
		}

		protected override void VisitMetadataReference (string itemName, string metadataName, int start, int length)
		{
			if (IsMatch (metadataName) && IsItemNameMatch (itemName)) {
				Results.Add ((start, length));
			}
			base.VisitMetadataReference (itemName, metadataName, start, length);
		}

		bool IsItemNameMatch (string name) => string.Equals (name, itemName, StringComparison.OrdinalIgnoreCase);
	}
}