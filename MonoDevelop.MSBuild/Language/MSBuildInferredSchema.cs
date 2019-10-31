// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Language
{
	class MSBuildInferredSchema : IMSBuildSchema
	{
		//FIXME: this means we can't re-use the inferred schema from other toplevels
		readonly bool isToplevel;

		public MSBuildInferredSchema (bool isToplevel) => this.isToplevel = isToplevel;

		public Dictionary<string, PropertyInfo> Properties { get; } = new Dictionary<string, PropertyInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, ItemInfo> Items { get; } = new Dictionary<string, ItemInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TaskInfo> Tasks { get; } = new Dictionary<string, TaskInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TargetInfo> Targets { get; } = new Dictionary<string, TargetInfo> (StringComparer.OrdinalIgnoreCase);

		public HashSet<string> Configurations { get; } = new HashSet<string> ();
		public HashSet<string> Platforms { get; } = new HashSet<string> ();

		public bool IsPrivate (string name)
		{
			//properties and items are always visible from files they're used in
			return !isToplevel && name[0] == '_';
		}

		public bool ContainsInfo (BaseInfo info) => info switch {
			PropertyInfo _ => Properties.ContainsKey (info.Name),
			ItemInfo _ => Items.ContainsKey (info.Name),
			TaskInfo _ => Tasks.ContainsKey (info.Name),
			TargetInfo _ => Targets.ContainsKey (info.Name),
			_ => false
		};

		public Dictionary<string, ReferenceUsage> ItemUsage { get; }
			= new Dictionary<string, ReferenceUsage> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, ReferenceUsage> PropertyUsage { get; }
			= new Dictionary<string, ReferenceUsage> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<(string, string), ReferenceUsage> MetadataUsage { get; }
			= new Dictionary<(string, string), ReferenceUsage> (new MetadataTupleComparer ());

		class MetadataTupleComparer : IEqualityComparer<(string itemName, string name)>
		{
			public bool Equals ((string itemName, string name) x, (string itemName, string name) y)
				=> StringComparer.OrdinalIgnoreCase.Equals (x.itemName, y.itemName)
				&& StringComparer.OrdinalIgnoreCase.Equals (x.name, y.name);

			public int GetHashCode ((string itemName, string name) obj)
				=> StringComparer.OrdinalIgnoreCase.GetHashCode (obj.itemName)
				^ StringComparer.OrdinalIgnoreCase.GetHashCode (obj.name);
		}
	}
}