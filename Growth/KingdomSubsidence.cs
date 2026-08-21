using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The consumer of the equilibrium arithmetic, and the slide that follows from it.
	/// <para>
	/// One reckoning per attended pass, run from <c>KingdomGrowth.UpdateStage</c> after the
	/// staffing pass has said which works are actually running. It does four things in order:
	/// sums what the finished works carry, converts that to a level at this settlement's own
	/// stage (<see cref="KingdomSubsidenceRules.SupportedLevel"/>), runs the slide forward over
	/// however much world time has passed, and tells the founder about it once.
	/// </para>
	/// <para>
	/// <b>The clock.</b> World time, uncapped, through <c>KingdomRules.ElapsedDays</c> and a
	/// checkpoint that advances by exactly the steps it cashed. The settlement lives whether the
	/// founder is there or not (Addendum 8 clause 1), so the slide runs the same length whether
	/// it is watched or not; what changes at a homecoming is only that somebody is told. The
	/// stamp is planted on the first pass before any days are counted &mdash; the same lesson
	/// <c>LastFetchTick</c> learned, where an unplanted stamp read as the age of the world.
	/// </para>
	/// <para>
	/// <b>The protection law.</b> Nothing here deletes or moves anything. Works are ruined by
	/// wear on the part the mending system already owns, capped at
	/// <c>KingdomMaterialRules.MaxWearPercent</c>, and every point of it is mendable. People
	/// leave through <c>KingdomGrowth.Emigrate</c>, which is the settlement's one departure path
	/// and floors at <c>KingdomRules.LoyalCoreSettlers</c> &mdash; and the level itself floors at
	/// <c>KingdomCatalogueRules.FloorLevel</c>, so the floor that actually binds is Camp's own
	/// equilibrium and nobody subsides out of existence.
	/// </para>
	/// <para>
	/// <b>One zone, not a realm.</b> The tally is taken from the pass's own survey, which is the
	/// zone the founder is standing in &mdash; the same ground every other per-pass resolver
	/// measures. A settlement spanning claimed zones is therefore measured by the one it is
	/// entered through, exactly as its beds, its crew and its stores already are.
	/// </para>
	/// </summary>
	public static class KingdomSubsidence
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionSubsidence") != "No";

		/// <summary>
		/// What this settlement's finished works carry between them.
		/// <para>
		/// A work that asks for crew carries at what the staffing pass gave it, reduced again by
		/// its own condition, so an unmanned field feeds nobody. That is Addendum 8 clause 2
		/// applied to the level: infrastructure times labour, never infrastructure alone.
		/// </para>
		/// <para>
		/// <b>And a work that asks for nobody carries at its CONDITION</b> (Addendum 10(b)). This
		/// used to be a flat 100 &mdash; wear reached the level only through the
		/// <c>KingdomStaffNeeded</c> gate, so a half-wrecked reservoir carried its full
		/// twenty-six drams and only the food lane, which never automates, could be hurt by ruin
		/// at all. The ruling overturned it: a ruined reservoir does not carry its full drams.
		/// Both arms are <see cref="KingdomWearRules.WorkEffectiveness"/>, which is also what
		/// <c>KingdomPower</c> asks, so the rule lives in exactly one place.
		/// </para>
		/// <para>
		/// <b>Why the condition is read off the work rather than off the stamp.</b>
		/// <c>KingdomEffectiveness</c> is the staffing pass's own crew stretch and nothing else;
		/// nobody folds wear into it any more. This function is called twice per pass from two
		/// different points in <c>KingdomGrowth</c> (the water works' daily make, at the top, and
		/// the level, after <c>AssignWork</c>), and reading condition from the part rather than
		/// from a property somebody else may or may not have already folded is what makes both
		/// answers the same arithmetic.
		/// </para>
		/// </summary>
		/// <param name="Survey">The pass's survey. Null carries nothing.</param>
		public static KingdomCatalogueRules.SupportTally Supports(KingdomSurvey Survey)
		{
			KingdomCatalogueRules.SupportTally tally = default(KingdomCatalogueRules.SupportTally);
			if (Survey == null)
			{
				return tally;
			}
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work))
				{
					continue;
				}
				string key = KingdomUpgrade.DesignKeyOf(work);
				KingdomRules.BuildEntry entry;
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
				{
					continue;
				}
				// A malformed Carries is already reported by the catalogue validator, and whatever
				// parsed before the bad pair still counts, so the verdict is deliberately unread.
				List<KindAmount> carries;
				KingdomCatalogueRules.TryParseTally(entry.Carries, out carries, out _);
				tally = KingdomCatalogueRules.FoldWork(tally, carries, KingdomWear.EffectivenessOf(work));
			}
			return tally;
		}

		/// <summary>
		/// The whole reckoning. Records the level and what holds it, runs the slide, ruins what
		/// the fall took, and speaks once each way (STANDARDS 7b).
		/// </summary>
		/// <param name="System">The seated settlement.</param>
		/// <param name="Z">The zone the pass is in.</param>
		/// <param name="Survey">The pass's survey.</param>
		/// <param name="TimeTicks">Now.</param>
		public static void Reckon(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)
		{
			if (System == null || !System.Founded || Z == null || Survey == null)
			{
				return;
			}
			KingdomCatalogueRules.SupportTally supports = Supports(Survey);
			string binding = KingdomSubsidenceRules.BindingSupportFor(supports, System.Stage);
			int level = KingdomSubsidenceRules.SupportedLevel(supports, System.Stage);
			// Recorded whether or not the slide is allowed to run: the level is knowledge, and a
			// founder who has turned subsidence off is still owed the number their works carry.
			System.SupportedLevel = level;
			System.SubsidenceBinding = binding;
			if (!Enabled)
			{
				Unsay(System, level);
				return;
			}
			if (System.LastSubsidenceTick <= 0)
			{
				System.LastSubsidenceTick = TimeTicks;
				return;
			}
			int elapsedDays = KingdomRules.ElapsedDays(TimeTicks - System.LastSubsidenceTick);
			if (elapsedDays <= 0)
			{
				return;
			}
			// A settlement inside its band, or already arrived, is not subsiding: unsay whatever
			// was said, spend the days so they cannot be banked against a future overreach, and
			// leave. This is the arrest, and it is why removing the cause stops the slide anywhere
			// along it - the level is re-derived every pass and never remembered.
			if (!KingdomSubsidenceRules.IsSubsiding(System.Population, level) && !System.SubsidenceAnnounced)
			{
				System.LastSubsidenceTick = Checkpoint(System.LastSubsidenceTick, elapsedDays / KingdomSubsidenceRules.StepDays);
				return;
			}
			if (KingdomSubsidenceRules.HasArrived(System.Population, level))
			{
				Unsay(System, level);
				System.LastSubsidenceTick = Checkpoint(System.LastSubsidenceTick, elapsedDays / KingdomSubsidenceRules.StepDays);
				return;
			}
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				System.Population, System.Stage, Survey.StorageCapacity, supports, elapsedDays, System.SubsidenceAnnounced);
			Say(System, binding, level);
			if (trajectory.Departed <= 0)
			{
				// Announced and standing above the level, but not a whole step of world time has
				// passed yet. Nothing is charged and nothing is banked.
				return;
			}
			long anchor = System.LastSubsidenceTick;
			GrowthStage from = System.Stage;
			string cause = KingdomSubsidenceRules.DepartureCause(binding);
			int departed = 0;
			int named = 0;
			// Told in rungs, sampled in names: the first few and the last of a long slide are
			// chronicled by name and everybody between them rides the summary line below, so a
			// City falling to Camp spends a modest share of the two-hundred-entry register
			// instead of a quarter of it (KingdomSubsidenceRules.ChronicleEntriesFor).
			while (departed < trajectory.Departed)
			{
				bool tell = KingdomSubsidenceRules.TellsDeparture(departed, trajectory.Departed);
				if (!KingdomGrowth.Emigrate(System, Z, Survey, null, cause, tell))
				{
					break;
				}
				departed++;
				if (tell)
				{
					named++;
				}
			}
			string summary = KingdomSubsidenceRules.SlideDepartureSummary(System.KingdomDisplayName, departed, named, cause);
			if (summary != null)
			{
				System.Ledger.Note("{{r|" + XRL.Language.Grammar.InitCap(summary) + ".}}");
				KingdomChronicle.Record(System, summary);
			}
			// Charged for exactly what was cashed. A settlement whose people are standing in
			// another claimed zone loses fewer than the trajectory called for, and keeps the rest
			// of the elapsed for the pass that can find them.
			int steps = trajectory.Steps * departed / trajectory.Departed;
			System.LastSubsidenceTick = Checkpoint(anchor, steps);
			if (departed <= 0)
			{
				return;
			}
			System.Stage = KingdomSubsidenceRules.SettledStage(from, System.Population, Survey.StorageCapacity);
			// Re-recorded against the rung the slide left, not the one it started from: the water
			// bill per head fell with the stage, so the level the founder is now looking at is a
			// different (higher) number from the one the announcement quoted.
			System.SupportedLevel = KingdomSubsidenceRules.SupportedLevel(supports, System.Stage);
			System.SubsidenceBinding = KingdomSubsidenceRules.BindingSupportFor(supports, System.Stage);
			Chronicle(System, Survey, anchor, TimeTicks, from, trajectory);
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("subsidence: level=" + level + "->" + System.SupportedLevel + " binding=" + binding
					+ " days=" + elapsedDays + " wanted=" + trajectory.Departed + " left=" + departed
					+ " pop=" + System.Population + " stage=" + System.Stage);
			}
			if (KingdomSubsidenceRules.HasArrived(System.Population, System.SupportedLevel))
			{
				Unsay(System, System.SupportedLevel);
			}
		}

		/// <summary>Moves the reckoning's stamp forward by exactly the steps just charged, keeping
		/// the part-step remainder so it counts toward the next one. The same bargain
		/// <c>KingdomRules.AdvanceCheckpoint</c> keeps, at this clock's own coarser granularity.
		/// </summary>
		private static long Checkpoint(long Previous, int Steps)
		{
			if (Steps <= 0)
			{
				return Previous;
			}
			return Previous + (long)Steps * KingdomSubsidenceRules.StepDays * KingdomRules.TicksPerDay;
		}

		// ==================================================================================
		// 7b. Once when it begins, and unsaid the moment it stops.
		// ==================================================================================

		private static void Say(KingdomSystem System, string Binding, int Level)
		{
			if (System.SubsidenceAnnounced)
			{
				return;
			}
			System.SubsidenceAnnounced = true;
			string line = KingdomSubsidenceRules.BeganNote(System.KingdomDisplayName, Binding, Level, System.Population);
			MessageQueue.AddPlayerMessage("{{r|" + line + "}}");
			System.Ledger.Note("{{r|" + line + "}}");
			KingdomChronicle.Record(System, KingdomSubsidenceRules.BeganChronicle(System.KingdomDisplayName, Binding, Level));
		}

		private static void Unsay(KingdomSystem System, int Level)
		{
			if (!System.SubsidenceAnnounced)
			{
				return;
			}
			System.SubsidenceAnnounced = false;
			string line = KingdomSubsidenceRules.ArrestedNote(System.KingdomDisplayName, Level, System.Population);
			MessageQueue.AddPlayerMessage("{{G|" + line + "}}");
			System.Ledger.Note("{{G|" + line + "}}");
			KingdomChronicle.Record(System, KingdomSubsidenceRules.ArrestedChronicle(System.KingdomDisplayName, Level));
		}

		// ==================================================================================
		// The breakpoints: the rungs, dated, and the works the fall left the worse for it.
		// ==================================================================================

		private static void Chronicle(KingdomSystem System, KingdomSurvey Survey, long Anchor, long TimeTicks,
			GrowthStage From, KingdomSubsidenceRules.Trajectory Trajectory)
		{
			if (Trajectory.Breakpoints == null)
			{
				return;
			}
			string settlementId = KingdomChronicle.SettlementId(System.KingdomFactionName);
			for (int i = 0; i < Trajectory.Breakpoints.Count; i++)
			{
				KingdomSubsidenceRules.Breakpoint breakpoint = Trajectory.Breakpoints[i];
				// Only the rungs the settlement actually reached. A slide cut short because its
				// people were standing somewhere else did not lose the rungs below where it
				// stopped, and must not claim to have.
				if (breakpoint.To < System.Stage)
				{
					continue;
				}
				long at = Anchor + (long)breakpoint.Day * KingdomRules.TicksPerDay;
				int daysAgo = KingdomRules.ElapsedDays(TimeTicks - at);
				KingdomChronicle.Record(System, KingdomSubsidenceRules.BreakpointChronicle(
					System.KingdomDisplayName, breakpoint.From, breakpoint.To, daysAgo));
				Ruin(System, Survey, settlementId, (ulong)((at > 0L) ? at : 0L), at, breakpoint.From);
			}
		}

		/// <summary>
		/// What one lost rung does to the works standing in it. Damage and nothing else: the part
		/// is the mending system's own, the ceiling is its own, and a mending puts every point of
		/// it back. Only <c>KingdomBuilt</c> works are candidates, which is the protection law's
		/// own list of what a kingdom system may touch at all.
		/// <para>
		/// <b>The reach</b> (Addendum 10(c)). Every standing work is asked, every rung, and how
		/// far the rung reaches is the rung's own scale
		/// (<see cref="KingdomSubsidenceRules.RuinChanceFor"/>). There is no quota: the flat
		/// two-works-a-rung allowance this replaced meant a City falling all the way to Camp left
		/// eight works scuffed however many dozen were standing, and every other plot pristine.
		/// Because the loop no longer stops at a count, it also cannot stop before a home that
		/// crossed the condemnation line &mdash; every crossing reaches the people under it,
		/// which is a correctness property of having no early exit rather than a check somewhere.
		/// </para>
		/// <para>
		/// <b>The telling is coarsened, the damage is not</b>: a rung that leaves eleven works the
		/// worse for it writes one named line and one that counts the rest
		/// (<see cref="KingdomSubsidenceRules.TellsRuin"/>,
		/// <see cref="KingdomSubsidenceRules.RuinSummary"/>), so the register's share of a whole
		/// collapse did not move when the reach did.
		/// </para>
		/// </summary>
		/// <param name="Ordinal">The breakpoint's own due tick, used as a draw ordinal. It sits on
		/// a fixed lattice from the reckoning's anchor and is never re-anchored, so a reload asks
		/// each work the same question and gets the same answer.</param>
		/// <param name="AtTick">The same tick as a clock rather than an ordinal, for the roof
		/// brink a condemned home owes the people who were living in it.</param>
		/// <param name="From">The rung being lost, which is how far this one reaches.</param>
		private static void Ruin(KingdomSystem System, KingdomSurvey Survey, string SettlementId, ulong Ordinal,
			long AtTick, GrowthStage From)
		{
			int ruined = 0;
			int named = 0;
			int deepest = 0;
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work) || !KingdomSubsidenceRules.RollRuin(SettlementId, work.id, Ordinal, From))
				{
					continue;
				}
				int increment = KingdomSubsidenceRules.RolledRuinIncrement(SettlementId, work.id, Ordinal);
				if (increment <= 0)
				{
					continue;
				}
				// Read before the damage lands, so the sentence names the building that stood
				// here rather than the ruin it is about to become: "the granary fell into
				// disrepair", not "the ruined granary fell into disrepair". The adjective the
				// work wears from here on is r_KingdomWear's own.
				string name = KingdomDesign.ReferenceFor(work, work.ShortDisplayName);
				r_KingdomWear wear = work.RequirePart<r_KingdomWear>();
				int before = wear.Wear;
				wear.Wear = KingdomMaterialRules.AddWear(wear.Wear, increment);
				if (wear.Wear == before)
				{
					// Already at the ceiling. A real thing happened and there is nothing new to
					// report, and 7b's "once" would be broken by saying it twice.
					continue;
				}
				ruined++;
				if (wear.Wear > deepest)
				{
					deepest = wear.Wear;
				}
				// A home the fall took past KingdomLodgingRules.CondemnedWearPercent stopped
				// being a roof on THIS day, not on the day somebody finally walked in and looked
				// at it. So the people who were living under it reach their brink here, dated
				// here, and the lodging pass that finds them later announces the honest elapsed.
				// Nothing is announced and no window is spent from inside an absence; a home that
				// was already condemned, one below the line, and one nobody sleeps in all record
				// nothing (KingdomBrink.Record is idempotent, and this only fires on the
				// crossing). It fires for EVERY home that crosses, not for the first couple: the
				// loop above has no count to stop at.
				if (KingdomLodgingRules.IsCondemned(wear.Wear) && !KingdomLodgingRules.IsCondemned(before))
				{
					int stranded = KingdomLodging.RecordCondemnedRoofBrink(work.CurrentZone, work, AtTick);
					if (stranded > 0 && KingdomLog.Enabled)
					{
						KingdomLog.Log("subsidence: condemned " + work.Blueprint + " wear=" + wear.Wear + " stranded=" + stranded);
					}
				}
				if (KingdomSubsidenceRules.TellsRuin(ruined - 1))
				{
					named++;
					string line = KingdomSubsidenceRules.RuinedWorkLine(name, System.KingdomDisplayName);
					System.Ledger.Note("{{r|" + XRL.Language.Grammar.InitCap(line) + ".}}");
					KingdomChronicle.Record(System, line);
				}
				if (KingdomLog.Enabled)
				{
					KingdomLog.Log("subsidence: ruined " + work.Blueprint + " wear=" + wear.Wear + " rung=" + From);
				}
			}
			string summary = KingdomSubsidenceRules.RuinSummary(System.KingdomDisplayName, ruined, named, deepest);
			if (summary != null)
			{
				System.Ledger.Note("{{r|" + XRL.Language.Grammar.InitCap(summary) + ".}}");
				KingdomChronicle.Record(System, summary);
			}
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("subsidence: rung=" + From + " reach=" + KingdomSubsidenceRules.RuinChanceFor(From)
					+ "% ruined=" + ruined + " deepest=" + deepest);
			}
		}
	}
}
