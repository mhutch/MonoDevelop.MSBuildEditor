// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CommonLanguageServerProtocol.Framework;

class MSBuildLspLogger : AbstractLspLogger
{
	public override void LogDebug(string message, params object[] @params) => throw new NotImplementedException();
    public override void LogStartContext(string message, params object[] @params) => throw new NotImplementedException();
    public override void LogEndContext(string message, params object[] @params) => throw new NotImplementedException();
    public override void LogInformation(string message, params object[] @params) => throw new NotImplementedException();
    public override void LogWarning(string message, params object[] @params) => throw new NotImplementedException();
    public override void LogError(string message, params object[] @params) => throw new NotImplementedException();
    public override void LogException(Exception exception, string? message = null, params object[] @params) => throw new NotImplementedException();

}
