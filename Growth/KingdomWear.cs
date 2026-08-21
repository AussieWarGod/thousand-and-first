using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name. This part is
// only ever added in code, but it lives here anyway alongside r_KingdomImprovement and
// r_KingdomScaffold: a part whose namespace depends on how it happened to be attached is a trap
// waiting for the first blueprint that names it.
namespace XRL.World.Parts
{
	/// <summary>
	/// One work's own wear record: how damaged it is, why, whether the founder has told the
	/// settlement to leave it be, and &mdash; while a mending is actually under way &mdash; how
	/// much labour is left to put into it.
	/// <para>
	/// Attached lazily, the instant a work first takes damage, and removed the instant a mending
	/// finishes: a sound work carries no part at all, which is the state every building in every
	/// existing save is in, and the state every building returns to once it is whole again. Absent
	/// means sound.
	/// </para>
	/// <para>
	/// What the wear COSTS to mend and how a worn work runs are <c>KingdomMaterialRules</c>' own
	/// (<c>MaxWearPercent</c>, <c>ConditionPercent</c>, <c>RepairCost</c>/<c>RepairBits</c>/
	/// <c>RepairEffort</c>). This part only ever holds the one work's own reading of them.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomWear : IPart
	{
		/// <summary>How worn this work is, 0 to <see cref="KingdomMaterialRules.MaxWearPercent"/>.
		/// Never reaches 100: "a damaged work stands."</summary>
		public int Wear;

		/// <summary>Which of <see cref="KingdomWearRules.WearCause"/> last added to
		/// <see cref="Wear"/>, held as an int so the field's serialized type never depends on an
		/// enum's backing type.</summary>
		public int LastCause;

		/// <summary>The founder's standing "leave this one as it is". Persists on the object,
		/// shows in its description, and is only ever set or cleared from the Charter.</summary>
		public bool Held;

		/// <summary>Labour left to put into the mending under way on this work. Zero means no
		/// mending is under way, whether because none has been started or because
		/// <see cref="Held"/> or a shortage is holding it back.</summary>
		public int RepairEffortLeft;

		/// <summary>
		/// The <c>KingdomWearRules.RepairVerdict</c> last announced to the founder for this
		/// work's mending, as an int. Zero means nothing has been announced &mdash; unambiguous
		/// because zero is <c>Ready</c>, which is never announced as a block. Announcing again is
		/// gated on the reason having actually CHANGED, so a settlement short of shaped stone for
		/// a season says so once and then stops.
		/// </summary>
		public int AnnouncedBlock;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID;
		}

