using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ImmichMCP.Resources;

[McpServerResourceType]
public static class GalleryResource
{
    [McpServerResource(
        UriTemplate = Tools.GalleryTools.ResourceUri,
        Name = "Immich photo gallery",
        Title = "Immich photo gallery",
        MimeType = "text/html;profile=mcp-app")]
    [Description("Interactive inline gallery for selected Immich photos")]
    public static ResourceContents GetGallery() => new TextResourceContents
    {
        Uri = Tools.GalleryTools.ResourceUri,
        MimeType = "text/html;profile=mcp-app",
        Text = GalleryHtml,
        Meta = new JsonObject
        {
            ["ui"] = new JsonObject
            {
                ["prefersBorder"] = true
            }
        }
    };

    private const string GalleryHtml = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <style>
            :root { color-scheme: light dark; font-family: ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
            * { box-sizing: border-box; }
            body { margin: 0; padding: 12px; color: CanvasText; background: transparent; }
            header { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; margin: 0 0 10px; }
            h1 { margin: 0; font-size: 18px; line-height: 1.3; }
            #count { color: GrayText; font-size: 13px; white-space: nowrap; }
            #status { color: GrayText; padding: 16px 2px; }
            #gallery { display: grid; grid-template-columns: repeat(auto-fill, minmax(155px, 1fr)); gap: 10px; }
            figure { margin: 0; min-width: 0; overflow: hidden; border: 1px solid color-mix(in srgb, CanvasText 15%, transparent); border-radius: 12px; background: color-mix(in srgb, Canvas 94%, CanvasText 6%); }
            button { display: block; width: 100%; padding: 0; border: 0; background: transparent; cursor: zoom-in; }
            img { display: block; width: 100%; aspect-ratio: 1 / 1; object-fit: cover; background: color-mix(in srgb, Canvas 90%, CanvasText 10%); }
            figcaption { padding: 8px 9px 9px; min-width: 0; }
            .name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 12px; font-weight: 600; }
            .meta { margin-top: 3px; color: GrayText; font-size: 11px; line-height: 1.3; }
            dialog { width: min(94vw, 1100px); max-width: 1100px; border: 0; border-radius: 14px; padding: 0; background: Canvas; color: CanvasText; box-shadow: 0 20px 70px rgb(0 0 0 / .45); }
            dialog::backdrop { background: rgb(0 0 0 / .72); }
            dialog img { width: 100%; max-height: 78vh; aspect-ratio: auto; object-fit: contain; border-radius: 14px 14px 0 0; }
            .dialog-meta { display: flex; justify-content: space-between; gap: 12px; padding: 10px 12px; font-size: 13px; }
            .close { width: auto; padding: 3px 8px; color: inherit; cursor: pointer; font: inherit; }
            @media (max-width: 420px) { body { padding: 8px; } #gallery { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 7px; } figcaption { padding: 7px; } }
          </style>
        </head>
        <body>
          <header><h1 id="title">Immich photos</h1><span id="count"></span></header>
          <div id="status">Loading photos…</div>
          <main id="gallery" aria-live="polite"></main>
          <dialog id="viewer">
            <img id="viewer-image" alt="">
            <div class="dialog-meta"><span id="viewer-label"></span><button class="close" type="button">Close</button></div>
          </dialog>
          <script>
            const title = document.getElementById("title");
            const count = document.getElementById("count");
            const status = document.getElementById("status");
            const gallery = document.getElementById("gallery");
            const viewer = document.getElementById("viewer");
            const viewerImage = document.getElementById("viewer-image");
            const viewerLabel = document.getElementById("viewer-label");

            function formatDate(value) {
              if (!value) return "";
              const date = new Date(value);
              return Number.isNaN(date.getTime()) ? "" : new Intl.DateTimeFormat(document.documentElement.lang || undefined, { dateStyle: "medium", timeStyle: "short" }).format(date);
            }

            function render(output) {
              const images = Array.isArray(output?.images) ? output.images : [];
              title.textContent = typeof output?.title === "string" ? output.title : "Immich photos";
              count.textContent = images.length === 1 ? "1 photo" : `${images.length} photos`;
              gallery.replaceChildren();
              status.hidden = images.length > 0;
              status.textContent = images.length > 0 ? "" : "No photos to display.";

              for (const image of images) {
                if (!image || typeof image.dataUri !== "string" || !image.dataUri.startsWith("data:image/")) continue;
                const figure = document.createElement("figure");
                const open = document.createElement("button");
                open.type = "button";
                const img = document.createElement("img");
                img.src = image.dataUri;
                img.alt = typeof image.fileName === "string" ? image.fileName : "Immich photo";
                img.loading = "eager";
                open.append(img);
                open.addEventListener("click", () => {
                  viewerImage.src = image.dataUri;
                  viewerImage.alt = img.alt;
                  viewerLabel.textContent = [image.fileName, formatDate(image.capturedAt), image.location].filter(Boolean).join(" · ");
                  viewer.showModal();
                });

                const caption = document.createElement("figcaption");
                const name = document.createElement("div");
                name.className = "name";
                name.textContent = typeof image.fileName === "string" ? image.fileName : "Photo";
                const meta = document.createElement("div");
                meta.className = "meta";
                meta.textContent = [formatDate(image.capturedAt), image.location].filter(Boolean).join(" · ");
                caption.append(name, meta);
                figure.append(open, caption);
                gallery.append(figure);
              }
            }

            function outputFrom(params) {
              const result = params?.toolResult ?? params?.result ?? params;
              return result?._meta?.gallery ?? result?.meta?.gallery ?? result?.structuredContent ?? null;
            }

            window.addEventListener("message", (event) => {
              if (event.source !== window.parent) return;
              const message = event.data;
              if (!message || message.jsonrpc !== "2.0") return;
              if (message.method === "ui/initialize" || message.method === "ui/notifications/tool-result") {
                render(outputFrom(message.params));
              }
            }, { passive: true });

            viewer.querySelector(".close").addEventListener("click", () => viewer.close());
            viewer.addEventListener("click", (event) => { if (event.target === viewer) viewer.close(); });

            if (window.openai?.toolOutput) render(outputFrom(window.openai.toolOutput));
          </script>
        </body>
        </html>
        """;
}
