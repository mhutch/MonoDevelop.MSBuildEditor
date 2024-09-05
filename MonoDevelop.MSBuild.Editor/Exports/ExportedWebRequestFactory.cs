using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using ProjectFileTools.NuGetSearch.IO;

namespace ProjectFileTools.Exports;

[Export(typeof(IWebRequestFactory))]
[Name("Default Web Request Factory")]
internal class ExportedWebRequestFactory : WebRequestFactory
{
}
