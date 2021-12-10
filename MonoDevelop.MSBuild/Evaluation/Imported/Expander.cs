// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
//  WARNING: THIS FILE WAS IMPORTED FROM MSBUILD
//
//  * IF YOU UPDATE IT, UPDATE THE HASH IN THE URL AND REAPPLY CHANGES FROM THE NOTES IN THIS HEADER
//  * IF YOU MODIFY IT, DESCRIBE THE CHANGES IN THE NOTES
//
//  URL: https://raw.githubusercontent.com/dotnet/msbuild/7434b575d12157ef98aeaad3b86c8f235f551c41/src/Build/Evaluation/Expander.cs
//
//  CHANGES: None



using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;
using Microsoft.Win32;
using AvailableStaticMethods = Microsoft.Build.Internal.AvailableStaticMethods;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using TaskItemFactory = Microsoft.Build.Execution.ProjectItemInstance.TaskItem.TaskItemFactory;

using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation
{

    /// <summary>
    /// Expands item/property/metadata in expressions.
    /// Encapsulates the data necessary for expansion.
    /// </summary>
    /// <remarks>
    /// Requires the caller to explicitly state what they wish to expand at the point of expansion (explicitly does not have a field for ExpanderOptions). 
    /// Callers typically use a single expander in many locations, and this forces the caller to make explicit what they wish to expand at the point of expansion.
    /// 
    /// Requires the caller to have previously provided the necessary material for the expansion requested.
    /// For example, if the caller requests ExpanderOptions.ExpandItems, the Expander will throw if it was not given items.
    /// </remarks>
    /// <typeparam name="P">Type of the properties used.</typeparam>
    /// <typeparam name="I">Type of the items used.</typeparam>
    internal class Expander<P, I>
        where P : class, IProperty
        where I : class, IItem
    {
        /// <summary>
        /// This class represents the function as extracted from an expression
        /// It is also responsible for executing the function.
        /// </summary>
        /// <typeparam name="T">Type of the properties used to expand the expression.</typeparam>
        private class Function<T>
            where T : class, IProperty
        {
            /// <summary>
            /// The type of this function's receiver.
            /// </summary>
            private Type _receiverType;

            /// <summary>
            /// The name of the function.
            /// </summary>
            private string _methodMethodName;

            /// <summary>
            /// The arguments for the function.
            /// </summary>
            private string[] _arguments;

            /// <summary>
            /// The expression that this function is part of.
            /// </summary>
            private string _expression;

            /// <summary>
            /// The property name that this function is applied on.
            /// </summary>
            private string _receiver;

            /// <summary>
            /// The binding flags that will be used during invocation of this function.
            /// </summary>
            private BindingFlags _bindingFlags;

            /// <summary>
            /// The remainder of the body once the function and arguments have been extracted.
            /// </summary>
            private string _remainder;

            /// <summary>
            /// List of properties which have been used but have not been initialized yet.
            /// </summary>
            private UsedUninitializedProperties _usedUninitializedProperties;

            private IFileSystem _fileSystem;

            /// <summary>
            /// Construct a function that will be executed during property evaluation.
            /// </summary>
            internal Function(
                Type receiverType,
                string expression,
                string receiver,
                string methodName,
                string[] arguments,
                BindingFlags bindingFlags,
                string remainder,
                UsedUninitializedProperties usedUninitializedProperties,
                IFileSystem fileSystem)
            {
                _methodMethodName = methodName;
                if (arguments == null)
                {
                    _arguments = Array.Empty<string>();
                }
                else
                {
                    _arguments = arguments;
                }

                _receiver = receiver;
                _expression = expression;
                _receiverType = receiverType;
                _bindingFlags = bindingFlags;
                _remainder = remainder;
                _usedUninitializedProperties = usedUninitializedProperties;
                _fileSystem = fileSystem;
            }

            /// <summary>
            /// Part of the extraction may result in the name of the property
            /// This accessor is used by the Expander
            /// Examples of expression root:
            ///     [System.Diagnostics.Process]::Start
            ///     SomeMSBuildProperty.
            /// </summary>
            internal string Receiver
            {
                get { return _receiver; }
            }

            /// <summary>
            /// Execute the function on the given instance.
            /// </summary>
            internal object Execute(object objectInstance, IPropertyProvider<T> properties, ExpanderOptions options, IElementLocation elementLocation)
            {
                object functionResult = String.Empty;
                object[] args = null;

                try
                {
                    // If there is no object instance, then the method invocation will be a static
                    if (objectInstance == null)
                    {
                        // Check that the function that we're going to call is valid to call
                        if (!IsStaticMethodAvailable(_receiverType, _methodMethodName))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionMethodUnavailable", _methodMethodName, _receiverType.FullName);
                        }

                        _bindingFlags |= BindingFlags.Static;

                        // For our intrinsic function we need to support calling of internal methods
                        // since we don't want them to be public
                        if (_receiverType == typeof(Microsoft.Build.Evaluation.IntrinsicFunctions))
                        {
                            _bindingFlags |= BindingFlags.NonPublic;
                        }
                    }
                    else
                    {
                        // Check that the function that we're going to call is valid to call
                        if (!IsInstanceMethodAvailable(_methodMethodName))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionMethodUnavailable", _methodMethodName, _receiverType.FullName);
                        }

                        _bindingFlags |= BindingFlags.Instance;

                        // The object that we're about to call methods on may have escaped characters
                        // in it, we want to operate on the unescaped string in the function, just as we
                        // want to pass arguments that are unescaped (see below)
                        if (objectInstance is string)
                        {
                            objectInstance = EscapingUtilities.UnescapeAll((string)objectInstance);
                        }
                    }

                    // We have a methodinfo match, need to plug in the arguments
                    args = new object[_arguments.Length];

                    // Assemble our arguments ready for passing to our method
                    for (int n = 0; n < _arguments.Length; n++)
                    {
                        object argument = PropertyExpander<T>.ExpandPropertiesLeaveTypedAndEscaped(
                            _arguments[n],
                            properties,
                            options,
                            elementLocation,
                            _usedUninitializedProperties,
                            _fileSystem);

                        if (argument is string argumentValue)
                        {
                            // Unescape the value since we're about to send it out of the engine and into
                            // the function being called. If a file or a directory function, fix the path
                            if (_receiverType == typeof(System.IO.File) || _receiverType == typeof(System.IO.Directory)
                                || _receiverType == typeof(System.IO.Path))
                            {
                                argumentValue = FileUtilities.FixFilePath(argumentValue);
                            }

                            args[n] = EscapingUtilities.UnescapeAll(argumentValue);
                        }
                        else
                        {
                            args[n] = argument;
                        }
                    }

                    // Handle special cases where the object type needs to affect the choice of method
                    // The default binder and method invoke, often chooses the incorrect Equals and CompareTo and 
                    // fails the comparison, because what we have on the right is generally a string.
                    // This special casing is to realize that its a comparison that is taking place and handle the
                    // argument type coercion accordingly; effectively pre-preparing the argument type so 
                    // that it matches the left hand side ready for the default binder’s method invoke.
                    if (objectInstance != null && args.Length == 1 && (String.Equals("Equals", _methodMethodName, StringComparison.OrdinalIgnoreCase) || String.Equals("CompareTo", _methodMethodName, StringComparison.OrdinalIgnoreCase)))
                    {
                        // change the type of the final unescaped string into the destination
                        args[0] = Convert.ChangeType(args[0], objectInstance.GetType(), CultureInfo.InvariantCulture);
                    }

                    if (_receiverType == typeof(IntrinsicFunctions))
                    {
                        // Special case a few methods that take extra parameters that can't be passed in by the user
                        //

                        if (_methodMethodName.Equals("GetPathOfFileAbove") && args.Length == 1)
                        {
                            // Append the IElementLocation as a parameter to GetPathOfFileAbove if the user only
                            // specified the file name.  This is syntactic sugar so they don't have to always
                            // include $(MSBuildThisFileDirectory) as a parameter.
                            //
                            string startingDirectory = String.IsNullOrWhiteSpace(elementLocation.File) ? String.Empty : Path.GetDirectoryName(elementLocation.File);

                            args = new []
                            {
                                args[0],
                                startingDirectory,
                            };
                        }
                    }

                    // If we've been asked to construct an instance, then we
                    // need to locate an appropriate constructor and invoke it
                    if (String.Equals("new", _methodMethodName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryExecuteWellKnownConstructorNoThrow(out functionResult, args))
                        {
                            functionResult = LateBindExecute(null /* no previous exception */, BindingFlags.Public | BindingFlags.Instance, null /* no instance for a constructor */, args, true /* is constructor */);
                        }
                    }
                    else
                    {
                        bool wellKnownFunctionSuccess = false;

                        try
                        {
                            // First attempt to recognize some well-known functions to avoid binding
                            // and potential first-chance MissingMethodExceptions
                            wellKnownFunctionSuccess = TryExecuteWellKnownFunction(out functionResult, objectInstance, args);
                        }
                        // we need to preserve the same behavior on exceptions as the actual binder
                        catch (Exception ex)
                        {
                            string partiallyEvaluated = GenerateStringOfMethodExecuted(_expression, objectInstance, _methodMethodName, args);
                            if (options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                            {
                                return partiallyEvaluated;
                            }

                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.Message.Replace("\r\n", " "));
                        }

                        if (!wellKnownFunctionSuccess)
                        {
                            // Execute the function given converted arguments
                            // The only exception that we should catch to try a late bind here is missing method
                            // otherwise there is the potential of running a function twice!
                            try
                            {
                                // First use InvokeMember using the standard binder - this will match and coerce as needed
                                functionResult = _receiverType.InvokeMember(_methodMethodName, _bindingFlags, Type.DefaultBinder, objectInstance, args, CultureInfo.InvariantCulture);
                            }
                            catch (MissingMethodException ex) // Don't catch and retry on any other exception
                            {
                                // If we're invoking a method, then there are deeper attempts that
                                // can be made to invoke the method
                                if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                                {
                                    // The standard binder failed, so do our best to coerce types into the arguments for the function
                                    // This may happen if the types need coercion, but it may also happen if the object represents a type that contains open type parameters, that is, ContainsGenericParameters returns true. 
                                    functionResult = LateBindExecute(ex, _bindingFlags, objectInstance, args, false /* is not constructor */);
                                }
                                else
                                {
                                    // We were asked to get a property or field, and we found that we cannot
                                    // locate it. Since there is no further argument coersion possible
                                    // we'll throw right now.
                                    throw;
                                }
                            }
                        }
                    }

                    // If the result of the function call is a string, then we need to escape the result
                    // so that we maintain the "engine contains escaped data" state.
                    // The exception is that the user is explicitly calling MSBuild::Unescape or MSBuild::Escape
                    if (functionResult is string && !String.Equals("Unescape", _methodMethodName, StringComparison.OrdinalIgnoreCase) && !String.Equals("Escape", _methodMethodName, StringComparison.OrdinalIgnoreCase))
                    {
                        functionResult = EscapingUtilities.Escape((string)functionResult);
                    }

                    // We have nothing left to parse, so we'll return what we have
                    if (String.IsNullOrEmpty(_remainder))
                    {
                        return functionResult;
                    }

                    // Recursively expand the remaining property body after execution
                    return PropertyExpander<T>.ExpandPropertyBody(
                        _remainder,
                        functionResult,
                        properties,
                        options,
                        elementLocation,
                        _usedUninitializedProperties,
                        _fileSystem);
                }

                // Exceptions coming from the actual function called are wrapped in a TargetInvocationException
                catch (TargetInvocationException ex)
                {
                    // We ended up with something other than a function expression
                    string partiallyEvaluated = GenerateStringOfMethodExecuted(_expression, objectInstance, _methodMethodName, args);
                    if (options.HasFlag(ExpanderOptions.LeavePropertiesUnexpandedOnError))
                    {
                        // If the caller wants to ignore errors (in a log statement for example), just return the partially evaluated value
                        return partiallyEvaluated;
                    }
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.InnerException.Message.Replace("\r\n", " "));
                    return null;
                }

                // Any other exception was thrown by trying to call it
                catch (Exception ex)
                {
                    if (ExceptionHandling.NotExpectedFunctionException(ex))
                    {
                        throw;
                    }

                    // If there's a :: in the expression, they were probably trying for a static function
                    // invocation. Give them some more relevant info in that case
                    if (s_invariantCompareInfo.IndexOf(_expression, "::", CompareOptions.OrdinalIgnoreCase) > -1)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionStaticMethodSyntax", _expression, ex.Message.Replace("Microsoft.Build.Evaluation.IntrinsicFunctions.", "[MSBuild]::"));
                    }
                    else
                    {
                        // We ended up with something other than a function expression
                        string partiallyEvaluated = GenerateStringOfMethodExecuted(_expression, objectInstance, _methodMethodName, args);
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.Message);
                    }

                    return null;
                }
            }

            /// <summary>
            /// Shortcut to avoid calling into binding if we recognize some most common functions.
            /// Binding is expensive and throws first-chance MissingMethodExceptions, which is
            /// bad for debugging experience and has a performance cost.
            /// A typical binding operation with exception can take ~1.500 ms; this call is ~0.050 ms
            /// (rough numbers just for comparison).
            /// See https://github.com/Microsoft/msbuild/issues/2217.
            /// </summary>
            /// <param name="returnVal">The value returned from the function call.</param>
            /// <param name="objectInstance">Object that the function is called on.</param>
            /// <param name="args">arguments.</param>
            /// <returns>True if the well known function call binding was successful.</returns>
            private bool TryExecuteWellKnownFunction(out object returnVal, object objectInstance, object[] args)
            {
                returnVal = null;

                if (objectInstance is string text)
                {
                    if (string.Equals(_methodMethodName, nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string arg0))
                        {
                            returnVal = text.StartsWith(arg0);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.Replace), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArgs(args, out string arg0, out string arg1))
                        {
                            returnVal = text.Replace(arg0, arg1);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.Contains), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string arg0))
                        {
                            returnVal = text.Contains(arg0);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 0)
                        {
                            returnVal = text.ToUpperInvariant();
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 0)
                        {
                            returnVal = text.ToLowerInvariant();
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string arg0))
                        {
                            returnVal = text.EndsWith(arg0);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.ToLower), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 0)
                        {
                            returnVal = text.ToLower();
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArgs(args, out string arg0, out StringComparison arg1))
                        {
                            returnVal = text.IndexOf(arg0, arg1);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string arg0))
                        {
                            returnVal = text.IndexOfAny(arg0.ToCharArray());
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string arg0))
                        {
                            returnVal = text.LastIndexOf(arg0);
                            return true;
                        }
                        else if (TryGetArgs(args, out arg0, out int startIndex))
                        {
                            returnVal = text.LastIndexOf(arg0, startIndex);
                            return true;
                        }
                        else if (TryGetArgs(args, out arg0, out StringComparison arg1))
                        {
                            returnVal = text.LastIndexOf(arg0, arg1);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string arg0))
                        {
                            returnVal = text.LastIndexOfAny(arg0.ToCharArray());
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.Length), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 0)
                        {
                            returnVal = text.Length;
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.Substring), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out int startIndex))
                        {
                            returnVal = text.Substring(startIndex);
                            return true;
                        }
                        else if (TryGetArgs(args, out startIndex, out int length))
                        {
                            returnVal = text.Substring(startIndex, length);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.Split), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string separator) && separator.Length == 1)
                        {
                            returnVal = text.Split(separator[0]);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out int totalWidth))
                        {
                            returnVal = text.PadLeft(totalWidth);
                            return true;
                        }
                        else if (TryGetArgs(args, out totalWidth, out string paddingChar) && paddingChar.Length == 1)
                        {
                            returnVal = text.PadLeft(totalWidth, paddingChar[0]);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.PadRight), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out int totalWidth))
                        {
                            returnVal = text.PadRight(totalWidth);
                            return true;
                        }
                        else if (TryGetArgs(args, out totalWidth, out string paddingChar) && paddingChar.Length == 1)
                        {
                            returnVal = text.PadRight(totalWidth, paddingChar[0]);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string trimChars) && trimChars.Length > 0)
                        {
                            returnVal = text.TrimStart(trimChars.ToCharArray());
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string trimChars) && trimChars.Length > 0)
                        {
                            returnVal = text.TrimEnd(trimChars.ToCharArray());
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, "get_Chars", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out int index))
                        {
                            returnVal = text[index];
                            return true;
                        }
                    }
                }
                else if (objectInstance is string[])
                {
                    string[] stringArray = (string[])objectInstance;
                    if (string.Equals(_methodMethodName, "GetValue", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out int index))
                        {
                            returnVal = stringArray[index];
                            return true;
                        }
                    }
                }
                else if (objectInstance == null) // Calling a well-known static function
                {
                    if (_receiverType == typeof(string))
                    {
                        if (string.Equals(_methodMethodName, nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = string.IsNullOrWhiteSpace(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = string.IsNullOrEmpty(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(string.Copy), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = arg0;
                                return true;
                            }
                        }
                    }
                    else if (_receiverType == typeof(Math))
                    {
                        if (string.Equals(_methodMethodName, nameof(Math.Max), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out var arg0, out double arg1))
                            {
                                returnVal = Math.Max(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Math.Min), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out double arg0, out var arg1))
                            {
                                returnVal = Math.Min(arg0, arg1);
                                return true;
                            }
                        }
                    }
                    else if (_receiverType == typeof(IntrinsicFunctions))
                    {
                        if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.EnsureTrailingSlash(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.ValueOrDefault(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase))
                        {
                            if (ElementsOfType(args, typeof(string)))
                            {
                                returnVal = IntrinsicFunctions.NormalizePath(Array.ConvertAll(args, o => (string) o));
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, _fileSystem);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length >= 4 &&
                                TryGetArgs(args, out string arg0, out string arg1, enforceLength: false))
                            {
                                returnVal = IntrinsicFunctions.GetRegistryValueFromView(arg0, arg1, args[2], new ArraySegment<object>(args, 3, args.Length - 3));
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.IsRunningFromVisualStudio();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.Escape(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.GetPathOfFileAbove(arg0, arg1, _fileSystem);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out double arg0, out double arg1))
                            {
                                returnVal = arg0 + arg1;
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out double arg0, out double arg1))
                            {
                                returnVal = arg0 - arg1;
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out double arg0, out double arg1))
                            {
                                returnVal = arg0 * arg1;
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out double arg0, out double arg1))
                            {
                                returnVal = arg0 / arg1;
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetCurrentToolsDirectory();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetToolsDirectory32();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetToolsDirectory64();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetMSBuildSDKsPath();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetVsInstallRoot();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetMSBuildExtensionsPath();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetProgramFiles32();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.VersionEquals(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.VersionNotEquals(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.VersionGreaterThan(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.VersionLessThan(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(arg0);
                                return true;
                            }
                            if (TryGetArgs(args, out string arg1, out int arg2))
                            {
                                returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(arg1, arg2);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.GetTargetPlatformIdentifier(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.GetTargetPlatformVersion(arg0);
                                return true;
                            }
                            if (TryGetArgs(args, out string arg1, out int arg2))
                            {
                                returnVal = IntrinsicFunctions.GetTargetPlatformVersion(arg1, arg2);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.StableStringHash(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out Version arg0))
                            {
                                returnVal = IntrinsicFunctions.AreFeaturesEnabled(arg0);
                                return true;
                            }
                        }
                    }
                    else if (_receiverType == typeof(Path))
                    {
                        if (string.Equals(_methodMethodName, nameof(Path.Combine), StringComparison.OrdinalIgnoreCase))
                        {
                            string arg0, arg1, arg2, arg3;

                            // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
                            switch (args.Length)
                            {
                                case 0:
                                    return false;
                                case 1:
                                    if (TryGetArg(args, out arg0))
                                    {
                                        returnVal = Path.Combine(arg0);
                                        return true;
                                    }
                                    break;
                                case 2:
                                    if (TryGetArgs(args, out arg0, out arg1))
                                    {
                                        returnVal = Path.Combine(arg0, arg1);
                                        return true;
                                    }
                                    break;
                                case 3:
                                    if (TryGetArgs(args, out arg0, out arg1, out arg2))
                                    {
                                        returnVal = Path.Combine(arg0, arg1, arg2);
                                        return true;
                                    }
                                    break;
                                case 4:
                                    if (TryGetArgs(args, out arg0, out arg1, out arg2, out arg3))
                                    {
                                        returnVal = Path.Combine(arg0, arg1, arg2, arg3);
                                        return true;
                                    }
                                    break;
                                default:
                                    if (ElementsOfType(args, typeof(string)))
                                    {
                                        returnVal = Path.Combine(Array.ConvertAll(args, o => (string) o));
                                        return true;
                                    }
                                    break;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = Path.DirectorySeparatorChar;
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Path.GetFullPath), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = Path.GetFullPath(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = Path.IsPathRooted(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Path.GetTempPath), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = Path.GetTempPath();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Path.GetFileName), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = Path.GetFileName(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = Path.GetDirectoryName(arg0);
                                return true;
                            }
                        }
                    }
                    else if (_receiverType == typeof(Version))
                    {
                        if (string.Equals(_methodMethodName, nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = Version.Parse(arg0);
                                return true;
                            }
                        }
                    }
                    else if (_receiverType == typeof(System.Guid))
                    {
                        if (string.Equals(_methodMethodName, nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = Guid.NewGuid();
                                return true;
                            }
                        }
                    }
                }

                if (Traits.Instance.LogPropertyFunctionsRequiringReflection)
                {
                    LogFunctionCall("PropertyFunctionsRequiringReflection", objectInstance, args);
                }

                return false;
            }

            /// <summary>
            /// Shortcut to avoid calling into binding if we recognize some most common constructors.
            /// Analogous to TryExecuteWellKnownFunction but guaranteed to not throw.
            /// </summary>
            /// <param name="returnVal">The instance as created by the constructor call.</param>
            /// <param name="args">Arguments.</param>
            /// <returns>True if the well known constructor call binding was successful.</returns>
            private bool TryExecuteWellKnownConstructorNoThrow(out object returnVal, object[] args)
            {
                returnVal = null;

                if (_receiverType == typeof(string))
                {
                    if (args.Length == 0)
                    {
                        returnVal = String.Empty;
                        return true;
                    }
                    if (TryGetArg(args, out string arg0))
                    {
                        returnVal = arg0;
                        return true;
                    }
                }
                return false;
            }

            private bool ElementsOfType(object[] args, Type type)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    if (args[i].GetType() != type)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool TryGetArgs(object[] args, out string arg0, out string arg1, bool enforceLength = true)
            {
                arg0 = null;
                arg1 = null;

                if (enforceLength && args.Length != 2)
                {
                    return false;
                }

                if (args[0] is string value0 &&
                    args[1] is string value1)
                {
                    arg0 = value0;
                    arg1 = value1;

                    return true;
                }

                return false;
            }

            private bool TryGetArgs(object[] args, out string arg0, out string arg1, out string arg2)
            {
                arg0 = null;
                arg1 = null;
                arg2 = null;

                if (args.Length != 3)
                {
                    return false;
                }

                if (args[0] is string value0 &&
                    args[1] is string value1 &&
                    args[2] is string value2
                    )
                {
                    arg0 = value0;
                    arg1 = value1;
                    arg2 = value2;

                    return true;
                }

                return false;
            }

            private bool TryGetArgs(object[] args, out string arg0, out string arg1, out string arg2, out string arg3)
            {
                arg0 = null;
                arg1 = null;
                arg2 = null;
                arg3 = null;

                if (args.Length != 4)
                {
                    return false;
                }

                if (args[0] is string value0 &&
                    args[1] is string value1 &&
                    args[2] is string value2 &&
                    args[3] is string value3
                    )
                {
                    arg0 = value0;
                    arg1 = value1;
                    arg2 = value2;
                    arg3 = value3;

                    return true;
                }

                return false;
            }

            private bool TryGetArgs(object[] args, out string arg0, out string arg1)
            {
                arg0 = null;
                arg1 = null;

                if (args.Length != 2)
                {
                    return false;
                }

                if (args[0] is string value0 &&
                    args[1] is string value1)
                {
                    arg0 = value0;
                    arg1 = value1;

                    return true;
                }

                return false;
            }

            private static bool TryGetArg(object[] args, out int arg0)
            {
                if (args.Length != 1)
                {
                    arg0 = 0;
                    return false;
                }

                return TryConvertToInt(args[0], out arg0);
            }

            private static bool TryGetArg(object[] args, out Version arg0)
            {
                if (args.Length != 1)
                {
                    arg0 = default;
                    return false;
                }

                return TryConvertToVersion(args[0], out arg0);
            }

            private static bool TryConvertToVersion(object value, out Version arg0)
            {
                string val = value as string;

                if (string.IsNullOrEmpty(val) || !Version.TryParse(val, out arg0))
                {
                    arg0 = default;
                    return false;
                }

                return true;
            }

            private static bool TryConvertToInt(object value, out int arg0)
            {
                switch (value)
                {
                    case double d:
                        arg0 = Convert.ToInt32(d);
                        return arg0 == d;
                    case int i:
                        arg0 = i;
                        return true;
                    case string s when int.TryParse(s, out arg0):
                        return true;
                }

                arg0 = 0;
                return false;
            }

            private static bool TryConvertToDouble(object value, out double arg)
            {
                if (value is double unboxed)
                {
                    arg = unboxed;
                    return true;
                }
                else if (value is string str && double.TryParse(str, out arg))
                {
                    return true;
                }

                arg = 0;
                return false;
            }

            private static bool TryGetArg(object[] args, out string arg0)
            {
                if (args.Length != 1)
                {
                    arg0 = null;
                    return false;
                }

                arg0 = args[0] as string;
                return arg0 != null;
            }

            private static bool TryGetArgs(object[] args, out string arg0, out StringComparison arg1)
            {
                if (args.Length != 2)
                {
                    arg0 = null;
                    arg1 = default;

                    return false;
                }

                arg0 = args[0] as string;

                // reject enums as ints. In C# this would require a cast, which is not supported in msbuild expressions
                if (arg0 == null || !(args[1] is string comparisonTypeName) || int.TryParse(comparisonTypeName, out _))
                {
                    arg1 = default;
                    return false;
                }

                // Allow fully-qualified enum, e.g. "System.StringComparison.OrdinalIgnoreCase"
                if (comparisonTypeName.Contains('.'))
                {
                    comparisonTypeName = comparisonTypeName.Replace("System.StringComparison.", "").Replace("StringComparison.", "");
                }

                return Enum.TryParse(comparisonTypeName, out arg1);
            }

            private static bool TryGetArgs(object[] args, out int arg0, out int arg1)
            {
                arg0 = 0;
                arg1 = 0;

                if (args.Length != 2)
                {
                    return false;
                }

                return TryConvertToInt(args[0], out arg0) &&
                       TryConvertToInt(args[1], out arg1);
            }

            private static bool TryGetArgs(object[] args, out double arg0, out double arg1)
            {
                arg0 = 0;
                arg1 = 0;

                if (args.Length != 2)
                {
                    return false;
                }

                return TryConvertToDouble(args[0], out arg0) &&
                       TryConvertToDouble(args[1], out arg1);
            }

            private static bool TryGetArgs(object[] args, out int arg0, out string arg1)
            {
                arg0 = 0;
                arg1 = null;

                if (args.Length != 2)
                {
                    return false;
                }

                arg1 = args[1] as string;
                if (arg1 == null && args[1] is char ch)
                {
                    arg1 = ch.ToString();
                }

                if (TryConvertToInt(args[0], out arg0) &&
                    arg1 != null)
                {
                    return true;
                }

                return false;
            }

            private static bool TryGetArgs(object[] args, out string arg0, out int arg1)
            {
                arg0 = null;
                arg1 = 0;

                if (args.Length != 2)
                {
                    return false;
                }

                var value1 = args[1] as string;
                arg0 = args[0] as string;
                if (value1 != null &&
                    arg0 != null &&
                    int.TryParse(value1, out arg1))
                {
                    return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void LogFunctionCall(string fileName, object objectInstance, object[] args)
            {
                var logFile = Path.Combine(Directory.GetCurrentDirectory(), fileName);

                var argSignature = args != null
                    ? string.Join(", ", args.Select(a => a?.GetType().Name ?? "null"))
                    : string.Empty;

                File.AppendAllText(logFile, $"ReceiverType={_receiverType?.FullName}; ObjectInstanceType={objectInstance?.GetType().FullName}; MethodName={_methodMethodName}({argSignature})\n");
            }

            /// <summary>
            /// Given a type name and method name, try to resolve the type.
            /// </summary>
            /// <param name="typeName">May be full name or assembly qualified name.</param>
            /// <param name="simpleMethodName">simple name of the method.</param>
            /// <returns></returns>
            private static Type GetTypeForStaticMethod(string typeName, string simpleMethodName)
            {
                Type receiverType;
                Tuple<string, Type> cachedTypeInformation;

                // If we don't have a type name, we already know that we won't be able to find a type.  
                // Go ahead and return here -- otherwise the Type.GetType() calls below will throw.
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    return null;
                }

                // Check if the type is in the whitelist cache. If it is, use it or load it.
                cachedTypeInformation = AvailableStaticMethods.GetTypeInformationFromTypeCache(typeName, simpleMethodName);
                if (cachedTypeInformation != null)
                {
                    // We need at least one of these set
                    ErrorUtilities.VerifyThrow(cachedTypeInformation.Item1 != null || cachedTypeInformation.Item2 != null, "Function type information needs either string or type represented.");

                    // If we have the type information in Type form, then just return that
                    if (cachedTypeInformation.Item2 != null)
                    {
                        return cachedTypeInformation.Item2;
                    }
                    else if (cachedTypeInformation.Item1 != null)
                    {
                        // This is a case where the Type is not available at compile time, so
                        // we are forced to bind by name instead
                        var assemblyQualifiedTypeName = cachedTypeInformation.Item1;

                        // Get the type from the assembly qualified type name from AvailableStaticMethods
                        receiverType = Type.GetType(assemblyQualifiedTypeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);

                        // If the type information from the cache is not loadable, it means the cache information got corrupted somehow
                        // Throw here to prevent adding null types in the cache
                        ErrorUtilities.VerifyThrowInternalNull(receiverType, $"Type information for {typeName} was present in the whitelist cache as {assemblyQualifiedTypeName} but the type could not be loaded.");

                        // If we've used it once, chances are that we'll be using it again
                        // We can record the type here since we know it's available for calling from the fact that is was in the AvailableStaticMethods table
                        AvailableStaticMethods.TryAdd(typeName, simpleMethodName, new Tuple<string, Type>(assemblyQualifiedTypeName, receiverType));

                        return receiverType;
                    }
                }

                // Get the type from mscorlib (or the currently running assembly)
                receiverType = Type.GetType(typeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);

                if (receiverType != null)
                {
                    // DO NOT CACHE THE TYPE HERE!
                    // We don't add the resolved type here in the AvailableStaticMethods table. This is because that table is used
                    // during function parse, but only later during execution do we check for the ability to call specific methods on specific types.
                    // Caching it here would load any type into the white list.
                    return receiverType;
                }

                // Note the following code path is only entered when MSBUILDENABLEALLPROPERTYFUNCTIONS == 1.
                // This environment variable must not be cached - it should be dynamically settable while the application is executing.
                if (Environment.GetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS") == "1")
                {
                    // We didn't find the type, so go probing. First in System
                    receiverType = GetTypeFromAssembly(typeName, "System");

                    // Next in System.Core
                    if (receiverType == null)
                    {
                        receiverType = GetTypeFromAssembly(typeName, "System.Core");
                    }

                    // We didn't find the type, so try to find it using the namespace
                    if (receiverType == null)
                    {
                        receiverType = GetTypeFromAssemblyUsingNamespace(typeName);
                    }

                    if (receiverType != null)
                    {
                        // If we've used it once, chances are that we'll be using it again
                        // We can cache the type here, since all functions are enabled
                        AvailableStaticMethods.TryAdd(typeName, new Tuple<string, Type>(typeName, receiverType));
                    }
                }

                return receiverType;
            }

            /// <summary>
            /// Gets the specified type using the namespace to guess the assembly that its in.
            /// </summary>
            private static Type GetTypeFromAssemblyUsingNamespace(string typeName)
            {
                string baseName = typeName;
                int assemblyNameEnd = baseName.Length;

                // If the string has no dot, or is nothing but a dot, we have no
                // namespace to look for, so we can't help.
                if (assemblyNameEnd <= 0)
                {
                    return null;
                }

                // We will work our way up the namespace looking for an assembly that matches
                while (assemblyNameEnd > 0)
                {
                    string candidateAssemblyName = baseName.Substring(0, assemblyNameEnd);

                    // Try to load the assembly with the computed name
                    Type foundType = GetTypeFromAssembly(typeName, candidateAssemblyName);

                    if (foundType != null)
                    {
                        // We have a match, so get the type from that assembly
                        return foundType;
                    }
                    else
                    {
                        // Keep looking as we haven't found a match yet
                        baseName = candidateAssemblyName;
                        assemblyNameEnd = baseName.LastIndexOf('.');
                    }
                }

                // We didn't find it, so we need to give up
                return null;
            }

            /// <summary>
            /// Get the specified type from the assembly partial name supplied.
            /// </summary>
            [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadWithPartialName", Justification = "Necessary since we don't have the full assembly name. ")]
            private static Type GetTypeFromAssembly(string typeName, string candidateAssemblyName)
            {
                Type objectType = null;

                // Try to load the assembly with the computed name
#if FEATURE_GAC
#pragma warning disable 618, 612
                // Unfortunately Assembly.Load is not an alternative to LoadWithPartialName, since
                // Assembly.Load requires the full assembly name to be passed to it.
                // Therefore we must ignore the deprecated warning.
                Assembly candidateAssembly = Assembly.LoadWithPartialName(candidateAssemblyName);
#pragma warning restore 618, 612
#else
                Assembly candidateAssembly = null;
                try
                {
                    candidateAssembly = Assembly.Load(new AssemblyName(candidateAssemblyName));
                }
                catch (FileNotFoundException)
                {
                    // Swallow the error; LoadWithPartialName returned null when the partial name
                    // was not found but Load throws.  Either way we'll provide a nice "couldn't
                    // resolve this" error later.
                }
#endif

                if (candidateAssembly != null)
                {
                    objectType = candidateAssembly.GetType(typeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);
                }

                return objectType;
            }

            /// <summary>
            /// Extracts the name, arguments, binding flags, and invocation type for an indexer
            /// Also extracts the remainder of the expression that is not part of this indexer.
            /// </summary>
            private static void ConstructIndexerFunction(string expressionFunction, IElementLocation elementLocation, object propertyValue, int methodStartIndex, int indexerEndIndex, ref FunctionBuilder<T> functionBuilder)
            {
                string argumentsContent = expressionFunction.Substring(1, indexerEndIndex - 1);
                string remainder = expressionFunction.Substring(methodStartIndex);
                string functionName;
                string[] functionArguments;

                // If there are no arguments, then just create an empty array
                if (String.IsNullOrEmpty(argumentsContent))
                {
                    functionArguments = Array.Empty<string>();
                }
                else
                {
                    // We will keep empty entries so that we can treat them as null
                    functionArguments = ExtractFunctionArguments(elementLocation, expressionFunction, argumentsContent);
                }

                // choose the name of the function based on the type of the object that we
                // are using.
                if (propertyValue is Array)
                {
                    functionName = "GetValue";
                }
                else if (propertyValue is string)
                {
                    functionName = "get_Chars";
                }
                else // a regular indexer
                {
                    functionName = "get_Item";
                }

                functionBuilder.Name = functionName;
                functionBuilder.Arguments = functionArguments;
                functionBuilder.BindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.InvokeMethod;
                functionBuilder.Remainder = remainder;
            }

            /// <summary>
            /// Extracts the name, arguments, binding flags, and invocation type for a static or instance function.
            /// Also extracts the remainder of the expression that is not part of this function.
            /// </summary>
            private static void ConstructFunction(IElementLocation elementLocation, string expressionFunction, int argumentStartIndex, int methodStartIndex, ref FunctionBuilder<T> functionBuilder)
            {
                // The unevaluated and unexpanded arguments for this function
                string[] functionArguments;

                // The name of the function that will be invoked
                ReadOnlySpan<char> functionName;

                // What's left of the expression once the function has been constructed
                ReadOnlySpan<char> remainder = ReadOnlySpan<char>.Empty;

                // The binding flags that we will use for this function's execution
                BindingFlags defaultBindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public;

                ReadOnlySpan<char> expressionFunctionAsSpan = expressionFunction.AsSpan();
                
                ReadOnlySpan<char> expressionSubstringAsSpan = argumentStartIndex > -1 ? expressionFunctionAsSpan.Slice(methodStartIndex, argumentStartIndex - methodStartIndex) : ReadOnlySpan<char>.Empty;

                // There are arguments that need to be passed to the function
                if (argumentStartIndex > -1 && !expressionSubstringAsSpan.Contains(".".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    // separate the function and the arguments
                    functionName = expressionSubstringAsSpan.Trim();

                    // Skip the '('
                    argumentStartIndex++;

                    // Scan for the matching closing bracket, skipping any nested ones
                    int argumentsEndIndex = ScanForClosingParenthesis(expressionFunction, argumentStartIndex, out _, out _);

                    if (argumentsEndIndex == -1)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, AssemblyResources.GetString("InvalidFunctionPropertyExpressionDetailMismatchedParenthesis"));
                    }

                    // We have been asked for a method invocation
                    defaultBindingFlags |= BindingFlags.InvokeMethod;

                    // It may be that there are '()' but no actual arguments content
                    if (argumentStartIndex == expressionFunction.Length - 1)
                    {
                        functionArguments = Array.Empty<string>();
                    }
                    else
                    {
                        // we have content within the '()' so let's extract and deal with it
                        string argumentsContent = expressionFunction.Substring(argumentStartIndex, argumentsEndIndex - argumentStartIndex);

                        // If there are no arguments, then just create an empty array
                        if (string.IsNullOrEmpty(argumentsContent))
                        {
                            functionArguments = Array.Empty<string>();
                        }
                        else
                        {
                            // We will keep empty entries so that we can treat them as null
                            functionArguments = ExtractFunctionArguments(elementLocation, expressionFunction, argumentsContent);
                        }

                        remainder = expressionFunctionAsSpan.Slice(argumentsEndIndex + 1).Trim();
                    }
                }
                else
                {
                    int nextMethodIndex = expressionFunction.IndexOf('.', methodStartIndex);
                    int methodLength = expressionFunction.Length - methodStartIndex;
                    int indexerIndex = expressionFunction.IndexOf('[', methodStartIndex);

                    // We don't want to consume the indexer
                    if (indexerIndex >= 0 && indexerIndex < nextMethodIndex)
                    {
                        nextMethodIndex = indexerIndex;
                    }

                    functionArguments = Array.Empty<string>();

                    if (nextMethodIndex > 0)
                    {
                        methodLength = nextMethodIndex - methodStartIndex;
                        remainder = expressionFunctionAsSpan.Slice(nextMethodIndex).Trim();
                    }

                    ReadOnlySpan<char> netPropertyName = expressionFunctionAsSpan.Slice(methodStartIndex, methodLength).Trim();

                    ProjectErrorUtilities.VerifyThrowInvalidProject(netPropertyName.Length > 0, elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);

                    // We have been asked for a property or a field
                    defaultBindingFlags |= (BindingFlags.GetProperty | BindingFlags.GetField);

                    functionName = netPropertyName;
                }

                // either there are no functions left or what we have is another function or an indexer
                if (remainder.IsEmpty || remainder[0] == '.' || remainder[0] == '[')
                {
                    functionBuilder.Name = functionName.ToString();
                    functionBuilder.Arguments = functionArguments;
                    functionBuilder.BindingFlags = defaultBindingFlags;
                    functionBuilder.Remainder = remainder.ToString();
                }
                else
                {
                    // We ended up with something other than a function expression
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);
                }
            }

            /// <summary>
            /// Coerce the arguments according to the parameter types
            /// Will only return null if the coercion didn't work due to an InvalidCastException.
            /// </summary>
            private static object[] CoerceArguments(object[] args, ParameterInfo[] parameters)
            {
                object[] coercedArguments = new object[args.Length];

                try
                {
                    // Do our best to coerce types into the arguments for the function
                    for (int n = 0; n < parameters.Length; n++)
                    {
                        if (args[n] == null)
                        {
                            // We can't coerce (object)null -- that's as general
                            // as it can get!
                            continue;
                        }

                        // Here we have special case conversions on a type basis
                        if (parameters[n].ParameterType == typeof(char[]))
                        {
                            coercedArguments[n] = args[n].ToString().ToCharArray();
                        }
                        else if (parameters[n].ParameterType.GetTypeInfo().IsEnum && args[n] is string && ((string)args[n]).Contains("."))
                        {
                            Type enumType = parameters[n].ParameterType;
                            string typeLeafName = enumType.Name + ".";
                            string typeFullName = enumType.FullName + ".";

                            // Enum.parse expects commas between enum components
                            // We'll support the C# type | syntax too
                            // We'll also allow the user to specify the leaf or full type name on the enum
                            string argument = args[n].ToString().Replace('|', ',').Replace(typeFullName, "").Replace(typeLeafName, "");

                            // Parse the string representation of the argument into the destination enum                                
                            coercedArguments[n] = Enum.Parse(enumType, argument);
                        }
                        else
                        {
                            // change the type of the final unescaped string into the destination
                            coercedArguments[n] = Convert.ChangeType(args[n], parameters[n].ParameterType, CultureInfo.InvariantCulture);
                        }
                    }
                }
                // The coercion failed therefore we return null
                catch (InvalidCastException)
                {
                    return null;
                }
                catch (FormatException)
                {
                    return null;
                }
                catch (OverflowException)
                {
                    // https://github.com/Microsoft/msbuild/issues/2882
                    // test: PropertyFunctionMathMaxOverflow
                    return null;
                }

                return coercedArguments;
            }

            /// <summary>
            /// Make an attempt to create a string showing what we were trying to execute when we failed.
            /// This will show any intermediate evaluation which may help the user figure out what happened.
            /// </summary>
            private string GenerateStringOfMethodExecuted(string expression, object objectInstance, string name, object[] args)
            {
                string parameters = String.Empty;
                if (args != null)
                {
                    foreach (object arg in args)
                    {
                        if (arg == null)
                        {
                            parameters += "null";
                        }
                        else
                        {
                            string argString = arg.ToString();
                            if (arg is string && argString.Length == 0)
                            {
                                parameters += "''";
                            }
                            else
                            {
                                parameters += arg.ToString();
                            }
                        }

                        parameters += ", ";
                    }

                    if (parameters.Length > 2)
                    {
                        parameters = parameters.Substring(0, parameters.Length - 2);
                    }
                }

                if (objectInstance == null)
                {
                    string typeName = _receiverType.FullName;

                    // We don't want to expose the real type name of our intrinsics
                    // so we'll replace it with "MSBuild"
                    if (_receiverType == typeof(Microsoft.Build.Evaluation.IntrinsicFunctions))
                    {
                        typeName = "MSBuild";
                    }
                    if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                    {
                        return "[" + typeName + "]::" + name + "(" + parameters + ")";
                    }
                    else
                    {
                        return "[" + typeName + "]::" + name;
                    }
                }
                else
                {
                    string propertyValue = $"\"{objectInstance as string}\"";

                    if ((_bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                    {
                        return propertyValue + "." + name + "(" + parameters + ")";
                    }
                    else
                    {
                        return propertyValue + "." + name;
                    }
                }
            }

            /// <summary>
            /// Check the property function whitelist whether this method is available.
            /// </summary>
            private static bool IsStaticMethodAvailable(Type receiverType, string methodName)
            {
                if (receiverType == typeof(Microsoft.Build.Evaluation.IntrinsicFunctions))
                {
                    // These are our intrinsic functions, so we're OK with those
                    return true;
                }

                if (Traits.Instance.EnableAllPropertyFunctions)
                {
                    // anything goes
                    return true;
                }

                return AvailableStaticMethods.GetTypeInformationFromTypeCache(receiverType.FullName, methodName) != null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsInstanceMethodAvailable(string methodName)
            {
                if (Traits.Instance.EnableAllPropertyFunctions)
                {
                    // anything goes
                    return true;
                }

                // This could be expanded to an allow / deny list.
                return methodName != "GetType";
            }

            /// <summary>
            /// Construct and instance of objectType based on the constructor or method arguments provided.
            /// Arguments must never be null.
            /// </summary>
            private object LateBindExecute(Exception ex, BindingFlags bindingFlags, object objectInstance /* null unless instance method */, object[] args, bool isConstructor)
            {

                // First let's try for a method where all arguments are strings..
                Type[] types = new Type[_arguments.Length];
                for (int n = 0; n < _arguments.Length; n++)
                {
                    types[n] = typeof(string);
                }

                MethodBase memberInfo;
                if (isConstructor)
                {
                    memberInfo = _receiverType.GetConstructor(bindingFlags, null, types, null);
                }
                else
                {
                    memberInfo = _receiverType.GetMethod(_methodMethodName, bindingFlags, null, types, null);
                }

                // If we didn't get a match on all string arguments,
                // search for a method with the right number of arguments
                if (memberInfo == null)
                {
                    MethodBase[] members;
                    // Gather all methods that may match
                    if (isConstructor)
                    {
                        members = _receiverType.GetConstructors(bindingFlags);
                    }
                    else
                    {
                        members = _receiverType.GetMethods(bindingFlags);
                    }

                    foreach (MethodBase member in members)
                    {
                        ParameterInfo[] parameters = member.GetParameters();

                        // Simple match on name and number of params, we will be case insensitive
                        if (parameters.Length == _arguments.Length)
                        {
                            if (isConstructor || String.Equals(member.Name, _methodMethodName, StringComparison.OrdinalIgnoreCase))
                            {
                                // Try to find a method with the right name, number of arguments and
                                // compatible argument types
                                // we have a match on the name and argument number
                                // now let's try to coerce the arguments we have
                                // into the arguments on the matching method
                                object[] coercedArguments = CoerceArguments(args, parameters);

                                if (coercedArguments != null)
                                {
                                    // We have a complete match
                                    memberInfo = member;
                                    args = coercedArguments;
                                    break;
                                }
                            }
                        }
                    }
                }

                object functionResult = null;

                // We have a match and coerced arguments, let's construct..
                if (memberInfo != null && args != null)
                {
                    if (isConstructor)
                    {
                        functionResult = ((ConstructorInfo)memberInfo).Invoke(args);
                    }
                    else
                    {
                        functionResult = ((MethodInfo)memberInfo).Invoke(objectInstance /* null if static method */, args);
                    }
                }
                else if (!isConstructor)
                {
                    throw ex;
                }

                if (functionResult == null && isConstructor)
                {
                    throw new TargetInvocationException(new MissingMethodException());
                }

                return functionResult;
            }
        }
    }
}