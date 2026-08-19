using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static class KingdomReports
	{
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
				.Append("\nUpkeep: ")
				.Append(KingdomRules.UpkeepDrams(System.Population))
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
			if (stored < KingdomRules.DramsPerArrival + KingdomRules.UpkeepDrams(System.Population))
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
