namespace SchemaImporter;

class DownloadCache
{
	readonly string cacheDir;

	public DownloadCache(string cacheDir)
	{
		this.cacheDir = cacheDir;
	}

	public bool ForceRefresh { get; set; }

	public async Task<Stream?> GetStream(string url, CancellationToken ct)
	{
		var cacheFileName = new Uri(url).Segments.Last();
		var cacheFile = Path.Combine(cacheDir, cacheFileName);

		if (!ForceRefresh && File.Exists(cacheFile)) {
			return File.OpenRead(cacheFile);
		}

		using var downloader = new HttpClient();
		using var response = await downloader.GetAsync(url, ct);
		if (!response.IsSuccessStatusCode) {
			return null;
		}

		if (File.Exists(cacheFile)) {
			if (response.Content.Headers.LastModified is DateTimeOffset lastModified) {
				if (lastModified <= File.GetLastWriteTime(cacheFile)) {
					return File.OpenRead(cacheFile);
				}
			}
		}

		using var contentStream = await response.Content.ReadAsStreamAsync(ct);

		var memoryStream = new MemoryStream();
		await contentStream.CopyToAsync(memoryStream, ct);
		memoryStream.Position = 0;

		Directory.CreateDirectory(cacheDir);
		using var fileStream = File.Create(cacheFile);
		await memoryStream.CopyToAsync(fileStream, ct);
		memoryStream.Position = 0;

		return memoryStream;
	}
}