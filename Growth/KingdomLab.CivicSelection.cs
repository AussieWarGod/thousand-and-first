using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomLabCivicRuntime
	{
		private static KingdomLabCivicReceipt PrepareSavantPrice(KingdomSystem System,
			Zone Z, KingdomSurvey Survey, GameObject Owner)
		{
			string cityCreed = KingdomCreed.SeatCreed(System);
			if (string.IsNullOrEmpty(cityCreed)) return null;
			List<KingdomResidentRow> rows = KingdomResidents.RollRows(System);
			rows.Sort((a, b) => a.ResidentId.CompareTo(b.ResidentId));
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomResidentRow row = rows[i];
				GameObject savant = Survey.FindCitizen(row.ResidentId);
				if (!ExactSavant(Z, savant, row, cityCreed, out GameObject home,
					out string homePlot, out string lodge)) continue;
				List<int> tastes = KingdomCeremonyRules.ChooseTastes(
					System.CurrentSettlementId, (ulong)row.ArrivedTick);
				if (tastes == null || tastes.Count == 0) continue;
				int taste = tastes[0];
				string tasteTag = KingdomCeremonyRules.TasteTag(taste);
				KingdomLabCivicRequest request = KingdomLabCivicRules.RequestForTaste(taste);
				GameObject target = null; GameObject targetHome = null;
				int targetResidentId = 0; string targetName = null; string targetPlot = null;
				string sourceHomeName = home?.ShortDisplayName;
				string targetHomeName = null;
				if (request == KingdomLabCivicRequest.ShrineUnconsecrated)
				{
					target = UnconsecratedShrine(Survey);
					targetName = target?.ShortDisplayName;
				}
				else if (!TryNeighbourTarget(Z, home, savant, homePlot, out target,
					out targetResidentId, out targetName, out targetHome, out targetPlot)) continue;
				if (!GameObject.Validate(target) || string.IsNullOrEmpty(sourceHomeName)
					|| string.IsNullOrEmpty(targetName)) continue;
				if (GameObject.Validate(targetHome)) targetHomeName = targetHome.ShortDisplayName;
				string ownerId = Owner?.IDIfAssigned;
				string savantId = savant.IDIfAssigned;
				string targetId = target.IDIfAssigned;
				string targetHomeId = targetHome?.IDIfAssigned;
				if (string.IsNullOrEmpty(ownerId) || string.IsNullOrEmpty(savantId)
					|| string.IsNullOrEmpty(targetId)
					|| (targetHome != null && string.IsNullOrEmpty(targetHomeId))) continue;
				return KingdomLabCivicRules.PrepareSavant(System.CurrentRealmId,
					System.CurrentSettlementId, Z.ZoneID, ownerId, savantId, row.ResidentId,
					row.Name, savant.GetStringProperty(KingdomCreed.CreedProperty), cityCreed,
					lodge, row.ArrivedTick, taste, tasteTag, targetId,
					targetHomeId, targetResidentId, targetName, homePlot, sourceHomeName,
					targetPlot, targetHomeName, Now());
			}
			return null;
		}

		private static bool ExactSavant(Zone Z, GameObject Body, KingdomResidentRow Row,
			string CityCreed, out GameObject Home, out string HomePlot, out string Lodge)
		{
			Home = null; HomePlot = null;
			Lodge = Body?.GetStringProperty(KingdomGuestbook.LodgeReceiptProperty);
			string name = Body?.GetStringProperty("KingdomName");
			string creed = Body?.GetStringProperty(KingdomCreed.CreedProperty);
			return Row.ArrivedTick > 0L && GameObject.Validate(Body) && Body.CurrentZone == Z
				&& KingdomPurpose.IsLodgedSpecialist(Z, Body, false)
				&& KingdomResidents.IdOf(Body) == Row.ResidentId
				&& !string.IsNullOrEmpty(name) && name == Row.Name
				&& !string.IsNullOrEmpty(Lodge)
				&& !Lodge.StartsWith("intent:", StringComparison.Ordinal)
				&& !string.IsNullOrEmpty(creed) && !string.IsNullOrEmpty(CityCreed)
				&& !string.Equals(creed, CityCreed, StringComparison.OrdinalIgnoreCase)
				&& KingdomLodging.TryLabHome(Z, Body, out Home, out HomePlot);
		}

		private static GameObject UnconsecratedShrine(KingdomSurvey Survey)
		{
			List<GameObject> candidates = Survey == null ? new List<GameObject>()
				: KingdomCapabilityRuntime.Roots(Survey.Ground, Survey,
					KingdomBenefitCapabilities.Shrine, "lab shrine request");
			for (int i = candidates.Count - 1; i >= 0; i--)
			{
				GameObject shrine = candidates[i];
				if (!KingdomUpgrade.IsFunctionallyBuilt(shrine)
					|| !string.IsNullOrEmpty(shrine.GetStringProperty(
						KingdomFaith.ShrineCreedProperty))
					|| string.IsNullOrEmpty(shrine.IDIfAssigned)) candidates.RemoveAt(i);
			}
			candidates.Sort((a, b) => string.CompareOrdinal(a.IDIfAssigned, b.IDIfAssigned));
			return candidates.Count == 0 ? null : candidates[0];
		}

		private static bool TryNeighbourTarget(Zone Z, GameObject Home, GameObject Savant,
			string SourcePlot, out GameObject Neighbour, out int ResidentId, out string Name,
			out GameObject TargetHome, out string TargetPlot)
		{
			Neighbour = null; ResidentId = 0; Name = null; TargetHome = null; TargetPlot = null;
			List<GameObject> candidates = KingdomLodging.ResidentsOf(Z, Home);
			candidates.Sort(delegate(GameObject a, GameObject b)
			{
				int byResident = KingdomResidents.IdOf(a).CompareTo(KingdomResidents.IdOf(b));
				return byResident != 0 ? byResident : string.CompareOrdinal(
					a?.IDIfAssigned, b?.IDIfAssigned);
			});
			for (int i = 0; i < candidates.Count; i++)
			{
				GameObject candidate = candidates[i];
				int id = KingdomResidents.IdOf(candidate);
				string name = candidate?.GetStringProperty("KingdomName");
				if (candidate == Savant || id <= 0 || string.IsNullOrEmpty(name)
					|| candidate.CurrentZone != Z
					|| string.IsNullOrEmpty(candidate.IDIfAssigned)) continue;
				if (!KingdomLodging.TryPrepareLabRehouse(Z, candidate, SourcePlot,
					out GameObject target, out string plot, out _)) continue;
				Neighbour = candidate; ResidentId = id; Name = name;
				TargetHome = target; TargetPlot = plot; return true;
			}
			return false;
		}

		private static KingdomLabCivicReceipt PrepareRefusalDeparture(KingdomSystem System,
			Zone Z, KingdomSurvey Survey, GameObject Owner)
		{
			if (!TryPhysicalOffer(Survey, Owner, out string[] offer, out string benefitFailure))
			{
				KingdomLog.Log("lab civic: " + benefitFailure); return null;
			}
			List<GameObject> residents = new List<GameObject>(Survey.Settlers);
			residents.Sort((a, b) =>
			{
				int byId = KingdomResidents.IdOf(a).CompareTo(KingdomResidents.IdOf(b));
				return byId != 0 ? byId : string.CompareOrdinal(
					a?.IDIfAssigned, b?.IDIfAssigned);
			});
			for (int i = 0; i < residents.Count; i++)
			{
				GameObject resident = residents[i];
				string name = resident?.GetStringProperty("KingdomName");
				int residentId = KingdomResidents.IdOf(resident);
				if (residentId <= 0 || string.IsNullOrEmpty(name)
					|| string.IsNullOrEmpty(resident?.IDIfAssigned)
					|| !KingdomLodging.TryLabHome(Z, resident, out GameObject home,
						out string plot) || !KingdomReach.Reaches(System, Z, Owner, home)) continue;
				string[] authored = KingdomQolRules.ParseTags(resident.GetPropertyOrTag(
					KingdomQolRules.RefusesTagName, ""));
				Array.Sort(authored, StringComparer.Ordinal);
				for (int tag = 0; tag < authored.Length; tag++)
					if (KingdomQolRules.Has(offer, authored[tag]))
						return KingdomLabCivicRules.PrepareDeparture(System.CurrentRealmId,
							System.CurrentSettlementId, Z.ZoneID, Owner.IDIfAssigned,
							resident.IDIfAssigned,
							residentId, name, plot, authored[tag], Now());
			}
			return null;
		}
	}
}
