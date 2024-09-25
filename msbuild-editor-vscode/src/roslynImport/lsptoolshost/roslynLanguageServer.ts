// https://raw.githubusercontent.com/dotnet/vscode-csharp/ba156937926e760759a7e5e13bcf2d1be8729dc8/src/lsptoolshost/roslynLanguageServer.ts
// this has been heavily edited to parameterize the extension-specific info
// and remove C#/Razor specific functionality

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';
import * as cp from 'child_process';
import * as uuid from 'uuid';
import * as net from 'net';
import { registerCommands } from './commands';
import { UriConverter } from './uriConverter';

import {
    LanguageClientOptions,
    ServerOptions,
    State,
    Trace,
    RequestType,
    RequestType0,
    PartialResultParams,
    ProtocolRequestType,
    SocketMessageWriter,
    SocketMessageReader,
    MessageTransports,
    RAL,
    CancellationToken,
    RequestHandler,
    ResponseError,
} from 'vscode-languageclient/node';
import { PlatformInformation } from '../shared/platform';
import TelemetryReporter from '@vscode/extension-telemetry';
import { DotnetRuntimeExtensionResolver, DotnetRuntimeResolverOptions } from './dotnetRuntimeExtensionResolver';
import { IHostExecutableResolver } from '../shared/constants/IHostExecutableResolver';
import { RoslynLanguageClient } from './roslynLanguageClient';
import { registerLanguageServerOptionChanges } from './optionChanges';
import { Observable } from 'rxjs';
import { registerShowToastNotification } from './showToastNotification';
import { NamedPipeInformation } from './roslynProtocol';

let _channel: vscode.OutputChannel;
let _traceChannel: vscode.OutputChannel;

export interface RoslynLanguageServerDefinition {
	clientId : string,
	clientName : string,
	clientOptions: LanguageClientOptions,
	serverPathEnvVar: string,
	bundledServerPath : string
	commandIdPrefix : string,
}

export interface RoslynLanguageServerOptions extends DotnetRuntimeResolverOptions {
	readonly serverPath: string | undefined;
	readonly startTimeout: number;
	readonly waitForDebugger: boolean;
	readonly logLevel: string;
	readonly suppressLspErrorToasts: boolean;
}

const RoslynLanguageOptionsThatTriggerReload: ReadonlyArray<keyof RoslynLanguageServerOptions> = [
    'dotnetPath',
    'serverPath',
    'waitForDebugger',
    'logLevel',
];

export class RoslynLanguageServer {
    /**
     * The encoding to use when writing to and from the stream.
     */
    private static readonly encoding: RAL.MessageBufferEncoding = 'utf-8';

    /**
     * The regular expression used to find the named pipe key in the LSP server's stdout stream.
     */
    private static readonly namedPipeKeyRegex = /{"pipeName":"[^"]+"}/;

    /**
     * The timeout for stopping the language server (in ms).
     */
    private static _stopTimeout = 10000;

    constructor(
        private _languageClient: RoslynLanguageClient,
        private _platformInfo: PlatformInformation,
        private _context: vscode.ExtensionContext,
        private _telemetryReporter: TelemetryReporter,
		private _options: RoslynLanguageServerOptions
    ) {
        this.registerSetTrace();

        registerShowToastNotification(this._languageClient);
    }

    private registerSetTrace() {
        // Set the language client trace level based on the log level option.
        // setTrace only works after the client is already running.
        this._languageClient.onDidChangeState(async (state) => {
            if (state.newState === State.Running) {
                const languageClientTraceLevel = RoslynLanguageServer.GetTraceLevel(this._options.logLevel);

                await this._languageClient.setTrace(languageClientTraceLevel);
            }
        });
    }

