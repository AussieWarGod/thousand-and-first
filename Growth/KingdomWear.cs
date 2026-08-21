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
		/// Tick this work's LEAK was last cashed, for a work that stores something (Addendum
		/// 10(b)). Zero means the leak has never been counted, and the first pass that looks
		/// PLANTS the stamp rather than counting from it &mdash; the lesson <c>LastFetchTick</c>
		/// learned, where an unplanted stamp read as the age of the world. Per-work state on the
		/// work's own part, so nothing on the settlement seat has to know that stores leak.
		/// </summary>
		public long LastLeakTick;

		/// <summary>Whether the founder has already been told this store is losing what it holds
		/// (STANDARDS 7b). Said once, and unsaid the moment a mending finishes &mdash; which is
		/// also the moment this whole part is removed, so it can never outlive the leak it
		/// records.</summary>
		public bool LeakAnnounced;

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
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID || ID == GetDisplayNameEvent.ID;
		}

		/// <summary>
		/// Puts the stage of ruin into the work's own NAME (Addendum 10(c)): a settlement that
		/// fell reads as a field of ruins on the map and in every list it appears in, not as
		/// pristine buildings with quiet arithmetic against them.
		/// <para>
		/// The ladder is <c>KingdomMaterialRules.ConditionAdjective</c>, which is a function of
		/// the wear and of nothing else &mdash; so a mending walks the name back down exactly the
		/// stages the ruin walked it up, and the last of it goes when this part does. A given
		/// name survives all of it: this ADDS an adjective the engine composes, it does not
		/// replace anything, so "the ruined Cistern of Six Winters" is still hers.
		/// </para>
		/// </summary>
		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			string adjective = KingdomMaterialRules.ConditionAdjective(Wear);
			if (!string.IsNullOrEmpty(adjective))
			{
				E.AddAdjective(adjective);
			}
			return base.HandleEvent(E);
		}

		/// <summary>
		/// Puts the work's own condition on the work itself, so the founder can read it by
		/// looking at the thing rather than only in the Status report. What it LOOKS like first
		/// (Addendum 10(c)), then the arithmetic, then whatever the mending is doing about it.
		/// </summary>
		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			string look = KingdomMaterialRules.ConditionLook(Wear);
			if (!string.IsNullOrEmpty(look))
			{
				E.Postfix.Append("\n").Append(look);
			}
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
	/// and a fourth, a lost rung, reaches a staffless work too (<c>KingdomSubsidence.Ruin</c>).
	/// Nothing else does. Absence never wears anything: every draw in
	/// <see cref="KingdomWearRules"/> is keyed to an event a real pass produced, never to elapsed
	/// time. What already-damaged works go on LOSING does run on world days, which is a
	/// consequence of the damage rather than a second cause of it.
	/// <para>
	/// A damaged work keeps working, at <c>KingdomMaterialRules.ConditionPercent(Wear)</c> of
	/// what it manages whole, and says so once (STANDARDS 7b) the moment it happens. That
	/// reduction reaches EVERY work, crewed or not (Addendum 10(b),
	/// <see cref="KingdomWearRules.WorkEffectiveness"/>), and on top of it damage has
	/// kind-appropriate consequences: a store loses what it holds (<see cref="Leak"/>), a power
	/// work makes less. Mending is a materials-and-hands job, auto-queued like an improvement but always
	/// visible (<c>r_KingdomWear.HandleEvent</c>) and holdable (<see cref="r_KingdomWear.Held"/>):
	/// one job at a time settlement-wide, the same "one gang, one job" law
	/// <c>KingdomMaterials.OnSettlementPass</c> already keeps for striking and clearing, costed
	/// and timed the same way a strike is &mdash; <c>KingdomMaterialRules.RepairCost</c>/
	/// <c>RepairBits</c> for what it costs, <c>RepairEffort</c> and
	/// <c>KingdomRules.ElapsedDays</c> for how long it takes. Nothing here spends water, and
	/// nothing here ever fails a work past <see cref="KingdomMaterialRules.MaxWearPercent"/>.
	/// </para>
	/// <para>
	/// <b>The clock.</b> <see cref="AdvanceRepair"/> is the reference for checkpoint ordering in
	/// this mod: it reads the gate, names the block once (STANDARDS 7b), and only then advances
	/// the stamp &mdash; so a mending nobody has hands for loses those days rather than banking
	/// them for a crew that was never there. <c>KingdomMaterials.WorkYard</c> keeps the same
	/// order for the same reason. The day count is the full elapsed, uncapped (Addendum 8
	/// clause 1): a crew mends through an absence exactly as it mends through a fortnight of
	/// visits, and what stops a season away from mending everything is that ordering &mdash;
	/// hands first, and one mending settlement-wide at a time. Idle hands put nothing back.
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

		/// <summary>
		/// The property <c>KingdomGrowth.AssignWork</c> stamps a work's crew-only effectiveness
		/// onto, 0-100. Read here to learn this pass's crew stretch, and never written: this file
		/// used to fold the work's own condition back into it, which made the property mean two
		/// different things at two different points in the same pass and quietly double-counted
		/// wear for anything that read it before the next staffing pass. It is now exactly one
		/// thing everywhere &mdash; what the CREW manages &mdash; and every consumer folds
		/// condition in for itself through <see cref="KingdomWearRules.WorkEffectiveness"/>.
		/// </summary>
		private const string EffectivenessProperty = "KingdomEffectiveness";

		/// <summary>The design's declared crew demand, as the staffing pass stamps it. Zero means
		/// the work asks for nobody, which after Addendum 10(b) no longer means it is immune to
		/// its own damage.</summary>
		private const string StaffNeededProperty = "KingdomStaffNeeded";

		/// <summary>The founder's mark on a vessel dedicated to the settlement's water. A store
		/// carrying it is a work whose CONTENTS can run out of a hole in it.</summary>
		private const string StoresProperty = "KingdomStores";

		/// <summary>
		/// One work's own wear, 0 when it carries no record at all. The single reader every
		/// consumer of <see cref="KingdomWearRules.WorkEffectiveness"/> goes through, so "absent
		/// means sound" is stated once rather than re-derived at four call sites.
		/// </summary>
		/// <param name="Work">Any object. Null and unvalidated read as sound.</param>
		public static int WearOf(GameObject Work)
		{
			if (!GameObject.Validate(Work))
			{
				return 0;
			}
			r_KingdomWear wear = Work.GetPart<r_KingdomWear>();
			return (wear != null && wear.Wear > 0) ? wear.Wear : 0;
		}

		/// <summary>
		/// What one finished work is worth to the settlement this pass, crewed or not: the
		/// staffing pass's own stretch for a work that asks for crew, its bare condition for one
		/// that does not, and 100 for a sound work either way (Addendum 10(b)).
		/// </summary>
		/// <param name="Work">A finished work. Null reads as carrying nothing.</param>
		public static int EffectivenessOf(GameObject Work)
		{
			if (!GameObject.Validate(Work))
			{
				return 0;
			}
			return KingdomWearRules.WorkEffectiveness(
				Work.GetIntProperty(StaffNeededProperty), Work.GetIntProperty(EffectivenessProperty), WearOf(Work));
		}

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
			// Everything the settlement finished, not only the works that ask for crew. Damage
			// reaches a staffless design (KingdomSubsidence.Ruin walks this same list), so mending
			// has to reach it back: a cistern the fall holed was previously damaged forever,
			// because nothing ever put it in front of the repair queue. Addendum 10(b) makes the
			// damage count against the level, and "mending restores function" is only true if the
			// mending can start.
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work))
				{
					continue;
				}
				// The two attended causes are causes of RUNNING, so they are only ever asked of a
				// work with a crew on it. A cistern is not run hard and a palisade does not act up.
				if (work.GetIntProperty(StaffNeededProperty) > 0)
				{
					RollWear(System, settlementId, work, work.GetIntProperty(EffectivenessProperty), timeTicks);
				}
				r_KingdomWear wear = work.GetPart<r_KingdomWear>();
				if (wear == null || wear.Wear <= 0)
				{
					continue;
				}
				// The kind-appropriate consequence, on top of the general effectiveness scale every
				// consumer now applies for itself (KingdomWearRules.WorkEffectiveness): a damaged
				// store loses what it is holding, on world time, until somebody mends it.
				Leak(System, Survey, work, wear, timeTicks);
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
			// Read before the damage lands. From here on the work wears its own stage of ruin in
			// its name (r_KingdomWear.HandleEvent, Addendum 10(c)), and the sentence about what
			// just happened should name the building that stood a moment ago - "the mill was
			// broken open", not "the ruined mill was broken open".
			string name = DisplayName(Work);
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
			string line = KingdomWearRules.DamagedLine(name, Cause, wear.Wear);
			MessageQueue.AddPlayerMessage("{{r|" + line + "}}");
			KingdomChronicle.Record(System, line);
			KingdomLog.Log("wear: damaged " + Work.Blueprint + " cause=" + Cause + " wear=" + wear.Wear);
		}

		// ==================================================================================
		// The kind-appropriate consequence (Addendum 10(b)): a damaged STORE loses what it holds.
		//
		// The clock is the P1 substrate and nothing else: KingdomRules.ElapsedDays over a stamp
		// that lives on the work's own part, planted on the first pass that looks at it and never
		// counted from zero. Days that produced no loss are BANKED rather than spent, so a small
		// store whose daily share rounds to nothing still empties honestly over a season, and a
		// founder cannot stop a leak by stepping in and out of the zone. Loss, not transfer: this
		// is water going into the ground, not the manifest's pour-on-ground surplus.
		// ==================================================================================

		private static void Leak(KingdomSystem System, KingdomSurvey Survey, GameObject Work, r_KingdomWear Wear, long TimeTicks)
		{
			if (Work.GetIntProperty(StoresProperty) == 1)
			{
				LiquidVolume vessel = Work.GetPart<LiquidVolume>();
				if (vessel != null && vessel.MaxVolume > 0)
				{
					LeakWater(System, Survey, Work, Wear, vessel, TimeTicks);
				}
				return;
			}
			if (Work.GetPart<r_KingdomPowerStore>() != null)
			{
				Capacitor bed = Work.GetPart<Capacitor>();
				if (bed != null && bed.MaxCharge > 0)
				{
					LeakCharge(System, Work, Wear, bed, TimeTicks);
				}
			}
		}

		private static void LeakWater(KingdomSystem System, KingdomSurvey Survey, GameObject Work, r_KingdomWear Wear,
			LiquidVolume Vessel, long TimeTicks)
		{
			int days = DueDays(Wear, TimeTicks);
			if (days <= 0)
			{
				return;
			}
			int wanted = KingdomWearRules.Leaked(Vessel.MaxVolume, Vessel.Volume, Wear.Wear, days);
			if (wanted <= 0)
			{
				// A dry vessel has nothing to lose, so its days are spent rather than banked - a
				// hole in an empty cistern does not owe the settlement anything once it is filled
				// again. A vessel that HAS something and merely rounded to nothing keeps its days.
				if (Vessel.Volume <= 0)
				{
					Wear.LastLeakTick = KingdomRules.AdvanceCheckpoint(Wear.LastLeakTick, TimeTicks);
				}
				return;
			}
			int lost = Survey.LeakFrom(Vessel, wanted);
			Wear.LastLeakTick = KingdomRules.AdvanceCheckpoint(Wear.LastLeakTick, TimeTicks);
			if (lost > 0)
			{
				SayLeak(System, Work, Wear, KingdomWearRules.LeakKind.Water, lost, days);
			}
		}

		private static void LeakCharge(KingdomSystem System, GameObject Work, r_KingdomWear Wear, Capacitor Bed, long TimeTicks)
		{
			int days = DueDays(Wear, TimeTicks);
			if (days <= 0)
			{
				return;
			}
			int wanted = KingdomWearRules.Leaked(Bed.MaxCharge, Bed.Charge, Wear.Wear, days);
			if (wanted <= 0)
			{
				if (Bed.Charge <= 0)
				{
					Wear.LastLeakTick = KingdomRules.AdvanceCheckpoint(Wear.LastLeakTick, TimeTicks);
				}
				return;
			}
			// Measured from the capacitor before and after, the way KingdomPower's own deposits and
			// withdrawals are, rather than taken on the word of the call.
			int before = Bed.Charge;
			Bed.UseCharge(wanted);
			int lost = before - Bed.Charge;
			Wear.LastLeakTick = KingdomRules.AdvanceCheckpoint(Wear.LastLeakTick, TimeTicks);
			if (lost > 0)
			{
				SayLeak(System, Work, Wear, KingdomWearRules.LeakKind.Charge, lost, days);
			}
		}

		/// <summary>Whole world days this store's leak is owed, planting the stamp on the first
		/// pass that ever asks. Zero means nothing is owed and nothing is spent.</summary>
		private static int DueDays(r_KingdomWear Wear, long TimeTicks)
		{
			if (Wear.LastLeakTick <= 0)
			{
				Wear.LastLeakTick = TimeTicks;
				return 0;
			}
			return KingdomRules.ElapsedDays(TimeTicks - Wear.LastLeakTick);
		}

		/// <summary>Once, by name, when a store first actually loses something (STANDARDS 7b).
		/// Unsaid by <see cref="AdvanceRepair"/> the moment the mending finishes.</summary>
		private static void SayLeak(KingdomSystem System, GameObject Work, r_KingdomWear Wear,
			KingdomWearRules.LeakKind Kind, int Lost, int Days)
		{
			if (Wear.LeakAnnounced)
			{
				return;
			}
			Wear.LeakAnnounced = true;
			string line = KingdomWearRules.LeakBegunLine(DisplayName(Work), Kind);
			MessageQueue.AddPlayerMessage("{{r|" + XRL.Language.Grammar.InitCap(line) + "}}");
			System.Ledger.Note("{{r|" + XRL.Language.Grammar.InitCap(line) + "}}");
			KingdomChronicle.Record(System, line);
			KingdomLog.Log("wear: leak " + Work.Blueprint + " kind=" + Kind + " lost=" + Lost + " days=" + Days + " wear=" + Wear.Wear);
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
			int days = KingdomRules.ElapsedDays(TimeTicks - worked);
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
				KingdomMaterials.WriteTick(Work, RepairWorkedProperty, KingdomRules.AdvanceCheckpoint(worked, TimeTicks));
				return;
			}
			WearPart.AnnouncedBlock = 0;
			KingdomMaterials.WriteTick(Work, RepairWorkedProperty, KingdomRules.AdvanceCheckpoint(worked, TimeTicks));
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
			// The unsaying 7b owes a store that was announced as leaking. Said here rather than
			// left to the leak pass, because the part carrying the memory of the announcement is
			// removed on the next line: mending restores function, and the consequence ends with
			// the damage rather than with the history of it (Addendum 10(b)).
			if (WearPart.LeakAnnounced)
			{
				string held = KingdomWearRules.LeakStoppedLine(name, LeakKindOf(Work));
				MessageQueue.AddPlayerMessage("{{G|" + XRL.Language.Grammar.InitCap(held) + "}}");
				System.Ledger.Note("{{G|" + XRL.Language.Grammar.InitCap(held) + "}}");
			}
			Work.RemovePart(WearPart);
			KingdomLog.Log("wear: repair complete " + Work.Blueprint);
		}

		private static string DisplayName(GameObject Work)
		{
			return KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
		}

		/// <summary>Which kind of contents this work stores, for the sentence a leak is told in.
		/// Water is the default because the vessel is the ordinary case; a work that stores
		/// nothing never reaches either line.</summary>
		private static KingdomWearRules.LeakKind LeakKindOf(GameObject Work)
		{
			return (Work.GetIntProperty(StoresProperty) != 1 && Work.GetPart<r_KingdomPowerStore>() != null)
				? KingdomWearRules.LeakKind.Charge
				: KingdomWearRules.LeakKind.Water;
		}
	}
}
