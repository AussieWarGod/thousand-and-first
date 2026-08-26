using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		/// <summary>Everything the settlement knows about one work's improvement, without
		/// changing anything.</summary>
		public struct Assessment
		{
			/// <summary>False when there was nothing to assess at all.</summary>
			public bool Valid;

			public KingdomUpgradeRules.UpgradeVerdict Verdict;

			/// <summary>Registry key of the standing design, or null.</summary>
			public string Key;

			/// <summary>Registry key of the design it grows into, or null.</summary>
			public string SuccessorKey;

			/// <summary>The successor's registry entry, or null when it did not resolve.</summary>
			public KingdomRules.BuildEntry Successor;

			public int CostDrams;

			public int Reserve;

			public int Shortfall;

			public int CrewNeeded;

			public GrowthStage StageNeeded;

			public long BuildTicks;

			/// <summary>Sustained output this work contributes, in drams a day &mdash; the
			/// <c>water</c> it carries. Zero for a work the settlement does not drink from.
			/// </summary>
			public int SupportPerDay;

			/// <summary>Drams the settlement goes without while this work is rebuilt, from
			/// <c>KingdomUpgradeRules.OutputLost</c>.</summary>
			public int OutputLost;

			/// <summary>Drams the stores would still hold above the reserve once the improvement
			/// is paid for and the outage borne. Negative is the dip a forced improvement takes.
			/// </summary>
			public int Margin;

			/// <summary>What the absorption law was told about this work, kept so the Charter can
			/// disclose the dip without measuring it a second time.</summary>
			public KingdomUpgradeRules.AbsorptionDemand Demand;

			/// <summary>The sentence the founder is owed, or null when the verdict correctly
			/// says nothing.</summary>
			public string Reason;

			/// <summary>Explicit same-set plan-change declaration; null for ordinary tier growth.</summary>
			public KingdomSocketTransition Transition;
		}

		/// <summary>
		/// Read-only, exact successor prepared by production preflight. Founder previews and commit
		/// share this object, so identity/variant selection cannot run twice around consent.
		/// </summary>
		public sealed class PreparedImprovement
		{
			internal string WorkId;
			internal string SourceKey;
			internal string SuccessorKey;
			internal string Payload;
			internal bool Legacy;
			public KingdomArchitectureIntent Architecture;
			public ArchitectureLayoutDelta Delta;
		}

		/// <summary>
		/// Reads one standing work against the settlement and reports what its improvement is
		/// waiting on, changing nothing. Safe to call for a listing.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the work stands in.</param>
		/// <param name="Work">The standing work.</param>
		/// <param name="Survey">This pass's survey, for the stores.</param>
		/// <param name="FreeHands">Settlers not already spoken for.</param>
		/// <param name="OtherWorkUnderway">Whether another improvement is already going on this
		/// ground.</param>
		public static Assessment Assess(KingdomSystem System, Zone Z, GameObject Work, KingdomSurvey Survey, int FreeHands, bool OtherWorkUnderway)
		{
			Assessment assessment = default;
			if (System == null || !System.Founded || Z == null || Survey == null)
			{
				return assessment;
			}
			if (Work == null || !GameObject.Validate(Work))
			{
				return assessment;
			}
			assessment.Valid = true;
			assessment.Key = DesignKeyOf(Work);
			if (!TryGetChain(assessment.Key, out KingdomUpgradeRules.UpgradeChain chain))
			{
				assessment.Verdict = KingdomUpgradeRules.UpgradeVerdict.NoSuccessor;
				return assessment;
			}
			assessment.SuccessorKey = chain.SuccessorKey;
			bool known = KingdomData.TryGetBuilding(chain.SuccessorKey, out KingdomRules.BuildEntry successor)
				&& GameObjectFactory.Factory.GetBlueprintIfExists(successor.Blueprint) != null;
			assessment.Successor = known ? successor : null;
			KingdomRules.BuildEntry predecessor;
			int predecessorCost = KingdomData.TryGetBuilding(assessment.Key, out predecessor) ? predecessor.CostDrams : 0;
			assessment.CostDrams = KingdomUpgradeRules.CostDrams(known ? successor.CostDrams : 0, predecessorCost, chain.CostDramsOverride);
			assessment.BuildTicks = KingdomUpgradeRules.BuildTicks(known ? successor.BuildTicks : 0L, chain.BuildTicksOverride);
			assessment.CrewNeeded = KingdomUpgradeRules.CrewRequired(known ? successor.Staff : 0, chain.CrewOverride);
			assessment.StageNeeded = KingdomUpgradeRules.StageRequired(known ? successor.MinStage : GrowthStage.Camp, chain.HasMinStageOverride, chain.MinStageOverride);
			assessment.Reserve = KingdomUpgradeRules.ReserveDrams(System.Population, System.Stage);
			assessment.Shortfall = KingdomUpgradeRules.Shortfall(Survey.StoredWater, assessment.CostDrams, assessment.Reserve);
			// The absorption law (brief, Addendum 3). Measured here, judged in the rules half.
			assessment.Demand = MeasureAbsorption(System, Z, Work, predecessor,
				assessment.SuccessorKey, assessment.BuildTicks, Survey);
			assessment.SupportPerDay = assessment.Demand.SupportPerDay;
			assessment.OutputLost = KingdomUpgradeRules.OutputLost(assessment.Demand.SupportPerDay, assessment.BuildTicks);
			assessment.Margin = KingdomUpgradeRules.AbsorptionMargin(Survey.StoredWater, assessment.CostDrams, assessment.Reserve, assessment.OutputLost);
			r_KingdomImprovement improvement = Work.GetPart<r_KingdomImprovement>();
			assessment.Verdict = KingdomUpgradeRules.Assess(
				HasSuccessor: true,
				SuccessorKnown: known,
				StyleAllowed: !known || KingdomRules.StyleAllows(successor.Styles, System.Style),
				OurWork: Work.GetIntProperty(BuiltProperty) == 1 && Work.GetIntProperty(AdoptedProperty) != 1,
				AlreadyWorking: (improvement != null && improvement.Working)
					|| HasActiveConstruction(Work),
				HeldOnThisGround: IsGroundHeld(Z),
				HeldByFounder: improvement != null && improvement.Held,
				Stage: System.Stage,
				StageNeeded: assessment.StageNeeded,
				FreeHands: FreeHands,
				CrewNeeded: assessment.CrewNeeded,
				ContentsFit: ContentsWouldFit(Work, known ? successor.Blueprint : null),
				StoredWater: Survey.StoredWater,
				Cost: assessment.CostDrams,
				Reserve: assessment.Reserve,
				OtherWorkUnderway: OtherWorkUnderway,
				Absorption: assessment.Demand);
			assessment.Reason = KingdomUpgradeRules.ReasonLine(assessment.Verdict, KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName), known ? successor.Name : null, assessment.StageNeeded, assessment.CrewNeeded, assessment.Shortfall);
			// An improvement climbs within the ground it was staked on. When the next tier wants more
			// of the plot than the founder staked, or the ground it would grow onto is where a
			// household's yard trade stands, the founder is told by name and chooses.
			if (KingdomUpgradeRules.IsReady(assessment.Verdict)
				&& KingdomPlots.GrowRefused(Work, assessment.SuccessorKey, out string groundRefusal))
			{
				assessment.Verdict = KingdomUpgradeRules.UpgradeVerdict.NoGroundToGrow;
				assessment.Reason = groundRefusal;
			}
			return assessment;
		}

		/// <summary>
		/// Whether everything the predecessor is carrying would have somewhere to go. Read off
		/// the successor's BLUEPRINT rather than a created object, because this is asked before
		/// anything is built and the answer must be able to refuse.
		/// </summary>
		/// <param name="Work">The standing work.</param>
		/// <param name="SuccessorBlueprint">Blueprint of what it would become. Null fits
		/// nothing that is being carried.</param>
	}
}
