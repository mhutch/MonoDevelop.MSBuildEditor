using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly:Addin (
	"MonoDevelop.MSBuildEditor", 
	Namespace = "MonoDevelop.MSBuildEditor",
	Version = "1.0"
)]

[assembly:AddinName ("MonoDevelop.MSBuildEditor")]
[assembly:AddinCategory ("IDE extensions")]
[assembly:AddinDescription ("MonoDevelop.MSBuildEditor")]
[assembly:AddinAuthor ("Michael Hutchinson")]
