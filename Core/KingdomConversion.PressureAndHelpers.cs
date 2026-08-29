using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomConversion
	{
		private static void Pressure(KingdomSystem System, Zone Z, GameObject Resident, long Now)
		{
			string roll = RollNameOf(Resident);
			if (roll == null)
			{
				return;
			}
			string pressing = ResentedPressure(System, Z, Resident);
			if (pressing == null)
			{
				// Nothing is being imposed on them, or nothing they mind. Forgetting the entry
				// rather than banking it is the same ruling housing makes: the founder is being
				// asked to act on THIS pressure, and if it comes back they get the whole window
				// again. And the arrest is SAID, wherever the founder is -- a warning that is
				// never withdrawn is a warning they stop believing -- but only when there was one:
				// an entry that never carried a warning day has nothing to unsay.
				int wasWarnedOn;
				bool had = System.ConversionResented.TryGetValue(roll, out wasWarnedOn);
				System.ConversionResented.Remove(roll);
				if (had && wasWarnedOn > KingdomConversionRules.NotWarned)
				{
					KingdomWord.Unsay(System, System.SeatName, KingdomWord.StandsIn(Z),
						KingdomBrinkRules.LiftedNote(BrinkKind.Creed,
							KingdomPresentation.Rich(roll)));
				}
				return;
			}
			int today = KingdomBrinkRules.DayNumber(Now);
			int warned;
			if (!System.ConversionResented.TryGetValue(roll, out warned) || warned <= KingdomConversionRules.NotWarned)
			{
				System.ConversionResented[roll] = today;
				Announce(System, Z, roll, pressing, KingdomConversionRules.ResentedWindowDays);
				// The day the word goes out is never the day they go.
				return;
			}
			if (!KingdomConversionRules.ResentmentRunOut(warned, today))
			{
				return;
			}
			long went = (long)(warned + KingdomConversionRules.ResentedWindowDays) * KingdomRules.TicksPerDay;
			string leaving = KingdomConversionRules.LeavingLine(
				KingdomPresentation.Rich(roll))
				+ KingdomBrinkRules.FiredClause(KingdomBrinkRules.DaysStood(went, Now));
			if (KingdomGrowth.Emigrate(System, Z, null, Resident, KingdomConversionRules.DepartureCause))
			{
				KingdomWord.Aftermath(System, System.SeatName, KingdomWord.StandsIn(Z), leaving);
				System.ConversionResented.Remove(roll);
				return;
			}
			// The settlement would not let them go -- they are the last of the loyal core, or the
			// emigration machinery could not take them. The window stays spent and is tried again
			// on the next resolve rather than being reset, so nothing is lost and nobody is told
			// they are going by a settlement that then kept them.
		}

		// The first source naming a creed this settler resents, or null. First rather than worst
		// on purpose: a second grievance does not make anybody leave twice, and the founder is
		// owed one name to act on rather than a list.
		private static string ResentedPressure(KingdomSystem System, Zone Z, GameObject Resident)
		{
			string creed = Resident.GetStringProperty(KingdomCreed.CreedProperty);
			if (Resents(creed, System.DeclaredCreed))
			{
				return System.DeclaredCreed;
			}
			for (int i = 0; i < Sources.Count; i++)
			{
				string pressing = null;
				// Third-party sources are untrusted (STANDARDS 9): one that throws disables itself
				// for the pass and is logged, and never takes the settlement pass down with it.
				KingdomSystem.Guard("conversion pressure source", delegate
				{
					pressing = Sources[i].PressingCreed(System, Z, Resident);
				});
				if (Resents(creed, pressing))
				{
					return pressing;
				}
			}
			return null;
		}

		private static bool Resents(string Creed, string Pressing)
		{
			return !string.IsNullOrEmpty(Pressing)
				&& KingdomConversionRules.Resents(KingdomCreed.HostilityBetween(Creed, Pressing));
		}

		private static void BeginResentment(KingdomSystem System, Zone Z, string Roll, string Pressing)
		{
			if (System.ConversionResented.ContainsKey(Roll))
			{
				return;
			}
			System.ConversionResented[Roll] = KingdomBrinkRules.DayNumber((The.Game != null) ? The.Game.TimeTicks : 0L);
			Announce(System, Z, Roll, Pressing, KingdomConversionRules.ResentedWindowDays);
		}

		// STANDARDS 7b and Addendum 10(a): said once, and PUSHED to wherever the founder is
		// standing rather than left in a report they read at the seat. The map entry IS the
		// announce flag, so a settler whose window is already running cannot be warned about a
		// second time, and one whose pressure lifted and returned is warned afresh.
		private static void Announce(KingdomSystem System, Zone Z, string Roll, string Pressing, int DaysLeft)
		{
			string creedName = KingdomCreed.CreedName(Pressing);
			string shownRoll = KingdomPresentation.Rich(Roll);
			KingdomWord.Warn(System, System.SeatName, KingdomWord.StandsIn(Z),
				KingdomConversionRules.PressureNote(shownRoll, creedName) + " " + KingdomBrinkRules.WindowPhrase(DaysLeft),
				KingdomConversionRules.PressureTelling(shownRoll, creedName),
				null);
		}

		// Names that have left the roll are names nothing will ever pull at again. Pruned so a
		// departed settler's progress cannot be inherited by a later settler of the same name, and
		// so both maps stay the size of the city rather than of its history.
		private static void ForgetDeparted(KingdomSystem System)
		{
			Simulation.City.KingdomCityState state;
			Simulation.City.KingdomResidentRollProjection roll;
			List<string> living = Simulation.City.KingdomResidents.TryRoll(System, out state,
				out roll) ? roll.Names : new List<string>();
			Prune(System.ConversionShared, living);
			Prune(System.ConversionResented, living);
			List<string> stale = null;
			foreach (KeyValuePair<string, string> entry in System.ConversionToward)
			{
				if (!System.ConversionShared.ContainsKey(entry.Key))
				{
					if (stale == null)
					{
						stale = new List<string>();
					}
					stale.Add(entry.Key);
				}
			}
			if (stale == null)
			{
				return;
			}
			for (int i = 0; i < stale.Count; i++)
			{
				System.ConversionToward.Remove(stale[i]);
			}
		}

		private static void Prune(Dictionary<string, int> Map, List<string> Roll)
		{
			if (Map.Count == 0)
			{
				return;
			}
			List<string> gone = null;
			foreach (KeyValuePair<string, int> entry in Map)
			{
				if (!Roll.Contains(entry.Key))
				{
					if (gone == null)
					{
						gone = new List<string>();
					}
					gone.Add(entry.Key);
				}
			}
			if (gone == null)
			{
				return;
			}
			for (int i = 0; i < gone.Count; i++)
			{
				Map.Remove(gone[i]);
			}
		}

		// --- Facts about people, and the two maps that remember them ----------------------

		private static ConversionProgress ProgressOf(KingdomSystem System, string Roll)
		{
			string toward;
			int shared;
			if (!System.ConversionToward.TryGetValue(Roll, out toward) || !System.ConversionShared.TryGetValue(Roll, out shared))
			{
				return ConversionProgress.None;
			}
			return new ConversionProgress(toward, shared);
		}

		private static void SetProgress(KingdomSystem System, string Roll, ConversionProgress Progress)
		{
			if (!Progress.Any)
			{
				System.ConversionShared.Remove(Roll);
				System.ConversionToward.Remove(Roll);
				return;
			}
			System.ConversionShared[Roll] = Progress.Shared;
			System.ConversionToward[Roll] = Progress.Creed;
		}

		private static Dictionary<string, List<GameObject>> Households(List<GameObject> Residents)
		{
			Dictionary<string, List<GameObject>> households = new Dictionary<string, List<GameObject>>();
			for (int i = 0; i < Residents.Count; i++)
			{
				string plotId = Residents[i].GetStringProperty(KingdomLodging.HomePlotIdProperty);
				if (string.IsNullOrEmpty(plotId))
				{
					continue;
				}
				List<GameObject> under;
				if (!households.TryGetValue(plotId, out under))
				{
					under = new List<GameObject>();
					households[plotId] = under;
				}
				under.Add(Residents[i]);
			}
			return households;
		}

		private static Dictionary<string, int> CreedCounts(List<GameObject> People)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>();
			for (int i = 0; i < People.Count; i++)
			{
				string creed = People[i].GetStringProperty(KingdomCreed.CreedProperty);
				if (string.IsNullOrEmpty(creed))
				{
					continue;
				}
				int held;
				counts.TryGetValue(creed, out held);
				counts[creed] = held + 1;
			}
			return counts;
		}

		private static List<GameObject> ResidentsIn(KingdomSystem System, Zone Z)
		{
			List<GameObject> list = new List<GameObject>();
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (KingdomCitizenship.BelongsTo(System, item))
				{
					list.Add(item);
				}
			}
			return list;
		}

		// The name the roll carries this person under, which is the key both maps are filed by and
		// the name the registers will write. Null for anybody the roll does not carry.
		private static string RollNameOf(GameObject Resident)
		{
			string name = (Resident == null) ? null : Resident.GetStringProperty("KingdomName");
			return string.IsNullOrEmpty(name) ? null : name;
		}
	}
}
