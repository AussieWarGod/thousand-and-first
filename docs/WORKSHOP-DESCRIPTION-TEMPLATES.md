# Workshop Listing Copy and Release Templates

Use this file to review listing copy before changing `manifest.json`,
`Tools/workshop_metadata.py`, or Steam. It is not the canonical live metadata source.

## v0.3.x Alpha uplift proposal

**State:** review-ready proposal; not yet copied into release metadata or published.

Changing the description or tags changes `workshop.json` and package bytes. Land an approved
version through a new `0.3.x` private-candidate and receipt flow; never edit the tagged `v0.3.0`
package or live page ad hoc.

**Recommended tags:**

`Building,Faction,Settlement,World,Script,Lore`

These are accurate Caves of Qud Workshop browse facets. Maturity stays visible in `[ALPHA]` and
the opening copy instead of consuming a discovery tag with `Alpha`. `Kingdom` and `Build` are not
current browse facets. Do not use `Stable` while the mod itself is explicitly Alpha; `Lore`
truthfully covers the Chronicle, named histories, and inherited realms without confusing maturity.

**Manifest description:**

> Found a faction through a water rite and raise up to three living settlements inside a normal
> Qud run. Govern physical water, food, labour, trade, raids, and succession; cross-world legacy
> is opt-in.

**Workshop BBCode:**

```text
[b]Found a faction. Raise settlements. Leave a history behind.[/b]

The Thousand and First turns settlement-building into part of a normal Caves of Qud adventure. Pour fresh water into a founder's basin—or choose Kingdom Quickstart—claim ground, raise a civic heart, and decide what kind of place grows there. Leave to explore. When you return, your settlements report what happened while you were away.

This is not a detached city-builder or a daily chore. Water sits in vessels, food in larders, materials in stores, and citizens live on the ground. You set intent; your people do the work.

[b]Build a living realm[/b]
- Found through a water rite or start quickly through the optional Kingdom Quickstart
- Establish a seat and up to two other cities across surface and underground claims
- Reserve typed plots in S, M, L, and XL sizes, then choose authored buildings that renovate, expand, or give way as the settlement grows
- Join districts with roads, shafts, utilities, porters, construction routes, and trade
- Build homes, workshops, farms, larders, markets, offices, shrines, fortifications, civic works, and late-game projects
- Manage physical fresh water, crops, meals, materials, power, wear, repair, and cargo
- Meet named settlers with origins, homes, work, creeds, conversations, petitions, rites, and funerals
- Answer raids by paying, fighting, fortifying, or talking; face diplomacy, dissent, rivals, exile, return, and succession
- Pursue research, certified machinery, laboratories, grafts, a becoming annexe, a crown, mirror-gates, and a hosted arcology
- Read a dated Chronicle and homecoming reports that remember what your realm became

[b]Shape each settlement[/b]
Site and circumstance matter. Ground, materials, technology, culture, creed, skills, staffing, and infrastructure affect which plans open and how the city looks and works. Choose plots and plans, set civic policy and water detail, commission upgrades, decide how to answer threats, and invest from hand-carried beginnings toward carts, conduits, and advanced works.

Affiliations and optional covenants are not cosmetic labels. They can change architecture, civic practices, relationships, and available projects without forcing every city in one realm to look alike.

Tune the experience with separate options for growth, water scarcity, raids, trade, research, civic stories, ambient activity, creed, roads, wear, and more.

Cross-world legacy is opt-in and must be enabled before world creation. It may carry bounded layout and history into a later world. It never carries items, liquids, charge, or old actor identity.

[b]Alpha, compatibility, and support[/b]
This is a public Alpha playtest. Expect bugs, rough edges, balance changes, and incomplete visual or compatibility coverage.

Built for Caves of Qud v1.0.5, core build 2.0.211.51. Later game builds are unverified. No dependency is required. Optional exact-version Hearthpyre 2.2.3 integration is included when Hearthpyre loads first; native compatibility remains unverified. Single-player only.

Back up saves before every Alpha install or update. Keep only one enabled copy of the mod; a local install plus a Workshop subscription can load the wrong one.

Bugs and playtest feedback:
[url=https://github.com/AussieWarGod/thousand-and-first/issues/new/choose]GitHub issue forms[/url]

Install, save, and test guidance:
[url=https://github.com/AussieWarGod/thousand-and-first/blob/main/PLAYTESTING.md]Alpha Playtesting Guide[/url]

Open source under MIT:
[url=https://github.com/AussieWarGod/thousand-and-first]Source and contributor docs[/url]
```

### Media sequence

Use real native captures from the release candidate. Each image should prove the adjacent claim:

1. Founder basin and first civic heart: the one-image explanation of the core loop.
2. One plot/building shown across growth, style, or creed variants: customization and progression.
3. Citizens, crops/larder, porter, road, or trade route in one readable scene: the living economy.
4. Chronicle/homecoming report beside a raid or civic choice: consequence and memory.
5. Hosted arcology or another late-game work: aspiration and payoff.

Keep captions outcome-led. Do not use debug overlays, synthetic mock-ups, unapproved art, or a
shot whose native state does not support its caption. Additional Workshop media remains an
attended Steam-page step. The proposed first uploader owns only the primary preview; gallery
automation would need a separate frozen API, ordering, query-verification, and receipt design.

### Pattern research

The proposed order is hook → player fantasy → grouped outcomes → customization → compatibility
and safety → support links. It borrows proven presentation patterns without copying prose:

- [Hearthpyre](https://steamcommunity.com/sharedfiles/filedetails/?id=1683847053),
  [QudUX](https://steamcommunity.com/sharedfiles/filedetails/?id=1804499742), and
  [Clever Girl](https://steamcommunity.com/sharedfiles/filedetails/?id=2921686606) demonstrate
  concise hooks, named feature groups, configuration, and practical support in Qud.
- [Gigastructural Engineering](https://steamcommunity.com/sharedfiles/filedetails/?id=1121692237)
  makes a large feature set scannable, then separates customization, compatibility, and docs.
- [Common Sense](https://steamcommunity.com/sharedfiles/filedetails/?id=2875848298) leads with the
  problem solved and keeps the first screen readable despite deeper mechanics.
- [Vanilla Factions Expanded — Empire](https://steamcommunity.com/sharedfiles/filedetails/?id=2938820380)
  connects a missing fantasy to role-play outcomes before listing systems and requirements.
- [The Qud Survival Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2128235668)
  shows how a short story hook and one clear purpose can make a large mod approachable.

Avoid volatile subscriber counts and giant undifferentiated catalogues. Update claims only from
current `README.md`, `VISION.md`, `docs/STATUS.md`, and release evidence.

## Future Beta listing draft

**Title:** The Thousand and First [BETA]

Start from the approved Alpha structure. Replace Alpha-specific copy with the exact Beta feature
delta, supported build, evidence status, known limits, and compatibility matrix. Beta remains a
separate Workshop item.

## Future Release listing draft

**Title:** The Thousand and First

Use only after final native, visual, accessibility, compatibility, and subscription evidence is
complete. Release remains a separate Workshop item.

## Listing maintenance checklist

- Treat `manifest.json` plus `Tools/workshop_metadata.py` as canonical at package boundary.
- Keep title, short pitch, full description, tags, preview, and `workshop.json` synchronized.
- Preserve Alpha/Beta/Release separation.
- Lead with player outcomes; keep warnings and compatibility easy to scan.
- Never claim unperformed evidence or ship media without provenance and native review.
- Run the complete private → subscribed receipt → public verification flow for every changed byte.
