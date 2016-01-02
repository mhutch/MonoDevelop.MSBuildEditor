//
// MSBuildDocumentParser.cs
//
// Author:
//       mhutch <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2015 Xamarin Inc.
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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildDocumentParser : TypeSystemParser
	{
		public override Task<ParsedDocument> Parse (ParseOptions options, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Run (() => ParseInternal (options, cancellationToken), cancellationToken);
		}

		ParsedDocument ParseInternal (ParseOptions options, CancellationToken cancellationToken)
		{
			var doc = new MSBuildParsedDocument (options.FileName);
			doc.Flags |= ParsedDocumentFlags.NonSerializable;

			var xmlParser = new XmlParser (new XmlRootState (), true);
			try {
				xmlParser.Parse (options.Content.CreateReader ());
			}
			catch (Exception ex) {
				Core.LoggingService.LogError ("Unhandled error parsing xml document", ex);
			}

			doc.XDocument = xmlParser.Nodes.GetRoot ();

			doc.AddRange (xmlParser.Errors);

			doc.Resolve ();

			if (doc.XDocument != null && doc.XDocument.RootElement != null) {
				if (!doc.XDocument.RootElement.IsEnded)
					doc.XDocument.RootElement.End (xmlParser.Location);
			}

			return doc;
		}
	}

	class MSBuildParsedDocument : XmlParsedDocument
	{
		public MSBuildParsedDocument (string filename) : base (filename)
		{
		}

		ConditionalWeakTable<XObject, object[]> annotations = new ConditionalWeakTable<XObject, object[]> ();

		public T GetAnnotation<T> (XObject o)
		{
			object[] values;
			if (!annotations.TryGetValue (o, out values))
				return default (T);
			return values.OfType<T> ().FirstOrDefault ();
		}

		public void AddAnnotation<T> (XObject o, T annotation)
		{
			if (Equals (annotation, default (T)))
				return;
			
			object[] values;
			if (!annotations.TryGetValue (o, out values)) {
				values = new object[1];
			} else {
				var idx = Array.FindIndex (values, obj => obj is T);
				if (idx > -1) {
					values [idx] = annotation;
					return;
				}
				Array.Resize (ref values, values.Length + 1);
			}

			values[values.Length - 1] = annotation;
		}

		internal void Resolve ()
		{
			if (XDocument == null)
				return;

			foreach (var el in XDocument.RootElement.Elements) {
				Resolve (el, null);
			}
		}

		void Resolve (XElement el, MSBuildElement parent)
		{
			var resolved = MSBuildElement.Get (el.Name.FullName, parent);
			if (resolved == null) {
				Add (new Error (ErrorType.Error, $"Unknown element '{el.Name.FullName}'", el.Region));
				return;
			}

			AddAnnotation (el, resolved);

			foreach (var child in el.Elements) {
				Resolve (child, resolved);
			}

			//TODO: task validation
			if (resolved.Kind == MSBuildKind.Task) {
				return;
			}

			//TODO: check required attributes
			//TODO: validate attribute expressions
			foreach (var att in el.Attributes) {
				var valid = resolved.Attributes.Any (a => att.Name.FullName == a);
				if (!valid) {
					Add (new Error (ErrorType.Error, $"Unknown attribute '{att.Name.FullName}'", att.Region));
				}
			}
		}
	}
}
