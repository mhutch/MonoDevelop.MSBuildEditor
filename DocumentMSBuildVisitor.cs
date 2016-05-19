//
// DocumentMSBuildVisitor.cs
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

using System.Linq;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor
{
	class DocumentMSBuildVisitor : MSBuildReferenceCollector
	{
		AnnotationTable<XObject, object> annotations = new AnnotationTable<XObject, object> ();
		MSBuildParsedDocument doc;

		protected override void VisitUnknown (XElement element)
		{
			doc.Add (new Error (ErrorType.Error, $"Unknown element '{element.Name.FullName}'", element.Region));
		}

		protected override void VisitResolved (XElement element, MSBuildElement kind)
		{
			//ValidateAttributes (element, kind);
			annotations.Add (element, kind);
		}

		protected override void VisitTask (XElement element)
		{
		}

		protected override void VisitImport (XElement element)
		{
		}
		/*
		void ValidateAttributes (XElement el, MSBuildElement kind)
		{
			//TODO: check required attributes
			//TODO: validate attribute expressions
			foreach (var att in el.Attributes) {
				var valid = resolved.Attributes.Any (a => att.Name.FullName == a);
				if (!valid) {
					doc.Add (new Error (ErrorType.Error, $"Unknown attribute '{att.Name.FullName}'", att.Region));
				}
			}
		}
		*/
	}
}