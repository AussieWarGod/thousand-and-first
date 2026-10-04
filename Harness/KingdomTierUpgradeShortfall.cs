using System;
using System.Collections.Generic;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The negative leg of behaviour-coverage row 8: with the canvas short, the SAME production
	/// assessment refuses the ordinary tier climb by its named reason, debits nothing, and then
	/// admits it again the moment the exact bill is on the shelf.
	/// <para>
	/// The shortage was made at setup and held across every real pass of leg one (see
	/// <see cref="UpgradeShortBy"/>): the store was minted one canvas unit short of the tent's
	/// bill plus the upgrade's, so the settlement pass could begin nothing on the standing tent.
	/// This leg mints only the missing unit - a disclosed synthetic input, never a unit the
	/// settlement or the founder placed - and requires that exactly
	/// <see cref="UpgradeShortBy"/> unit admits the bill, which proves the boundary both ways.
	/// </para>
	/// <para>
	/// The strength of this leg is the LADDER POSITION, not the refusal alone.
	/// <c>KingdomUpgradeRules.Assess</c> is ordered
	/// (<c>Growth/KingdomUpgradeRules.Assessment.cs:60-118</c>) and
	/// <c>NotEnoughMaterial</c> is its thirteenth rung, so landing exactly there proves every
	/// earlier gate - ownership, style, stage, craft, free hands, contents, water - was already
	/// satisfied when the materials refused.
	/// </para>
	/// <para>
	/// READINESS IS ASKED WITH PRODUCTION'S OWN INPUTS (docs/DEVELOPMENT.md). Every assessment
	/// here uses the free hands and the competing-work flag the settlement pass itself computes
	/// (<c>Growth/KingdomUpgrade.13.Resolve.cs</c>), never an assumed empty queue, and the
	/// journal keeps those inputs, the last announced verdict, the switches, the zoning
	/// judgement and the refusal reason beside every verdict, so a ready-looking quote the pass
	/// later declines can be told from one it began.
	/// </para>
	/// </summary>
	internal static partial class KingdomTierUpgradeChecks
	{
		/// <summary>Ledger notes journaled from the tail. The ledger keeps at most twelve until the
		/// founder's homecoming (<c>Core/KingdomLedger.cs</c>), so their count is journaled too:
		/// a full ledger silently drops every later note.</summary>
		internal const int LedgerTailNotes = 4;

		private sealed partial class Frame
		{
			private void NamedMaterialRefusal()
			{
				GameObject tent = Standing(FromKey);
				Require(tent != null && tent.IDIfAssigned == PredecessorId,
					"the shortfall leg lost the exact standing tent");
				string failure;
				Require(!KingdomMaterials.CanPayUpgrade(Zone, FromKey, out failure),
					"the stores already cover the upgrade bill before the shortfall leg");
				int water = KingdomGrowth.CountStoredWater(Zone);
				KingdomMaterialTally before = KingdomMaterials.Stock(Zone).Tally.Copy();
				string shortContext;
				KingdomUpgrade.Assessment shortAssessment = Assess(tent, out shortContext);
				Evidence.Append("\nshort-inputs ").Append(shortContext);
				Require(shortAssessment.Valid, "the short assessment was not valid at all");
				Require(shortAssessment.Verdict
					== KingdomUpgradeRules.UpgradeVerdict.NotEnoughMaterial,
					"the short assessment did not refuse for material: " + shortAssessment.Verdict
						+ " / " + KingdomScenarioRules.Bounded(shortAssessment.Reason));
				Require(KingdomUpgradeRules.IsBlocked(shortAssessment.Verdict),
					"a material refusal must speak rather than drop silently");
				string expected = KingdomUpgradeRules.ReasonLine(
					KingdomUpgradeRules.UpgradeVerdict.NotEnoughMaterial,
					KingdomDesign.ReferenceFor(tent, tent.ShortDisplayName),
					shortAssessment.Successor?.Name, shortAssessment.StageNeeded,
					shortAssessment.CrewNeeded, shortAssessment.Shortfall,
					shortAssessment.Demand.CraftDetail,
					shortAssessment.Demand.KnowledgeMissing);
				Require(!string.IsNullOrEmpty(expected) && shortAssessment.Reason == expected,
					"the refusal sentence is not the production material line");
				Require(KingdomGrowth.CountStoredWater(Zone) == water,
					"the refused assessment moved stored water");
				KingdomMaterialTally after = KingdomMaterials.Stock(Zone).Tally;
				for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
					Require(after.Get((KingdomMaterial)i) == before.Get((KingdomMaterial)i),
						"the refused assessment moved material: " + (KingdomMaterial)i);
				int supplied = SupplyMissingBrush();
				Require(supplied == UpgradeShortBy, "the upgrade shortage was not exactly "
					+ UpgradeShortBy + " canvas unit(s): supplied " + supplied);
				Require(KingdomMaterials.CanPayUpgrade(Zone, FromKey, out failure),
					"the supplied stores still do not cover the upgrade bill: " + failure);
				string readyContext;
				KingdomUpgrade.Assessment ready = Assess(tent, out readyContext);
				Evidence.Append("\nready-inputs ").Append(readyContext).Append(LedgerTail());
				Require(ready.Valid
					&& ready.Verdict == KingdomUpgradeRules.UpgradeVerdict.Ready
					&& ready.SuccessorKey == ToKey && ready.Transition == null,
					"the supplied assessment is not an ordinary ready tier climb: "
						+ ready.Verdict + " / " + KingdomScenarioRules.Bounded(ready.Reason));
				Require(ready.CostDrams == QuoteDrams && ready.CrewNeeded == QuoteCrew
					&& ready.BuildTicks == QuoteTicks
					&& ready.StageNeeded == GrowthStage.Camp,
					"the ready quote is not the frozen 2 drams / 1 hand / 900 ticks / Camp");
				Evidence.Append("\nshort verdict=NotEnoughMaterial")
					.Append("; supplied-brush=").Append(supplied)
					.Append("; reason=").Append(KingdomScenarioRules.Bounded(expected))
					.Append("; ready-drams=").Append(ready.CostDrams)
					.Append("; ready-ticks=").Append(ready.BuildTicks)
					.Append("; ready-crew=").Append(ready.CrewNeeded)
					.Append("; ready-stage=").Append(ready.StageNeeded);
			}

			/// <summary>Production's own assessment, asked with the inputs the settlement pass
			/// computes in <c>Growth/KingdomUpgrade.13.Resolve.cs</c>: free hands from the
			/// population less the assigned crew, and competing work from every working
			/// improvement plus every surveyed built work whose receipt still blocks
			/// (<c>KingdomConstruction.ReceiptBlocksCurrent</c>, which Resolve's
			/// <c>HasActiveConstruction</c> asks), read the way
			/// <c>Harness/KingdomCampHeartChainPayment.cs</c> reads them. Non-mutating:
			/// <c>KingdomUpgrade.Assess</c> only reads. <paramref name="Context"/> receives those
			/// inputs, the work's last announced verdict, the switches and the zoning judgement
			/// Begin asks again (<c>Growth/KingdomUpgrade.14.Begin.cs</c>).</summary>
			private KingdomUpgrade.Assessment Assess(GameObject Work, out string Context)
			{
				string context = null;
				KingdomUpgrade.Assessment result = OnLocalSurvey(survey =>
				{
					var competing = new List<string>();
					foreach (GameObject root in survey.Improvements)
						if (root.GetPart<r_KingdomImprovement>()?.Working == true)
							competing.Add("working:" + root.IDIfAssigned + ":" + root.Blueprint);
					foreach (GameObject root in survey.Built)
						if (KingdomConstruction.ReceiptBlocksCurrent(root))
							competing.Add("receipt:" + root.IDIfAssigned + ":" + root.Blueprint);
					int free = Math.Max(0, System.Population - System.AssignedCrew);
					KingdomUpgrade.Assessment assessment = KingdomUpgrade.Assess(System, Zone, Work,
						survey, free, competing.Count > 0);
					var part = Work.GetPart<r_KingdomImprovement>();
					context = "population=" + System.Population + "; assigned=" + System.AssignedCrew
						+ "; free=" + free + "; needed=" + assessment.CrewNeeded
						+ "; competing=" + (competing.Count == 0 ? "none" : string.Join(",", competing))
						+ "; announced=" + (part == null ? "absent"
							: ((KingdomUpgradeRules.UpgradeVerdict)part.AnnouncedReason).ToString())
						+ "; enabled=" + KingdomUpgrade.Enabled
						+ "; automatic-work=" + KingdomMaster.AutomaticWorkAllowed(System)
						+ "; zoning=" + (assessment.Successor == null ? "no-successor"
							: KingdomZoning.Judge(System, Zone.ZoneID, assessment.Successor)
								.Verdict.ToString());
					return assessment;
				});
				Context = context;
				return result;
			}

			/// <summary>The newest production ledger notes, bounded, after their count. Begin's
			/// zoning, contents and outstanding-funding outcomes are written only here
			/// (<c>Core/KingdomLedger.cs</c> keeps them in memory; they never reach Player.log).
			/// </summary>
			private string LedgerTail()
			{
				List<string> notes = System.Ledger?.Notes;
				if (notes == null) return "; ledger=absent";
				var text = new StringBuilder("; ledger-notes=").Append(notes.Count);
				for (int i = Math.Max(0, notes.Count - LedgerTailNotes); i < notes.Count; i++)
					text.Append("; ledger=").Append(KingdomScenarioRules.Bounded(notes[i]));
				return text.ToString();
			}

			/// <summary>Runs one read against the production local-operation survey, bound for
			/// exactly that read and proved released afterwards. Occupant classification reads
			/// the BOUND survey (<c>Growth/KingdomPlot2.26e.EnvelopeClearance.cs</c>), so an
			/// unbound assessment could not see a resident as movable.</summary>
			private T OnLocalSurvey<T>(Func<KingdomSurvey, T> Read)
			{
				Require(!KingdomSurvey.HasBoundPass, "a local survey read found a pass bound");
				string failure;
				KingdomSurvey.PassScope scope;
				Require(KingdomSurvey.TryBindLocalOperation(Zone, System, out scope, out failure),
					failure ?? "the local survey could not bind");
				T result;
				using (scope)
				{
					KingdomSurvey survey = KingdomSurvey.ActiveFor(Zone);
					Require(survey != null, "the bound local survey is absent");
					result = Read(survey);
				}
				Require(!KingdomSurvey.HasBoundPass, "the local survey read left its pass bound");
				return result;
			}

			/// <summary>Mints canvas into the camp store one unit at a time until production
			/// says the upgrade bill can be paid. Bounded by the bill itself.</summary>
			private int SupplyMissingBrush()
			{
				int bill = KingdomMaterials.UpgradeCostFor(FromKey).Get(KingdomMaterial.Brush);
				int supplied = 0;
				string failure;
				while (!KingdomMaterials.CanPayUpgrade(Zone, FromKey, out failure))
				{
					Require(supplied < bill,
						"the whole canvas bill was supplied and still did not cover: " + failure);
					Mint(KingdomMaterial.Brush, 1);
					supplied++;
				}
				return supplied;
			}
		}
	}
}
