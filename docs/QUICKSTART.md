# Kingdom Quickstart

Kingdom Quickstart is an optional new-game mode for testing or learning The Thousand and First. It does not alter Classic or Kingdom starts.

## Start a world

1. Before creating the world, set **The Thousand and First: place a benefit-free camp guide...** to Yes or No. The choice is read during world creation; changing it later does not add or remove the guide.
2. Choose **New Game**, then **Kingdom Quickstart**.
3. Build a character normally.
4. Choose one reviewed holding camp:
   - **Reedwake** — salt marsh.
   - **Riftside** — desert canyon.
   - **Saltwake** — salt dunes.
5. Enter the world. After placement, the ordinary founding transaction creates the first heart and city identity.

Each successful camp physically contains 24 drams of fresh water in dedicated casks, 12 style-appropriate meals in a larder, and a chest containing 1 mud, 3 brush, and 4 timber. These are finite objects and items. They grant no hidden production and replenish only through ordinary settlement work.

The optional named camp guide explains this opening inventory. The guide is passive and immobile, carries no stock, awards no experience, provides no labour, staffing, support, or defence, and is not a citizen.

## Safety and compatibility

- The selected parasang is reserved before dynamic villages, lairs, or encounters claim it. Only the heart apron and supply approach are prepared; the rest of the wilderness remains intact. Nearby danger is still possible.
- Creatures, loose items, and liquid-bearing objects on required cells are relocated when safe. Stairs or an unsafe preparation result stop the bootstrap.
- Kingdom Quickstart never offers legacy realm inheritance in the same world. Use another supported mode to test inheritance.
- The bootstrap stores a checksummed, phase-by-phase receipt containing the exact physical object identities. Each cask, larder, chest, and included guide is completely prepared off-map, receives a profile/ground/role-bound reservation mark, and then enters the zone in one visible placement. A save or callback cut can therefore leave only no object or one exact, fully prepared object; load, zone-activation, and bounded end-turn wakes adopt that object before advancing the receipt and never place a second one.
- Once a grant phase is receipted, later recovery proves its object identity, dedicated role, position, and non-producing shape. It does not demand the opening water, meal, or material quantities again: using those finite provisions is normal play, not corruption and not authority to replenish them.
- A malformed receipt, mismatched profile or zone, unavailable founding authority, unsafe site, or failed physical measurement stops further grants. It does not synthesize replacement resources.
- Do not treat changing the advisor option after world creation as a retroactive toggle. Quickstart
  registers one serializable player-system wake for load, zone activation, and bounded end-turn
  recovery. Its only mutable member is explicitly non-serialized; the checksummed game-state
  receipt above remains the sole durable authority. Quickstart adds no custom player part.

This alpha flow does not promise a combat-free start, staffed production, citizens, custom Quickstart art, or compatibility with saves created before the mode existed.
