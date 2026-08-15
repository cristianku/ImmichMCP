# ChatGPT instructions for displaying photos from the Immich plugin

Copy the following text into the instructions used by ChatGPT for the **Immich**
connector.

---

Whenever the user asks to search for and display a photo from the **Immich**
plugin, you must follow this procedure.

## 1. Find the photo

Use the most appropriate tool available in the Immich plugin:

- latest items: `immich_assets_list`;
- semantic search: `immich_search_smart`;
- metadata search: `immich_search_metadata`;
- photos of a person: use the available people tools, then retrieve the assets
  associated with that person.

Only consider items where `type === "IMAGE"`. Do not assume that the first
result is the most relevant or the most recent. Verify relevance, compare
`fileCreatedAt`, and use `localDateTime` when displaying the local time. Keep
the UUID of the selected photo.

For semantic searches, use concrete descriptions, preferably in English.
Inspect no more than three candidate previews unless the user explicitly asks
for a broader comparison, then select the final result.

## 2. Download the final preview

Always use the following tool to display a photo:

```text
immich_assets_download_thumbnail
```

Pass the final UUID:

```json
{
  "id": "PHOTO_UUID"
}
```

With `DOWNLOAD_MODE=base64`, the text result contains:

- `result.data`: the model-readable Base64 preview;
- `result.mime_type`: the preview's actual MIME type;
- `result.captured_at`, `result.location`, and any available GPS metadata;
- an additional MCP image block for compatibility.

Use `result.data` to create the final file. An image shown only inside the
technical tool result does not count as a photo displayed in the final answer.

## 3. Materialize the image

Decode `result.data` into a writable directory in the workspace. Choose the
file extension from `result.mime_type`, not from the original filename:

- `image/jpeg` → `.jpg`;
- `image/png` → `.png`;
- `image/webp` → `.webp`.

A JPEG preview of an HEIC original must therefore be saved as `.jpg`.

If the Base64 string exceeds the shell argument limit, split it into chunks of
60,000 characters. The chunk size must be divisible by 4. The first chunk must
create or overwrite the file, and subsequent chunks must be appended. Do not
hardcode a path from a previous session.

## 4. Verify the file

Before answering, verify that the file exists and is a valid image, for example:

```sh
file /ABSOLUTE/WORKSPACE/PATH/immich-preview.jpg
```

When available, also use `view_image` to verify visually that it is the correct
photo.

## 5. Actually display the photo

Embed the file in the final answer using an absolute `sandbox:` path:

```markdown
![Photo](sandbox:/ABSOLUTE/WORKSPACE/PATH/immich-preview.jpg)
```

This line must appear in the final answer, not only in tool output or a progress
update.

Immediately below the photo, add a concise caption using only the metadata
returned by the tool:

```text
📍 Densbüren, Aargau, Switzerland — 📅 15 August 2026, 17:57
```

Use `location` when available; otherwise compose the location from `city`,
`state`, and `country`. Use `captured_at` for the date and time. Omit any
unavailable part, and never infer the location by looking at the photo.

## 6. Multiple photos

For multiple photos, order the UUIDs from newest to oldest unless the user asks
otherwise. Call `immich_assets_download_thumbnail` for each final UUID, save
each image to a separate file, and put the corresponding caption immediately
below each photo.

## Essential rule

Do not say "here is the photo" and do not finish the answer until the final
message actually contains the embedded image. Filenames, UUIDs, internal URLs,
metadata, and previews shown only in technical tool output do not replace the
final image.

Do not call `immich_assets_show`. Do not use
`immich_assets_download_original` for previews.

## Error handling

- If the search does not return relevant results, try an alternative semantic
  query before concluding that the photo does not exist.
- If the tool returns only an internal `192.168.x.x` URL, do not use it as the
  only way to display the photo.
- If `result.data` is missing or is not valid Base64, report that the server did
  not return materializable image data.
- If several photos were taken during the same minute, compare the seconds in
  `fileCreatedAt` as well.
- For videos, use the appropriate tools and format instead of this photo
  procedure.
