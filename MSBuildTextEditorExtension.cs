//
// MSBuildTextEditorExtension.cs
//
// Authors:
//       Mikayla Hutchinson <m.j.hutchinson@gmail.com>
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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Xml.Completion;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildTextEditorExtension : BaseXmlEditorExtension
	{
		public static readonly string MSBuildMimeType = "application/x-msbuild";

		MSBuildParsedDocument GetDocument ()
		{
			return (MSBuildParsedDocument)DocumentContext.ParsedDocument;
		}

		public override Task<ICompletionDataList> HandleCodeCompletionAsync (CodeCompletionContext completionContext, char completionChar, CancellationToken token = default (CancellationToken))
		{
			var expressionCompletion = HandleExpressionCompletion (completionChar);
			if (expressionCompletion != null) {
				return Task.FromResult (expressionCompletion);
			}

			return base.HandleCodeCompletionAsync (completionContext, completionChar, token);
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
			string elName = null, attName = null;
			for (int i = 0; i < path.Count; i++) {
				var xatt = path [i] as XAttribute;
				if (xatt != null) {
					attName = xatt.Name.Name;
					break;
				}
				//if children of parent is known to be arbitrary data, don't go into it
				if (el != null && el.ChildType == MSBuildKind.Data)
					break;
				//code completion is forgiving, all we care about best guess resolve for deepest child
				var xel = path [i] as XElement;
				if (xel != null && xel.Name.Prefix == null) {
					elName = xel.Name.Name;
					el = MSBuildElement.Get (elName, el);
					if (el != null)
						continue;
				}
				el = null;
				elName = null;
			}
			if (el == null)
				return null;

			return new ResolveResult {
				AttributeName = attName,
				ElementName = elName,
				ElementType = el.Kind,
				ChildType = el.ChildType,
				BuiltinAttributes = el.Attributes,
				BuiltinChildren = el.Children,
			};
		}

		class ResolveResult
		{
			public string AttributeName;
			public string ElementName;
			public MSBuildKind? ElementType;
			public MSBuildKind? ChildType;
			public IEnumerable<string> BuiltinAttributes;
			public IEnumerable<string> BuiltinChildren;
		}

		ICompletionDataList HandleExpressionCompletion (char completionChar)
		{
			var doc = GetDocument ();
			if (doc == null)
				return null;

			var path = GetCurrentPath ();
			var rr = ResolveElement (path);

			if (rr == null || rr.ElementType == null) {
				return null;
			}

			var state = Tracker.Engine.CurrentState;
			bool isAttribute = state is Xml.Parser.XmlAttributeValueState;
			if (isAttribute) {
				//FIXME: assume all attributes accept expressions for now
			} else if (state is Xml.Parser.XmlRootState) {
				if (rr.ChildType != MSBuildKind.Expression)
					return null;
			} else {
				return null;
			}

			//FIXME: This is very rudimentary. We should parse the expression for real.
			int currentPosition = Editor.CaretOffset;
			int lineStart = Editor.GetLine (Editor.CaretLine).Offset;
			int expressionStart = currentPosition - Tracker.Engine.CurrentStateLength;
			if (isAttribute && GetAttributeValueDelimiter (Tracker.Engine) != 0) {
				expressionStart += 1;
			}
			int start = Math.Max (expressionStart, lineStart);
			var expression = Editor.GetTextAt (start, currentPosition - start);

			if (expression.Length < 2) {
				return null;
			}

			//trigger on letter after $(, @(
			if (expression.Length >= 3 && char.IsLetter (expression [expression.Length - 1]) && expression [expression.Length - 2] == '(') {
				char c = expression [expression.Length - 3];
				if (c == '$') {
					return new CompletionDataList (GetPropertyExpressionCompletions (doc)) { TriggerWordLength = 1 };
				}
				if (c == '@') {
					return new CompletionDataList (GetItemExpressionCompletions (doc)) { TriggerWordLength = 1 };
				}
				return null;
			}

			//trigger on $(, @(
			if (expression [expression.Length - 1] == '(') {
				char c = expression [expression.Length - 2];
				if (c == '$') {
					return new CompletionDataList (GetPropertyExpressionCompletions (doc));
				}
				if (c == '@') {
					return new CompletionDataList (GetItemExpressionCompletions (doc));
				}
				return null;
			}

			return null;
		}

		//FIXME: this is fragile, need API in core
		static char GetAttributeValueDelimiter (XmlParser parser)
		{
			var ctx = (IXmlParserContext)parser;
			switch (ctx.StateTag) {
			case 3: return '"';
			case 2: return '\'';
			default: return (char)0;
			}
		}

		IEnumerable<CompletionData> GetItemExpressionCompletions (MSBuildParsedDocument doc)
		{
			foreach (var item in doc.Context.GetItems ()) {
				yield return new CompletionData (item.Name, MonoDevelop.Ide.Gui.Stock.Class, item.Description);
			}
		}

		IEnumerable<CompletionData> GetPropertyExpressionCompletions (MSBuildParsedDocument doc)
		{
			foreach (var prop in doc.Context.GetProperties (true)) {
				yield return new CompletionData (prop.Name, MonoDevelop.Ide.Gui.Stock.Class, prop.Description);
			}
		}
	}
}