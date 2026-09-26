# Third-party notices

Obhijatri (অভিযাত্রী) ships the following third-party components.

## Fonts

### Hind Siliguri
- Files: `src/Obhijatri.App/Assets/Fonts/HindSiliguri-Regular.ttf`, `HindSiliguri-SemiBold.ttf`
- Copyright (c) 2015 Indian Type Foundry
- License: SIL Open Font License 1.1, full text in `src/Obhijatri.App/Assets/Fonts/HindSiliguri-OFL.txt`
- Source: https://github.com/google/fonts/tree/main/ofl/hindsiliguri (downloaded 2026-09-25, unmodified)
- Used as the app's user interface font.

### Noto Sans Bengali
- File: `src/Obhijatri.App/Assets/Fonts/NotoSansBengali-Variable.ttf` (upstream name `NotoSansBengali[wdth,wght].ttf`, renamed only)
- Copyright 2022 The Noto Project Authors
- License: SIL Open Font License 1.1, full text in `src/Obhijatri.App/Assets/Fonts/NotoSansBengali-OFL.txt`
- Source: https://github.com/google/fonts/tree/main/ofl/notosansbengali (downloaded 2026-09-25, font data unmodified)
- Used as a fallback for Bangla characters that Hind Siliguri does not cover.

The OFL allows these fonts to be bundled and redistributed with software, including commercial software, provided the license text is included and the fonts are not sold on their own.

## Avro Phonetic rules (Bangla phonetic typing)
- Files (MPL 2.0):
  - `src/Obhijatri.Bangla/Phonetic/AvroPhoneticRules.json`: the rule data from `avrolib.js`, extracted unchanged into JSON.
  - `src/Obhijatri.Bangla/Phonetic/AvroPhonetic.cs`: C# port of the conversion algorithm.
  - `src/Obhijatri.App/Web/avro-phonetic.js`: the JavaScript algorithm, adapted so the rules are passed in.
- Original: jsAvroPhonetic, initial developer Rifat Nabi. Copyright (C) OmicronLab (http://www.omicronlab.com).
- Source: https://github.com/sarim/ibus-avro (`avrolib.js`, commit dd521a139af0b4bb64eaeeca44fef03f5fb770cf, downloaded 2026-09-26).
- License: Mozilla Public License 2.0, full text in `src/Obhijatri.Bangla/Phonetic/MPL-2.0.txt`.
- MPL 2.0 obligation: the source code of these three files, including any changes we make, must be made available to everyone who receives Obhijatri (for example by shipping them with the installer or linking a public copy from the About page). The rest of Obhijatri is not affected by the MPL.
- The Bangla word list used for suggestions (`src/Obhijatri.Bangla/Phonetic/BanglaWords.txt`) was written for Obhijatri and is not part of Avro.

## Libraries (NuGet)

| Package | License |
| --- | --- |
| Microsoft.WindowsAppSDK | MIT |
| Microsoft.Web.WebView2 | BSD-style (Microsoft WebView2 SDK license) |
| Microsoft.Data.Sqlite | MIT |
| SQLitePCLRaw (dependency of Microsoft.Data.Sqlite) | Apache 2.0 |
| SQLite (bundled by SQLitePCLRaw) | Public domain |

Test-only packages (not shipped): xunit (Apache 2.0), xunit.runner.visualstudio (Apache 2.0), Microsoft.NET.Test.Sdk (MIT).

## Runtime

Microsoft Edge WebView2 Runtime is installed separately by Windows or the installer and is covered by Microsoft's own license terms.
