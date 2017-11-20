// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
{
	abstract class MSBuildReferenceCollector : MSBuildVisitor
	{
		public List<(int Offset,int Length)> Results { get; } = new List<(int,int)> ();
		public string Name { get; }

		protected MSBuildReferenceCollector (string name)
		{
			if (string.IsNullOrEmpty (name)) {
				throw new ArgumentException ("Name cannot be null or empty", name);
			}
			Name = name;
		}

		protected bool IsMatch (string name) => string.Equals (name, Name, StringComparison.OrdinalIgnoreCase);
		protected bool IsMatch (INamedXObject obj) => IsMatch (obj.Name.Name);

		protected void AddResult (XElement element)
		{
			Results.Add ((
				ConvertLocation (element.Region.Begin) + 1,
				element.Name.Name.Length
			));
		}

		protected void AddResult (XAttribute attribute)
		{
			Results.Add ((
				ConvertLocation (attribute.Region.Begin),
				attribute.Name.Name.Length
			));
		}

		public static bool CanCreate (MSBuildResolveResult rr)
		{
			if (rr == null || rr.LanguageElement == null) {
				return false;
			}

			switch (rr.ReferenceKind) {
			case MSBuildReferenceKind.Property:
			case MSBuildReferenceKind.Item:
			case MSBuildReferenceKind.Task:
			case MSBuildReferenceKind.Metadata:
				return !string.IsNullOrEmpty (rr.ReferenceName);
			}

			return false;
		}

		public static MSBuildReferenceCollector Create (MSBuildResolveResult rr)
		{
			switch (rr.ReferenceKind) {
			case MSBuildReferenceKind.Property:
				return new MSBuildPropertyReferenceCollector (rr.ReferenceName);
			case MSBuildReferenceKind.Item:
				return new MSBuildItemReferenceCollector (rr.ReferenceName);
			case MSBuildReferenceKind.Metadata:
				return new MSBuildMetadataReferenceCollector (rr.ReferenceItemName, rr.ReferenceName);
			case MSBuildReferenceKind.Task:
				return new MSBuildTaskReferenceCollector (rr.ReferenceName);
			}

			throw new ArgumentException ($"Cannot create collector for resolve result", nameof (rr));
		}

		public static bool ShouldSearchReference (MSBuildResolveContext ctx, MSBuildResolveResult rr)
		{
			switch (rr.ReferenceKind) {
			case MSBuildReferenceKind.Property:
				return Builtins.Properties.ContainsKey (rr.ReferenceName)
					|| ctx.Properties.ContainsKey (rr.ReferenceName);
			case MSBuildReferenceKind.Item:
				return ctx.Items.ContainsKey (rr.ReferenceName);
			case MSBuildReferenceKind.Task:
				return ctx.Items.ContainsKey (rr.ReferenceName);
			case MSBuildReferenceKind.Metadata:
				return Builtins.Metadata.ContainsKey (rr.ReferenceName)
					|| rr.ReferenceItemName == null
					|| (ctx.Items.TryGetValue (rr.ReferenceItemName, out ItemInfo item) && item.Metadata.ContainsKey (rr.ReferenceName));
			case MSBuildReferenceKind.Target:
				return ctx.Targets.ContainsKey (rr.ReferenceName);
			}
			return true;
		}
	}

	class MSBuildItemReferenceCollector : MSBuildReferenceCollector
	{
		public MSBuildItemReferenceCollector (string itemName) : base (itemName)
		{
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

		protected override void VisitTask (XElement element, MSBuildLanguageElement resolved)
		{
			if (IsMatch (element)) {
				AddResult (element);
			}
			base.VisitTask (element, resolved);
		}
	}

	class MSBuildMetadataReferenceCollector : MSBuildReferenceCollector
	{
		readonly string itemName;

		public MSBuildMetadataReferenceCollector (string itemName, string metadataName) : base (metadataName)
		{
			this.itemName = itemName;
		}

		protected override void VisitMetadataReference (string itemName, string metadataName, int start, int length)
		{
			if (IsMatch (metadataName) && (this.itemName == null || IsItemNameMatch (itemName))) {
				Results.Add ((start, length));
			}
			base.VisitMetadataReference (itemName, metadataName, start, length);
		}

		bool IsItemNameMatch (string name) => string.Equals (name, itemName, StringComparison.OrdinalIgnoreCase);
	}
}