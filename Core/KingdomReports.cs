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
