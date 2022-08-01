// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage ("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Using threadpool thread to avoid stack overflow", Scope = "member", Target = "~M:Roslyn.Utilities.ObjectReader.ReadValue~System.Object")]
[assembly: SuppressMessage ("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Using threadpool thread to avoid stack overflow", Scope = "member", Target = "~M:Roslyn.Utilities.ObjectWriter.WriteObject(System.Object,Roslyn.Utilities.IObjectWritable)")]
[assembly: SuppressMessage ("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Using threadpool thread to avoid stack overflow", Scope = "member", Target = "~M:Roslyn.Utilities.ObjectWriter.WriteArray(System.Array)")]
