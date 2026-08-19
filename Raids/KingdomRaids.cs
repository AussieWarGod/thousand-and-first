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

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Shared = null)
		{
			if (!Enabled || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID) || System.Stage < GrowthStage.Steading)
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			if (System.RaidState == 0)
			{
				if (timeTicks < System.LastRaidTick + KingdomRules.PolicyRaidCooldown(KingdomRules.RaidCooldownTicks, System.Gate))
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
				if (timeTicks - System.RaidDueTick > KingdomRules.TicksPerDay)
				{
					ResolveRaidInAbsence(System, Z, Shared);
				}
				else
				{
					ExecuteRaid(System, Z, Shared);
				}
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
			int demand = KingdomRules.TributeDemand(KingdomRules.RaidTributeDrams, System.RaidTimesDeferred);
			if (Z == null || KingdomGrowth.CountStoredWater(Z) < demand)
			{
				Failure = "Tribute costs {{C|" + demand + " drams}} from the stores here, and the stores cannot bear it.";
				return false;
			}
			KingdomGrowth.ConsumeStoredWater(Z, demand);
			string displayName = Faction.GetFormattedName(System.RaidFactionName);
			System.AdjustStanding(System.RaidFactionName, 50);
			System.RaidState = 0;
			System.RaidFactionName = null;
			System.RaidTimesDeferred = 0;
			System.LastRaidTick = The.Game.TimeTicks;
			KingdomChronicle.Record(System, System.KingdomDisplayName + " paid tribute in water, and " + displayName + " turned away");
			System.RecordDeed("the tribute " + System.KingdomDisplayName + " pays to keep the peace");
			MessageQueue.AddPlayerMessage("{{G|The tribute is paid. " + displayName + " turn away, for now.}}");
			return true;
		}

		/// <summary>
		/// A raid that fell while the founder was away is resolved as an aftermath, not an
		/// ambush at the gate. Raiders do not loiter at the border for a season waiting to be
		/// fought: the settlement met them, lost water, and buried what it lost, and the
		/// homecoming report says so.
		/// </summary>
		public static void ResolveRaidInAbsence(KingdomSystem System, Zone Z, KingdomSurvey Shared = null)
		{
			string displayName = Faction.GetFormattedName(System.RaidFactionName);
			System.RaidState = 0;
			System.RaidFactionName = null;
			System.RaidTimesDeferred = 0;
			System.LastRaidTick = The.Game.TimeTicks;
			int defence = (Shared != null) ? Shared.Defence() : 0;
			KingdomRules.RaidOutcome outcome = KingdomRules.ResolveRaid(defence, KingdomRules.RaidSize(System.Stage));
			if (outcome == KingdomRules.RaidOutcome.Repelled)
			{
				KingdomChronicle.Record(System, "raiders of " + displayName + " came upon " + System.KingdomDisplayName + " in the founder's absence and were turned back at the wall", Accomplishment: true);
				System.RecordDeed("the walls of " + System.KingdomDisplayName + ", which have turned raiders back");
				System.Ledger.Note("{{G|Raiders of " + displayName + " came while you were away, and the watch turned them back. Nothing was taken.}}");
				if (KingdomLog.Enabled) KingdomLog.Log("raid repelled in absence: defence=" + defence);
				return;
			}
			int asked = KingdomRules.RaidPlunder(KingdomRules.RaidPlunderDrams, defence, outcome);
			int plundered = (Shared != null) ? Shared.Consume(asked) : KingdomGrowth.ConsumeStoredWater(Z, asked);
			bool tookSomeone = false;
			if (System.Population > KingdomRules.LoyalCoreSettlers && Stat.Random(1, 100) <= KingdomRules.RaidCasualtyChance(defence, outcome))
			{
				tookSomeone = KingdomGrowth.Emigrate(System, Z, Shared);
				if (tookSomeone)
				{
					System.Dead++;
				}
			}
			KingdomChronicle.Record(System, "raiders of " + displayName + " came upon " + System.KingdomDisplayName + " in the founder's absence and broke open the stores");
			System.Ledger.Plundered += plundered;
			System.Ledger.Note("{{R|Raiders of " + displayName + " came while you were away. " + ((plundered > 0) ? (plundered + " drams were carried off.") : "The stores were already dry.") + ((defence > 0) ? " The watch made them pay for it." : "") + (tookSomeone ? " One of the settlement did not survive it." : "") + "}}");
			if (KingdomLog.Enabled) KingdomLog.Log("raid resolved in absence: plundered=" + plundered + " casualty=" + tookSomeone);
		}

		/// <summary>
		/// The third exit: a faction that already holds the kingdom in regard can be talked
		/// down once, without payment. Goodwill earned is goodwill spendable.
		/// </summary>
		public static bool TryTalkDown(KingdomSystem System, out string Failure)
		{
			Failure = null;
			if (System.RaidState != 1)
			{
				Failure = "No raid threatens.";
				return false;
			}
			if (!KingdomRules.CanTalkDown(System.GetStanding(System.RaidFactionName), System.RaidTimesDeferred))
			{
				Failure = "They will not hear us. Either the regard is not there, or the moment for words has passed.";
				return false;
			}
			string displayName = Faction.GetFormattedName(System.RaidFactionName);
			System.RaidState = 0;
			System.RaidFactionName = null;
			System.RaidTimesDeferred = 0;
			System.LastRaidTick = The.Game.TimeTicks;
			KingdomChronicle.Record(System, System.KingdomDisplayName + " sent word to " + displayName + ", and the scouts turned back without water changing hands", Accomplishment: true);
			MessageQueue.AddPlayerMessage("{{G|Word is sent, and " + displayName + " turn back. Nothing is paid but the regard you had already earned.}}");
			return true;
		}

		public static void ExecuteRaid(KingdomSystem System, Zone Z, KingdomSurvey Shared = null)
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
				plundered = (Shared != null) ? Shared.Consume(KingdomRules.RaidPlunderDrams) : KingdomGrowth.ConsumeStoredWater(Z, KingdomRules.RaidPlunderDrams);
			}
			KingdomLog.Log("raid: executed faction=" + displayName + " spawned=" + spawned + " size=" + size + " plundered=" + plundered);
			if (spawned > 0)
			{
				KingdomChronicle.Record(System, "raiders of " + displayName + " descended upon " + System.KingdomDisplayName + " and broke open the stores");
				System.Ledger.Plundered += plundered;
				MessageQueue.AddPlayerMessage("{{R|Raiders of " + displayName + " descend upon " + System.KingdomDisplayName + "!" + ((plundered > 0) ? (" They stave in the casks: " + plundered + " drams lost.") : "") + "}}");
			}
		}
	}
}
