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

			/// <summary>Exact present-tense craft and material requirements.</summary>
			public KingdomUpgradeRules.ImprovementDemand Demand;

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
			// Infrastructure is frozen into the quoted duration before affordability and debit are
			// judged. Craft districts use the same bounded best-wins percent as fresh work;
			// an authored positive UpgradeTicks remains exact inside BuildTicks.
			int infrastructureDurationPercent = KingdomRules.DistrictsBuildPercent(
				System.ZoneDistricts.Values);
			assessment.BuildTicks = KingdomUpgradeRules.BuildTicks(
				known ? successor.BuildTicks : 0L, chain.BuildTicksOverride,
				infrastructureDurationPercent);
			assessment.CrewNeeded = KingdomUpgradeRules.CrewRequired(known ? successor.Staff : 0, chain.CrewOverride);
			assessment.StageNeeded = KingdomUpgradeRules.StageRequired(known ? successor.MinStage : GrowthStage.Camp, chain.HasMinStageOverride, chain.MinStageOverride);
			assessment.Reserve = KingdomUpgradeRules.ReserveDrams(System.Population, System.Stage);
			assessment.Shortfall = KingdomUpgradeRules.Shortfall(Survey.StoredWater, assessment.CostDrams, assessment.Reserve);
			assessment.Demand = MeasureRequirements(System, Z, predecessor,
				assessment.SuccessorKey);
			r_KingdomImprovement improvement = Work.GetPart<r_KingdomImprovement>();
			assessment.Verdict = KingdomUpgradeRules.Assess(
				HasSuccessor: true,
				SuccessorKnown: known,
				StyleAllowed: !known || KingdomRules.StyleAllows(successor.Styles,
					KingdomData.StyleKeys(System.Style)),
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
				Demand: assessment.Demand);
			assessment.Reason = KingdomUpgradeRules.ReasonLine(assessment.Verdict,
				KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName),
				known ? successor.Name : null, assessment.StageNeeded, assessment.CrewNeeded,
				assessment.Shortfall, assessment.Demand.CraftDetail,
				assessment.Demand.KnowledgeMissing);
			// Current authored layouts must reach the same exact preflight used by Begin. This admits
			// declared a4 envelope growth and refuses occupied annexed ground before the menu calls a
			// mutating path. Save-era layouts retain the narrower legacy footprint/yard check.
			if (KingdomUpgradeRules.IsReady(assessment.Verdict)
				&& ImprovementGroundRefused(System, Z, Work, assessment,
					out string groundRefusal))
			{
				assessment.Verdict = KingdomUpgradeRules.UpgradeVerdict.NoGroundToGrow;
				assessment.Reason = groundRefusal;
			}
			return assessment;
		}

		private static bool ImprovementGroundRefused(KingdomSystem System, Zone Z,
			GameObject Work, Assessment A, out string Refusal)
		{
			Refusal = null;
			if (!TryPrepareImprovementPayload(System, Z, Work, A, out string ignoredPayload,
				out KingdomArchitectureIntent ignoredArchitecture,
				out ArchitectureLayoutDelta ignoredDelta, out bool legacy, out string failure))
			{
				Refusal = string.IsNullOrEmpty(failure)
					? "The authored improvement cannot prove safe ground."
					: failure;
				return true;
			}
			return legacy && KingdomPlots.GrowRefused(Work, A.SuccessorKey, out Refusal);
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
