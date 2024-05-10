// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
//  WARNING: THIS FILE WAS IMPORTED FROM MSBUILD
//
//  * IF YOU UPDATE IT, UPDATE THE HASH IN THE URL AND REAPPLY CHANGES FROM THE NOTES IN THIS HEADER
//  * IF YOU MODIFY IT, DESCRIBE THE CHANGES IN THE NOTES
//
//  URL: https://raw.githubusercontent.com/dotnet/msbuild/7434b575d12157ef98aeaad3b86c8f235f551c41/src/Build/BackEnd/Components/SdkResolution/SdkResolverManifest.cs

using Microsoft.Build.Shared;

using System.IO;
using System.Xml;

namespace Microsoft.Build.BackEnd.SdkResolution
{
	/// <summary>
	/// Serialization contract for an SDK Resolver manifest
	/// </summary>
	internal class SdkResolverManifest
	{
		internal string Path { get; set; }

		/// <summary>
		/// Deserialize the file into an SdkResolverManifest.
		/// </summary>
		/// <param name="filePath">Path to the manifest xml file.</param>
		/// <returns>New deserialized collection instance.</returns>
		internal static SdkResolverManifest Load (string filePath)
		{
			XmlReaderSettings readerSettings = new XmlReaderSettings () {
				IgnoreComments = true,
				IgnoreWhitespace = true,
				DtdProcessing = DtdProcessing.Ignore,
				XmlResolver = null
			};

			using (FileStream stream = new FileStream (filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (XmlReader reader = XmlReader.Create (stream, readerSettings)) {
				while (reader.Read ()) {
					if (reader.NodeType == XmlNodeType.Element && reader.Name == "SdkResolver") {
						return ParseSdkResolverElement (reader);
					} else {
						throw new XmlException (ResourceUtilities.FormatResourceStringStripCodeAndKeyword ("UnrecognizedElement", reader.Name));
					}
				}
			}

			return null;
		}

		private static SdkResolverManifest ParseSdkResolverElement (XmlReader reader)
		{
			SdkResolverManifest manifest = new SdkResolverManifest ();

			while (reader.Read ()) {
				switch (reader.NodeType) {
				case XmlNodeType.Element: {
					manifest.Path = reader.Name switch {
						"Path" => reader.ReadElementContentAsString (),
						_ => throw new XmlException (ResourceUtilities.FormatResourceStringStripCodeAndKeyword ("UnrecognizedElement", reader.Name)),
					};
				}
				break;

				default:
					throw new XmlException (ResourceUtilities.FormatResourceStringStripCodeAndKeyword ("UnrecognizedElement", reader.Name));
				}
			}

			return manifest;
		}
	}
}