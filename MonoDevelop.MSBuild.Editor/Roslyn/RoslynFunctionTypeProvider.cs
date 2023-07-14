// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Typesystem;

using ISymbol = MonoDevelop.MSBuild.Language.ISymbol;

namespace MonoDevelop.MSBuild.Editor.Roslyn
{
	[Export (typeof (IFunctionTypeProvider))]
	partial class RoslynFunctionTypeProvider : IFunctionTypeProvider
	{
		readonly ILogger logger;

		[ImportingConstructor]
		public RoslynFunctionTypeProvider ([Import (AllowDefault = true)] IRoslynCompilationProvider assemblyLoader, MSBuildEnvironmentLogger environmentLogger)
			: this (assemblyLoader, environmentLogger.Logger)
		{ }

		public RoslynFunctionTypeProvider (IRoslynCompilationProvider assemblyLoader, ILogger logger)
		{
			AssemblyLoader = assemblyLoader;
			this.logger = logger;
		}

		public IRoslynCompilationProvider AssemblyLoader { get; }

		readonly object locker = new object ();
		Compilation compilation;
		Task compilationLoadTask;

		//we need the reference assembly to get docs
		static string GetMscorlibReferenceAssembly ()
		{
			if (Util.Platform.IsWindows) {
				//FIXME: enumerate installed frameworks
				var programFiles86 = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
				var file = Path.Combine (programFiles86, @"Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\mscorlib.dll");
				if (File.Exists (file)) {
					return file;
				}
			}
			return typeof (string).Assembly.Location;
		}

		Task LoadCoreCompilation ()
		{
			if (AssemblyLoader == null) {
				return Task.CompletedTask;
			}
			return Task.Run (() => {
				try {
					var mscorlibPath = GetMscorlibReferenceAssembly ();
					compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create (
						"FunctionCompletion",
						references: new[] {
							AssemblyLoader.CreateReference (mscorlibPath)
						});
				} catch (Exception ex) {
					LogFailedToResolveCompilation (logger, ex);
				}
			});
		}

		public Task EnsureInitialized (CancellationToken token)
		{
			if (compilationLoadTask == null) {
				lock (locker) {
					if (compilationLoadTask == null) {
						compilationLoadTask = LoadCoreCompilation ();
					}
				}
			}
			if (compilationLoadTask.IsCompleted || !token.CanBeCanceled) {
				return compilationLoadTask;
			}

			//allow aborting the await without cancelling the loader task
			return Task.WhenAny (compilationLoadTask, Task.Delay (-1, token));
		}

		public IEnumerable<FunctionInfo> GetPropertyFunctionNameCompletions (ExpressionNode triggerExpression)
		{
			if (triggerExpression is ConcatExpression expression) {
				triggerExpression = expression.Nodes.Last ();
			}

			var last = triggerExpression.Find (triggerExpression.End);
			if (last == null) {
				return null;
			}

			if (last is ExpressionFunctionName pn) {
				last = pn.Parent;
			}

			if (!(last is ExpressionPropertyFunctionInvocation node)) {
				return null;
			}

			if (node.Target is ExpressionClassReference classRef) {
				if (classRef.Name == "MSBuild") {
					return IntrinsicFunctions.GetIntrinsicPropertyFunctions ();
				}
				if (IntrinsicFunctions.TryGetPermittedStaticFunctions (classRef.Name, out Predicate<string> isPermitted)) {
					return GetStaticFunctions (classRef.Name, isPermitted).CollapseOverloads ();
				}
				return null;
			}

			//function completion
			if (node.Target is ExpressionPropertyNode epn && (epn is ExpressionPropertyName || epn is ExpressionPropertyFunctionInvocation)) {
				var type = ResolveType (epn);
				return GetInstanceFunctions (type, true, false).CollapseOverloads ();
			}

			return null;
		}

