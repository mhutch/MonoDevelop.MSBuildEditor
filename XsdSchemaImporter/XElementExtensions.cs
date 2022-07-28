using System.Xml.Linq;

namespace SchemaImporter
{
	static class XElementExtensions
	{
		static XNamespace nsXsd = XNamespace.Get("http://www.w3.org/2001/XMLSchema");

		public static void LogUnexpected(this XNode n) => Console.WriteLine($"Unexpected node {n}");

		public static IEnumerable<XElement> XsdElements(this XElement el)
		{
			foreach (var child in el.Elements()) {
				if (child.Name.Namespace == nsXsd) {
					yield return child;
					continue;
				}
				LogUnexpected(child);
			}
			yield return el;
		}

		public static IEnumerable<XElement> XsdElements(this XElement el, string name)
		{
			foreach (var child in el.Elements()) {
				if (child.Name.Namespace == nsXsd && child.Name.LocalName == name) {
					yield return child;
					continue;
				}
				LogUnexpected(child);
			}
			yield return el;
		}

		public static XElement? XsdSingleElement(this XElement el, string name)
		{
			XElement? single = null;
			foreach (var child in el.Elements()) {
				if (single is null && child.Name.Namespace == nsXsd && child.Name.LocalName == name) {
					single = child;
					continue;
				}
				LogUnexpected(child);
			}
			return single;
		}
	}
}