    /**
     * Resolves server options and starts the dotnet language server process.
     * This promise will complete when the server starts.
     */
    public static async initializeAsync(
		lspDefinition : RoslynLanguageServerDefinition,
        platformInfo: PlatformInformation,
        hostExecutableResolver: IHostExecutableResolver,
        context: vscode.ExtensionContext,
        telemetryReporter: TelemetryReporter,
		lspOptions: RoslynLanguageServerOptions
    ): Promise<RoslynLanguageServer> {
        const serverOptions: ServerOptions = async () => {
            return await this.startServer(
                platformInfo,
                hostExecutableResolver,
                context,
                telemetryReporter,
				lspDefinition.serverPathEnvVar,
				lspDefinition.bundledServerPath,
				lspOptions
            );
        };

		// TODO: clone instead of mutate, or replace lspDefinition.clientOptions with a set of specific things we can copy
		lspDefinition.clientOptions.traceOutputChannel = _traceChannel;
		lspDefinition.clientOptions.outputChannel = _channel;
		lspDefinition.clientOptions.uriConverters = {
			// VSCode encodes the ":" as "%3A" in file paths, for example "file:///c%3A/Users/dabarbet/source/repos/ConsoleApp8/ConsoleApp8/Program.cs".
			// System.Uri does not decode the LocalPath property correctly into a valid windows path, instead you get something like
			// "/c:/Users/dabarbet/source/repos/ConsoleApp8/ConsoleApp8/Program.cs" (note the incorrect forward slashes and prepended "/").
			// Properly decoded, it would look something like "c:\Users\dabarbet\source\repos\ConsoleApp8\ConsoleApp8\Program.cs"
			// So instead we decode the URI here before sending to the server.
			code2Protocol: UriConverter.serialize,
			protocol2Code: UriConverter.deserialize,
		};

		/*
            middleware: {
                workspace: {
                    configuration: (params) => readConfigurations(params),
                },
            },
		*/

        // Create the language client and start the client.
        const client = new RoslynLanguageClient(
            lspDefinition.clientId,
            lspDefinition.clientName,
            serverOptions,
            lspDefinition.clientOptions,
			lspOptions
        );

        client.registerProposedFeatures();

        const server = new RoslynLanguageServer(client, platformInfo, context, telemetryReporter, lspOptions);

        // Start the client. This will also launch the server process.
        await client.start();
        return server;
    }

    public async stop(): Promise<void> {
        await this._languageClient.stop(RoslynLanguageServer._stopTimeout);
    }

    public async restart(): Promise<void> {
        await this._languageClient.restart();
    }

    /**
     * Returns whether or not the underlying LSP server is running or not.
     */
    public isRunning(): boolean {
        return this._languageClient.state === State.Running;
    }

    /**
     * Makes an LSP request to the server with a given type and parameters.
     */
    public async sendRequest<Params, Response, Error>(
        type: RequestType<Params, Response, Error>,
        params: Params,
        token: vscode.CancellationToken
    ): Promise<Response> {
        if (!this.isRunning()) {
            throw new Error('Tried to send request while server is not started.');
        }

        try {
            const response = await this._languageClient.sendRequest(type, params, token);
            return response;
        } catch (e) {
            throw this.convertServerError(type.method, e);
        }
    }

    /**
     * Makes an LSP request to the server with a given type and no parameters
     */
    public async sendRequest0<Response, Error>(
        type: RequestType0<Response, Error>,
        token: vscode.CancellationToken
    ): Promise<Response> {
        if (!this.isRunning()) {
            throw new Error('Tried to send request while server is not started.');
        }

        try {
            const response = await this._languageClient.sendRequest(type, token);
            return response;
        } catch (e) {
            throw this.convertServerError(type.method, e);
        }
    }

    public async sendRequestWithProgress<P extends PartialResultParams, R, PR, E, RO>(
        type: ProtocolRequestType<P, R, PR, E, RO>,
        params: P,
        onProgress: (p: PR) => Promise<any>,
        cancellationToken?: vscode.CancellationToken
    ): Promise<R> {
        // Generate a UUID for our partial result token and apply it to our request.
        const partialResultToken: string = uuid.v4();
        params.partialResultToken = partialResultToken;
        // Register the callback for progress events.
        const disposable = this._languageClient.onProgress(type, partialResultToken, async (partialResult) => {
            await onProgress(partialResult);
        });

        try {
            const response = await this._languageClient.sendRequest(type, params, cancellationToken);
            return response;
        } catch (e) {
            throw this.convertServerError(type.method, e);
        } finally {
            disposable.dispose();
        }
    }

    /**
     * Sends an LSP notification to the server with a given method and parameters.
     */
    public async sendNotification<Params>(method: string, params: Params): Promise<any> {
        if (!this.isRunning()) {
            throw new Error('Tried to send request while server is not started.');
        }

        const response = await this._languageClient.sendNotification(method, params);
        return response;
    }

    public registerOnRequest<Params, Result, Error>(
        type: RequestType<Params, Result, Error>,
        handler: RequestHandler<Params, Result, Error>
    ) {
        this._languageClient.addDisposable(this._languageClient.onRequest(type, handler));
    }

    private convertServerError(request: string, e: any): Error {
        let error: Error;
        if (e instanceof ResponseError && e.code === -32800) {
            // Convert the LSP RequestCancelled error (code -32800) to a CancellationError so we can handle cancellation uniformly.
            error = new vscode.CancellationError();
        } else if (e instanceof Error) {
            error = e;
        } else if (typeof e === 'string') {
            error = new Error(e);
        } else {
            error = new Error(`Unknown error: ${e.toString()}`);
        }

        if (!(error instanceof vscode.CancellationError)) {
            _channel.appendLine(`Error making ${request} request: ${error.message}`);
        }
        return error;
    }

