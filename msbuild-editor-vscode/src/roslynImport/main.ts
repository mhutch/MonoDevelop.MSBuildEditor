// https://raw.githubusercontent.com/dotnet/vscode-csharp/89da0f69901965627158f0120e1273ea2fc2486b/src/main.ts
// heavily edited to remove C# specific functionality, comment out unused functionality
// not yet parameterized - hardcodes MSBuild specific paths/ids/etc

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as util from './common';
import * as vscode from 'vscode';

import { ActivationFailure } from './omnisharp/loggingEvents';
//import { CsharpChannelObserver } from './shared/observers/csharpChannelObserver';
//import { CsharpLoggerObserver } from './shared/observers/csharpLoggerObserver';
import { EventStream } from './eventStream';
import { PlatformInformation } from './shared/platform';
//import { TelemetryObserver } from './observers/telemetryObserver';
import TelemetryReporter from '@vscode/extension-telemetry';
import createOptionStream from './shared/observables/createOptionStream';
import { RoslynLanguageServer, activateRoslynLanguageServer, RoslynLanguageServerOptions } from './lsptoolshost/roslynLanguageServer';
import path from 'path';
/*
import { ServerStateChange } from './lsptoolshost/serverStateChange';
import { languageServerOptions } from './shared/options';
import { getComponentFolder } from './lsptoolshost/builtInComponents';
*/

export async function activate(context: vscode.ExtensionContext, lspOptions : RoslynLanguageServerOptions) {
    const optionStream = createOptionStream("msbuild");

    const eventStream = new EventStream();

    util.setExtensionPath(context.extension.extensionPath);

    let platformInfo: PlatformInformation;
    try {
        platformInfo = await PlatformInformation.GetCurrent();
    } catch (error) {
        eventStream.post(new ActivationFailure());
        throw error;
    }

    const aiKey = "80591d65-3815-4ee1-9572-d1b3691a83b1"; // context.extension.packageJSON.contributes.debuggers[0].aiKey;
    const reporter = new TelemetryReporter(aiKey);
    // ensure it gets properly disposed. Upon disposal the events will be flushed.
    context.subscriptions.push(reporter);

    const csharpChannel = vscode.window.createOutputChannel('MSBuild');
	/*
    const csharpChannelObserver = new CsharpChannelObserver(csharpChannel);
    const csharpLogObserver = new CsharpLoggerObserver(csharpChannel);
    eventStream.subscribe(csharpChannelObserver.post);
    eventStream.subscribe(csharpLogObserver.post);

    // If the dotnet bundle is installed, this will ensure the dotnet CLI is on the path.
    //await initializeDotnetPath();

    const telemetryObserver = new TelemetryObserver(platformInfo, () => reporter);
    eventStream.subscribe(telemetryObserver.post);

    const roslynLanguageServerEvents = new RoslynLanguageServerEvents();
    context.subscriptions.push(roslynLanguageServerEvents);
	*/
    let roslynLanguageServerStartedPromise: Promise<RoslynLanguageServer> | undefined = undefined;
	/*
    let projectInitializationCompletePromise: Promise<void> | undefined = undefined;

	// Setup a listener for project initialization complete before we start the server.
	projectInitializationCompletePromise = new Promise((resolve, _) => {
		roslynLanguageServerEvents.onServerStateChange(async (state) => {
			if (state === ServerStateChange.ProjectInitializationComplete) {
				resolve();
			}
		});
	});
*/
	const commandIdPrefix = "msbuild";
	const clientId = "msbuild-lsp";
	const clientName = "MSBuild LSP";

	// Start the server, but do not await the completion to avoid blocking activation.
	roslynLanguageServerStartedPromise = activateRoslynLanguageServer(
		commandIdPrefix,
		clientId,
		clientName,
		context,
		platformInfo,
		optionStream,
		csharpChannel,
		reporter,
		[{ scheme: 'file', language: 'msbuild' }],
		"MSBUILD_LANGUAGE_SERVER_PATH",
		path.join(context.extensionPath, 'server', 'MSBuildLanguageServer.dll'),
		lspOptions
	);

    if (!isSupportedPlatform(platformInfo)) {
        let errorMessage = `The ${context.extension.packageJSON.displayName} extension for Visual Studio Code is incompatible on ${platformInfo.platform} ${platformInfo.architecture}`;
		await vscode.window.showErrorMessage(errorMessage);

        // Unsupported platform
        return null;
    }

    reporter.sendTelemetryEvent('MSBuildActivated');

	// If we got here, the server should definitely have been created.
	util.isNotNull(roslynLanguageServerStartedPromise);
	//util.isNotNull(projectInitializationCompletePromise);

	/*
	const languageServerExport = new RoslynLanguageServerExport(roslynLanguageServerStartedPromise);
	return {
		initializationFinished: async () => {
			await coreClrDebugPromise;
			await razorLanguageServerStartedPromise;
			await roslynLanguageServerStartedPromise;
			await projectInitializationCompletePromise;
		},
		profferBrokeredServices: (container) =>
			profferBrokeredServices(context, container, roslynLanguageServerStartedPromise!),
		logDirectory: context.logUri.fsPath,
		determineBrowserType: BlazorDebugConfigurationProvider.determineBrowserType,
		experimental: {
			sendServerRequest: async (t, p, ct) => await languageServerExport.sendRequest(t, p, ct),
			languageServerEvents: roslynLanguageServerEvents,
		},
		getComponentFolder: (componentName) => {
			return getComponentFolder(componentName, languageServerOptions);
		},
	};
	*/
}

function isSupportedPlatform(platform: PlatformInformation): boolean {
    if (platform.isWindows()) {
        return platform.architecture === 'x86_64' || platform.architecture === 'arm64';
    }

    if (platform.isMacOS()) {
        return true;
    }

    if (platform.isLinux()) {
        return (
            platform.architecture === 'x86_64' ||
            platform.architecture === 'x86' ||
            platform.architecture === 'i686' ||
            platform.architecture === 'arm64'
        );
    }

    return false;
}

/*
async function initializeDotnetPath(): Promise<void> {
    const dotnetPackApi = await getDotnetPackApi();
    if (dotnetPackApi !== undefined) {
        await dotnetPackApi.getDotnetPath();
    }
}
*/