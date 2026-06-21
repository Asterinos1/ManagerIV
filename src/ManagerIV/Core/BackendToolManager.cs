using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Octokit;

namespace ManagerIV.Core;

/// <summary>
/// Details about a GitHub release for caching.
/// </summary>
public record ReleaseInfo(
    string TagName,
    string HtmlUrl,
    string? DownloadUrl,
    long SizeBytes,
    DateTime CachedAt
);

/// <summary>
/// Manages backend tool downloads and updates from GitHub releases, protecting rate limits with caching.
/// </summary>
public class BackendToolManager
{
    private readonly GitHubClient _githubClient;
    private readonly string _cacheDir;
    private readonly HttpClient _httpClient;

    public BackendToolManager(string cacheDir, string? githubToken = null)
    {
        _cacheDir = cacheDir ?? throw new ArgumentNullException(nameof(cacheDir));
        _githubClient = new GitHubClient(new ProductHeaderValue("ManagerIV"));
        if (!string.IsNullOrEmpty(githubToken))
        {
            _githubClient.Credentials = new Credentials(githubToken);
        }
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Queries the latest release of a GitHub repository, caching the metadata locally.
    /// </summary>
    public async Task<ReleaseInfo> GetLatestReleaseAsync(string repoOwner, string repoName)
    {
        string cacheFile = Path.Combine(_cacheDir, $"{repoOwner}_{repoName}_release.json");
        if (File.Exists(cacheFile))
        {
            try
            {
                var writeTime = File.GetLastWriteTime(cacheFile);
                // Cache is valid for 1 hour to respect unauthenticated rate limit (60/hr)
                if (DateTime.Now - writeTime < TimeSpan.FromHours(1))
                {
                    string json = await File.ReadAllTextAsync(cacheFile);
                    var cached = JsonSerializer.Deserialize<ReleaseInfo>(json);
                    if (cached != null)
                    {
                        return cached;
                    }
                }
            }
            catch
            {
                // Fallback to fetch if cache read fails
            }
        }

        try
        {
            var release = await _githubClient.Repository.Release.GetLatest(repoOwner, repoName);
            var asset = release.Assets.FirstOrDefault();
            var info = new ReleaseInfo(
                release.TagName,
                release.HtmlUrl,
                asset?.BrowserDownloadUrl,
                asset?.Size ?? 0,
                DateTime.Now
            );

            Directory.CreateDirectory(_cacheDir);
            string serialized = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(cacheFile, serialized);
            return info;
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // Rate limit hit: return expired cache if it exists, otherwise throw
            if (File.Exists(cacheFile))
            {
                string json = await File.ReadAllTextAsync(cacheFile);
                var cached = JsonSerializer.Deserialize<ReleaseInfo>(json);
                if (cached != null)
                {
                    return cached;
                }
            }
            throw new Exception("GitHub API rate limit exceeded and no local cache was available.", ex);
        }
    }

    /// <summary>
    /// Downloads a tool and verifies its SHA-256 checksum.
    /// </summary>
    public async Task<string> DownloadToolAsync(string downloadUrl, string destinationPath, string? expectedSha256 = null)
    {
        string? dir = Path.GetDirectoryName(destinationPath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Fetch tool
        using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            using (var fs = new FileStream(destinationPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None))
            {
                await response.Content.CopyToAsync(fs);
            }
        }

        // Validate checksum
        if (!string.IsNullOrEmpty(expectedSha256))
        {
            string actualSha256 = await ComputeSha256Async(destinationPath);
            if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                throw new InvalidDataException($"Checksum validation failed for '{destinationPath}'. Expected: {expectedSha256}, Actual: {actualSha256}");
            }
        }

        return destinationPath;
    }

    private async Task<string> ComputeSha256Async(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read, 4096, useAsync: true);
        byte[] hashBytes = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