    private static async startServer(
        platformInfo: PlatformInformation,
        hostExecutableResolver: IHostExecutableResolver,
        context: vscode.ExtensionContext,
        telemetryReporter: TelemetryReporter,
		serverPathEnvVar: string,
		bundledServerPath : string,
		options : RoslynLanguageServerOptions
    ): Promise<MessageTransports> {
        const serverPath = getServerPath(platformInfo, serverPathEnvVar, bundledServerPath, options);

        const dotnetInfo = await hostExecutableResolver.getHostExecutableInfo();
        const dotnetExecutablePath = dotnetInfo.path;

        _channel.appendLine('Dotnet path: ' + dotnetExecutablePath);

        let args: string[] = [];

        if (options.waitForDebugger) {
            args.push('--debug');
        }

        const logLevel = options.logLevel;
        if (logLevel) {
            args.push('--logLevel', logLevel);
        }

        if (logLevel && [Trace.Messages, Trace.Verbose].includes(this.GetTraceLevel(logLevel))) {
            _channel.appendLine(`Starting server at ${serverPath}`);
        }

        // shouldn't this arg only be set if it's running with CSDevKit?
        args.push('--telemetryLevel', telemetryReporter.telemetryLevel);

        args.push('--extensionLogDirectory', context.logUri.fsPath);

        let childProcess: cp.ChildProcessWithoutNullStreams;
        const cpOptions: cp.SpawnOptionsWithoutStdio = {
            detached: true,
            windowsHide: true,
            env: dotnetInfo.env,
        };

        if (serverPath.endsWith('.dll')) {
            // If we were given a path to a dll, launch that via dotnet.
            const argsWithPath = [serverPath].concat(args);

            if (logLevel && [Trace.Messages, Trace.Verbose].includes(this.GetTraceLevel(logLevel))) {
                _channel.appendLine(`Server arguments ${argsWithPath.join(' ')}`);
            }

            childProcess = cp.spawn(dotnetExecutablePath, argsWithPath, cpOptions);
        } else {
            // Otherwise assume we were given a path to an executable.
            if (logLevel && [Trace.Messages, Trace.Verbose].includes(this.GetTraceLevel(logLevel))) {
                _channel.appendLine(`Server arguments ${args.join(' ')}`);
            }

            childProcess = cp.spawn(serverPath, args, cpOptions);
        }

        // Record the stdout and stderr streams from the server process.
        childProcess.stdout.on('data', (data: { toString: (arg0: any) => any }) => {
            const result: string = isString(data) ? data : data.toString(RoslynLanguageServer.encoding);
            _channel.append('[stdout] ' + result);
        });
        childProcess.stderr.on('data', (data: { toString: (arg0: any) => any }) => {
            const result: string = isString(data) ? data : data.toString(RoslynLanguageServer.encoding);
            _channel.append('[stderr] ' + result);
        });
        childProcess.on('exit', (code) => {
            _channel.appendLine(`Language server process exited with ${code}`);
        });

        // Timeout promise used to time out the connection process if it takes too long.
        const timeout = new Promise<undefined>((resolve) => {
            RAL().timer.setTimeout(resolve, options.startTimeout);
        });

        const connectionPromise = new Promise<net.Socket>((resolveConnection, rejectConnection) => {
            // If the child process exited unexpectedly, reject the promise early.
            // Error information will be captured from the stdout/stderr streams above.
            childProcess.on('exit', (code) => {
                if (code && code !== 0) {
                    rejectConnection(new Error('Language server process exited unexpectedly'));
                }
            });

            // The server process will create the named pipe used for communication. Wait for it to be created,
            // and listen for the server to pass back the connection information via stdout.
            const namedPipePromise = new Promise<NamedPipeInformation>((resolve) => {
                _channel.appendLine('waiting for named pipe information from server...');
                childProcess.stdout.on('data', (data: { toString: (arg0: any) => any }) => {
                    const result: string = isString(data) ? data : data.toString(RoslynLanguageServer.encoding);
                    // Use the regular expression to find all JSON lines
                    const jsonLines = result.match(RoslynLanguageServer.namedPipeKeyRegex);
                    if (jsonLines) {
                        const transmittedPipeNameInfo: NamedPipeInformation = JSON.parse(jsonLines[0]);
                        _channel.appendLine('received named pipe information from server');
                        resolve(transmittedPipeNameInfo);
                    }
                });
            });

            const socketPromise = namedPipePromise.then(async (pipeConnectionInfo) => {
                return new Promise<net.Socket>((resolve, reject) => {
                    _channel.appendLine('attempting to connect client to server...');
                    const socket = net.createConnection(pipeConnectionInfo.pipeName, () => {
                        _channel.appendLine('client has connected to server');
                        resolve(socket);
                    });

                    // If we failed to connect for any reason, ensure the error is propagated.
                    socket.on('error', (err) => reject(err));
                });
            });

            socketPromise.then(resolveConnection, rejectConnection);
        });

        // Wait for the client to connect to the named pipe.
        let socket: net.Socket | undefined;
        if (options.waitForDebugger) {
            // Do not timeout the connection when the waitForDebugger option is set.
            socket = await connectionPromise;
        } else {
            socket = await Promise.race([connectionPromise, timeout]);
        }

        if (socket === undefined) {
            throw new Error('Timeout. Client could not connect to server via named pipe');
        }

        return {
            reader: new SocketMessageReader(socket, RoslynLanguageServer.encoding),
            writer: new SocketMessageWriter(socket, RoslynLanguageServer.encoding),
        };
    }

