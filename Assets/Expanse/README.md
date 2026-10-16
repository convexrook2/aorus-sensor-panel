# The Expanse local media pack

The main theme, **The Expanse**, cycles through eight ships and shows associated personnel
alongside its built-in space view. Optional portraits and backgrounds are
loaded locally. The app does not download media, and local packs are not part
of the repository or installer.

Choose **Settings → Theme → The Expanse**. Under **Expanse showcase**, enable
**Auto cycle** and choose an interval from 15–120 seconds; the default is 45.
A manual ship selection holds that ship until Auto cycle is enabled again.
**Holographic display** remains a separate set of map and rotation controls.

## Create a pack

Place `scenes.json` and your PNG/JPEG files in:

```text
%LOCALAPPDATA%\X890Sensor\ExpanseMedia
```

`scenes.json` is a JSON array. Each entry customizes one `ShipId`. Image paths
are relative to this folder and must remain inside it; web URLs and absolute
paths are not accepted. Supported extensions are `.png`, `.jpg` and `.jpeg`.

```json
[
  {
    "ShipId": "Rocinante",
    "Character": "James Holden",
    "Actor": "Steven Strait",
    "Association": "CAPTAIN / ROCINANTE",
    "Scene": "Rocinante in deep space",
    "Background": "backgrounds/rocinante.jpg",
    "Portrait": "portraits/holden.png",
    "Credit": "Image creator and permission or licence details",
    "Source": "https://example.com/original-source"
  }
]
```

| Field | Meaning |
| --- | --- |
| `ShipId` | Exact ship identifier from the list below. |
| `Character` | Verified character associated with this ship; empty for unidentified crew. |
| `Actor` | Performer of the named character. |
| `Association` | Relationship to this ship, such as captain, test pilot, or original racing pilot. |
| `Scene` | Short scene caption. |
| `Background` | Optional relative path to the full space background. |
| `Portrait` | Optional relative path to the cast portrait. |
| `Credit` | Creator, attribution and permission/licence details. |
| `Source` | Original source reference; this is recorded text, not a download URL. |

Valid identifiers: `Rocinante`, `Donnager`, `Truman`, `Tynan`, `Razorback`,
`Morrigan`, `Dropship`, `Epstein`. Omit entries to retain built-in content.
Missing or unreadable images retain the built-in view. Keep `scenes.json`
at most 128 KiB and each image at most 24 MiB.

Personnel cards follow the selected ship. Built-in associations include Solomon
Epstein as his yacht's test pilot, Theresa Yao as Donnager's captain, and Michael
Souther specifically with the Agatha King, a Truman-class ship. Generic ship
types without a verified named crew show their class identity instead of a face.
Keep custom portraits, relationship labels and backdrops accurate to the ship
they accompany. Changes to the manifest load at the next scene selection; the
Reload local scene media command also refreshes the current scene immediately.

## ArtCraft and other image editors

ArtCraft PNG/JPEG exports can be placed in the same folder and referenced by
the manifest. Any editor that exports these formats works; the panel does not
launch or require ArtCraft. No ArtCraft-generated media is bundled here.

Use images you are permitted to use. A wiki page, credit or source link alone
does not grant redistribution rights. Keep private show stills and portraits
in this local folder rather than adding them to commits or release packages.
