//
// MSBuildTextEditorExtension.cs
//
// Authors:
//       mhutch <m.j.hutchinson@gmail.com>
//
// Copyright (C) 2014 Xamarin Inc. (http://www.xamarin.com)
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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.Core;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Xml.Completion;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor;
using System;
using System.Linq;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildTextEditorExtension : BaseXmlEditorExtension
	{
		public static readonly string MSBuildMimeType = "application/x-msbuild";

		MSBuildParsedDocument GetDocument ()
		{
			return (MSBuildParsedDocument)DocumentContext.ParsedDocument;
		}

		protected override Task<CompletionDataList> GetElementCompletions (CancellationToken token)
		{
			var list = new CompletionDataList ();
			AddMiscBeginTags (list);

			var path = GetCurrentPath ();

			if (path.Count == 0) {
				list.Add (new XmlCompletionData ("Project", XmlCompletionData.DataType.XmlElement));
				return Task.FromResult (list);
			}

			var rr = ResolveElement (path);
			if (rr == null)
				return Task.FromResult (list);

			foreach (var c in rr.BuiltinChildren)
				list.Add (new XmlCompletionData (c, XmlCompletionData.DataType.XmlElement));

			foreach (var item in GetInferredChildren (rr)) {
				list.Add (new XmlCompletionData (item.Name, item.Description, XmlCompletionData.DataType.XmlElement));
			}

			return Task.FromResult (list);
		}

		IEnumerable<BaseInfo> GetInferredChildren (ResolveResult rr)
		{
			var doc = GetDocument ();
			if (doc == null)
				return new BaseInfo[0];

			if (rr.ElementType == MSBuildKind.Item) {
				return doc.Context.GetItemMetadata (rr.ElementName, false);
			}

			if (rr.ChildType.HasValue) {
				switch (rr.ChildType.Value) {
				case MSBuildKind.Item:
					return doc.Context.GetItems ();
				case MSBuildKind.Task:
					return doc.Context.GetTasks ();
				case MSBuildKind.Property:
					return doc.Context.GetProperties (false);
				}
			}
			return new BaseInfo [0];
		}

		protected override Task<CompletionDataList> GetAttributeCompletions (IAttributedXObject attributedOb,
			Dictionary<string, string> existingAtts, CancellationToken token)
		{
			var path = GetCurrentPath ();

			var rr = ResolveElement (path);
			if (rr == null)
				return null;

			var list = new CompletionDataList ();
			foreach (var a in rr.BuiltinAttributes)
				if (!existingAtts.ContainsKey (a))
					list.Add (new XmlCompletionData (a, XmlCompletionData.DataType.XmlAttribute));

			var inferredAttributes = GetInferredAttributes (rr);
			if (inferredAttributes != null)
				foreach (var a in inferredAttributes)
					if (!existingAtts.ContainsKey (a))
						list.Add (new XmlCompletionData (a, XmlCompletionData.DataType.XmlAttribute));

			return Task.FromResult (list);
		}

		IEnumerable<string> GetInferredAttributes (ResolveResult rr)
		{
			var doc = GetDocument ();
			if (doc == null || rr.ElementType != MSBuildKind.Task)
				return new string [0];

			var result = new HashSet<string> ();
			foreach (var task in doc.Context.GetTask (rr.ElementName)) {
				foreach (var p in task.Parameters) {
					result.Add (p);
				}
			}

			return result;
		}

		static ResolveResult ResolveElement (IList<XObject> path)
		{
			//need to look up element by walking how the path, since at each level, if the parent has special children,
			//then that gives us information to identify the type of its children
			MSBuildElement el = null;
			string elName = null;
			MSBuildKind? elType = null;
			for (int i = 0; i < path.Count; i++) {
				//if children of parent is known to be arbitrary data, give up on completion
				if (el != null && el.ChildType == MSBuildKind.Data)
					return null;
				//code completion is forgiving, all we care about best guess resolve for deepest child
				var xel = path [i] as XElement;
				if (xel != null && xel.Name.Prefix == null) {
					if (el != null)
						elType = el.ChildType;
					elName = xel.Name.Name;
					el = MSBuildElement.Get (elName, el);
					if (el != null)
						continue;
				}
				el = null;
				elName = null;
				elType = null;
			}
			if (el == null)
				return null;

			return new ResolveResult {
				ElementName = elName,
				ElementType = elType,
				ChildType = el.ChildType,
				BuiltinAttributes = el.Attributes,
				BuiltinChildren = el.Children,
			};
		}

		class ResolveResult
		{
			public string ElementName;
			public MSBuildKind? ElementType;
			public MSBuildKind? ChildType;
			public IEnumerable<string> BuiltinAttributes;
			public IEnumerable<string> BuiltinChildren;
		}
	}
}