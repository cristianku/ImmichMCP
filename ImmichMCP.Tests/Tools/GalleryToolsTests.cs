using System.Net;
using System.Text.Json.Nodes;
using FluentAssertions;
using ImmichMCP.Tests.Fixtures;
using ImmichMCP.Tools;
using ModelContextProtocol.Protocol;
using RichardSzalay.MockHttp;

namespace ImmichMCP.Tests.Tools;

public class GalleryToolsTests
{
    private const string AssetId = "asset-1";

    [Fact]
    public async Task ShowGallery_ReturnsLightweightModelDataAndPrivateWidgetPreview()
    {
        // Arrange
        var (client, handler) = MockHttpClientFactory.CreateMockClient();
        var asset = TestFixtures.CreateAsset(id: AssetId, originalFileName: "beach.jpg");
        handler.When(HttpMethod.Get, $"*/assets/{AssetId}")
            .Respond("application/json", TestFixtures.ToJson(asset));

        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03 };
        handler.When(HttpMethod.Get, $"*/assets/{AssetId}/thumbnail")
            .WithQueryString("size", "preview")
            .Respond("image/jpeg", new MemoryStream(imageBytes));

        // Act
        var result = await GalleryTools.ShowGallery(client, [AssetId], "Foto al mare");

        // Assert
        result.IsError.Should().NotBeTrue();
        result.StructuredContent.HasValue.Should().BeTrue();
        var structured = result.StructuredContent!.Value;
        structured.GetProperty("title").GetString().Should().Be("Foto al mare");
        structured.GetProperty("images")[0].GetProperty("fileName").GetString().Should().Be("beach.jpg");
        structured.GetRawText().Should().NotContain("data:image/");

        result.Meta.Should().NotBeNull();
        var widget = result.Meta!["gallery"].Should().BeOfType<JsonObject>().Subject;
        var image = widget["images"].Should().BeOfType<JsonArray>().Subject[0]
            .Should().BeOfType<JsonObject>().Subject;
        image["dataUri"].Should().BeOfType<JsonValue>().Which.GetValue<string>()
            .Should().Be($"data:image/jpeg;base64,{Convert.ToBase64String(imageBytes)}");
    }

    [Fact]
    public async Task ShowGallery_ReturnsAnErrorWhenNoAssetCanBeDisplayed()
    {
        // Arrange
        var (client, handler) = MockHttpClientFactory.CreateMockClient();
        handler.When(HttpMethod.Get, "*/assets/missing")
            .Respond(HttpStatusCode.NotFound);

        // Act
        var result = await GalleryTools.ShowGallery(client, ["missing"]);

        // Assert
        result.IsError.Should().BeTrue();
        result.Content.OfType<TextContentBlock>().Single().Text.Should().Contain("None of the requested Immich assets");
    }
}
