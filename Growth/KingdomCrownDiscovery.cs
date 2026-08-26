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

	internal static partial class KingdomCrown
	{
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
					KingdomSurvey survey = KingdomSurvey.ActiveFor(Active)
						?? KingdomSurvey.Take(Active);
					for (int i = 0; i < survey.Built.Count; i++)
					{
						GameObject work = survey.Built[i];
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
