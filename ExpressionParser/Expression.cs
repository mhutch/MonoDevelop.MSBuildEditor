//
// Expression.cs: Stores references to items or properties.
//
// Authors:
//   Marek Sieradzki (marek.sieradzki@gmail.com)
//   Marek Safar (marek.safar@gmail.com)
// 
// (C) 2005 Marek Sieradzki
// Copyright (c) 2014 Xamarin Inc. (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MonoDevelop.MSBuildEditor.ExpressionParser
{

	// Properties and items are processed in two ways
	// 1. Evaluate, Project calls evaluate on all the item and property groups.
	//    At this time, the items are fully expanded, all item and property
	//    references are expanded to get the item's value.
	//    Properties on the other hand, expand property refs, but _not_
	//    item references.
	//
	// 2. After the 'evaluation' phase, this could be when executing a target/task,
	//    - Items : no expansion required, as they are already at final value
	//    - Properties: Item references get expanded now, in the context of the
	//      batching
	//
	// The enum ExpressionOptions is for specifying this expansion of item references.
	//
	// GroupingCollection.Evaluate, evaluates all properties and then items

	internal class Expression {
	
		enum TokenKind
		{
			OpenParens,
			CloseParens,
			Dot,
			End
		}

		static Regex item_regex;
		static Regex metadata_regex;
		List<object> expressionCollection = new List<object>();

		public Expression ()
		{
		}

		public int AbsoluteIndex { get; set; }

		// Split: Split on ';'
		//	   Eg. Property values don't need to be split
		//
		// AllowItems: if false, item refs should not be treated as item refs!
		//	        it converts them to strings in the final expressionCollection
		//
		// AllowMetadata: same as AllowItems, for metadata
		public void Parse (string expression, ParseOptions options)
		{
			bool split = (options & ParseOptions.Split) == ParseOptions.Split;
			bool allowItems = (options & ParseOptions.AllowItems) == ParseOptions.AllowItems;
			bool allowMd = (options & ParseOptions.AllowMetadata) == ParseOptions.AllowMetadata;

			expression = expression.Replace ('\\', Path.DirectorySeparatorChar);
		
			string [] parts;
			if (split)
				parts = expression.Split (new char [] {';'}, StringSplitOptions.RemoveEmptyEntries);
			else
				parts = new string [] { expression };

			// TODO: Too complicated, each part parses only its known part
			// we should simply do it in one go and avoid all this parts code madness

			List <ArrayList> p1 = new List <ArrayList> (parts.Length);
			List <ArrayList> p2 = new List <ArrayList> (parts.Length);
			List <ArrayList> p3 = new List <ArrayList> (parts.Length);

			Prepare (p1, parts.Length);
			Prepare (p2, parts.Length);
			Prepare (p3, parts.Length);

			for (int i = 0; i < parts.Length; i++)
				p1 [i] = SplitItems (parts [i], allowItems, AbsoluteIndex);

			for (int i = 0; i < parts.Length; i++) {
				p2 [i] = new ArrayList ();
				int absIdx = AbsoluteIndex;
				foreach (object o in p1 [i]) {
					if (o is string s) {
						p2 [i].AddRange (ExtractProperties (s, absIdx));
						absIdx += s.Length;
					} else if (o is ItemReference ir) {
						absIdx = AbsoluteIndex + ir.End + 1;
						p2 [i].Add (o);
					} else {
						p2 [i].Add (o);
					}
				}
			}

			for (int i = 0; i < parts.Length; i++) {
				p3 [i] = new ArrayList ();
				int absIdx = AbsoluteIndex;
				foreach (object o in p2 [i]) {
					if (o is string s) {
						p3 [i].AddRange (SplitMetadata (s, absIdx));
						absIdx += s.Length;
					} else {
						p3 [i].Add (o);
					}
				}
			}

			CopyToExpressionCollection (p3, allowItems, allowMd);
		}

		void Prepare (List <ArrayList> l, int length)
		{
			for (int i = 0; i < length; i++)
				l.Add (null);
		}
		
		void CopyToExpressionCollection (List <ArrayList> lists, bool allowItems, bool allowMd)
		{
			for (int i = 0; i < lists.Count; i++) {
				foreach (object o in lists [i]) {
					if (o is string)
						expressionCollection.Add (MSBuildUtils.Unescape ((string) o));
					else if (!allowItems && o is ItemReference)
						expressionCollection.Add (((ItemReference) o).OriginalString);
					else if (!allowMd && o is MetadataReference) {
						expressionCollection.Add (((MetadataReference) o).OriginalString);
					}
					else if (o is IReference)
						expressionCollection.Add ((IReference) o);
				}
				if (i < lists.Count - 1)
					expressionCollection.Add (";");
			}
		}

		ArrayList SplitItems (string text, bool allowItems, int absoluteOffset)
		{
			ArrayList phase1 = new ArrayList ();
			Match m;
			m = ItemRegex.Match (text);

			while (m.Success) {
				string name = null, transform = null, separator = null;
				ItemReference ir;

				var nameGroup = m.Groups [ItemRegex.GroupNumberFromName ("itemname")];
				name = nameGroup.Value;

				int transformIndex = 0;
				if (m.Groups [ItemRegex.GroupNumberFromName ("has_transform")].Success) {
					var transformGroup = m.Groups [ItemRegex.GroupNumberFromName ("transform")];
					transform = transformGroup.Value;
					transformIndex = transformGroup.Index;
				}
				
				if (m.Groups [ItemRegex.GroupNumberFromName ("has_separator")].Success)
					separator = m.Groups [ItemRegex.GroupNumberFromName ("separator")].Value;

				ir = new ItemReference (text.Substring (m.Groups [0].Index, m.Groups [0].Length),
				                        name, transform, transformIndex, separator,
				                        m.Groups [0].Index, m.Groups [0].Length, nameGroup.Index + absoluteOffset);
				phase1.Add (ir);
				m = m.NextMatch ();
			}

			ArrayList phase2 = new ArrayList ();
			int last_end = -1;
			int end = text.Length - 1;

			foreach (ItemReference ir in phase1) {
				int a,b;

				a = last_end;
				b = ir.Start;

				if (b - a - 1 > 0) {
					phase2.Add (text.Substring (a + 1, b - a - 1));
				}

				last_end = ir.End;
				phase2.Add (ir);
			}

			if (last_end < end)
				phase2.Add (text.Substring (last_end + 1, end - last_end));

			return phase2;
		}

		//
		// Parses property syntax
		//
		static List<object> ExtractProperties (string text, int absoluteIndex)
		{
			var phase = new List<object> ();

			var	pos = text.IndexOf ("$(", StringComparison.Ordinal);
			if (pos < 0) {
				phase.Add (text);
				return phase;
			}

			if (pos != 0) {
				// Extract any whitespaces before property reference
				phase.Add (text.Substring (0, pos));
			}

			while (pos < text.Length) {
				pos += 2;
				int start = pos;
				int end = 0;
				bool requires_closing_parens = true;
					
				var ch = text [pos];
				if ((ch == 'r' || ch == 'R') && text.Substring (pos + 1).StartsWith ("egistry:", StringComparison.OrdinalIgnoreCase)) {
					pos += 9;
					ParseRegistryFunction (text, pos);
				} else {
					while (char.IsWhiteSpace (ch))
						ch = text [pos++];

					if (ch == '[') {
						phase.Add (ParsePropertyFunction (text, ref pos));
					} else {
						// TODO: There is something like char index syntax as well: $(aa [10])
						// text.IndexOf ('[');

						end = text.IndexOf (')', pos) + 1;
						if (end > 0) {
							//
							// Instance string method, $(foo.Substring (0, 3))
							//
							var dot = text.IndexOf ('.', pos, end - pos);
							if (dot > 0) {
								var name = text.Substring (start, dot - start);
								++dot;
								var res = ParseInvocation (text, ref dot, null, new PropertyReference (name, start + absoluteIndex));
								if (res != null) {
									phase.Add (res);
									end = dot;
								}
							} else {
								var name = text.Substring (start, end - start - 1);

								//
								// Check for wrong syntax e.g $(foo()
								//
								var open_parens = name.IndexOf ('(');
								if (open_parens < 0) {
									//
									// Simple property reference $(Foo)
									//
									phase.Add (new PropertyReference (name, start + absoluteIndex));
									requires_closing_parens = false;
								} else {
									end = 0;
								}
							}
						}

						if (end == 0) {
							end = text.Length;
							start -= 2;
							phase.Add (text.Substring (start, end - start));
						}

						pos = end;
					}

					if (requires_closing_parens) {
						end = text.IndexOf (')', pos);
						if (end < 0)
							end = 0;
						else
							pos = end + 1;
					}
				}

				end = text.IndexOf ("$(", pos, StringComparison.Ordinal);
				if (end < 0)
					end = text.Length;

				if (end - pos > 0)
					phase.Add (text.Substring (pos, end - pos));

				pos = end;
			}

			return phase;
		}

		//
		// Property function with syntax $([Class]::Method(Parameters))
		//
		static object ParsePropertyFunction (string text, ref int pos)
		{
			int p = text.IndexOf ("]::", pos, StringComparison.Ordinal);
			if (p < 0) {
				return new InvalidExpressionError (string.Format ("Invalid static method invocation syntax '{0}'", text.Substring (pos)), pos);
			}

			var type_name = text.Substring (pos + 1, p - pos - 1);
			var type = GetTypeForStaticMethod (type_name);
			if (type == null) {
				if (type_name.Contains ("."))
					return new InvalidExpressionError (string.Format ("Invalid type '{0}' used in static method invocation", type_name), pos);

				return new InvalidExpressionError (string.Format ("'{0}': Static method invocation requires full type name to be used", type_name), pos);
			}

			pos = p + 3;
			return ParseInvocation (text, ref pos, type, null);
		}

		//
		// Property function with syntax $(Registry:Call)
		//
		static int ParseRegistryFunction (string text, int pos)
		{
			//ignore
			var end = text.IndexOf (')', pos) + 1;
			return end > 0 ? end : text.Length;
		}

		static Type GetTypeForStaticMethod (string typeName)
		{
			//
			// In static property functions, you can use any static method or property of these system classes:
			//
			switch (typeName.ToLowerInvariant ()) {
			case "system.byte":
				return typeof (byte);
			case "system.char":
				return typeof (char);
			case "system.convert":
				return typeof (Convert);
			case "system.datetime":
				return typeof (DateTime);
			case "system.decimal":
				return typeof (decimal);
			case "system.double":
				return typeof (double);
			case "system.enum":
				return typeof (Enum);
			case "system.guid":
				return typeof (Guid);
			case "system.int16":
				return typeof (Int16);
			case "system.int32":
				return typeof (Int32);
			case "system.int64":
				return typeof (Int64);
			case "system.io.path":
				return typeof (System.IO.Path);
			case "system.math":
				return typeof (Math);
			case "system.uint16":
				return typeof (UInt16);
			case "system.uint32":
				return typeof (UInt32);
			case "system.uint64":
				return typeof (UInt64);
			case "system.sbyte":
				return typeof (sbyte);
			case "system.single":
				return typeof (float);
			case "system.string":
				return typeof (string);
			case "system.stringcomparer":
				return typeof (StringComparer);
			case "system.timespan":
				return typeof (TimeSpan);
			case "system.text.regularexpressions.regex":
				return typeof (System.Text.RegularExpressions.Regex);
			case "system.version":
				return typeof (Version);
			case "microsoft.build.utilities.toollocationhelper":
				return typeof (Microsoft.Build.Utilities.ToolLocationHelper);
			case "msbuild":
				return typeof (PredefinedPropertyFunctions);
			case "system.environment":
				return typeof (System.Environment);
			case "system.io.directory":
				return typeof (System.IO.Directory);
			case "system.io.file":
				return typeof (System.IO.File);
			}

			return null;
		}

		static bool IsMethodAllowed (Type type, string name)
		{
			if (type == typeof (System.Environment)) {
				switch (name.ToLowerInvariant ()) {
				case "commandline":
				case "expandenvironmentvariables":
				case "getenvironmentvariable":
				case "getenvironmentvariables":
				case "getfolderpath":
				case "getlogicaldrives":
					return true;
				}

				return false;
			}

			if (type == typeof (System.IO.Directory)) {
				switch (name.ToLowerInvariant ()) {
				case "getdirectories":
				case "getfiles":
				case "getlastaccesstime":
				case "getlastwritetime":
					return true;
				}

				return false;
			}

			if (type == typeof (System.IO.File)) {
				switch (name.ToLowerInvariant ()) {
				case "getcreationtime":
				case "getattributes":
				case "getlastaccesstime":
				case "getlastwritetime":
				case "readalltext":
					return true;
				}
			}

			return true;
		}

		static List<string> ParseArguments (string text, ref int pos)
		{
			List<string> args = new List<string> ();
			int parens = 0;
			bool backticks = false;
			int start = pos;
			for (; pos < text.Length; ++pos) {
				var ch = text [pos];

				if (ch == '`') {
					backticks = !backticks;
					continue;
				}

				if (backticks)
					continue;

				if (ch == '(') {
					++parens;
					continue;
				}

				if (ch == ')') {
					if (parens == 0) {
						var arg = text.Substring (start, pos - start).Trim ();
						if (arg.Length > 0)
							args.Add (arg);

						++pos;
						return args;
					}

					--parens;
					continue;
				}

				if (parens != 0)
					continue;

				if (ch == ',') {
					args.Add (text.Substring (start, pos - start));
					start = pos + 1;
					continue;
				}
			}

			// Invalid syntax
			return null;
		}

		static object ParseInvocation (string text, ref int p, Type type, IReference instance)
		{
			TokenKind token;
			MemberInvocationReference mir = null;

			while (true) {
				int prev = p;
				token = ScanName (text, ref p);
				var name = text.Substring (prev, p - prev).TrimEnd ();

				switch (token) {
				case TokenKind.Dot:
				case TokenKind.OpenParens:
					break;
				case TokenKind.CloseParens:
					return new MemberInvocationReference (type, name) {
						Instance = instance
					};

				case TokenKind.End:
					if (mir == null || name.Length != 0)
					return new InvalidExpressionError (string.Format ("Invalid static method invocation syntax '{0}'", text.Substring (p)), p);

					return mir;
				default:
					throw new NotImplementedException ();
				}

				instance = mir = new MemberInvocationReference (type, name) {
					Instance = instance
				};

				if (type != null) {
					if (!IsMethodAllowed (type, name))
						return new InvalidExpressionError (string.Format ("The function '{0}' on type '{1}' has not been enabled for execution", name, type.FullName), p);

					type = null;
				}

				if (token == TokenKind.OpenParens) {
					++p;
					mir.Arguments = ParseArguments (text, ref p);
				}

				if (p < text.Length && text [p] == '.') {
					++p;
					continue;
				}

				return mir;
			}
		}

		static TokenKind ScanName (string text, ref int p)
		{
			for (; p < text.Length; ++p) {
				switch (text [p]) {
				case '(':
					return TokenKind.OpenParens;
				case '.':
					return TokenKind.Dot;
				case ')':
					return TokenKind.CloseParens;
				}
			}

			return TokenKind.End;
		}

		ArrayList SplitMetadata (string text, int offset)
		{
			ArrayList phase1 = new ArrayList ();
			Match m;
			m = MetadataRegex.Match (text);

			while (m.Success) {
				string name = null, meta = null;
				MetadataReference mr;

				var nameGroup = m.Groups [MetadataRegex.GroupNumberFromName ("name")];
				if (nameGroup.Success)
					name = nameGroup.Value;

				var metaGroup = m.Groups [MetadataRegex.GroupNumberFromName ("meta")];
				meta = metaGroup.Value;
				
				mr = new MetadataReference (text.Substring (m.Groups [0].Index, m.Groups [0].Length),
				                            name, meta, m.Groups [0].Index, m.Groups [0].Length, offset + metaGroup.Index);
				phase1.Add (mr);
				m = m.NextMatch ();
			}

			ArrayList phase2 = new ArrayList ();
			int last_end = -1;
			int end = text.Length - 1;

			foreach (MetadataReference mr in phase1) {
				int a,b;

				a = last_end;
				b = mr.Start;

				if (b - a - 1> 0) {
					phase2.Add (text.Substring (a + 1, b - a - 1));
				}

				last_end = mr.End;
				phase2.Add (mr);
			}

			if (last_end < end)
				phase2.Add (text.Substring (last_end + 1, end - last_end));

			return phase2;
		}

		public List<object> Collection {
			get { return expressionCollection; }
		}

		static Regex ItemRegex {
			get {
				if (item_regex == null)
					item_regex = new Regex (
						@"@\(\s*"
						+ @"(?<itemname>[_A-Za-z][_\-0-9a-zA-Z]*)"
						+ @"(?<has_transform>\s*->\s*'(?<transform>[^']*)')?"
						+ @"(?<has_separator>\s*,\s*'(?<separator>[^']*)')?"
						+ @"\s*\)");
				return item_regex;
			}
		}
			
		static Regex MetadataRegex {
			get {
				if (metadata_regex == null)
					metadata_regex = new Regex (
						@"%\(\s*"
						+ @"((?<name>[_a-zA-Z][_\-0-9a-zA-Z]*)\.)?"
						+ @"(?<meta>[_a-zA-Z][_\-0-9a-zA-Z]*)"
						+ @"\s*\)");
				return metadata_regex;
			}
		}
	}

	[Flags]
	enum ParseOptions {
		// absence of one of these flags, means
		// false for that option
		AllowItems = 0x1,
		Split = 0x2,
		AllowMetadata = 0x4,

		None = 0x8, // == no items, no metadata, and no split

		// commonly used options
		AllowItemsMetadataAndSplit = AllowItems | Split | AllowMetadata,
		AllowItemsNoMetadataAndSplit = AllowItems | Split
	}

	enum ExpressionOptions {
		ExpandItemRefs,
		DoNotExpandItemRefs
	}
}
