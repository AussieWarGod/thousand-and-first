using System;
using System.Collections.Generic;

using ThousandAndFirst;

// XRL.World.Parts, for the reason r_KingdomPlot, r_KingdomMirrorGate and the lab's four all state:
// GamePartBlueprint resolves a part named in XML as exactly "XRL.World.Parts.<Name>" and tries no
// other name. Only the part moves; everything it does lives in ThousandAndFirst.KingdomCrown below.
namespace XRL.World.Parts
{
	/// <summary>
	/// The crown hall: a room built to hold one thing, and the city that holds it is the capital.
	/// <para>
	/// <b>The crown is a building</b> (Addendum 22 A4), which is rule (b) of
	/// END-STATE-CITIES-RESEARCH &sect;5.2 &mdash; Civ's movable Palace, the only designation rule
	/// in the comparables with actual praise attached, and praised for exactly what this mod wants:
	/// the roleplay and the strategic consequence at once. Raising the hall is the whole project;
	/// setting the crown down in it is the moment, and moving the crown means raising another hall
	/// somewhere else and walking there.
	/// </para>
	/// <para>
	/// The part carries no state of its own. Which hall holds the crown is a REALM fact and is
	/// carried as one (<c>KingdomCrownRules.RegisterStateKey</c>), because the city that keeps it is
	/// dormant most of the time and a field on a dormant object cannot answer a menu.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomCrownHall : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID
				|| ID == GetShortDescriptionEvent.ID;
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append(KingdomCrown.DescriptionLine(ParentObject));
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Crown", KingdomCrown.TakeUpLabel(ParentObject), "r_TakeUpCrown", null, 'c', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_TakeUpCrown" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("crown", delegate
				{
					KingdomCrown.TakeUp(ParentObject);
				});
				return true;
			}
			return base.HandleEvent(E);
		}

		/// <summary>
		/// The arch's own anchoring discipline, for the same reason: a building fires EnteredCell
		/// exactly once, at placement, and that is where its ground becomes a name the realm can
		/// write down. Cheap and idempotent, so it is run rather than scheduled.
		/// </summary>
		public override bool FireEvent(Event E)
		{
			if (E.ID == "EnteredCell")
			{
				KingdomSystem.Guard("crown anchor", delegate
				{
					KingdomCrown.Anchor(ParentObject);
				});
			}
			return base.FireEvent(E);
		}
	}
}

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
	internal static class KingdomCrown
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
				? KingdomCrownRules.CrownPrompt(here)
				: KingdomCrownRules.MovePrompt(capital, here);
			if (Popup.ShowYesNo(prompt) != DialogResult.Yes)
			{
				return;
			}
			Anchor(Hall);
			Write(here, Hall.GetStringProperty(HallKeyProperty, ""));
			ClearCache();
			if (verdict == KingdomCrownVerdict.Crowns)
			{
				Tell(system, KingdomCrownRules.CrownedLine(here));
				system.RecordDeed("the crown was set down at " + here);
				KingdomChronicle.Record(system, KingdomCrownRules.CrownedTelling(here, system.KingdomDisplayName), Accomplishment: true);
			}
			else
			{
				Tell(system, KingdomCrownRules.MovedLine(capital, here));
				Tell(system, KingdomCrownRules.FormerCrownLine(capital));
				system.RecordDeed("the crown moved from " + capital + " to " + here);
				KingdomChronicle.Record(system, KingdomCrownRules.MovedTelling(capital, here), Accomplishment: true);
			}
			// Addendum 22 A2: the network hubs at the capital. Done in the same breath as the
			// crowning, because a realm whose arches answered the old capital for a day would be a
			// realm that had two of them.
			KingdomMirrorGate.Hub(system, here);
		}

		/// <summary>The line the hall carries in its own description.</summary>
		internal static string DescriptionLine(GameObject Hall)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			string capital = CapitalOf(system);
			return KingdomCrownRules.DescriptionLine(Holds(system, Hall, capital), capital);
		}

		/// <summary>What the action reads as in the list.</summary>
		internal static string TakeUpLabel(GameObject Hall)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			string capital = CapitalOf(system);
			return KingdomCrownRules.TakeUpLabel(Holds(system, Hall, capital), capital);
		}

		/// <summary>
		/// Whether the crown is in THIS hall rather than merely in this hall's city. Asked of the
		/// hall's own ground key when the record kept one, and of the city otherwise &mdash; a
		/// record written before the hall had anchored still names the right city, and a founder
		/// reading the only hall in that city is reading the right hall.
		/// </summary>
		private static bool Holds(KingdomSystem System, GameObject Hall, string Capital)
		{
			if (System == null || Hall == null || string.IsNullOrEmpty(Capital) || The.Game == null)
			{
				return false;
			}
			string registeredCity;
			string registeredKey;
			if (!KingdomCrownRules.TryParseCrown(
				The.Game.GetStringGameState(KingdomCrownRules.RegisterStateKey, ""), out registeredCity, out registeredKey))
			{
				return false;
			}
			string mine = Hall.GetStringProperty(HallKeyProperty, "");
			if (!string.IsNullOrEmpty(registeredKey) && !string.IsNullOrEmpty(mine))
			{
				return string.Equals(registeredKey, mine, StringComparison.Ordinal);
			}
			Zone zone = Hall.CurrentCell?.ParentZone;
			return zone != null && string.Equals(CityOf(System, zone.ZoneID), Capital, StringComparison.OrdinalIgnoreCase);
		}

		private static void Write(string City, string Key)
		{
			The.Game?.SetStringGameState(KingdomCrownRules.RegisterStateKey, KingdomCrownRules.FormatCrown(City, Key));
		}

		private static void Tell(KingdomSystem System, string Line)
		{
			if (System == null || string.IsNullOrEmpty(Line))
			{
				return;
			}
			System.Ledger.Note(Line);
			MessageQueue.AddPlayerMessage(Line);
		}

		/// <summary>
		/// Every city keeping a standing crown hall, in NAME order.
		/// <para>
		/// Name order rather than seat order, and that is the whole of &sect;5.1's warning made
		/// operational: seat and Away exchange every time the founder walks into the other city's
		/// ground, so a tie-break that read them would hand the realm a different capital depending
		/// on where its founder happened to be standing. Names do not move.
		/// </para>
		/// </summary>
		private static List<string> CitiesWithCrown(KingdomSystem System, Zone Active)
		{
			List<string> found = new List<string>();
			string blueprint = BlueprintOfCrown();
			AddIfKeeping(found, System.SeatName, System.City, blueprint);
			KingdomSettlement away = System.Away;
			if (away != null)
			{
				AddIfKeeping(found, string.IsNullOrEmpty(away.SettlementName) ? System.KingdomDisplayName : away.SettlementName,
					away.City, blueprint);
			}
			// The freshness patch: a hall finished since this zone's last settlement pass stands in
			// the world and is not yet in the book. The book is still the record -- it covers ground
			// nobody has stood in for a season -- and the two only ever disagree in this one
			// direction.
			if (Active != null)
			{
				string city = CityOf(System, Active.ZoneID);
				if (city != null && !Holding(found, city))
				{
					foreach (GameObject work in Active.GetObjects())
					{
						if (work != null && work.GetIntProperty("KingdomBuilt") == 1
							&& string.Equals(KingdomUpgrade.DesignKeyOf(work), KingdomCrownRules.CrownKey, StringComparison.OrdinalIgnoreCase))
						{
							found.Add(city);
							break;
						}
					}
				}
			}
			found.Sort(StringComparer.OrdinalIgnoreCase);
			return found;
		}

		private static void AddIfKeeping(List<string> Found, string City, Simulation.City.KingdomCityBook Book, string Blueprint)
		{
			if (string.IsNullOrEmpty(City) || Book == null || Book.WorkDesignKeys == null || Holding(Found, City))
			{
				return;
			}
			for (int i = 0; i < Book.WorkDesignKeys.Count; i++)
			{
				string stored = Book.WorkDesignKeys[i];
				if (string.IsNullOrEmpty(stored))
				{
					continue;
				}
				// The book's column carries a BLUEPRINT (KingdomCity.ReadWorks) and a loaded-zone
				// read carries a KEY, so both are matched -- a rule that read only one of the two
				// would be right about half its callers.
				if (string.Equals(stored, KingdomCrownRules.CrownKey, StringComparison.OrdinalIgnoreCase)
					|| (!string.IsNullOrEmpty(Blueprint) && string.Equals(stored, Blueprint, StringComparison.OrdinalIgnoreCase)))
				{
					Found.Add(City);
					return;
				}
			}
		}

		private static bool Holding(List<string> Found, string City)
		{
			for (int i = 0; i < Found.Count; i++)
			{
				if (string.Equals(Found[i], City, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		private static string BlueprintOfCrown()
		{
			List<KingdomRules.BuildEntry> entries = KingdomData.Buildings;
			for (int i = 0; i < entries.Count; i++)
			{
				if (string.Equals(entries[i].Key, KingdomCrownRules.CrownKey, StringComparison.OrdinalIgnoreCase))
				{
					return entries[i].Blueprint;
				}
			}
			return null;
		}

		/// <summary>
		/// Which of the realm's cities holds this ground, or null when the realm does not hold it at
		/// all. The seat's own zones are read off the system's flat fields and the other city's off
		/// its record, which is the whole of the seat idiom &mdash; and it is the ONLY thing the
		/// seat roles are asked for here.
		/// <para>
		/// <b>The one copy.</b> The arch, the register office and the crown all need to turn a zone
		/// into the founder's own word for a city, and three copies of six lines is how the answers
		/// start disagreeing (STANDARDS &sect;2's shared-utility rule). It lives here because the
		/// crown is the lane that must never get it wrong.
		/// </para>
		/// </summary>
		internal static string CityOf(KingdomSystem System, string ZoneId)
		{
			if (System == null || string.IsNullOrEmpty(ZoneId))
			{
				return null;
			}
			if (System.ClaimedZones != null && System.ClaimedZones.Contains(ZoneId))
			{
				return System.SeatName;
			}
			KingdomSettlement away = System.Away;
			if (away != null && away.ClaimedZones != null && away.ClaimedZones.Contains(ZoneId))
			{
				return string.IsNullOrEmpty(away.SettlementName) ? System.KingdomDisplayName : away.SettlementName;
			}
			return null;
		}
	}
}

