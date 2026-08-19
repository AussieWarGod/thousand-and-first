using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static class KingdomRaids
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionRaids") != "No";

		public static void OnZoneActivated(KingdomSystem System, Zone Z)
		{
			if (!Enabled || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID) || System.Stage < GrowthStage.Steading)
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			if (System.RaidState == 0)
			{
				if (timeTicks < System.LastRaidTick + KingdomRules.RaidCooldownTicks)
				{
					return;
				}
				string provoked = FindProvokedFaction(System);
				if (provoked == null)
				{
					return;
				}
				System.RaidState = 1;
				System.RaidFactionName = provoked;
				System.RaidDueTick = timeTicks + KingdomRules.RaidWarningLeadTicks;
				KingdomLog.Log("raid: warned faction=" + provoked + " due=" + System.RaidDueTick);
				string displayName = Faction.GetFormattedName(provoked);
				KingdomChronicle.Record(System, "scouts of " + displayName + " were seen eyeing the stores of " + System.KingdomDisplayName);
				MessageQueue.AddPlayerMessage("{{r|Scouts of " + displayName + " have been seen nearby. They will come for the stores. Tribute may yet turn them (" + KingdomRules.RaidTributeDrams + " drams).}}");
			}
			else if (System.RaidState == 1 && timeTicks >= System.RaidDueTick)
			{
				ExecuteRaid(System, Z);
			}
		}

		public static string FindProvokedFaction(KingdomSystem System)
		{
			string result = null;
			int worst = KingdomRules.RaidStandingThreshold;
			foreach (KeyValuePair<string, int> standing in System.Standings)
			{
				if (standing.Value <= worst && KingdomRules.RaiderTableFor(standing.Key) != null)
				{
					worst = standing.Value;
					result = standing.Key;
				}
			}
			return result;
		}

		public static bool TryTribute(KingdomSystem System, Zone Z, out string Failure)
		{
			Failure = null;
			if (System.RaidState != 1)
			{
				Failure = "No raid threatens.";
				return false;
			}
			if (Z == null || KingdomGrowth.CountStoredWater(Z) < KingdomRules.RaidTributeDrams)
			{
				Failure = "Tribute costs {{C|" + KingdomRules.RaidTributeDrams + " drams}} from the stores here, and the stores cannot bear it.";
				return false;
			}
			KingdomGrowth.ConsumeStoredWater(Z, KingdomRules.RaidTributeDrams);
			string displayName = Faction.GetFormattedName(System.RaidFactionName);
			System.AdjustStanding(System.RaidFactionName, 50);
			System.RaidState = 0;
			System.RaidFactionName = null;
			System.LastRaidTick = The.Game.TimeTicks;
			KingdomChronicle.Record(System, System.KingdomDisplayName + " paid tribute in water, and " + displayName + " turned away");
			MessageQueue.AddPlayerMessage("{{G|The tribute is paid. " + displayName + " turn away, for now.}}");
			return true;
		}

		public static void ExecuteRaid(KingdomSystem System, Zone Z)
		{
			string[] table = KingdomRules.RaiderTableFor(System.RaidFactionName);
			string displayName = Faction.GetFormattedName(System.RaidFactionName);
			System.RaidState = 0;
			System.LastRaidTick = The.Game.TimeTicks;
			if (table == null)
			{
				return;
			}
			int size = KingdomRules.RaidSize(System.Stage);
			int spawned = 0;
			List<Cell> emptyCells = Z.GetEmptyCells((Cell c) => c.X == 0 || c.X == Z.Width - 1 || c.Y == 0 || c.Y == Z.Height - 1);
			if (emptyCells == null || emptyCells.Count == 0)
			{
				emptyCells = Z.GetEmptyCells();
			}
			for (int i = 0; i < size && emptyCells != null && emptyCells.Count > 0; i++)
			{
				Cell cell = emptyCells.GetRandomElement();
				GameObject raider = GameObject.Create(table[Stat.Random(0, table.Length - 1)]);
				if (raider == null)
				{
					continue;
				}
				cell.AddObject(raider);
				raider.MakeActive();
				raider.SetIntProperty("KingdomRaider", 1);
				spawned++;
			}
			System.RaidFactionName = null;
			int plundered = 0;
			if (spawned > 0)
			{
				plundered = KingdomGrowth.ConsumeStoredWater(Z, KingdomRules.RaidPlunderDrams);
			}
			KingdomLog.Log("raid: executed faction=" + displayName + " spawned=" + spawned + " size=" + size + " plundered=" + plundered);
			if (spawned > 0)
			{
				KingdomChronicle.Record(System, "raiders of " + displayName + " descended upon " + System.KingdomDisplayName + " and broke open the stores");
				MessageQueue.AddPlayerMessage("{{R|Raiders of " + displayName + " descend upon " + System.KingdomDisplayName + "!" + ((plundered > 0) ? (" They stave in the casks: " + plundered + " drams lost.") : "") + "}}");
			}
		}
	}
}
