using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImmichMCP.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ImmichMCP.Tools;

[McpServerToolType]
public static class GalleryTools
{
    public const string ResourceUri = "ui://immich/gallery-v3.html";
    private const int MaxAssets = 8;

    [McpServerTool(
        Name = "immich_assets_show",
        Title = "Display Immich photos in ChatGPT",
        ReadOnly = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(GalleryOutput))]
    [McpMeta("ui", JsonValue = """{"resourceUri":"ui://immich/gallery-v3.html"}""")]
    [McpMeta("openai/outputTemplate", ResourceUri)]
    [McpMeta("openai/toolInvocation/invoking", "Preparing the photo gallery…")]
    [McpMeta("openai/toolInvocation/invoked", "Photo gallery ready")]
    [Description("The only Immich tool that displays photos visibly to the user in ChatGPT. REQUIRED final step whenever the user asks to see, show, display, browse, or find photos. First obtain the final asset IDs with search/list/people tools, then pass those IDs here newest first. Do not finish with immich_assets_download_thumbnail, filenames, or metadata because those do not display photos to the user.")]
    public static async Task<CallToolResult> ShowGallery(
        ImmichClient client,
        [Description("Final Immich asset IDs to display, ordered as they should appear (maximum 8)")] string[] assetIds,
        [Description("Short user-facing gallery title in the user's language")] string? title = null,
        CancellationToken cancellationToken = default)
    {
        var ids = assetIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxAssets)
            .ToArray();

        if (ids.Length == 0)
        {
            return Error("At least one Immich asset ID is required.");
        }

        var images = new List<LoadedGalleryImage>(ids.Length);
        var missing = new List<string>();

        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var asset = await client.GetAssetAsync(id, cancellationToken).ConfigureAwait(false);
                if (asset is null)
                {
                    missing.Add(id);
                    continue;
                }

                var (bytes, mimeType) = await client
                    .DownloadAssetThumbnailAsync(id, cancellationToken)
                    .ConfigureAwait(false);

                images.Add(new LoadedGalleryImage(
                    new GalleryImageSummary
                    {
                        Id = id,
                        FileName = asset.OriginalFileName,
                        CapturedAt = asset.ExifInfo?.DateTimeOriginal ?? asset.LocalDateTime,
                        Location = FormatLocation(asset.ExifInfo?.City, asset.ExifInfo?.State, asset.ExifInfo?.Country)
                    },
                    bytes,
                    mimeType));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                missing.Add(id);
            }
        }

        if (images.Count == 0)
        {
            return Error("None of the requested Immich assets could be loaded.");
        }

        var output = new GalleryOutput
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Immich photos" : title.Trim(),
            Images = images.Select(image => image.Summary).ToList(),
            MissingAssetIds = missing
        };

        var content = new List<ContentBlock>
        {
            new TextContentBlock
            {
                Text = $"Displayed {images.Count} Immich photo{(images.Count == 1 ? string.Empty : "s")} in the gallery."
            }
        };
        content.AddRange(images.Select(image => ImageContentBlock.FromBytes(image.Bytes, image.MimeType)));

        return new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToElement(output),
            Content = content
        };
    }

    private static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }]
    };

    private static string? FormatLocation(string? city, string? state, string? country)
    {
        var parts = new[] { city, state, country }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var location = string.Join(", ", parts);
        return location.Length == 0 ? null : location;
    }

    private sealed record LoadedGalleryImage(GalleryImageSummary Summary, byte[] Bytes, string MimeType);
}

public sealed class GalleryOutput
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "Immich photos";

    [JsonPropertyName("images")]
    public List<GalleryImageSummary> Images { get; init; } = [];

    [JsonPropertyName("missingAssetIds")]
    public List<string> MissingAssetIds { get; init; } = [];
}

public class GalleryImageSummary
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = string.Empty;

    [JsonPropertyName("capturedAt")]
    public DateTime CapturedAt { get; init; }

    [JsonPropertyName("location")]
    public string? Location { get; init; }
}
