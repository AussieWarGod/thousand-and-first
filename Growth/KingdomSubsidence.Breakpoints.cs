using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomSubsidence
	{
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
			string settlementId = KingdomChronicle.SettlementId(System);
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
					KingdomPresentation.Rich(System.KingdomDisplayName), breakpoint.From, breakpoint.To, daysAgo));
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
				if (!GameObject.Validate(work) || !KingdomSubsidenceRules.RollRuin(SettlementId, work.ID, Ordinal, From))
				{
					continue;
				}
				int increment = KingdomSubsidenceRules.RolledRuinIncrement(SettlementId, work.ID, Ordinal);
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
					string line = KingdomSubsidenceRules.RuinedWorkLine(name, KingdomPresentation.Rich(System.KingdomDisplayName));
					System.Ledger.Note("{{r|" + XRL.Language.Grammar.InitCap(line) + ".}}");
					KingdomChronicle.Record(System, line);
				}
				if (KingdomLog.Enabled)
				{
					KingdomLog.Log("subsidence: ruined " + work.Blueprint + " wear=" + wear.Wear + " rung=" + From);
				}
			}
			string summary = KingdomSubsidenceRules.RuinSummary(KingdomPresentation.Rich(System.KingdomDisplayName), ruined, named, deepest);
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
		}	}
}
