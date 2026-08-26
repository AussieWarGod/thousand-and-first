using System;
using System.Collections.Generic;

using ThousandAndFirst;
namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	/// <summary>
	/// The engine-coupled half of the crown: which of the founder's cities is the capital, the act
	/// that moves it, and the re-keying of the realm's arches that follows.
	/// <para>
	/// <b>The capital is DERIVED, and the record is only a tie-break.</b> Which cities keep a
	/// standing crown hall is read off the city books &mdash; the same two sources
	/// <c>KingdomZoning.KeptMegastructure</c> reads and in the same order of authority: the books
	/// are the record because they cover zones nobody has stood in for a season, and the loaded zone
	/// is the freshness patch because a hall finished since the last settlement pass is standing in
	/// the world and not yet written down. What the record in game state adds is the one thing the
	/// world cannot say: which of two standing halls the crown is IN. When the record names a city
	/// with no hall left, the halls win and the record is repaired out loud.
	/// </para>
	/// <para>
	/// <b>Never <c>Seat</c>.</b> The seat is the settlement the founder is standing in and it
	/// exchanges on <c>TrySeat</c>; the capital does not move when a founder walks
	/// (END-STATE-CITIES-RESEARCH &sect;5.1). Nothing in this file reads the seat/Away roles to
	/// decide the capital &mdash; only to name which city a piece of ground belongs to, which is
	/// what those roles are actually for.
	/// </para>
	/// </summary>
	internal static partial class KingdomCrown
	{
		/// <summary>Module toggle, per STANDARDS &sect;3. An absent option reads as on, which is
		/// what every other module's read does.</summary>
		internal static bool Enabled => Options.GetOption("r_TAF_OptionCapital") != "No";

		// One answer, keyed by the ground and the tick it was read on, and cleared outright when
		// the crown moves. The purpose gate asks this once per catalogue row per menu redraw --
		// the same hot path KingdomZoning.KeptMegastructure caches for, and for the same reason.
		private static string CacheZone;

		private static long CacheTick = -1L;

		private static string CacheValue;

		/// <summary>Drops the cached answer. Called the moment the crown moves, because the tick
		/// may not turn over between the act and the next menu, and a founder who has just moved
		/// their capital must not open the commission list and be told the old one.</summary>
		internal static void ClearCache()
		{
			CacheTick = -1L;
			CacheZone = null;
			CacheValue = null;
		}

		/// <summary>
		/// Writes this hall's own ground under its own key, so a hall can tell whether the crown is
		/// in IT rather than merely in its city. Two writes and a string compose; nothing here loads
		/// a zone.
		/// </summary>
		internal static void Anchor(GameObject Hall)
		{
			if (Hall == null || The.Game == null)
			{
				return;
			}
			Cell cell = Hall.CurrentCell;
			Zone zone = cell?.ParentZone;
			if (zone == null)
			{
				return;
			}
			string key = KingdomCrownRules.ComposeLocationKey(zone.ZoneID, cell.X, cell.Y);
			if (!string.IsNullOrEmpty(key))
			{
				Hall.SetStringProperty(HallKeyProperty, key);
			}
		}

		/// <summary>The property a hall keeps its own composed ground-key under. A property rather
		/// than a serialized field: it is derived from the cell and can always be re-derived, and
		/// STANDARDS &sect;1 would rather add nothing to a save it does not have to.</summary>
		internal const string HallKeyProperty = "r_TAF_CrownHallKey";

		/// <summary>
		/// The city keeping the crown, or null when the realm has none.
		/// <para>
		/// Repairs the record when it has fallen out of step with the halls, and says so once
		/// &mdash; once and only once because the repair makes the condition non-recurring, which is
		/// the same bargain the arches' register keeps and the reason neither needs a latch.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Null or unfounded yields null.</param>
		internal static string CapitalOf(KingdomSystem System)
		{
			if (!Enabled || System == null || !System.Founded || The.Game == null)
			{
				return null;
			}
			KingdomData.EnsureBuildings();
			Zone active = The.ZoneManager?.ActiveZone;
			string here = (active != null) ? active.ZoneID : "";
			long now = The.Game.TimeTicks;
			if (CacheTick == now && string.Equals(CacheZone, here))
			{
				return CacheValue;
			}
			string registered;
			string ignored;
			bool read = KingdomCrownRules.TryParseCrown(
				The.Game.GetStringGameState(KingdomCrownRules.RegisterStateKey, ""), out registered, out ignored);
			List<string> halls = CitiesWithCrown(System, active);
			string capital;
			bool agreed = KingdomCrownRules.Resolve(registered, halls, out capital);
			if (!read || !agreed)
			{
				Write(capital, "");
				Tell(System, string.IsNullOrEmpty(capital)
					? KingdomCrownRules.StruckLine(registered)
					: KingdomCrownRules.RepairedLine(capital));
			}
			CacheZone = here;
			CacheTick = now;
			CacheValue = capital;
			return capital;
		}

		/// <summary>Whether the crown is set down in this city. The one question the zoning gate
		/// asks, and it is asked by NAME because names do not exchange when the founder walks.</summary>
		internal static bool CrownedHere(KingdomSystem System, string City)
		{
			if (string.IsNullOrEmpty(City))
			{
				return false;
			}
			return string.Equals(CapitalOf(System), City, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>The same question for the ground the founder is standing on, which is what a
		/// commission menu actually has in hand.</summary>
		internal static bool CrownedOn(KingdomSystem System, string ZoneID)
		{
			return CrownedHere(System, CityOf(System, ZoneID));
		}

		/// <summary>
		/// The same question for the ground under the founder's feet, which is the only ground a
		/// commission menu is ever opened on. The one call the zoning gate makes.
		/// </summary>
		internal static bool CrownedOnActiveGround(KingdomSystem System)
		{
			Zone active = The.ZoneManager?.ActiveZone;
			return active != null && CrownedOn(System, active.ZoneID);
		}

		/// <summary>The capital's name for a refusal that would rather say where the crown IS than
		/// what the rule is. Null when the realm has no capital, which is its own sentence.</summary>
		internal static string CapitalName(KingdomSystem System)
		{
			return CapitalOf(System);
		}

		/// <summary>
		/// Setting the crown down. The Charter's own dedication grammar &mdash; disclose the whole
		/// cost, ask, then act &mdash; and the cost disclosed for a MOVE is the one that matters,
		/// because the second hall is already built by the time this is read and the crossings are
		/// not yet re-keyed.
		/// </summary>
		internal static void TakeUp(GameObject Hall)
		{
			if (!Enabled || Hall == null)
			{
				return;
			}
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			Cell cell = Hall.CurrentCell;
			Zone zone = cell?.ParentZone;
			string here = (system == null || zone == null) ? null : CityOf(system, zone.ZoneID);
			bool ours = Hall.GetIntProperty("KingdomBuilt") == 1 || Hall.GetIntProperty("KingdomGrid") == 1;
			string capital = (system == null) ? null : CapitalOf(system);
			KingdomCrownVerdict verdict = KingdomCrownRules.JudgeTakeUp(
				Founded: system != null && system.Founded,
				OurGround: here != null,
				OurWork: ours,
				Crowned: capital,
				Here: here);
			if (verdict == KingdomCrownVerdict.AlreadyHere)
			{
				Popup.Show(KingdomCrownRules.AlreadyHereLine(here));
				return;
			}
			if (verdict != KingdomCrownVerdict.Crowns && verdict != KingdomCrownVerdict.Moves)
			{
				Popup.Show(KingdomCrownRules.RefusalLine(verdict));
				return;
			}
			string prompt = (verdict == KingdomCrownVerdict.Crowns)
				? KingdomCrownRules.CrownPrompt(KingdomPresentation.Rich(here))
				: KingdomCrownRules.MovePrompt(KingdomPresentation.Rich(capital), KingdomPresentation.Rich(here));
			if (Popup.ShowYesNo(prompt) != DialogResult.Yes)
			{
				return;
			}
			Anchor(Hall);
			Write(here, Hall.GetStringProperty(HallKeyProperty, ""));
			ClearCache();
			string shownHere = KingdomPresentation.Rich(here);
			string shownCapital = KingdomPresentation.Rich(capital);
			if (verdict == KingdomCrownVerdict.Crowns)
			{
				Tell(system, KingdomCrownRules.CrownedLine(shownHere));
				system.RecordDeed("the crown was set down at " + shownHere);
				KingdomChronicle.Record(system, KingdomCrownRules.CrownedTelling(shownHere, KingdomPresentation.Rich(system.KingdomDisplayName)), Accomplishment: true);
			}
			else
			{
				Tell(system, KingdomCrownRules.MovedLine(shownCapital, shownHere));
				Tell(system, KingdomCrownRules.FormerCrownLine(shownCapital));
				system.RecordDeed("the crown moved from " + shownCapital + " to " + shownHere);
				KingdomChronicle.Record(system, KingdomCrownRules.MovedTelling(shownCapital, shownHere), Accomplishment: true);
			}
			// Addendum 22 A2: the network hubs at the capital. Done in the same breath as the
			// crowning, because a realm whose arches answered the old capital for a day would be a
			// realm that had two of them.
			KingdomMirrorGate.Hub(system, here);
		}
	}
}
