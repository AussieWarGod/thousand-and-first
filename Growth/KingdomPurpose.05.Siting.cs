using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool TrySiteProof(KingdomSystem System, Zone Z,
			KingdomPurposeDefinition Definition, out string Proof, out GameObject Specialist,
			out string Failure)
		{
			Proof = null;
			Specialist = null;
			Failure = null;
			HashSet<string> standing = StandingKeys(Z);
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			if (Definition.Site == KingdomPurposeSite.LivingSurgery)
			{
				if (!LivingGround(System.FoundingTerrainBlueprint,
					System.FoundingRegionName, System.Style))
					return Fail("The chimeric theatre wants butcherable living-biome ground: a watervine, marsh, flower, banana, jungle, or fungal founding site. This city's founding ground cannot supply it.", out Failure);
				string provider = standing.Contains("graftinghall") ? "graftinghall"
					: standing.Contains("vathouse") ? "vathouse" : null;
				if (provider == null)
					return Fail("The chimeric theatre wants real damp and offal on this ground. Raise a vat-house or grafting hall here and ask again.", out Failure);
				Specialist = LodgedSpecialist(Z, survey.Settlers, false);
				if (!GameObject.Validate(Specialist))
					return Fail("The chimeric theatre wants a lodged savant with Intelligence 18 or better. House one on this ground before committing it.", out Failure);
				Proof = "living-biome=" + Safe(System.FoundingRegionName,
					System.FoundingTerrainBlueprint) + ";damp-offal=" + provider
					+ ";savant=" + Specialist.ID;
				return true;
			}
			if (Definition.Site == KingdomPurposeSite.RuinEnrollment)
			{
				if (!KingdomRules.IsRuinSite(System.FoundingTerrainBlueprint)
					&& (System.FoundingRegionName ?? "").IndexOf("Ruin",
						StringComparison.OrdinalIgnoreCase) < 0)
					return Fail("The becoming annexe wants ruin-ground or ruin-adjacent founding evidence. Found or seat this purpose on a city whose founding terrain is a ruin.", out Failure);
				if (!standing.Contains("smelter") || !standing.Contains("chargingpost"))
					return Fail("The becoming annexe wants a real smelter and charging post on this ground. Raise both so metal and arclight are physical facts here.", out Failure);
				if (!CreedReach(System, "Mechanimists") && !CreedReach(System, "Templar"))
					return Fail("The becoming annexe wants Mechanimist or Templar reach: people here must presently or historically hold one of those creeds.", out Failure);
				Specialist = LodgedSpecialist(Z, survey.Settlers, true);
				if (!GameObject.Validate(Specialist))
					return Fail("The becoming annexe wants a lodged psyberneticist: an Intelligence-18 tinker, technician, or Mechanimist resident housed on this ground.", out Failure);
				Proof = "ruin=" + Safe(System.FoundingRegionName,
					System.FoundingTerrainBlueprint) + ";arclight=smelter+chargingpost;creed="
					+ (CreedReach(System, "Mechanimists") ? "Mechanimists" : "Templar")
					+ ";psyberneticist=" + Specialist.ID;
				return true;
			}
			return Fail("The purpose names no implemented physical site predicate.", out Failure);
		}

		private static GameObject LodgedSpecialist(Zone Z, IList<GameObject> Settlers,
			bool Psyberneticist)
		{
			List<GameObject> candidates = new List<GameObject>();
			for (int i = 0; Settlers != null && i < Settlers.Count; i++)
			{
				GameObject resident = Settlers[i];
				if (!IsLodgedSpecialist(Z, resident, Psyberneticist)) continue;
				candidates.Add(resident);
			}
			candidates.Sort((a, b) => string.CompareOrdinal(a.ID, b.ID));
			return candidates.Count == 0 ? null : candidates[0];
		}

		/// <summary>The live, revocable labour fact shared by purpose siting and the annexe's
		/// register. It is deliberately about one concrete resident on this ground: a name retained
		/// in the old roster is not a lodged specialist, and moving out or losing the required craft
		/// closes the register without erasing its rolls.</summary>
		internal static bool IsLodgedSpecialist(Zone Z, GameObject Resident,
			bool Psyberneticist)
		{
			return Z != null && GameObject.Validate(Resident)
				&& Resident.CurrentZone == Z
				&& Resident.GetIntProperty("KingdomCitizen") == 1
				&& KingdomCrews.CapabilityOf(Resident).Intelligence >= 18
				&& !string.IsNullOrEmpty(KingdomLodging.HomeDesignKeyOf(Z, Resident))
				&& (!Psyberneticist || PsyberneticistTruth(Resident));
		}

		private static bool PsyberneticistTruth(GameObject Resident)
		{
			string words = (Resident.Blueprint ?? "") + " "
				+ (Resident.ShortDisplayName ?? "") + " " + (Resident.GetCulture() ?? "");
			return Resident.HasSkill("Tinkering") || Resident.HasSkill("Tinkering_Tinker1")
				|| Resident.HasSkill("Tinkering_Tinker2")
				|| words.IndexOf("tinker", StringComparison.OrdinalIgnoreCase) >= 0
				|| words.IndexOf("technician", StringComparison.OrdinalIgnoreCase) >= 0
				|| words.IndexOf("mechanimist", StringComparison.OrdinalIgnoreCase) >= 0
				|| words.IndexOf("psyber", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool LivingGround(string Terrain, string Region, string Style)
		{
			if (Style == "verdant" || Style == "fungal") return true;
			string ground = (Terrain ?? "") + " " + (Region ?? "");
			string[] living = new string[7]
				{ "Watervine", "Saltmarsh", "Flowerfield", "BananaGrove", "Jungle", "Fungal", "Marsh" };
			for (int i = 0; i < living.Length; i++)
				if (ground.IndexOf(living[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
			return false;
		}

		private static bool CreedReach(KingdomSystem System, string Creed)
		{
			return System != null && ((System.CreedCounts != null
				&& System.CreedCounts.TryGetValue(Creed, out int present) && present > 0)
				|| (System.CreedPastCounts != null
					&& System.CreedPastCounts.TryGetValue(Creed, out int past) && past > 0));
		}

		private static bool TrySettlementIdentity(KingdomSystem System, string ZoneId,
			out string SettlementId)
		{
			SettlementId = null;
			if (System == null || string.IsNullOrEmpty(ZoneId)
				|| !System.TryExactSettlementIds(true, out List<string> ids, out _)) return false;
			if (System.ClaimedZones != null && System.ClaimedZones.Contains(ZoneId))
				SettlementId = System.City?.SettlementId;
			else if (System.Away?.ClaimedZones != null && System.Away.ClaimedZones.Contains(ZoneId))
				SettlementId = System.Away.City?.SettlementId;
			return !string.IsNullOrEmpty(SettlementId) && ids.Contains(SettlementId);
		}

		private static string Safe(string First, string Second)
		{
			string value = !string.IsNullOrEmpty(First) ? First : Second;
			return string.IsNullOrEmpty(value) ? "unrecorded" : value.Replace(';', ',');
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
