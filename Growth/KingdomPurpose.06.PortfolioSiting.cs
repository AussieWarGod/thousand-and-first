using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool TryPortfolioSiteProof(KingdomSystem System, Zone Z,
			KingdomPurposeDefinition Definition, HashSet<string> Standing, KingdomSurvey Survey,
			out string Proof, out GameObject Specialist, out string Failure)
		{
			Proof = null;
			Specialist = null;
			Failure = null;
			if (Survey == null || Survey.Settlers == null || Survey.Settlers.Count < 6)
				return Fail("This purpose wants six present citizens before its XL ground is committed.",
					out Failure);
			if (!KingdomPurposeRules.ProducersSatisfied(Definition.ProducerSpec, Standing,
				out string missing))
				return Fail("This purpose wants the local precursor group {{C|"
					+ (missing ?? Definition.ProducerSpec).Replace('|', '/') + "}}.", out Failure);
			if (Definition.Site == KingdomPurposeSite.DeepDelve)
				return TryDeepSite(System, Z, Survey, out Proof, out Specialist, out Failure);
			if (Definition.Site == KingdomPurposeSite.ForgeQuench)
				return TryForgeSite(Z, Survey, out Proof, out Specialist, out Failure);
			if (Definition.Site == KingdomPurposeSite.HarvestWater)
				return TryHarvestSite(Z, Survey, out Proof, out Specialist, out Failure);
			return Fail("The purpose names no implemented portfolio site predicate.", out Failure);
		}

		private static bool TryDeepSite(KingdomSystem System, Zone Z, KingdomSurvey Survey,
			out string Proof, out GameObject Specialist, out string Failure)
		{
			Proof = null;
			Specialist = null;
			Failure = null;
			if (!KingdomPlotRules.IsUnderground(Z.Z) || Survey.OpenWater > 0
				|| !TryDelveFoot(System, Z.ZoneID, out KingdomDelveLinkReceipt link))
				return Fail("The Deep-Bore wants claimed dry deep ground at the exact foot of a standing reciprocal delve.",
					out Failure);
			Specialist = PortfolioForeman(Z, Survey.Settlers, KingdomPurposeKind.Deep);
			if (!GameObject.Validate(Specialist))
				return Fail("The Deep-Bore wants one present Strength-18 or Tinkering foreman.",
					out Failure);
			string specialistId = Specialist.IDIfAssigned;
			if (string.IsNullOrEmpty(specialistId))
				return Fail("The Deep-Bore foreman lacks assigned identity.", out Failure);
			Proof = "delve=" + link.Token + ";dry-foot=" + Z.ZoneID
				+ ";foreman=" + specialistId;
			return true;
		}

		private static bool TryForgeSite(Zone Z, KingdomSurvey Survey, out string Proof,
			out GameObject Specialist, out string Failure)
		{
			Proof = null;
			Specialist = null;
			Failure = null;
			GameObject quench = FirstFreshStore(Survey);
			if (!GameObject.Validate(quench))
				return Fail("The Great Foundry wants a dedicated vessel holding fresh quench water.",
					out Failure);
			Specialist = PortfolioForeman(Z, Survey.Settlers, KingdomPurposeKind.Forge);
			if (!GameObject.Validate(Specialist))
				return Fail("The Great Foundry wants one present Intelligence-18 or Tinkering foreman.",
					out Failure);
			string quenchId = quench.IDIfAssigned;
			string specialistId = Specialist.IDIfAssigned;
			if (string.IsNullOrEmpty(quenchId) || string.IsNullOrEmpty(specialistId))
				return Fail("The foundry site proof lacks assigned identity.", out Failure);
			Proof = "quench=" + quenchId + ";foreman=" + specialistId;
			return true;
		}

		private static bool TryHarvestSite(Zone Z, KingdomSurvey Survey, out string Proof,
			out GameObject Specialist, out string Failure)
		{
			Proof = null;
			Specialist = null;
			Failure = null;
			if (Survey.CropRows == null || Survey.CropRows.Count < 1)
				return Fail("The Granary-Colossus wants real planted crop rows on this ground.",
					out Failure);
			GameObject mill = null;
			for (int i = 0; i < Survey.Built.Count; i++)
				if (KingdomCrops.IsMill(Survey.Built[i])) { mill = Survey.Built[i]; break; }
			if (!GameObject.Validate(mill))
				return Fail("The Granary-Colossus wants a standing physical mill on this ground.",
					out Failure);
			GameObject water = FirstFreshStore(Survey);
			if (!GameObject.Validate(water))
				return Fail("The Granary-Colossus wants a dedicated vessel holding fresh water.",
					out Failure);
			Specialist = PortfolioForeman(Z, Survey.Settlers, KingdomPurposeKind.Harvest);
			if (!GameObject.Validate(Specialist))
				return Fail("The Granary-Colossus wants one present Harvestry or Customs steward.",
					out Failure);
			string cropId = Survey.CropRows[0]?.IDIfAssigned;
			string millId = mill.IDIfAssigned;
			string waterId = water.IDIfAssigned;
			string specialistId = Specialist.IDIfAssigned;
			if (string.IsNullOrEmpty(cropId) || string.IsNullOrEmpty(millId)
				|| string.IsNullOrEmpty(waterId) || string.IsNullOrEmpty(specialistId))
				return Fail("The harvest site proof lacks assigned identity.", out Failure);
			Proof = "crop-row=" + cropId + ";mill=" + millId
				+ ";water=" + waterId + ";steward=" + specialistId;
			return true;
		}

		private static bool TryDelveFoot(KingdomSystem System, string FootZoneId,
			out KingdomDelveLinkReceipt Receipt)
		{
			Receipt = null;
			if (System?.ClaimedZones == null || !System.ClaimedZones.Contains(FootZoneId))
				return false;
			for (int i = 0; i < System.ClaimedZones.Count; i++)
			{
				string head = System.ClaimedZones[i];
				if (KingdomDelveLink.TryReadPhysicalReceipt(head, out var candidate)
					&& candidate.FootZoneId == FootZoneId
					&& KingdomDelveLink.PhysicalLinkStands(head))
				{
					Receipt = candidate;
					return true;
				}
			}
			return false;
		}

		private static GameObject FirstFreshStore(KingdomSurvey Survey)
		{
			List<GameObject> found = new List<GameObject>();
			for (int i = 0; Survey?.Stores != null && i < Survey.Stores.Count; i++)
			{
				LiquidVolume liquid = Survey.Stores[i];
				if (liquid?.ParentObject != null && KingdomLiquids.HasFreshWater(liquid)
					&& !string.IsNullOrEmpty(liquid.ParentObject.IDIfAssigned))
					found.Add(liquid.ParentObject);
			}
			found.Sort((a, b) => string.CompareOrdinal(a.IDIfAssigned, b.IDIfAssigned));
			return found.Count == 0 ? null : found[0];
		}

		private static GameObject PortfolioForeman(Zone Z, IList<GameObject> Settlers,
			KingdomPurposeKind Kind)
		{
			List<GameObject> found = new List<GameObject>();
			for (int i = 0; Settlers != null && i < Settlers.Count; i++)
			{
				GameObject resident = Settlers[i];
				if (!GameObject.Validate(resident) || resident.CurrentZone != Z
					|| resident.GetIntProperty("KingdomCitizen") != 1) continue;
				var capability = KingdomCrews.CapabilityOf(resident);
				bool answers = Kind == KingdomPurposeKind.Deep
					? capability.ValueOf(KingdomCrewRules.KindStrength) >= 18
						|| capability.ValueOf(KingdomCrewRules.KindTinkering) > 0
					: Kind == KingdomPurposeKind.Forge
						? capability.Intelligence >= 18
							|| capability.ValueOf(KingdomCrewRules.KindTinkering) > 0
						: capability.ValueOf(KingdomCrewRules.KindHarvestry) > 0
							|| capability.ValueOf(KingdomCrewRules.KindCustoms) > 0;
				if (answers && !string.IsNullOrEmpty(resident.IDIfAssigned)) found.Add(resident);
			}
			found.Sort((a, b) => string.CompareOrdinal(a.IDIfAssigned, b.IDIfAssigned));
			return found.Count == 0 ? null : found[0];
		}
	}
}