		/// <summary>
		/// Puts the work's own condition on the work itself, so the founder can read it by
		/// looking at the thing rather than only in the Status report.
		/// </summary>
		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append("\n{{rules|").Append(KingdomMaterialRules.ConditionWord(Wear))
				.Append(", running ").Append(KingdomMaterialRules.ConditionPercent(Wear)).Append(" parts in a hundred.")
				.Append(Held ? " Mending is held." : (RepairEffortLeft > 0 ? " Being mended." : "")).Append("}}");
			return base.HandleEvent(E);
		}
	}
}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// Wear and repair (BUILDING-CATALOGUE-BRIEF.md Addendum 7: "maintenance/wear translation").
	/// Three causes damage a work &mdash; raiders who got past the wall
	/// (<see cref="OnRaidDamage"/>, called from <c>KingdomRaids.ExecuteRaid</c>), a streak of
	/// consecutive full-stretch attended passes, and certified salvage acting up on use &mdash;
	/// and nothing else does. Absence never wears anything: every draw in
	/// <see cref="KingdomWearRules"/> is keyed to an event a real pass produced, never to elapsed
	/// time.
	/// <para>
	/// A damaged work keeps working, at <c>KingdomMaterialRules.ConditionPercent(Wear)</c> of
	/// what its crew would otherwise manage, and says so once (STANDARDS 7b) the moment it
	/// happens. Mending is a materials-and-hands job, auto-queued like an improvement but always
	/// visible (<c>r_KingdomWear.HandleEvent</c>) and holdable (<see cref="r_KingdomWear.Held"/>):
	/// one job at a time settlement-wide, the same "one gang, one job" law
	/// <c>KingdomMaterials.OnSettlementPass</c> already keeps for striking and clearing, costed
	/// and timed the same way a strike is &mdash; <c>KingdomMaterialRules.RepairCost</c>/
	/// <c>RepairBits</c> for what it costs, <c>RepairEffort</c> and
	/// <c>KingdomRules.HeartbeatDays</c> for how long it takes, so a long absence resolves a few
	/// honest days of mending and never the whole job in one step. Nothing here spends water, and
	/// nothing here ever fails a work past <see cref="KingdomMaterialRules.MaxWearPercent"/>.
	/// </para>
	/// <para>
	/// <b>Two clock notes, both deliberate.</b> First, <see cref="AdvanceRepair"/> is the
	/// reference for checkpoint ordering in this mod: it reads the gate, names the block once
	/// (STANDARDS 7b), and only then advances the stamp &mdash; so a mending nobody has hands for
	/// loses those days rather than banking them for a crew that was never there.
	/// <c>KingdomMaterials.WorkYard</c> now keeps the same order for the same reason. Second, the
	/// day count itself is still <c>KingdomRules.HeartbeatDays</c>, which is capped: mending is
	/// an unmigrated row (<c>KingdomRules.LegacyAbsenceCap</c>), waiting on the package that owns
	/// the rest of the worked-property family. Uncapping it is a denominator swap with nothing
	/// else attached &mdash; the labour term it needs is already here.
	/// </para>
	/// </summary>
	public static class KingdomWear
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionWear") != "No";

		/// <summary>Consecutive full-stretch attended passes a work carries right now. A plain
		/// property rather than a part field: every crewed work implicitly carries this at zero,
		/// the same way it implicitly carries <c>KingdomEffectiveness</c> at zero, and giving a
		/// sound work a whole part just to hold one counter would mean every crewed building in
		/// the game grows one.</summary>
		public const string HardRunStreakProperty = "KingdomHardRunStreak";

		/// <summary>Tick a mending under way last had labour charged against it. Read and written
		/// through <c>KingdomMaterials.ReadTick</c>/<c>WriteTick</c>, exactly as
		/// <c>KingdomMaterials.StrikeWorkedProperty</c> is: the same "day since this was last
		/// worked" accounting a strike already uses, so a founder cannot speed a mending by
		/// stepping in and out of the zone, and a long absence still resolves honestly.</summary>
		public const string RepairWorkedProperty = "KingdomRepairWorked";

		/// <summary>The property <c>KingdomGrowth.AssignWork</c> stamps a work's crew-only
		/// effectiveness onto, 0-100. Read here to learn this pass's crew stretch, and written
		/// here again to fold in this work's own wear.</summary>
		private const string EffectivenessProperty = "KingdomEffectiveness";

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			Resolve(System, Z, Survey);
		}

		private static void Resolve(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			string settlementId = KingdomChronicle.SettlementId(System.KingdomFactionName);
			long timeTicks = The.Game.TimeTicks;
			int hands = KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew);
			List<GameObject> damaged = new List<GameObject>();
			GameObject workingRepair = null;
			for (int i = 0; i < Survey.Works.Count; i++)
			{
				GameObject work = Survey.Works[i];
				if (!GameObject.Validate(work))
				{
					continue;
				}
				int crewStretch = work.GetIntProperty(EffectivenessProperty);
				RollWear(System, settlementId, work, crewStretch, timeTicks);
				r_KingdomWear wear = work.GetPart<r_KingdomWear>();
				if (wear == null || wear.Wear <= 0)
				{
					continue;
				}
				work.SetIntProperty(EffectivenessProperty, KingdomWearRules.CombinedEffectiveness(crewStretch, wear.Wear));
				damaged.Add(work);
				if (wear.RepairEffortLeft > 0 && workingRepair == null)
				{
					workingRepair = work;
				}
			}
			System.DamagedWorks = damaged.Count;
			if (damaged.Count == 0)
			{
				return;
			}
			if (workingRepair != null)
			{
				AdvanceRepair(System, workingRepair, workingRepair.RequirePart<r_KingdomWear>(), hands, timeTicks);
				AnnounceQueued(System, damaged, workingRepair);
				return;
			}
			GameObject readyWork = null;
			GameObject speaksFirst = null;
			KingdomWearRules.RepairVerdict speaksFirstVerdict = KingdomWearRules.RepairVerdict.Ready;
			for (int i = 0; i < damaged.Count; i++)
			{
				GameObject work = damaged[i];
				r_KingdomWear wear = work.RequirePart<r_KingdomWear>();
				KingdomWearRules.RepairVerdict verdict = Assess(Z, work, wear, hands);
				if (verdict == KingdomWearRules.RepairVerdict.Ready && readyWork == null)
				{
					readyWork = work;
				}
				else if (KingdomWearRules.IsBlocked(verdict) && speaksFirst == null && wear.AnnouncedBlock != (int)verdict)
				{
					speaksFirst = work;
					speaksFirstVerdict = verdict;
				}
			}
			if (readyWork != null)
			{
				StartRepair(System, readyWork, readyWork.RequirePart<r_KingdomWear>(), timeTicks);
				return;
			}
			if (speaksFirst != null)
			{
				r_KingdomWear wear = speaksFirst.RequirePart<r_KingdomWear>();
				wear.AnnouncedBlock = (int)speaksFirstVerdict;
				string line = KingdomWearRules.ReasonLine(speaksFirstVerdict, DisplayName(speaksFirst));
				if (line != null)
				{
					System.Ledger.Note("{{r|" + line + "}}");
				}
			}
		}

		/// <summary>Every OTHER damaged work says once, if it has not already, that this pass's
		/// hands went to the one mending already under way &mdash; the same "one gang, one job"
		/// news a second condemned building gets from <c>KingdomMaterials.OnSettlementPass</c>.</summary>
		private static void AnnounceQueued(KingdomSystem System, List<GameObject> Damaged, GameObject Working)
		{
			for (int i = 0; i < Damaged.Count; i++)
			{
				GameObject work = Damaged[i];
				if (work == Working)
				{
					continue;
				}
				r_KingdomWear wear = work.RequirePart<r_KingdomWear>();
				if (wear.Held || wear.AnnouncedBlock == (int)KingdomWearRules.RepairVerdict.OtherWorkUnderway)
				{
					continue;
				}
				wear.AnnouncedBlock = (int)KingdomWearRules.RepairVerdict.OtherWorkUnderway;
				string line = KingdomWearRules.ReasonLine(KingdomWearRules.RepairVerdict.OtherWorkUnderway, DisplayName(work));
				if (line != null)
				{
					System.Ledger.Note("{{K|" + line + "}}");
				}
			}
		}

		// ==================================================================================
		// The three causes.
		// ==================================================================================

		private static void RollWear(KingdomSystem System, string SettlementId, GameObject Work, int CrewStretch, long TimeTicks)
		{
			if (CrewStretch >= 100)
			{
				int streak = Work.GetIntProperty(HardRunStreakProperty) + 1;
				Work.SetIntProperty(HardRunStreakProperty, streak);
				if (KingdomWearRules.RollHardRun(SettlementId, Work.id, streak))
				{
					Damage(System, Work, KingdomWearRules.WearCause.HardRunning);
				}
			}
			else
			{
				Work.SetIntProperty(HardRunStreakProperty, 0);
			}
			if (CrewStretch > 0 && Work.GetIntProperty(KingdomSalvage.CertifiedProperty) == 1
				&& KingdomWearRules.RollTemperamental(SettlementId, Work.id, TimeTicks))
			{
				Damage(System, Work, KingdomWearRules.WearCause.TemperamentalTech);
			}
		}

		/// <summary>
		/// Raiders who got past the wall may leave one or two works worse for it. Called from
		/// <c>KingdomRaids.ExecuteRaid</c> once for a raid that actually put raiders on the
		/// ground; does nothing for one the wall turned back outright, because nothing got past
		/// it to damage anything.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Z">Zone the raid landed in.</param>
		/// <param name="Survey">This pass's survey, so the candidate list is exactly the works
		/// already known to be crewed here. A fresh survey is taken when null.</param>
		/// <param name="RaidersThrough">Raiders who made it past the wall this raid.</param>
		/// <param name="RaidTick">The raid's own due tick, so a reload asks each candidate work
		/// this exact question exactly once.</param>
		public static void OnRaidDamage(KingdomSystem System, Zone Z, KingdomSurvey Survey, int RaidersThrough, long RaidTick)
		{
			if (!Enabled || System == null || Z == null || RaidersThrough <= 0)
			{
				return;
			}
			KingdomSurvey survey = Survey ?? KingdomSurvey.Take(Z, System);
			if (survey.Works.Count == 0)
			{
				return;
			}
			int want = KingdomWearRules.WorksToDamage(RaidersThrough);
			if (want <= 0)
			{
				return;
			}
			string settlementId = KingdomChronicle.SettlementId(System.KingdomFactionName);
			int hit = 0;
			for (int i = 0; i < survey.Works.Count && hit < want; i++)
			{
				GameObject work = survey.Works[i];
				if (!GameObject.Validate(work) || !KingdomWearRules.RollRaidDamage(settlementId, work.id, RaidTick))
				{
					continue;
				}
				Damage(System, work, KingdomWearRules.WearCause.Raid);
				hit++;
			}
		}

		private static void Damage(KingdomSystem System, GameObject Work, KingdomWearRules.WearCause Cause)
		{
			r_KingdomWear wear = Work.RequirePart<r_KingdomWear>();
			int before = wear.Wear;
			wear.Wear = KingdomMaterialRules.AddWear(wear.Wear, KingdomWearRules.IncrementFor(Cause));
			wear.LastCause = (int)Cause;
			if (wear.Wear == before)
			{
				// Already at the ceiling: a real event happened, but there is nothing new to
				// report, and 7b's "once" would be violated by saying the same thing twice.
				return;
			}
			string line = KingdomWearRules.DamagedLine(DisplayName(Work), Cause, wear.Wear);
			MessageQueue.AddPlayerMessage("{{r|" + line + "}}");
			KingdomChronicle.Record(System, line);
			KingdomLog.Log("wear: damaged " + Work.Blueprint + " cause=" + Cause + " wear=" + wear.Wear);
		}

		// ==================================================================================
		// Repair: costed and timed exactly as a strike is (KingdomMaterials.WorkStrike), one job
		// settlement-wide at a time.
		// ==================================================================================

		private static KingdomWearRules.RepairVerdict Assess(Zone Z, GameObject Work, r_KingdomWear WearPart, int FreeHands)
		{
			if (WearPart.Held)
			{
				return KingdomWearRules.RepairVerdict.Held;
			}
			bool covered = Covers(Z, Work, WearPart.Wear);
			return KingdomWearRules.AssessRepair(WearPart.Held, FreeHands, covered);
		}

		private static bool Covers(Zone Z, GameObject Work, int Wear)
		{
			BuildTallies(Work, Wear, out KingdomMaterialTally cost, out KingdomBitTally bitCost);
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			return KingdomMaterialRules.Covers(stock.Tally, cost) && KingdomMaterialRules.CoversBits(stock.Bits, bitCost);
		}

		private static void BuildTallies(GameObject Work, int Wear, out KingdomMaterialTally Cost, out KingdomBitTally BitCost)
		{
			string designKey = KingdomUpgrade.DesignKeyOf(Work);
			KingdomMaterialTally buildCost = string.IsNullOrEmpty(designKey) ? null : KingdomMaterials.CostFor(designKey);
			KingdomBitTally buildBits = string.IsNullOrEmpty(designKey) ? null : KingdomMaterials.BitCostFor(designKey);
			Cost = KingdomMaterialRules.RepairCost(buildCost, Wear);
			BitCost = KingdomMaterialRules.RepairBits(buildBits, Wear);
		}

		private static void StartRepair(KingdomSystem System, GameObject Work, r_KingdomWear WearPart, long TimeTicks)
		{
			BuildTallies(Work, WearPart.Wear, out KingdomMaterialTally cost, out KingdomBitTally bitCost);
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Work.CurrentZone);
			if (!stock.Spend(cost) || !stock.SpendBits(bitCost))
			{
				// Assess already checked coverage this same pass; a failure here means the ground
				// changed under us (another spender in the same pass). Leave the work exactly as
				// it was and let the next pass ask again, rather than half-charge it.
				return;
			}
			WearPart.RepairEffortLeft = KingdomMaterialRules.RepairEffort(cost.Total() + bitCost.Total(), WearPart.Wear);
			KingdomMaterials.WriteTick(Work, RepairWorkedProperty, TimeTicks);
			WearPart.AnnouncedBlock = 0;
			string name = DisplayName(Work);
			System.Ledger.Note("{{K|" + KingdomWearRules.RepairBegunLine(name) + "}}");
			KingdomLog.Log("wear: repair begun " + Work.Blueprint + " wear=" + WearPart.Wear + " effort=" + WearPart.RepairEffortLeft);
		}

		private static void AdvanceRepair(KingdomSystem System, GameObject Work, r_KingdomWear WearPart, int Hands, long TimeTicks)
		{
			long worked = KingdomMaterials.ReadTick(Work, RepairWorkedProperty);
			if (worked <= 0)
			{
				KingdomMaterials.WriteTick(Work, RepairWorkedProperty, TimeTicks);
				return;
			}
			int days = KingdomRules.HeartbeatDays(TimeTicks - worked);
			if (days <= 0)
			{
				return;
			}
			if (Hands <= 0)
			{
				if (WearPart.AnnouncedBlock != (int)KingdomWearRules.RepairVerdict.NoHands)
				{
					WearPart.AnnouncedBlock = (int)KingdomWearRules.RepairVerdict.NoHands;
					string blockLine = KingdomWearRules.ReasonLine(KingdomWearRules.RepairVerdict.NoHands, DisplayName(Work));
					if (blockLine != null)
					{
						System.Ledger.Note("{{r|" + blockLine + "}}");
					}
				}
				KingdomMaterials.WriteTick(Work, RepairWorkedProperty, KingdomRules.HeartbeatCheckpoint(worked, TimeTicks));
				return;
			}
			WearPart.AnnouncedBlock = 0;
			KingdomMaterials.WriteTick(Work, RepairWorkedProperty, KingdomRules.HeartbeatCheckpoint(worked, TimeTicks));
			int left = WearPart.RepairEffortLeft - KingdomMaterialRules.EffortWorked(Hands, days);
			if (left > 0)
			{
				WearPart.RepairEffortLeft = left;
				return;
			}
			WearPart.RepairEffortLeft = 0;
			WearPart.Wear = 0;
			WearPart.LastCause = (int)KingdomWearRules.WearCause.None;
			string name = DisplayName(Work);
			string line = KingdomWearRules.RepairCompleteLine(name);
			MessageQueue.AddPlayerMessage("{{G|" + line + "}}");
			KingdomChronicle.Record(System, line, Accomplishment: true);
			Work.RemovePart(WearPart);
			KingdomLog.Log("wear: repair complete " + Work.Blueprint);
		}

		private static string DisplayName(GameObject Work)
		{
			return KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
		}
	}
}
