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
					RewarnRaidOnReturn(System, timeTicks);
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
		/// A raid that came due while the founder was away is never resolved without them.
		/// Raiders who find no one home do not loot in the dark and vanish into the record as a
		/// debt already paid &mdash; they wait. The threat stays exactly as it was
		/// (<see cref="KingdomSystem.RaidState"/> untouched, faction unchanged, no water taken, no
		/// one lost), only the due tick is pushed out by the same lead the original warning used,
		/// so the homecoming itself buys a fresh window to pay tribute, talk it down, or simply be
		/// standing there the next time it comes due. What accrues in absence is the news that
		/// they came and found the gate shut, never a loss no one witnessed
		/// (VISION.md's absence pillar: absence never punishes, and what it moves is supply-carried
		/// level, never a raid nobody witnessed; STANDARDS.md &sect;5.3: "witnessed-only
		/// accounting").
		/// </summary>
		public static void RewarnRaidOnReturn(KingdomSystem System, long TimeTicks)
		{
			string displayName = Faction.GetFormattedName(System.RaidFactionName);
			System.RaidDueTick = TimeTicks + KingdomRules.RaidWarningLeadTicks;
			int demand = KingdomRules.TributeDemand(KingdomRules.RaidTributeDrams, System.RaidTimesDeferred);
			KingdomChronicle.Record(System, "raiders of " + displayName + " came looking for " + System.KingdomDisplayName + " while the founder was afield, and found no one to answer them");
			System.Ledger.Note("{{r|Raiders of " + displayName + " came for the stores while you were away and found no one to meet them. Nothing was taken. They have not given up: tribute may yet turn them (" + demand + " drams), or stand and meet them yourself.}}");
			if (KingdomLog.Enabled) KingdomLog.Log("raid re-warned on return: faction=" + System.RaidFactionName + " due=" + System.RaidDueTick);
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
			// The walls fight first. Crewed works and a garrison district decide how much of the
			// band gets past the perimeter at all, and how much the ones who do can carry off.
			// Without this, fortification would be decoration: nothing else in the mod reads
			// KingdomSurvey.Defence(), and a settlement's palisades would change nothing a
			// player could observe.
			int defence = (Shared != null) ? Shared.Defence() : 0;
			int size = KingdomRules.RaidSize(System.Stage);
			KingdomRules.RaidOutcome outcome = KingdomRules.ResolveRaid(defence, size);
			int party = KingdomRules.RaidingPartySize(size, defence, outcome);
			if (party <= 0)
			{
				System.RaidFactionName = null;
				KingdomChronicle.Record(System, "raiders of " + displayName + " came against " + System.KingdomDisplayName + " and were turned back at the wall", Accomplishment: true);
				System.RecordDeed("the walls of " + System.KingdomDisplayName + ", which have turned raiders back");
				MessageQueue.AddPlayerMessage(KingdomVoices.Say(System, VoiceOccasion.RaidRepelled, "{{G|Raiders of " + displayName + " come against " + System.KingdomDisplayName + " and break on the walls. The watch holds.}}"));
				KingdomLog.Log("raid: repelled at the wall faction=" + displayName + " defence=" + defence + " size=" + size);
				return;
			}
			int spawned = 0;
			List<Cell> emptyCells = Z.GetEmptyCells((Cell c) => c.X == 0 || c.X == Z.Width - 1 || c.Y == 0 || c.Y == Z.Height - 1);
			if (emptyCells == null || emptyCells.Count == 0)
			{
				emptyCells = Z.GetEmptyCells();
			}
			for (int i = 0; i < party && emptyCells != null && emptyCells.Count > 0; i++)
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
				int asked = KingdomRules.RaidPlunder(KingdomRules.RaidPlunderDrams, defence, outcome);
				plundered = (Shared != null) ? Shared.Consume(asked) : KingdomGrowth.ConsumeStoredWater(Z, asked);
			}
			KingdomLog.Log("raid: executed faction=" + displayName + " spawned=" + spawned + " party=" + party + " size=" + size + " defence=" + defence + " plundered=" + plundered);
			if (spawned > 0)
			{
				KingdomChronicle.Record(System, "raiders of " + displayName + " descended upon " + System.KingdomDisplayName + " and broke open the stores");
				System.Ledger.Plundered += plundered;
				MessageQueue.AddPlayerMessage("{{R|Raiders of " + displayName + " descend upon " + System.KingdomDisplayName + "!"
					+ ((party < size) ? (" The watch turns back " + (size - party) + " of them at the wall.") : "")
					+ ((plundered > 0) ? (" They stave in the casks: " + plundered + " drams lost.") : "") + "}}");
				// Raiders who actually got past the wall may leave a work worse for it -- bounded,
				// never a player-placed object (candidates come only from Shared.Works, the
				// settlement's own crewed works), never total destruction (BUILDING-CATALOGUE-
				// BRIEF.md Addendum 7: "a damaged work stands").
				KingdomWear.OnRaidDamage(System, Z, Shared, spawned, System.LastRaidTick);
			}
		}
	}
}
