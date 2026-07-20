# Music & audio licensing

**Rule for this folder: only CC0 / public-domain audio, or something you hold a licence for.**
Music rights-holders issue takedowns quickly, and on Google Play that can put the whole
developer account at risk — not just the app. Never use a track from a commercial game
(the Pet Society soundtrack, for instance, belongs to Playfish/EA).

Style is *not* copyrightable; a specific recording very much is. "Make something that feels
like X" is fine. "Use X" is not.

## Currently shipping

| File | Track | Author | Licence | Source |
| --- | --- | --- | --- | --- |
| `Assets/Resources/Music/theme_a_new_town.mp3` | A New Town (RPG Theme) | cynicmusic (The Cynic Project) | **CC0** — public domain, no attribution required | [OpenGameArt](https://opengameart.org/content/a-new-town-rpg-theme) |

The author asks — but under CC0 cannot require — that you credit
*The Cynic Project / pixelsphere.org / cynicmusic.com*. It costs nothing to honour that in
the Settings credits, so it's worth doing regardless of the legal position.

**This track was chosen blind.** It is licence-verified, not taste-verified — nobody has
listened to it before shipping it. Audition it and swap if it isn't right.

## Swapping the music

Put any audio file in `Assets/Resources/Music/` (any filename, any format Unity imports).
`Sfx.SetMusic` loads the first clip it finds there; delete the folder's contents and the
game falls back to the built-in procedural waltz. No code change needed either way.

Keep loops short-ish — the whole file is decompressed into memory on load. Under ~3 MB is
comfortable. In the importer, set **Load Type: Streaming** for anything larger.

## Where to find more (all usable commercially, no attribution required)

- **[OpenGameArt — CC0 Calm/Relaxing collection](https://opengameart.org/content/cc0-calm-relaxing-music)**
  — game-ready and filterable by licence. ⚠️ It's a *user-curated* collection: check the
  licence on each individual track's page, not just the collection's.
- **[Pixabay Music](https://pixabay.com/music/)** — Pixabay Licence, free commercially, no
  attribution. Large "calm"/"lo-fi" selection.
- **[Chosic — no-attribution filter](https://www.chosic.com/free-music/all/?attribution=no)**
  — aggregator; confirm the licence shown per track.
- **[Free Music Archive — public domain](https://freemusicarchive.org/)** — filter to CC0/PD;
  licences are mixed, so check each one.
- **[itch.io CC0 soundtracks](https://itch.io/soundtracks/assets-cc0)**

Whatever you pick, **record it in the table above** — if you ever need to prove provenance
for a Play appeal, that table is the evidence.
