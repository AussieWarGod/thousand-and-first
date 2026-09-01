using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomResearch
	{
		public static void EnsureBenches(KingdomSystem System, Zone Z)
		{
			KingdomSystem.Guard("research benches", delegate
			{
				if (!Enabled || System == null || !System.Founded || Z == null
					|| System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID))
				{
					return;
				}
				KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z, System);
				List<GameObject> benches = KingdomCapabilityRuntime.Roots(Z, survey,
					KingdomBenefitCapabilities.Inquiry, "research benches");
				for (int i = 0; i < benches.Count; i++)
				{
					GameObject work = benches[i];
					if (work.HasPart<XRL.World.Parts.r_KingdomInquiry>()) continue;
					work.AddPart(new XRL.World.Parts.r_KingdomInquiry());
					KingdomLog.Log("research: " + work.ShortDisplayName + " at " + System.SeatName + " is a bench");
				}
			});
		}

		internal static bool LiveBench(GameObject Bench)
		{
			Zone zone = Bench?.CurrentZone;
			KingdomSurvey survey = zone == null ? null
				: KingdomSurvey.ActiveFor(zone) ?? KingdomSurvey.Take(zone);
			return zone != null && KingdomCapabilityRuntime.HasRoot(zone, survey, Bench,
				KingdomBenefitCapabilities.Inquiry, "research bench");
		}

		internal static long PauseUnavailable(KingdomSystem System, long TimeTick,
			string LabName)
		{
			if (KingdomMaster.AutomaticWorkAllowed(System))
				Stall(System, KingdomResearchRules.UnavailableBenchLine(LabName));
			return TimeTick;
		}

		// ==================================================================================
		// The loop: one stretch of thinking, charged
		// ==================================================================================

		/// <summary>
		/// Charges one stretch of elapsed world time against the seated city's subject, at the pace
		/// this one lab's crew, condition, best mind and bench actually manage, and completes the
		/// node when the work runs out.
		/// <para>
		/// <b>Each lab charges its own stretch, from whichever is later of its own last-worked stamp
		/// and the tick the city took the subject up.</b> That is what makes a second lab THROUGHPUT
		/// rather than a second lane (RR2) while keeping idle time SPENT and never banked: a bench
		/// nobody has looked at since last winter cannot cash the winter against a subject set this
		/// morning.
		/// </para>
		/// <para>
		/// An unstaffed, unsupplied, or over-its-head lab produces nothing and says so once, and
		/// unsays it the moment the block lifts (Addendum 8 clause 2, STANDARDS 7b).
		/// </para>
		/// <para>
		/// Preconditions: none; every degenerate case answers by charging nothing. Side effects: the
		/// city's accrual, subject and stall flag may change, and a completed node mints roster
		/// keys. Failure mode: guarded, so a fault logs and charges nothing.
		/// </para>
		/// </summary>
		/// <param name="System">The realm; the seated city is the one that thinks.</param>
		/// <param name="TimeTick">Now.</param>
		/// <param name="LabLastWorkedTick">This lab's own previous stamp, 0 before its first look.</param>
		/// <param name="CrewEffectiveness">Headcount and capability combined, 0 to 100.</param>
		/// <param name="WearEffectiveness">What the lab's condition leaves of it, 0 to 100.</param>
		/// <param name="LabPercent">The bench's own rung
		/// (<see cref="KingdomResearchRules.ScriptoriumPercent"/> and its kin).</param>
		/// <param name="LabName">What the founder calls the building, for the 7b sentence.</param>
		/// <returns>The lab's new stamp, which the caller stores. Always
		/// <paramref name="TimeTick"/> once a look has happened.</returns>
		public static long Advance(KingdomSystem System, long TimeTick, long LabLastWorkedTick, int CrewEffectiveness,
			int WearEffectiveness, int LabPercent, string LabName,
			int IdentityAffinity = KingdomIdentityAffinityRules.NeutralPercent)
		{
			if (!KingdomMaster.AutomaticWorkAllowed(System)) return LabLastWorkedTick;
			KingdomSystem.Guard("research", delegate
			{
				if (!Enabled || System == null || !System.Founded)
				{
					return;
				}
				ResearchNode node;
				if (string.IsNullOrEmpty(System.ResearchSubject) || !TryGetNode(System.ResearchSubject, out node))
				{
					Stall(System, KingdomResearchRules.NoSubjectLine(LabName, KingdomPresentation.Rich(System.SeatName)));
					return;
				}
				// Addendum 17's culture/species holdings are live. Taking a subject proves the
				// door was open then, not forever: if a forbidding identity arrives, hide the
				// reason; if a required bearer leaves, name the missing source and keep all paid
				// labour. Returning the source resumes from that exact accrual.
				if (!Admissible(System, node))
				{
					Stall(System, KingdomResearchRules.ClosedSubjectLine(LabName, KingdomPresentation.Rich(System.SeatName)));
					return;
				}
				List<string> roster = KingdomZoning.Roster(System);
				List<string> missing = KingdomZoningRules.MissingKnowledge(roster, node.Requires);
				if (missing.Count > 0)
				{
					Stall(System, KingdomResearchRules.MissingSourceLine(LabName, node.Named,
						KingdomPresentation.Rich(System.SeatName), KingdomZoningRules.JoinAnd(
							KingdomZoningRules.DescribeKeys(missing))));
					return;
				}
				long from = (LabLastWorkedTick > System.ResearchTakenUpTick) ? LabLastWorkedTick : System.ResearchTakenUpTick;
				long elapsed = TimeTick - from;
				if (from <= 0L || elapsed <= 0L)
				{
					return;
				}
				int mind = BestMind(System);
				int bonus = KingdomResearchRules.TierBonus(mind, node.Tier);
				int rate = KingdomResearchRules.InquiryRate(CrewEffectiveness,
					WearEffectiveness, bonus, LabPercent, IdentityAffinity);
				if (rate <= 0)
				{
					Stall(System, KingdomResearchRules.StallLine(LabName, node.Named, CrewEffectiveness, WearEffectiveness,
						mind, KingdomResearchRules.IntelligenceForTier(node.Tier)));
					return;
				}
				Unstall(System);
				int worked = KingdomResearchRules.Worked(elapsed, rate);
				if (worked <= 0)
				{
					return;
				}
				int effort = KingdomResearchRules.EffortTicks(node.Effort);
				System.ResearchAccrued += worked;
				if (System.ResearchAccrued < effort)
				{
					return;
				}
				System.ResearchAccrued = 0;
				System.ResearchSubject = null;
				Complete(System, node, System.SeatName + "'s own bench");
			});
			return TimeTick;
		}

		private static void Stall(KingdomSystem System, string Line)
		{
			if (Line == null || System.ResearchStalledAnnounced)
			{
				return;
			}
			System.ResearchStalledAnnounced = true;
			System.Ledger.Note("{{r|" + Line + "}}");
		}

		private static void Unstall(KingdomSystem System)
		{
			System.ResearchStalledAnnounced = false;
		}

	}
}
