using Mono.Addins;
using Mono.Addins.Description;

[assembly: Addin (
	"MSBuildEditor",
	Namespace = "MonoDevelop",
	Version = ThisAssembly.AssemblyFileVersion
)]

[assembly: AddinName ("MSBuild Editor")]
[assembly: AddinCategory ("IDE extensions")]
[assembly: AddinDescription ("Editing support for MSBuild files")]
[assembly: AddinAuthor ("Mikayla Hutchinson")]
