// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Roslyn.Utilities
{
    internal partial class BKTree
    {
        internal void WriteTo(ObjectWriter writer)
        {
            writer.WriteInt32(_concatenatedLowerCaseWords.Length);
            foreach (var c in _concatenatedLowerCaseWords)
            {
                writer.WriteChar(c);
            }

            writer.WriteInt32(_nodes.Length);
            foreach (var node in _nodes)
            {
                node.WriteTo(writer);
            }

            writer.WriteInt32(_edges.Length);
            foreach (var edge in _edges)
            {
                edge.WriteTo(writer);
            }
        }

        internal static BKTree ReadFrom(ObjectReader reader, ILogger logger)
        {
            try
            {
                var concatenatedLowerCaseWords = new char[reader.ReadInt32()];
                for (var i = 0; i < concatenatedLowerCaseWords.Length; i++)
                {
                    concatenatedLowerCaseWords[i] = reader.ReadChar();
                }

                var nodeCount = reader.ReadInt32();
                var nodes = ImmutableArray.CreateBuilder<Node>(nodeCount);
                for (var i = 0; i < nodeCount; i++)
                {
                    nodes.Add(Node.ReadFrom(reader));
                }

                var edgeCount = reader.ReadInt32();
                var edges = ImmutableArray.CreateBuilder<Edge>(edgeCount);
                for (var i = 0; i < edgeCount; i++)
                {
                    edges.Add(Edge.ReadFrom(reader));
                }

                return new BKTree(concatenatedLowerCaseWords, nodes.MoveToImmutable(), edges.MoveToImmutable());
            }
            catch (Exception ex)
            {
				LogExceptionInCacheRead (logger, ex);
                return null;
            }
        }

		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Exception in BKTree cache read")]
		static partial void LogExceptionInCacheRead (ILogger logger, Exception ex);
	}
}
