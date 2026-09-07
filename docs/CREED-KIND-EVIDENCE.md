# Creed semantic-kind evidence

Frozen against Caves of Qud 1.0.5/core 2.0.211.51. Vanilla has no faction ontology field:
`Faction.cs:28-82` exposes grammar, visibility, antiquity, parent, reputation, worship, and
interest data; `Factions.cs:518-846` loads those facts but no semantic kind. `Parent`, `Plural`,
and `FormatWithArticle` therefore MUST NOT infer kind. TAF curates mergeable metadata in
`RuntimeData/KingdomCreeds.xml`.

| Kind | Exact shipped keys | Installed-source basis |
|---|---|---|
| `community` | Chavvah, Ezra, Joppa, Kyakukya | Settlement/tree-city identities; `Base/Books.xml:464`; faction display names in `Base/Factions.xml` |
| `people` | Baetyls, Cragmensch, Dromad, Entropic, Girsh, Goatfolk, Gyre Wights, Hindren, Issachari, Mopango, Naphtaali, Robots, Snapjaws, Strangers, Svardym, Trolls | Beings, species, cultures, or tribes. Gyre Wights are an exact faction of humanoid bodies who worship Girsh, not a freestanding doctrine: `Base/Factions.xml:1328-1351`; `Base/ObjectBlueprints/Creatures.xml:891-900`; `Base/Conversations.xml:524-542`. Hindren: `Base/ObjectBlueprints/Creatures.xml:3584,3587`; Mopango: `Base/ObjectBlueprints/Creatures.xml:950-951`. Strangers is intentionally conservative and lowest-confidence. |
| `polity` | Water, YdFreehold | Water barons; freeholds are city-states and Yd has democratic governance: `Base/Books.xml:768,1391`. |
| `order` | Barathrumites, Consortium, Daughters, Farmers, Merchants, Templar, Wardens | Named institutional fellowships, guilds, companies, or orders; membership alone does not prove theology. |
| `doctrine` | Mechanimists, Seekers | Mechanimist belief/faith and joining: `Base/Conversations.xml:4303-4306,12077-12080`; Sightless Way/aggregate-mind joining: `Base/Books.xml:564-572`. Q Girl's attributed analysis of Gyre Wight ethos (`Base/Books.xml:452-469`) does not turn that people key into a separate doctrine. |
| `cult` | Mamon, Resheph | Children of Mamon plus blood rite: `Base/Factions.xml:1354-1380`; literal Cult of the Coiled Lamb: `Base/Factions.xml:1688-1719`. |

Counts are exact: `4 + 16 + 2 + 7 + 2 + 2 = 33`. Theology is allowed only for doctrine,
cult, or an order with explicit `Theology="yes"`. Thus shipped theological outputs are exactly
Mechanimists, Seekers, Mamon, and Resheph. Gyre Wights, Baetyls, Girsh, Naphtaali, robots, and
other peoples can own architecture and allegiance but cannot become shrine-conversion outputs.

Compatibility boundary: save/body property `KingdomCreed`, public `Creed` names, catalogue
`Creed` gates, and old values stay unchanged. Kind resolves from current merged data and is never
written into save state. An unknown third-party key remains a neutral affiliation: it may be
arrived with or explicitly adopted, but receives no passive conversion, belief prose, shrine
consecration, or resented-theology pressure until its mod ships a valid kind declaration.