		public MSBuildValueKind ResolveType (ExpressionPropertyNode node)
		{
			if (node is ExpressionPropertyName) {
				return MSBuildValueKind.Unknown;
			}

			if (node is ExpressionPropertyFunctionInvocation inv && inv.Target is ExpressionPropertyNode epn) {
				if (epn is ExpressionClassReference classRef) {
					var info = GetStaticPropertyFunctionInfo (classRef.Name, inv.Function.Name);
					return info.ReturnType;
				}

				//FIXME: maybe this could pass the types along directly instead of constantly converting
				var targetType = ResolveType (epn);

				//FIXME: overload resolution
				var match = Find (GetInstanceFunctions (targetType, true, true), inv.Function?.Name);
				if (match != null) {
					return match.ReturnType;
				}
				return MSBuildValueKind.Unknown;
			}

			return MSBuildValueKind.Unknown;
		}

		public IEnumerable<FunctionInfo> GetItemFunctionNameCompletions ()
			=> IntrinsicFunctions.GetIntrinsicItemFunctions ().Concat (GetStringFunctions (false, false).CollapseOverloads ());

		public IEnumerable<ClassInfo> GetClassNameCompletions ()
		{
			foreach (var typeName in IntrinsicFunctions.GetPermittedStaticFunctionTypes ()) {
				var type = compilation?.GetTypeByMetadataName (typeName);
				if (type != null) {
					yield return new RoslynClassInfo (typeName, type);
				} else {
					yield return new ClassInfo (typeName, null);
				}
			}
			yield return new ClassInfo ("MSBuild", "Intrinsic MSBuild functions");
		}

		//FIXME: make this lookup cheaper
		public FunctionInfo GetStaticPropertyFunctionInfo (string className, string name)
		{
			if (className == null) {
				return null;
			}
			if (className == "MSBuild") {
				return IntrinsicFunctions.GetIntrinsicPropertyFunctions ().FirstOrDefault (n => n.Name == name);
			}
			if (IntrinsicFunctions.TryGetPermittedStaticFunctions (className, out var permittedFunctions)) {
				return GetStaticFunctions (className, permittedFunctions).FirstOrDefault (n => n.Name == name);
			}
			return null;
		}

		//FIXME: make this lookup cheaper
		public FunctionInfo GetPropertyFunctionInfo (MSBuildValueKind valueKind, string name)
		{
			return Find (GetInstanceFunctions (valueKind, true, true), name);
		}

		static FunctionInfo Find (IEnumerable<FunctionInfo> functions, string name)
		{
			if (name == null) {
				return functions.FirstOrDefault (f => f is RoslynPropertyInfo rf && rf.Symbol.IsIndexer);
			}
			return functions.FirstOrDefault (f => f.Name == name);
		}

		//FIXME: make this lookup cheaper
		public FunctionInfo GetItemFunctionInfo (string name)
		{
			return GetItemFunctionNameCompletions ().FirstOrDefault (n => n.Name == name);
		}

		//FIXME: make this lookup cheaper
		public ClassInfo GetClassInfo (string name)
		{
			return GetClassNameCompletions ().FirstOrDefault (n => n.Name == name);
		}

		public ISymbol GetEnumInfo (string reference)
		{
			//FIXME: resolve enum values
			return new ConstantSymbol (reference, null, MSBuildValueKind.Unknown);
		}

		IEnumerable<FunctionInfo> GetStringFunctions (bool includeProperties, bool includeIndexers)
		{
			var type = compilation?.GetTypeByMetadataName ("System.String");
			if (type != null) {
				return GetInstanceFunctions (type, includeProperties, includeIndexers);
			}
			return Array.Empty<FunctionInfo> ();
		}

