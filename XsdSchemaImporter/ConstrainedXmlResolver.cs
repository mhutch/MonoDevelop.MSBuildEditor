using System.Collections;

namespace SchemaImporter;

/// <summary>
/// XmlResolver that resolves only from a fixed (optionally remapped) list
/// </summary>
class ConstrainedXmlResolver : System.Xml.XmlResolver, IEnumerable<Uri>
{
	readonly Action<Uri> logBlockedUri;
	readonly Dictionary<Uri, Uri> uriMap = new();
	readonly Func<Uri, Stream?> loadUri;

	public ConstrainedXmlResolver(Func<Uri, Stream?> loadUri, Action<Uri> logBlockedUri)
	{
		this.loadUri = loadUri;
		this.logBlockedUri = logBlockedUri;
	}

	public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
	{
		if (!uriMap.TryGetValue(absoluteUri, out var remapped)) {
			logBlockedUri(absoluteUri);
			return null;
		}

		return loadUri(remapped);
	}

	public IEnumerator<Uri> GetEnumerator() => uriMap.Keys.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public void Add (string uriString)
	{
		var uri = new Uri(uriString);
		uriMap.Add(uri, uri);
	}

	public void Add(string uriString, string remappedUriString)
	{
		var uri = new Uri(uriString);
		var remappedUri = new Uri(remappedUriString);
		uriMap.Add(uri, remappedUri);
	}
}