using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomZoning
	{
		/// <summary>Whether the zoning gates are switched on. Off, every design is offered
		/// wherever the style and stage already allowed it, exactly as before this existed.</summary>
		public static bool Enabled => Options.GetOption("r_TAF_OptionZoning") != "No";

		/// <summary>
		/// Game-state key the keepers' roster USED to be stored under, kept only so
		/// <see cref="Stored"/> can fold an older save's roster into the city it belongs to and
		/// retire the key.
		/// <para>
		/// It was a flat entry on the game, and that was wrong in a way nobody chose: the store was
		/// game-wide rather than realm-wide, so a seceding city walked away with none of what its
		/// own keepers had learned, and an exiled founder founded their next realm already holding
		/// every design the old one had been taught. The exile modal says <i>"the charter is taken
		/// from you"</i>; the tech base walked out of the gate with them. Addendum 22 B1 ends it:
		/// the rolls sit on the city (<see cref="KingdomSettlement.KeepersRoster"/>), the leads sit
		/// with the founder (the journal), and the realm reads rather than holds.
		/// </para>
		/// </summary>
		public const string RosterState = "r_TAF_KeepersRoster";

		// Gates live beside the catalog rather than inside KingdomRules.BuildEntry so that the
		// registry parser needs two lines of wiring instead of a rewritten entry type. Keyed by
		// building Key, which is what the registry already overrides by (STANDARDS 6): a later
		// file re-using a key registers its own gate over the earlier one, including an entry
		// that declares no gates at all, which correctly un-gates the design.
		private static readonly Dictionary<string, ZoneGate> Gates = new Dictionary<string, ZoneGate>();

		/// <summary>
		/// Forgets every registered gate. Called by the registry loader before it re-reads the
		/// XML streams, so a reload never leaves a gate behind for an entry that no longer
		/// declares one.
		/// </summary>
		public static void ClearGates()
		{
			Gates.Clear();
			// The purpose cache is derived from these, so it cannot outlive them. This is also the
			// per-load invalidation: the catalogue is re-read on every AfterGameLoaded
			// (KingdomLoader), so a second game in the same session cannot inherit the first one's
			// answer about what its cities were about.
			KeptCacheZone = null;
			KeptCacheTick = -1L;
			KeptCacheValue = null;
			// The capital's two lanes are emptied in the same breath and for the same reason. The
			// outpost registry is keyed by building key and rebuilt by HandleBuilding in this one
			// pass, exactly as the gates are; the crown's cached answer is derived from the
			// catalogue's own keys, so it cannot outlive them either.
			KingdomSatellite.Reset();
			KingdomCrown.ClearCache();
		}

		/// <summary>
		/// Registers one entry's gate attributes as the registry parses it. Call once per
		/// <c>&lt;building&gt;</c> element that parsed successfully, with the raw attribute
		/// strings; all four may be null, which registers an open gate.
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Blank keys are ignored.</param>
		/// <param name="Districts">Raw <c>Districts</c> attribute.</param>
		/// <param name="MinZones">Raw <c>MinZones</c> attribute.</param>
		/// <param name="Knowledge">Raw <c>Knowledge</c> attribute.</param>
		/// <param name="MinTech">Raw <c>MinTech</c> attribute.</param>
		public static void RegisterGate(string Key, string Districts, string MinZones, string Knowledge, string MinTech)
		{
			RegisterGate(Key, Districts, MinZones, Knowledge, MinTech, null, null, null);
		}

		/// <summary>
		/// The same registration with Addendum 16's creed stack. Every one of the three is
		/// optional and an absent attribute gates nothing, exactly like the four before them.
		/// </summary>
		/// <param name="Builders">Raw <c>Builders</c> attribute.</param>
		/// <param name="Creed">Raw <c>Creed</c> attribute.</param>
		/// <param name="CreedShare">Raw <c>CreedShare</c> attribute.</param>
		public static void RegisterGate(string Key, string Districts, string MinZones, string Knowledge, string MinTech,
			string Builders, string Creed, string CreedShare)
		{
			RegisterGate(Key, Districts, MinZones, Knowledge, MinTech, Builders, Creed, CreedShare, null);
		}

		/// <summary>
		/// The same registration with Addendum 15's <c>Strata</c>. Optional like the seven before
		/// it: an entry that names no stratum stands in every one of them, which is what every
		/// entry in the catalogue did the day before this landed.
		/// </summary>
		/// <param name="Strata">Raw <c>Strata</c> attribute.</param>
		public static void RegisterGate(string Key, string Districts, string MinZones, string Knowledge, string MinTech,
			string Builders, string Creed, string CreedShare, string Strata)
		{
			RegisterGate(Key, Districts, MinZones, Knowledge, MinTech, Builders, Creed, CreedShare, Strata, null);
		}

		/// <summary>
		/// The same registration with Addendum 22 A1's <c>Megastructure</c>. Optional like the eight
		/// before it: a design that does not claim to be one of the great works is ordinary, and
		/// every design in the catalogue but one is.
		/// </summary>
		/// <param name="Megastructure">Raw <c>Megastructure</c> attribute.</param>
		public static void RegisterGate(string Key, string Districts, string MinZones, string Knowledge, string MinTech,
			string Builders, string Creed, string CreedShare, string Strata, string Megastructure)
		{
			RegisterGate(Key, Districts, MinZones, Knowledge, MinTech, Builders, Creed, CreedShare, Strata, Megastructure, null);
		}

		/// <summary>
		/// The same registration with the capital ruling's <c>Capital</c>. Optional like the nine
		/// before it: a design that does not claim the capital may be raised in any city, and every
		/// design in the catalogue could be the day before this landed.
		/// </summary>
		/// <param name="Capital">Raw <c>Capital</c> attribute.</param>
		public static void RegisterGate(string Key, string Districts, string MinZones, string Knowledge, string MinTech,
			string Builders, string Creed, string CreedShare, string Strata, string Megastructure, string Capital)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes(Key, Districts, MinZones, Knowledge, MinTech,
				Builders, Creed, CreedShare, Strata, Megastructure, Capital, out string error);
			if (error != null)
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + error);
			}
			Gates[Key] = gate;
		}

		/// <summary>The gate declared for a design key. An unregistered key is open, which is
		/// what any caller reaching a design the registry never saw should get.</summary>
		public static ZoneGate GateFor(string Key)
		{
			// The gates are filled by KingdomData's own pass, so asking for one before anything has
			// read the catalog would answer "open" for every design in the game.
			KingdomData.EnsureBuildings();
			if (!string.IsNullOrEmpty(Key) && Gates.TryGetValue(Key, out ZoneGate gate))
			{
				return gate;
			}
			return ZoneGate.Open;
		}

	}
}
