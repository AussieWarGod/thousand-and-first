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
		public static bool ContentsWouldFit(GameObject Work, string SuccessorBlueprint)
		{
			if (Work == null)
			{
				return false;
			}
			int storedLiquid = 0;
			LiquidVolume volume = Work.GetPart<LiquidVolume>();
			if (volume != null && volume.Volume > 0)
			{
				if (!KingdomUpgradeContentRules.LiquidEndpointSafe(volume.MaxVolume, false,
					r_KingdomImprovement.LiquidEndpointHasContextRisk(volume))) return false;
				storedLiquid = volume.Volume;
			}
			int heldItems = (Work.Inventory != null) ? Work.Inventory.Objects.Count : 0;
			if (!KingdomUpgradeContentRules.ManifestCardinalityValid(heldItems)) return false;
			GameObjectBlueprint blueprint = string.IsNullOrEmpty(SuccessorBlueprint) ? null : GameObjectFactory.Factory.GetBlueprintIfExists(SuccessorBlueprint);
			if (blueprint == null)
			{
				return storedLiquid <= 0 && heldItems <= 0;
			}
			int capacity = 0;
			if (blueprint.HasPart("LiquidVolume"))
			{
				capacity = blueprint.HasPartParameter("LiquidVolume", "MaxVolume")
					? blueprint.GetPartParameter("LiquidVolume", "MaxVolume", KingdomUpgradeRules.UnknownCapacity)
					: KingdomUpgradeRules.UnknownCapacity;
			}
			return KingdomUpgradeRules.ContentsWouldFit(storedLiquid, capacity, heldItems, blueprint.HasPart("Inventory"));
		}

		/// <summary>Measures real craft and material requirements without mutating ground.</summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Z">Zone the work stands in.</param>
		/// <param name="Predecessor">Its registry entry, or null when it did not resolve.</param>
		/// <param name="SuccessorKey">Registry key of the design it would become.</param>
		public static KingdomUpgradeRules.ImprovementDemand MeasureRequirements(
			KingdomSystem System, Zone Z, KingdomRules.BuildEntry Predecessor,
			string SuccessorKey)
		{
			KingdomUpgradeRules.ImprovementDemand demand =
				KingdomUpgradeRules.ImprovementDemand.None;
			if (Predecessor == null)
			{
				return demand;
			}
			demand.MaterialsInHand = Z == null
				|| KingdomMaterials.CanPayUpgrade(Z, Predecessor.Key, out _);
			demand.CraftMet = CraftReaches(System, Z, SuccessorKey,
				out ZoningJudgement judgement);
			demand.CraftDetail = judgement.Detail;
			demand.KnowledgeMissing = judgement.Verdict == ZoningVerdict.RefusedUnlearned;
			return demand;
		}
	}
}
