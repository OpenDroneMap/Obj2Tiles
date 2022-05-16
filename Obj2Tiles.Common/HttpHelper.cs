namespace Obj2Tiles.Common;

public static class HttpHelper
{
    private static readonly HttpClient _httpClient = new HttpClient();

    public static async Task DownloadFileAsync(string uri, string outputPath)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
            throw new InvalidOperationException("URI is invalid.");

        var fileBytes = await _httpClient.GetByteArrayAsync(uri);
        await File.WriteAllBytesAsync(outputPath, fileBytes);
    }
}