		IEnumerable<FunctionInfo> GetInstanceFunctions (MSBuildValueKind kind, bool includeProperties, bool includeIndexers)
		{
			// FIXME: support arrays
			if (kind.AllowsLists ()) {
				return Array.Empty<FunctionInfo> ();
			}

			INamedTypeSymbol type = null;

			if (DotNetTypeMap.FromValueKind (kind) is string dotNetType) {
				type = compilation?.GetTypeByMetadataName (dotNetType);
			}

			// if unresolved, assume it's coerced to string?
			type ??= compilation?.GetTypeByMetadataName ("System.String");

			if (type is not null) {
				return GetInstanceFunctions (type, includeProperties, includeIndexers);
			}

			return Array.Empty<FunctionInfo> ();
		}

		static IEnumerable<FunctionInfo> GetInstanceFunctions (INamedTypeSymbol type, bool includeProperties, bool includeIndexers)
		{
			foreach (var member in type.GetMembers ()) {
				if (member.IsStatic || !member.DeclaredAccessibility.HasFlag (Accessibility.Public)) {
					continue;
				}
				if (member is IMethodSymbol method) {
					switch (method.MethodKind) {
					case MethodKind.Ordinary:
					case MethodKind.PropertyGet:
					case MethodKind.BuiltinOperator:
					case MethodKind.UserDefinedOperator:
						break;
					default:
						continue;
					}
					if (ConvertType (method.ReturnType).IsUnknown ()) {
						continue;
					}
					bool unknownType = false;
					foreach (var p in method.Parameters) {
						if (ConvertType (p.Type).IsUnknown ()) {
							unknownType = true;
							break;
						}
					}
					if (unknownType) {
						continue;
					}
					yield return new RoslynFunctionInfo (method);
				} else if (includeProperties && member is IPropertySymbol prop) {
					if (ConvertType (prop.Type).IsUnknown ()) {
						continue;
					}
					if (!includeIndexers && prop.IsIndexer) {
						continue;
					}
					yield return new RoslynPropertyInfo (prop);
				}
			}
		}

		IEnumerable<FunctionInfo> GetStaticFunctions (string className, Predicate<string> filter)
		{
			var type = compilation?.GetTypeByMetadataName (className);
			if (type == null) {
				yield break;
			}
			foreach (var member in type.GetMembers ()) {
				if (!member.DeclaredAccessibility.HasFlag (Accessibility.Public)) {
					continue;
				}
				if (!member.IsStatic && (member as IMethodSymbol)?.MethodKind != MethodKind.Constructor) {
					continue;
				}
				if (!filter (member.Name)) {
					continue;
				}
				if (member is IMethodSymbol method) {
					switch (method.MethodKind) {
					case MethodKind.Ordinary:
					case MethodKind.PropertyGet:
					case MethodKind.BuiltinOperator:
					case MethodKind.UserDefinedOperator:
						if (!method.IsStatic) {
							continue;
						}
						if (ConvertType (method.ReturnType).IsUnknown ()) {
							continue;
						}
						break;
					case MethodKind.Constructor:
						break;
					default:
						continue;
					}

					bool unknownType = false;
					foreach (var p in method.Parameters) {
						if (ConvertType (p.Type).IsUnknown ()) {
							unknownType = true;
							break;
						}
					}
					//FIXME relax this
					if (unknownType) {
						continue;
					}
					yield return new RoslynFunctionInfo (method);
				} else if (member is IPropertySymbol prop) {
					if (ConvertType (prop.Type).IsUnknown ()) {
						continue;
					}
					yield return new RoslynPropertyInfo (prop);
				}
			}
		}

		public static MSBuildValueKind ConvertType (ITypeSymbol type)
		{
			if (type is IArrayTypeSymbol arr) {
				return ConvertType (arr.ElementType) | MSBuildValueKind.ListSemicolon;
			}

			string fullTypeName = RoslynHelpers.GetFullName (type);

			return DotNetTypeMap.ToValueKind (fullTypeName);
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Failed to load Roslyn compilation for function completion")]
		static partial void LogFailedToResolveCompilation (ILogger logger, Exception ex);
	}
}