    private static GetTraceLevel(logLevel: string): Trace {
        switch (logLevel) {
            case 'Trace':
                return Trace.Verbose;
            case 'Debug':
                return Trace.Messages;
            case 'Information':
                return Trace.Off;
            case 'Warning':
                return Trace.Off;
            case 'Error':
                return Trace.Off;
            case 'Critical':
                return Trace.Off;
            case 'None':
                return Trace.Off;
            default:
                _channel.appendLine(
                    `Invalid log level ${logLevel}, server will not start. Please set the 'dotnet.server.trace' configuration to a valid value`
                );
                throw new Error(`Invalid log level ${logLevel}`);
        }
    }
}

/**
 * Creates and activates the Roslyn language server.
 * The returned promise will complete when the server starts.
 */
export async function activateRoslynLanguageServer(
	lspDefinition : RoslynLanguageServerDefinition,
    context: vscode.ExtensionContext,
    platformInfo: PlatformInformation,
    optionObservable: Observable<void>,
    outputChannel: vscode.OutputChannel,
    reporter: TelemetryReporter,
	lspOptions: RoslynLanguageServerOptions
): Promise<RoslynLanguageServer> {
    // Create a channel for outputting general logs from the language server.
    _channel = outputChannel;
    // Create a separate channel for outputting trace logs - these are incredibly verbose and make other logs very difficult to see.
    _traceChannel = vscode.window.createOutputChannel(`${lspDefinition.clientName} Trace Logs`);

    const hostExecutableResolver = new DotnetRuntimeExtensionResolver(
        platformInfo,
        (platformInfo: PlatformInformation) => getServerPath(platformInfo, lspDefinition.serverPathEnvVar, lspDefinition.bundledServerPath, lspOptions),
        outputChannel,
        context.extensionPath,
		context.extension.id,
		lspOptions
    );

    const languageServer = await RoslynLanguageServer.initializeAsync(
		lspDefinition,
        platformInfo,
        hostExecutableResolver,
        context,
        reporter,
		lspOptions
    );

	registerCommands(lspDefinition.commandIdPrefix, context, languageServer, hostExecutableResolver, _channel);

    context.subscriptions.push(registerLanguageServerOptionChanges(optionObservable, languageServer, lspOptions, RoslynLanguageOptionsThatTriggerReload));

    return languageServer;
}

function getServerPath(platformInfo: PlatformInformation, serverPathEnvVar: string, bundledServerPath : string, options: RoslynLanguageServerOptions) {
    let serverPath = process.env[serverPathEnvVar];

    if (serverPath) {
        _channel.appendLine(`Using server path override from ${serverPathEnvVar}: ${serverPath}`);
    } else {
        serverPath = options.serverPath;
        if (!serverPath) {
            // Option not set, use the path from the extension.
            serverPath = getInstalledServerPath(platformInfo, bundledServerPath);
        }
    }

    if (!fs.existsSync(serverPath)) {
        throw new Error(`Cannot find language server in path '${serverPath}'`);
    }

    return serverPath;
}

function getInstalledServerPath(platformInfo: PlatformInformation, bundledServerPath : string): string {
    let serverFilePath = bundledServerPath;

    if (!path.isAbsolute(serverFilePath)) {
        const clientRoot = __dirname;
        serverFilePath = path.join(clientRoot, bundledServerPath);
    }

    let extension = '';
    if (platformInfo.isWindows()) {
        extension = '.exe';
    } else if (platformInfo.isMacOS()) {
        // MacOS executables must be signed with codesign.  Currently all Roslyn server executables are built on windows
        // and therefore dotnet publish does not automatically sign them.
        // Tracking bug - https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1767519/
        extension = '.dll';
    }

    let pathWithExtension = `${serverFilePath}${extension}`;
    if (!fs.existsSync(pathWithExtension)) {
        // We might be running a platform neutral vsix which has no executable, instead we run the dll directly.
        pathWithExtension = `${serverFilePath}.dll`;
    }

    return pathWithExtension;
}

export function isString(value: any): value is string {
    return typeof value === 'string' || value instanceof String;
}