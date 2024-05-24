// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.Logging;

namespace MonoDevelop.MSBuild.Editor.LanguageServer;

static class LspLoggerExtensions
{
    public static ILogger ToILogger(this ILspLogger lspLogger) => new LspLoggerWrapper (lspLogger);

    class LspLoggerWrapper(ILspLogger lspLogger) : ILogger
    {
        readonly ILspLogger lspLogger = lspLogger;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            if (state.ToString () is string message) {
                lspLogger.LogStartContext (message);
                return new LogState (this, message);
            }
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) {
                lspLogger.LogException (exception, formatter (state, exception));
                return;
            }

            switch(logLevel) {
                case LogLevel.Information:
                    lspLogger.LogInformation (formatter (state, exception));
                    break;
                case LogLevel.Warning:
                lspLogger.LogWarning (formatter (state, exception));
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    lspLogger.LogError (formatter (state, exception));
                    break;
            }
        }

        readonly struct LogState(LspLoggerWrapper logger, string message) : IDisposable
        {
            public void Dispose()
            {
                logger.lspLogger.LogEndContext (message);
            }
        }
    }
}