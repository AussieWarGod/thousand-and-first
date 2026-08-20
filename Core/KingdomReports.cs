using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static class KingdomReports
	{
		/// <summary>
		/// The watch and the larder, as the founder would see them standing here: crewed defence
		/// (including any garrison district) and what the dedicated larders hold. Both come from
		/// a fresh survey because both are facts about this ground, not fields on the system.
		/// </summary>
		private static string DefenceAndPantryLine(KingdomSystem System, Zone Here)
		{
			KingdomSurvey survey = KingdomSurvey.Take(Here, System);
			return "\nDefence: " + survey.Defence()
				+ (survey.DistrictDefenceBonus > 0 ? ("  (garrison " + survey.DistrictDefenceBonus + ")") : "")
				+ "  Larder: " + survey.FoodAbundance + " (" + survey.FoodStored + ")";
		}

		public static string Status(KingdomSystem System)
		{
			Zone currentZone = The.Player?.CurrentZone;
			bool currentClaimed = currentZone != null && System.ClaimedZones.Contains(currentZone.ZoneID);
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("{{C|").Append(System.KingdomDisplayName).Append("}}, founded tick ").Append(System.FoundedTick)
				.Append("\nStage: ")
				.Append(System.Stage)
				.Append(System.Withered ? " {{r|(withered)}}" : "")
				.Append("  Population: ")
				.Append(System.Population);
			if (System.OriginCounts.Count > 0)
			{
				stringBuilder.Append("\nPeoples:");
				foreach (System.Collections.Generic.KeyValuePair<string, int> originCount in System.OriginCounts)
				{
					stringBuilder.Append(" ").Append(originCount.Value).Append(" of ").Append(originCount.Key).Append(";");
				}
			}
			stringBuilder.Append("\nClaimed zones: ").Append(System.ClaimedZones.Count)
				.Append(currentClaimed ? ("  (here: " + KingdomGrowth.CountStoredWater(currentZone) + " drams stored, " + KingdomGrowth.CountOpenWater(currentZone) + " open, space for " + KingdomGrowth.CountStorageSpace(currentZone) + ")") : "")
				.Append("\nShops: tier ").Append(System.ShopTier).Append(System.IdleWorks > 0 ? ("  {{r|" + System.IdleWorks + " works idle for want of hands}}") : "")
				// Defence and the pantry are surveyed live: both are facts about the ground the
				// founder is standing on, and neither is carried on the system.
				.Append(currentClaimed ? DefenceAndPantryLine(System, currentZone) : "")
				.Append("\nUpkeep: ")
				.Append(KingdomRules.PolicyUpkeep(KingdomRules.UpkeepDrams(System.Population), System.Stores))
				.Append(" drams per interval  Thirst streak: ")
				.Append(System.DryStreak)
				.Append("\nNext arrival due: tick ")
				.Append(System.NextArrivalTick)
				.Append(" (now ")
				.Append(The.Game.TimeTicks)
				.Append(")\nPlayer rep with kingdom: ")
				.Append(The.Game.PlayerReputation.Get(System.KingdomFactionName));
			string need = NextNeed(System, currentZone);
			if (!string.IsNullOrEmpty(need))
			{
				stringBuilder.Append("\r\n\r\n{{W|").Append(need).Append("}}");
			}
			if (System.ActiveDealKeys.Count > 0)
			{
				stringBuilder.Append("\nCharters: ");
				for (int i = 0; i < System.ActiveDealKeys.Count; i++)
				{
					stringBuilder.Append(System.ActiveDealKeys[i]).Append(" with ").Append(System.ActiveDealFactions[i]).Append("; ");
				}
			}
			return stringBuilder.ToString();
		}

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
			if (stored < KingdomRules.DramsPerArrival + KingdomRules.PolicyUpkeep(KingdomRules.UpkeepDrams(System.Population), System.Stores))
			{
				return "The stores are nearly dry. Pour water into a dedicated vessel; nothing else can happen until there is water to share.";
			}
			if (!KingdomRules.HasRoomToHouse(System.Population, KingdomGrowth.CountBeds(Here)))
			{
				return "There is no bed free. Commission a communal bunk and the next settler will stay.";
			}
			if (System.IdleWorks > 0)
			{
				return System.IdleWorks + " of the works stand idle for want of hands. More settlers, or fewer works.";
			}
			if (System.ShorthandedWorks > 0)
			{
				return System.ShorthandedWorks + " of the works run shorthanded and produce less than they could.";
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
			stringBuilder.Append("{{C|The roll of ").Append(System.KingdomDisplayName).Append("}}\n");
			if (System.RosterNames.Count == 0)
			{
				stringBuilder.Append("\nNo one has come yet. Water and a bed will change that.");
				return stringBuilder.ToString();
			}
			int start = (System.RosterNames.Count > Limit) ? (System.RosterNames.Count - Limit) : 0;
			for (int i = start; i < System.RosterNames.Count; i++)
			{
				stringBuilder.Append("\n").Append(System.RosterNames[i]);
				if (i < System.RosterOrigins.Count)
				{
					stringBuilder.Append(", of ").Append(System.RosterOrigins[i]);
				}
				if (i < System.RosterArrived.Count)
				{
					stringBuilder.Append(" {{K|(came the ").Append(System.RosterArrived[i]).Append(")}}");
				}
			}
			stringBuilder.Append("\n\n{{K|").Append(System.RosterNames.Count).Append(" named; ").Append(System.Population).Append(" living in the settlement.}}");
			return stringBuilder.ToString();
		}

		public static string Standings(KingdomSystem System, int Limit = 18)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("{{C|Standings of ").Append(System.KingdomDisplayName).Append("}}\n");
			int shown = 0;
			foreach (Faction faction in Factions.Loop())
			{
				if (faction.Name == System.KingdomFactionName || faction.Name == "Player" || !faction.Visible)
				{
					continue;
				}
				int standing = System.GetStanding(faction.Name);
				if (standing != 0)
				{
					faction.FactionFeeling.TryGetValue(System.KingdomFactionName, out var feeling);
					stringBuilder.Append("\n").Append(faction.DisplayName).Append(": standing ")
						.Append(standing)
						.Append(", their feeling toward us ")
						.Append(feeling);
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
				stringBuilder.Append("\nThe world holds no strong opinion of us yet.");
			}
			return stringBuilder.ToString();
		}

		public static string Chronicle(KingdomSystem System, bool Outsider = false, int Limit = 25)
		{
			System.Collections.Generic.List<string> entries = Outsider ? System.OutsiderEntries : System.ChronicleEntries;
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Outsider ? "{{r|As others tell it}}" : ("{{C|The Chronicle of " + System.KingdomDisplayName + "}}")).Append("\n");
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
