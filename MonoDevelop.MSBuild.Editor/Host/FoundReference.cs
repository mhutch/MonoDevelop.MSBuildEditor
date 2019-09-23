// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Language.StandardClassification;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Host
{
	public class FoundReference
	{
		public FoundReference (
			string filePath,
			int startOffset,
			int startLine,
			int startCol,
			ReferenceUsage usage,
			ImageId imageId,
			ImmutableArray<ClassifiedText> classifiedSpans,
			TextSpan highlight)
		{
			FilePath = filePath;
			StartOffset = startOffset;
			StartLine = startLine;
			StartCol = startCol;
			Usage = usage;
			ImageId = imageId;
			ClassifiedSpans = classifiedSpans;
			Highlight = highlight;
		}

		FoundReference (ImageId imageId, ImmutableArray<ClassifiedText> classifiedSpans)
		{
			ImageId = imageId;
			ClassifiedSpans = classifiedSpans;
		}

		public string FilePath { get; }
		public int StartOffset { get; }
		public int StartLine { get; }
		public int StartCol { get; }
		public ReferenceUsage Usage { get; }
		public ImageId ImageId { get; }
		public ImmutableArray<ClassifiedText> ClassifiedSpans { get; }
		public TextSpan Highlight { get; }
		public string Name { get; }


		public bool CanNavigateTo (IMSBuildEditorHost host)
			=> host != null && System.IO.File.Exists(FilePath);

		public bool TryNavigateTo (IMSBuildEditorHost host, bool isPreview)
			=> host.OpenFile (FilePath, StartOffset, isPreview);

		public static FoundReference NoResults { get; } = new FoundReference (
			KnownImages.StatusInformation.ToImageId (),
			ImmutableArray.Create (new ClassifiedText(
                    "Search found no results",
                PredefinedClassificationTypeNames.NaturalLanguage))
			);

		public bool DisplayIfNoReferences { get; set; }
		public string ProjectName { get; set; }
	}

	public struct ClassifiedText
	{
		public string Text { get; }
		public string ClassificationType { get; }

		public ClassifiedText(string text, string classificationType)
        {
			Text = text;
			ClassificationType = classificationType;
		}
	}

	public static class ClassifiedTextExtensions
	{
		public static string JoinText (this ImmutableArray<ClassifiedText> taggedText)
			=> string.Concat (taggedText.Select (t => t.Text));

		public static ImmutableArray<T> WhereAsArray<T> (this ImmutableArray<T> array, Func<T, bool> predicate)
			=> ImmutableArray<T>.Empty.AddRange (array.Where (predicate));

		public static ImmutableArray<TOut> SelectAsArray<TIn, TOut> (this ImmutableArray<TIn> array, Func<TIn, TOut> selector)
			=> ImmutableArray<TOut>.Empty.AddRange (array.Select (selector));
	}
}