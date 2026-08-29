using System;
using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;


namespace ThousandAndFirst
{
	public static partial class KingdomReports
	{
		/// <summary>
		/// One plain sentence naming the settlement's most pressing want, so a founder always
		/// knows the next thing to do without reading a manual. Ordered by what actually
		/// blocks growth: water, then beds, then hands, then storage.
		/// </summary>
		/// <returns>Advice line, or empty when nothing is wanting.</returns>
		public static string NextNeed(KingdomSystem System, Zone Here)
		{
			if (Here == null || !System.ClaimedZones.Contains(Here.ZoneID))
			{
				return "Stand on the kingdom's own ground to see what it wants.";
			}
			int stored = KingdomGrowth.CountStoredWater(Here);
			int capacity = KingdomGrowth.CountStorageCapacity(Here);
			if (capacity <= 0)
			{
				return "Nothing here is dedicated to the stores. Dedicate a vessel, or commission a cask rack, and the settlement can begin to keep water.";
			}
			// Named before the thirst line, because it is the answer to it: a settlement with
			// nobody on the water is not failing, it is waiting to be told what to do.
			if (System.WaterCrew <= 0 && System.Population > 0 && KingdomGrowth.CountOpenWater(Here) > 0)
			{
				return "Nobody is carrying water. There is open water on this ground - set a water detail from the Charter, or keep pouring it in yourself.";
			}
			if (stored < KingdomRules.DramsPerArrival + KingdomRules.PolicyUpkeep(KingdomRules.UpkeepDrams(System.Population, System.Stage), System.Stores))
			{
				return "The stores are nearly dry. Pour water into a dedicated vessel; nothing else can happen until there is water to share.";
			}
			// The food half of the two lines above, in the same order and for the same reason: a
			// settlement with nowhere to keep food is not failing, it is waiting to be told what
			// to do, and one that is short of a harvest is failing and must be able to say so.
			KingdomSurvey pantry = KingdomSurvey.Take(Here, System);
			if (pantry.FoodCapacity <= 0 && KingdomGrowth.FoodMadePerDay(pantry) > 0)
			{
				return "The fields have nowhere to send a harvest. Dedicate a larder, or commission one, and what they grow will be kept.";
			}
			if (System.HungerStreak > 0)
			{
				return "The larders came up short and the settlement went hungry. More fields, or fewer mouths — a field feeds four settlers to the hand.";
			}
			if (!KingdomRules.HasRoomToHouse(System.Population, KingdomGrowth.CountBeds(Here)))
			{
				return "There is no bed free. Commission a communal bunk — and if the beds that exist are ones nobody arriving will take, the roll says whose needs they fail.";
			}
			if (System.IdleWorks > 0)
			{
				return System.IdleWorks + " of the works stand idle for want of hands. More settlers, or fewer works.";
			}
			if (System.ShorthandedWorks > 0)
			{
				return System.ShorthandedWorks + " of the works run shorthanded and produce less than they could.";
			}
			if (System.DamagedWorks > 0)
			{
				return KingdomWearRules.NextNeedLine(System.DamagedWorks);
			}
			if (System.Stage < GrowthStage.Steading && capacity < 16)
			{
				return "Storage is thin. A great cistern would carry the settlement toward a steading.";
			}
			return "";
		}

		/// <summary>
		/// The roll of settlers: who came, from where, and when. A settlement of numbers is a
		/// spreadsheet; a settlement of names is a place you come back to.
		/// </summary>
		public static string Roll(KingdomSystem System, int Limit = 30)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("{{C|The roll of ").Append(KingdomPresentation.Rich(System.SeatName)).Append("}}\n");
			KingdomCityState state;
			KingdomResidentRollProjection roll;
			if (!KingdomResidents.TryRoll(System, out state, out roll) || roll.Population == 0)
			{
				stringBuilder.Append("\nNo one has come yet. Water and a bed will change that.");
				return stringBuilder.ToString();
			}
			Zone currentZone = The.Player?.CurrentZone;
			bool hereIsOurs = currentZone != null && System.ClaimedZones.Contains(currentZone.ZoneID);
			int start = (roll.Names.Count > Limit) ? (roll.Names.Count - Limit) : 0;
			for (int i = start; i < roll.Names.Count; i++)
			{
				stringBuilder.Append("\n").Append(KingdomPresentation.Rich(roll.Names[i]));
				if (i < roll.Origins.Count)
				{
					stringBuilder.Append(", of ").Append(KingdomPresentation.Rich(roll.Origins[i]));
				}
				if (i < roll.Arrived.Count)
				{
					stringBuilder.Append(" {{K|(came the ").Append(roll.Arrived[i]).Append(")}}");
				}
				if (hereIsOurs)
				{
					stringBuilder.Append(KingdomLodging.RollLine(currentZone, roll.Names[i]));
				}
			}
			if (hereIsOurs)
			{
				List<string> yardLines = KingdomYards.RollLines(currentZone);
				if (yardLines.Count > 0)
				{
					stringBuilder.Append("\n\nTrades taken up:");
					for (int i = 0; i < yardLines.Count; i++)
					{
						stringBuilder.Append("\n").Append(yardLines[i]);
					}
				}
			}
			stringBuilder.Append("\n\n{{K|").Append(roll.Names.Count).Append(" named; ")
				.Append(roll.Population).Append(" living in the settlement.}}");
			stringBuilder.Append(KingdomGuestbook.RollAppendix(System));
			return stringBuilder.ToString();
		}

		public static string Standings(KingdomSystem System, int Limit = 18)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("{{C|Relationships of ").Append(
				KingdomPresentation.Rich(System.KingdomDisplayName)).Append("}}\n")
				.Append("{{K|Their regard for us and our policy toward them are separate.}}\n");
			int shown = 0;
			foreach (Faction faction in Factions.Loop())
			{
				if (faction.Name == System.KingdomFactionName || faction.Name == "Player" || !faction.Visible)
				{
					continue;
				}
				bool hasRegard = System.RegardForRealm.ContainsKey(faction.Name);
				bool hasPolicy = System.TryGetRealmPolicyToward(faction.Name, out int policy);
				if (hasRegard || hasPolicy)
				{
					stringBuilder.Append("\n").Append(
						KingdomPresentation.Rich(faction.DisplayName)).Append(": their regard ")
						.Append(hasRegard ? System.GetRegardForRealm(faction.Name).ToString()
							: "unspecified").Append("; our policy ")
						.Append(hasPolicy ? policy.ToString() : "unspecified");
					shown++;
					if (shown >= Limit)
					{
						stringBuilder.Append("\n...");
						break;
					}
				}
			}
			if (shown == 0)
			{
				stringBuilder.Append("\nNo civic relationship has been recorded yet.");
			}
			return stringBuilder.ToString();
		}

		public static string Chronicle(KingdomSystem System, bool Outsider = false, int Limit = 25)
		{
			System.Collections.Generic.List<string> entries = Outsider ? System.OutsiderEntries : System.ChronicleEntries;
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Outsider ? "{{r|As others tell it}}" : ("{{C|The Chronicle of " + KingdomPresentation.Rich(System.KingdomDisplayName) + "}}")).Append("\n");
			if (entries.Count == 0)
			{
				stringBuilder.Append("\nNothing is written yet.");
				return stringBuilder.ToString();
			}
			int start = (entries.Count > Limit) ? (entries.Count - Limit) : 0;
			for (int i = start; i < entries.Count; i++)
			{
				stringBuilder.Append("\n").Append(entries[i]);
			}
			return stringBuilder.ToString();
		}
	}
}
