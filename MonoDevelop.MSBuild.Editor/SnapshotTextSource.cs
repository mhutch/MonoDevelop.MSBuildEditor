// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.VisualStudio.Text;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class SnapshotTextSource : ITextSource
	{
		public SnapshotTextSource (ITextSnapshot snapshot, string filename)
		{
			Snapshot = snapshot;
			FileName = filename;
		}

		public ITextSnapshot Snapshot { get; }

		public string FileName { get; }

		public int Length => Snapshot.Length;

		public TextReader CreateReader () => new SnapshotTextReader (Snapshot);

		public char GetCharAt (int offset) => Snapshot[offset];

		public string GetTextBetween (int begin, int end) => Snapshot.GetText (new Span (begin, end - begin));

		class SnapshotTextReader : TextReader
		{
			readonly ITextSnapshot snapshot;
			int position;

			public SnapshotTextReader (ITextSnapshot snapshot)
			{
				this.snapshot = snapshot;
			}

			public override int Peek ()
			{
				if (position + 1 < snapshot.Length) {
					return snapshot[position + 1];
				}
				return -1;
			}

			public override int Read ()
			{
				if (position < snapshot.Length) {
					return snapshot[position++];
				}
				return -1;
			}

			public override string ReadToEnd ()
			{
				return snapshot.GetText (new Span (position, snapshot.Length - position));
			}
		}
	}
}