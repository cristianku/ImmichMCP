using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using ImmichMCP.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ImmichMCP.Tools;

[McpServerToolType]
public static class GalleryTools
{
    public const string ResourceUri = "ui://immich/gallery-v2.html";
    private const int MaxAssets = 8;

    [McpServerTool(
        Name = "immich_gallery_show",
        Title = "Show Immich photo gallery",
        ReadOnly = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(GalleryOutput))]
    [McpMeta("ui", JsonValue = """{"resourceUri":"ui://immich/gallery-v2.html"}""")]
    [McpMeta("openai/outputTemplate", ResourceUri)]
    [McpMeta("openai/toolInvocation/invoking", "Preparing the photo gallery…")]
    [McpMeta("openai/toolInvocation/invoked", "Photo gallery ready")]
    [Description("Render the selected Immich assets as a visible inline photo gallery. After searching and visually checking matching photos, ALWAYS call this tool when the user asks to show, display, see, or find photos. Pass only the final matching asset IDs, ordered newest first. Do not substitute a filename-only list for this tool.")]
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

        var images = new List<GalleryWidgetImage>(ids.Length);
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

                images.Add(new GalleryWidgetImage
                {
                    Id = id,
                    FileName = asset.OriginalFileName,
                    CapturedAt = asset.ExifInfo?.DateTimeOriginal ?? asset.LocalDateTime,
                    Location = FormatLocation(asset.ExifInfo?.City, asset.ExifInfo?.State, asset.ExifInfo?.Country),
                    DataUri = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}"
                });
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
            Images = images.Select(image => new GalleryImageSummary
            {
                Id = image.Id,
                FileName = image.FileName,
                CapturedAt = image.CapturedAt,
                Location = image.Location
            }).ToList(),
            MissingAssetIds = missing
        };

        return new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToElement(output),
            Meta = new JsonObject
            {
                ["gallery"] = JsonSerializer.SerializeToNode(new GalleryWidgetOutput
                {
                    Title = output.Title,
                    Images = images,
                    MissingAssetIds = missing
                })
            },
            Content =
            [
                new TextContentBlock
                {
                    Text = $"Displayed {images.Count} Immich photo{(images.Count == 1 ? string.Empty : "s")} in the gallery."
                }
            ]
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

/// <summary>
/// Data delivered only to the MCP Apps widget through the tool result metadata.
/// Keeping preview bytes out of <c>structuredContent</c> prevents them from being
/// added to the model context while still allowing the iframe to render the photos.
/// </summary>
public sealed class GalleryWidgetImage : GalleryImageSummary
{
    [JsonPropertyName("dataUri")]
    public string DataUri { get; init; } = string.Empty;
}

public sealed class GalleryWidgetOutput
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "Immich photos";

    [JsonPropertyName("images")]
    public List<GalleryWidgetImage> Images { get; init; } = [];

    [JsonPropertyName("missingAssetIds")]
    public List<string> MissingAssetIds { get; init; } = [];
}
