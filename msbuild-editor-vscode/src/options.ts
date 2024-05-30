import { RoslynLanguageServerOptions } from "./roslynImport/lsptoolshost/roslynLanguageServer";
import { readOption } from "./roslynImport/shared/options";

export interface MSBuildEditorOptions extends RoslynLanguageServerOptions
{
}

export class MSBuildEditorOptionsImpl implements MSBuildEditorOptions
{
	public get serverPath() {
		return readOption<string>('msbuild.server.path', '');
	}

	public get startTimeout(){
		return readOption<number>('msbuild.server.startTimeout', 30000);
	}

	public get waitForDebugger(){
		return readOption<boolean>('msbuild.server.waitForDebugger', false);
	}

	public get logLevel(){
		return readOption<string>('msbuild.server.logLevel', 'Information');
	}

	public get suppressLspErrorToasts(){
		return readOption<boolean>('msbuild.server.suppressLspErrorToasts', false);
	}

	public get dotnetPath(){
		return readOption<string>('msbuild.server.dotnetPath', '');
	}

	public get crashDumpPath(){
		return readOption<string | undefined>('msbuild.server.crashDumpPath', undefined);
	}
}

export const msbuildEditorOptions : MSBuildEditorOptions = new MSBuildEditorOptionsImpl();