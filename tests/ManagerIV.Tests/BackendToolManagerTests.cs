using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ManagerIV.Core;

namespace ManagerIV.Tests;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handlerFunc;

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handlerFunc)
    {
        _handlerFunc = handlerFunc;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _handlerFunc(request);
    }
}

public class BackendToolManagerTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly string _cacheDir;

    public BackendToolManagerTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "ManagerIV_ToolTests_" + Guid.NewGuid().ToString("N"));
        _cacheDir = Path.Combine(_testBaseDir, "Cache");
        Directory.CreateDirectory(_cacheDir);
    }

    [Fact]
    public async Task TestDownloadToolSuccess()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("dummy binary payload content")
            };
            res.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return Task.FromResult(res);
        });

        using var httpClient = new HttpClient(mockHandler);
        var manager = new BackendToolManager(_cacheDir, httpClient);

        string destFile = Path.Combine(_testBaseDir, "tool.zip");

        // Act
        string result = await manager.DownloadToolAsync("https://example.com/tool.zip", destFile);

        // Assert
        Assert.Equal(destFile, result);
        Assert.True(File.Exists(destFile));
        Assert.Equal("dummy binary payload content", File.ReadAllText(destFile));
    }

    [Fact]
    public async Task TestDownloadToolFailsOnHtml()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body>GitHub Angry Unicorn Error Details</body></html>")
            };
            res.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return Task.FromResult(res);
        });

        using var httpClient = new HttpClient(mockHandler);
        var manager = new BackendToolManager(_cacheDir, httpClient);

        string destFile = Path.Combine(_testBaseDir, "bad_tool.zip");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await manager.DownloadToolAsync("https://example.com/tool.zip", destFile);
        });

        Assert.Contains("Expected binary/text payload but received an HTML response", ex.Message);
        Assert.Contains("GitHub Angry Unicorn", ex.Message);
        Assert.False(File.Exists(destFile), "Destination file should not be written or kept if it was an HTML page.");
    }

    [Fact]
    public async Task TestDownloadToolFailsOnHttpError()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            return Task.FromResult(res);
        });

        using var httpClient = new HttpClient(mockHandler);
        var manager = new BackendToolManager(_cacheDir, httpClient);

        string destFile = Path.Combine(_testBaseDir, "error_tool.zip");

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await manager.DownloadToolAsync("https://example.com/tool.zip", destFile);
        });

        Assert.False(File.Exists(destFile), "File should not exist on server error.");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, recursive: true);
            }
        }
        catch { }
    }
}
