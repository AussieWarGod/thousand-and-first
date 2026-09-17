using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The negative leg of behaviour-coverage row 8: with the canvas short, the SAME production
	/// assessment refuses the ordinary tier climb by its named reason, debits nothing, and then
	/// admits it again the moment the exact bill is on the shelf.
	/// <para>
	/// The shortage is made exact by WITHHOLDING fixture-minted brush units from the camp store -
	/// a disclosed synthetic input, and never a unit the settlement or the founder placed. The
	/// units are returned in full before the positive leg runs, so the improvement pass pays the
	/// real bill out of the real store.
	/// </para>
	/// <para>
	/// The strength of this leg is the LADDER POSITION, not the refusal alone.
	/// <c>KingdomUpgradeRules.Assess</c> is ordered
	/// (<c>Growth/KingdomUpgradeRules.Assessment.cs:60-118</c>) and
	/// <c>NotEnoughMaterial</c> is its thirteenth rung, so landing exactly there proves every
	/// earlier gate - ownership, style, stage, craft, free hands, contents, water - was already
	/// satisfied when the materials refused.
	/// </para>
	/// </summary>
	internal static partial class KingdomTierUpgradeChecks
	{
		private sealed partial class Frame
		{
			private void NamedMaterialRefusal()
			{
				GameObject tent = Standing(FromKey);
				Require(tent != null && tent.IDIfAssigned == PredecessorId,
					"the shortfall leg lost the exact standing tent");
				int withdrawn = WithholdBrush();
				string failure;
				Require(!KingdomMaterials.CanPayUpgrade(Zone, FromKey, out failure),
					"the stores still cover the upgrade bill after the shortage was made exact");
				int water = KingdomGrowth.CountStoredWater(Zone);
				KingdomMaterialTally before = KingdomMaterials.Stock(Zone).Tally.Copy();
				KingdomUpgrade.Assessment shortAssessment = Assess(tent);
				Require(shortAssessment.Valid, "the short assessment was not valid at all");
				Require(shortAssessment.Verdict
					== KingdomUpgradeRules.UpgradeVerdict.NotEnoughMaterial,
					"the short assessment did not refuse for material: "
						+ shortAssessment.Verdict);
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
				RestoreBrush(withdrawn);
				Require(KingdomMaterials.CanPayUpgrade(Zone, FromKey, out failure),
					"the restored stores still do not cover the upgrade bill: " + failure);
				KingdomUpgrade.Assessment ready = Assess(tent);
				Require(ready.Valid
					&& ready.Verdict == KingdomUpgradeRules.UpgradeVerdict.Ready
					&& ready.SuccessorKey == ToKey && ready.Transition == null,
					"the supplied assessment is not an ordinary ready tier climb: "
						+ ready.Verdict);
				Require(ready.CostDrams == QuoteDrams && ready.CrewNeeded == QuoteCrew
					&& ready.BuildTicks == QuoteTicks
					&& ready.StageNeeded == GrowthStage.Camp,
					"the ready quote is not the frozen 2 drams / 1 hand / 900 ticks / Camp");
				Evidence.Append("\nshort verdict=NotEnoughMaterial")
					.Append("; withheld-brush=").Append(withdrawn)
					.Append("; reason=").Append(KingdomScenarioRules.Bounded(expected))
					.Append("; ready-drams=").Append(ready.CostDrams)
					.Append("; ready-ticks=").Append(ready.BuildTicks)
					.Append("; ready-crew=").Append(ready.CrewNeeded)
					.Append("; ready-stage=").Append(ready.StageNeeded);
			}

			/// <summary>Production's own assessment, on a bound local operation, with the free
			/// hands the settlement pass itself computes
			/// (<c>Growth/KingdomUpgrade.13.Resolve.cs:23-27</c>) and no competing work.
			/// Non-mutating: <c>KingdomUpgrade.Assess</c> only reads.</summary>
			private KingdomUpgrade.Assessment Assess(GameObject Work)
			{
				KingdomSurvey survey = KingdomSurvey.Take(Zone, System);
				Require(survey != null, "the settlement could not be surveyed to assess");
				int hands = System.Population - System.AssignedCrew;
				if (hands < 0) hands = 0;
				Require(hands >= QuoteCrew,
					"the fixture settlement has no free hand for an improvement");
				string failure;
				KingdomSurvey.PassScope scope;
				Require(KingdomSurvey.TryBindLocalOperation(Zone, System, out scope, out failure),
					failure);
				using (scope)
				{
					return KingdomUpgrade.Assess(System, Zone, Work, survey, hands, false);
				}
			}

			/// <summary>Removes fixture-minted brush units from the camp store until production
			/// says the upgrade bill cannot be paid. Only bodies this fixture minted are touched;
			/// the count is disclosed.</summary>
			private int WithholdBrush()
			{
				Require(Withheld.Count == 0, "brush was already withheld once");
				string failure;
				for (int i = 0; i < MintedBrushIds.Count; i++)
				{
					if (!KingdomMaterials.CanPayUpgrade(Zone, FromKey, out failure)) break;
					GameObject unit = MintedUnit(MintedBrushIds[i]);
					if (unit == null) continue;
					Require(unit.RemoveFromContext(), "a minted brush unit refused withdrawal");
					Require(unit.InInventory == null && unit.CurrentCell == null,
						"a withheld brush unit kept a custody");
					Withheld.Add(unit);
				}
				Require(Withheld.Count > 0,
					"the store already could not pay the upgrade bill before any withdrawal");
				return Withheld.Count;
			}

			private void RestoreBrush(int Expected)
			{
				Require(Withheld.Count == Expected, "the withheld brush count drifted");
				for (int i = 0; i < Withheld.Count; i++)
				{
					GameObject unit = Withheld[i];
					Require(ReferenceEquals(Store.Inventory.AddObject(unit, null, Silent: true,
						NoStack: true), unit), "a withheld brush unit refused its return");
				}
				Withheld.Clear();
			}

			/// <summary>The still-live minted unit with this exact identity, or null.</summary>
			private GameObject MintedUnit(string Id)
			{
				List<GameObject> held = Store.Inventory.Objects;
				for (int i = 0; i < held.Count; i++)
					if (GameObject.Validate(held[i]) && held[i].IDIfAssigned == Id)
						return held[i];
				return null;
			}
		}
	}
}
