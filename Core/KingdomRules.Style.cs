namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		/// <summary>
		/// The five built-in city styles and the compatibility terrain resolver's answer set.
		/// The live open registry is <c>KingdomData.Styles</c>; founding uses its data-driven
		/// selector before this compatibility surface, so third-party styles are not closed out.
		/// </summary>
		public static readonly string[] Styles = new string[5] { "common", "verdant", "fungal", "moonstair", "eater" };

		/// <summary>Whether a string names one of the five built-in compatibility styles. Engine
		/// callers that need the open registry use <c>KingdomData.TryGetStyle</c>.</summary>
		public static bool IsKnownStyle(string Style)
		{
			for (int i = 0; i < Styles.Length; i++)
			{
				if (Styles[i] == Style)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// The surface stratum. This is the engine's own number, not ours:
		/// <c>XRL.World.Zone.GetTerrainDisplayName</c> answers "the deep underground" for any
		/// <c>Z &gt; 10</c>, so 10 is the surface and larger is deeper.
		/// </summary>
		public const int SurfaceZLevel = 10;

		// Ground-to-style matching, against Caves of Qud 2.0.211.51,
		// StreamingAssets/Base/ObjectBlueprints/WorldTerrain.xml. A zone reports its ground two
		// ways: GetTerrainObject().Blueprint gives the exact blueprint ("TerrainSaltmarsh2",
		// "TerrainFungalOuterGw", "TerrainBaroqueRuins"), and GetTerrainRegion() gives that
		// blueprint's Terrain tag ("Saltmarsh", "Fungal", "Ruins", "Jungle"). Matching is by
		// substring because the game splits one region across dozens of variants, and the tag is
		// the variants' shared stem. A game update that renames these loses the match silently and
		// every site falls back to "common", which is the designed failure rather than a defect.
		private static readonly string[] FungalGround = new string[1] { "Fungal" };

		// TerrainRuins, TerrainBaroqueRuins, TerrainJoppaRuins by blueprint; TerrainGritGate and
		// TerrainRustWell carry Terrain="Ruins". "TheSpindle" and not "Spindle", or
		// TerrainMountainsSpindleShadow - a mountain that merely stands in the Spindle's shadow -
		// would read as the ancients' own chrome.
		private static readonly string[] EaterGround = new string[4] { "Ruins", "BethesdaSusa", "GritGate", "TheSpindle" };

		// The Moon Stair is its own crystal-and-warm-static biome. Gyre Wights and Girsh may live
		// there, but faction presence is creed evidence and never terrain identity.
		private static readonly string[] MoonStairGround = new string[2] { "Brightsheol", "MoonStair" };

		// TerrainWatervine and TerrainJoppaRuins both carry Terrain="Saltmarsh"; "Jungle" catches
		// TerrainJungle, TerrainDeepJungle, and Kyakukya, which carries Terrain="Jungle".
		private static readonly string[] VerdantGround = new string[5] { "Watervine", "Saltmarsh", "Flowerfields", "BananaGrove", "Jungle" };

		/// <summary>
		/// Resolves the ground a settlement stands on to a city style. The blueprint is read
		/// first and the region only if the blueprint says nothing, so a ruin in the marshes
		/// founds an "eater" city rather than a "verdant" one.
		/// <para>
		/// Below the surface only the two styles whose material is actually down there survive:
		/// spore-lit caverns and the ancients' works. Nobody thatches a roof with watervine in a
		/// cave, so a deep site of any other ground falls back to "common".
		/// </para>
		/// <para>
		/// The fallback is total. Unmapped ground, a renamed blueprint, null, and empty all answer
		/// "common", which is the one style every base building design allows.
		/// </para>
		/// </summary>
		/// <param name="TerrainBlueprint">The zone's terrain blueprint
		/// (<c>Zone.GetTerrainObject()?.Blueprint</c>), or null if it could not be read.</param>
		/// <param name="RegionName">The zone's terrain region (<c>Zone.GetTerrainRegion()</c>),
		/// or null if it could not be read.</param>
		/// <param name="ZLevel">The zone's stratum; see <see cref="SurfaceZLevel"/>.</param>
		/// <returns>A member of <see cref="Styles"/>. Never null.</returns>
		public static string StyleForSite(string TerrainBlueprint, string RegionName, int ZLevel)
		{
			string style = StyleForGround(TerrainBlueprint);
			if (style == null)
			{
				style = StyleForGround(RegionName);
			}
			if (style == null)
			{
				return "common";
			}
			if (ZLevel > SurfaceZLevel && style != "fungal" && style != "eater")
			{
				return "common";
			}
			return style;
		}

		/// <summary>
		/// First match wins, in the declared order. No two shipped grounds match two lists, but
		/// third-party terrain can: a "FungalRuins" is fungal, and that precedence is the contract.
		/// </summary>
		/// <returns>Null where the ground is unmapped, so the caller can try the other reading.</returns>
		private static string StyleForGround(string Ground)
		{
			if (string.IsNullOrEmpty(Ground))
			{
				return null;
			}
			if (ContainsAny(Ground, FungalGround))
			{
				return "fungal";
			}
			if (ContainsAny(Ground, EaterGround))
			{
				return "eater";
			}
			if (ContainsAny(Ground, MoonStairGround))
			{
				return "moonstair";
			}
			if (ContainsAny(Ground, VerdantGround))
			{
				return "verdant";
			}
			return null;
		}

		private static bool ContainsAny(string Text, string[] Needles)
		{
			for (int i = 0; i < Needles.Length; i++)
			{
				if (Text.IndexOf(Needles[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Overland terrain blueprints that mark a world tile as an ordinary, unclaimed ruin
		/// (StreamingAssets/Base/ObjectBlueprints/WorldTerrain.xml: <c>TerrainRuins</c> and
		/// <c>TerrainBaroqueRuins</c>, the two objects the vanilla <c>Ruins</c> zone builder is
		/// wired to in <c>Worlds.xml</c>). Exact match, not the substring test
		/// <see cref="StyleForGround"/> uses: that match is deliberately loose because it is only
		/// choosing a building theme, and it is why Grit Gate, Golgotha, and Bethesda Susa all
		/// read as "eater" style even though none of them is a ruin to restore. This one gates an
		/// actual founding path, so a site is a ruin only if the ground the engine reports is
		/// exactly one of these two &mdash; <c>TerrainJoppaRuins</c>, despite its name, is a salt
		/// marsh (<c>Terrain="Saltmarsh"</c>) and never matches.
		/// </summary>
		public static readonly string[] RuinTerrainBlueprints = new string[2] { "TerrainRuins", "TerrainBaroqueRuins" };

		/// <summary>
		/// Whether the founding site is ground the world already built, rather than merely empty:
		/// a founding rite poured here restores instead of raising from nothing. See
		/// <see cref="RuinTerrainBlueprints"/> for exactly what counts and why.
		/// </summary>
		/// <param name="TerrainBlueprint">The founding zone's terrain blueprint
		/// (<c>Zone.GetTerrainObject()?.Blueprint</c>), or null if it could not be read.</param>
		/// <returns>False for null, empty, or any ground not in <see cref="RuinTerrainBlueprints"/>
		/// &mdash; an unresolvable site degrades to an ordinary founding, never a ruin one.</returns>
		public static bool IsRuinSite(string TerrainBlueprint)
		{
			if (string.IsNullOrEmpty(TerrainBlueprint))
			{
				return false;
			}
			for (int i = 0; i < RuinTerrainBlueprints.Length; i++)
			{
				if (RuinTerrainBlueprints[i] == TerrainBlueprint)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Founder-facing clause naming how many already-standing structures a ruin founding
		/// credited to the settlement. Composes cleanly onto the end of the founding chronicle
		/// line whether or not the ruin happened to have anything worth keeping standing &mdash;
		/// most ruin decoration is rubble and puddles, not furniture, and that is not a failure of
		/// the rite.
		/// </summary>
		/// <param name="StructuresRestored">Count from the restoration pass. Zero or negative
		/// (defensive; a caller error, never produced by the pass itself) yields the empty
		/// clause.</param>
		/// <returns>A trailing clause starting with ", " or "" for nothing to report.</returns>
		public static string RuinRestorationClause(int StructuresRestored)
		{
			if (StructuresRestored <= 0)
			{
				return "";
			}
			if (StructuresRestored == 1)
			{
				return ", and one of its standing works is the settlement's now";
			}
			return ", and " + StructuresRestored + " of its standing works are the settlement's now";
		}

	}
}
