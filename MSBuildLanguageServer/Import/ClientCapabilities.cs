// based on https://raw.githubusercontent.com/dotnet/roslyn/f477a900832b63285a32b1fa5c90be1c312204b9/src/Features/LanguageServer/Protocol/Protocol/ClientCapabilities.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Roslyn.LanguageServer.Protocol
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class which represents client capabilities.
    ///
    /// See the <see href="https://microsoft.github.io/language-server-protocol/specifications/specification-current/#clientCapabilities">Language Server Protocol specification</see> for additional information.
    /// </summary>
    internal class ClientCapabilities
    {
        /// <summary>
        /// Gets or sets the workspace capabilities.
        /// </summary>
        [JsonPropertyName("workspace")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkspaceClientCapabilities? Workspace
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the text document capabilities.
        /// </summary>
        [JsonPropertyName("textDocument")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TextDocumentClientCapabilities? TextDocument
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the experimental capabilities.
        /// </summary>
        [JsonPropertyName("general")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeneralClientCapabilities? General
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the experimental capabilities.
        /// </summary>
        [JsonPropertyName("experimental")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Experimental
        {
            get;
            set;
        }
    }

    class GeneralClientCapabilities
    {
        [JsonPropertyName("markdown")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MarkdownClientCapabilities? Markdown { get; set; }
    }

    class MarkdownClientCapabilities
    {
        [JsonPropertyName("parser")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required string Parser { get; set; }

        [JsonPropertyName("version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Version { get; set; }

        [JsonPropertyName("allowedTags")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? AllowedTags { get; set; }
    }
}