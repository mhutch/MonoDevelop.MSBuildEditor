using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;

args= [ "schema", "generate", "Foo.props" ];

var verbosityOption = new Option<string>(
	"--verbosity",
	"Set the verbosity level of the command. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]"
);
verbosityOption.SetDefaultValue("m");

ILoggerFactory CreateLoggerFactory(ParseResult result) =>
	LoggerFactory.Create (builder => builder
		.AddConsole ()
		.SetMinimumLevel(result.GetValueForOption (verbosityOption) switch {
			"q" => LogLevel.Error,
			"quiet" => LogLevel.Error,
			"m" => LogLevel.Warning,
			"minimal" => LogLevel.Warning,
			"n" => LogLevel.Information,
			"normal" => LogLevel.Information,
			"d" => LogLevel.Debug,
			"detailed" => LogLevel.Debug,
			"diag" => LogLevel.Trace,
			"diagnostic" => LogLevel.Trace,
			_ => LogLevel.Information
		})
);

// msbuildtool Hello.targets --update
// msbuildtool Hello.targets --schema Foo.buildschema.json
var schemaGenerateProjectFiles = new Argument<IEnumerable<string>>("project", "MSBuild project file(s) for which to generate an MSBuild schema");
var schemaGenerateSchemaFile = new Option<string>("schema",
	"Path of the schema file to generate. Defaults to the name of the first project file argument with the extension changed " +
	"to `.buildschema.json`. If the schema file already exists, it will instead be updated.");

// msbuild schema generate
var schemaGenerateCommand = new Command ("generate", "Generate MSBuild schema") {
	schemaGenerateProjectFiles,
	schemaGenerateSchemaFile,
	verbosityOption
};

Handler.SetHandler (schemaGenerateCommand, (InvocationContext ctx) => SchemaCommands.GenerateSchema (
	CreateLoggerFactory (ctx.ParseResult),
	ctx.ParseResult.GetValueForArgument (schemaGenerateProjectFiles),
	ctx.ParseResult.GetValueForOption (schemaGenerateSchemaFile),
	CreateConsoleCancellationToken ()
));

// msbuild schema validate
var schemaValidateProject = new Argument<IEnumerable<string>>("schemas", "MSBuild schema files to validate");
var schemaValidateCommand = new Command ("validate", "Check that an MSBuild schema file is well-formed and internally consistent") {
	schemaValidateProject,
	verbosityOption
};

Handler.SetHandler (schemaValidateCommand, (InvocationContext ctx) => SchemaCommands.ValidateSchema (
	CreateLoggerFactory (ctx.ParseResult),
	ctx.ParseResult.GetValueForArgument (schemaValidateProject),
	CreateConsoleCancellationToken ()
));

// msbuild analyze
var analyzeProjectFiles = new Argument<IEnumerable<string>>("project", "MSBuild project file(s) to analyze");
var analyzeProjectRecursiveOption = new Option<string>("filter", "");
var analyzeCommand = new Command ("analyze", "Run MSBuild static analyzers on one or more MSBuild project files to identify potential issues") {
	analyzeProjectFiles,
	analyzeProjectRecursiveOption,
	verbosityOption
};

var rootCommand = new RootCommand {
	new Command ("schema", "Commands that generate and validate MSBuild schemas") {
		schemaGenerateCommand,
		schemaValidateCommand
	},
	analyzeCommand
};

rootCommand.Invoke(args);


static CancellationToken CreateConsoleCancellationToken ()
{
	var cts = new CancellationTokenSource ();
	bool cancelling = false;
	Console.CancelKeyPress += (s, e) => {
		// on first ctrl-c, soft cancel - try to cancel the operation
		// on second ctrl-c, hard cancel - allow process to terminate
		if (!cancelling) {
			cancelling = true;
			e.Cancel = true;
			cts.Cancel ();
		} else {
			e.Cancel = false;
		}
	};
	return cts.Token;
}
