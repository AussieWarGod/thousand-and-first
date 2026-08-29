using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Engine-free schema, parser, and canonical receipt laws.</summary>
	public static partial class KingdomPurposeRules
	{
		public const int MaxReceiptChars = 4096;
		private const int ManifestFieldCount = 19;
		private const int LegacyCommitmentFieldCount = 5;
		private const int PortfolioCommitmentFieldCount = 10;
		private const int CommitmentFieldCount = 11;

		public static bool TryCreateDefinition(string BuildKey, string Purpose,
			string Site, string CargoKey, string CargoName, string CargoMaterial,
			string CargoWater, string CargoCost, string Producers, string Effect,
			out KingdomPurposeDefinition Definition, out string Error)
		{
			Definition = null;
			Error = null;
			bool any = !string.IsNullOrWhiteSpace(Purpose)
				|| !string.IsNullOrWhiteSpace(Site) || !string.IsNullOrWhiteSpace(CargoKey)
				|| !string.IsNullOrWhiteSpace(CargoName)
				|| !string.IsNullOrWhiteSpace(CargoMaterial)
				|| !string.IsNullOrWhiteSpace(CargoWater)
				|| !string.IsNullOrWhiteSpace(CargoCost)
				|| !string.IsNullOrWhiteSpace(Producers) || !string.IsNullOrWhiteSpace(Effect);
			if (!any) return true;
			if (!Token(BuildKey, 128)) return Fail("purpose has no bounded building key", out Error);
			if (!TryKind(Purpose, out KingdomPurposeKind kind))
				return Fail("building " + BuildKey + " names an unknown Purpose", out Error);
			if (!TrySite(Site, out KingdomPurposeSite site))
				return Fail("building " + BuildKey + " names an unknown PurposeSite", out Error);
			bool portfolio = kind >= KingdomPurposeKind.Deep;
			if (portfolio && !PortfolioSiteMatches(kind, site))
				return Fail("building " + BuildKey + " mismatches its portfolio purpose and site", out Error);
			if (portfolio)
			{
				if (!string.IsNullOrWhiteSpace(CargoKey) || !string.IsNullOrWhiteSpace(CargoName)
					|| !string.IsNullOrWhiteSpace(CargoMaterial)
					|| !string.IsNullOrWhiteSpace(CargoWater)
					|| !string.IsNullOrWhiteSpace(CargoCost))
					return Fail("building " + BuildKey
						+ " must use the frozen reciprocal recipe table, not one legacy cargo row", out Error);
				if (!TryProducerSpec(Producers, out string portfolioProducers))
					return Fail("building " + BuildKey + " has an invalid purpose producer specification", out Error);
				if (!Text(Effect, 1, 360))
					return Fail("building " + BuildKey + " has no bounded purpose effect", out Error);
				Definition = new KingdomPurposeDefinition
				{
					BuildKey = BuildKey, Kind = kind, Site = site,
					CargoCost = new KingdomMaterialTally(), ProducerSpec = portfolioProducers,
					Effect = Effect.Trim(), PortfolioOnly = true
				};
				return true;
			}
			if (!Token(CargoKey, 128) || !Text(CargoName, 1, 180))
				return Fail("building " + BuildKey + " has no bounded purpose cargo", out Error);
			if (!KingdomMaterialRules.TryParseMaterial(CargoMaterial,
				out KingdomMaterial material))
				return Fail("building " + BuildKey + " names an unknown purpose cargo material", out Error);
			if (!int.TryParse(CargoWater, NumberStyles.None, CultureInfo.InvariantCulture,
				out int water) || water < 1 || water > 100000)
				return Fail("building " + BuildKey + " has an invalid purpose cargo water cost", out Error);
			if (!KingdomMaterialRules.TryParseMaterialCost(CargoCost,
				out KingdomMaterialTally cost, out string costError) || cost.IsEmpty())
				return Fail("building " + BuildKey + " has an invalid purpose cargo cost: "
					+ (costError ?? "empty cost"), out Error);
			// The transported thing conserves one unit of its declared physical material. It is not
			// a token minted beside the producer's ordinary inputs.
			if (cost.Get(material) < 1)
				return Fail("building " + BuildKey + " purpose cargo cost does not contain its cargo material", out Error);
			if (!TryProducerSpec(Producers, out string producerSpec))
				return Fail("building " + BuildKey + " has an invalid purpose producer specification", out Error);
			if (!Text(Effect, 1, 360))
				return Fail("building " + BuildKey + " has no bounded purpose effect", out Error);
			Definition = new KingdomPurposeDefinition
			{
				BuildKey = BuildKey,
				Kind = kind,
				Site = site,
				CargoKey = CargoKey,
				CargoName = CargoName.Trim(),
				CargoMaterial = material,
				CargoWater = water,
				CargoCost = cost,
				ProducerSpec = producerSpec,
				Effect = Effect.Trim(),
				PortfolioOnly = false
			};
			return true;
		}

		/// <summary>
		/// Producer grammar: comma-separated requirements, each optionally a pipe-separated set of
		/// alternatives. Thus <c>smelter,chargingpost</c> requires both; <c>vathouse|graftinghall</c>
		/// requires either. Canonical output preserves declaration order and rejects duplicates.
		/// </summary>
		public static bool TryProducerSpec(string Raw, out string Canonical)
		{
			Canonical = null;
			if (string.IsNullOrWhiteSpace(Raw) || Raw.Length > 512) return false;
			string[] groups = Raw.Split(',');
			if (groups.Length < 1 || groups.Length > 16) return false;
			HashSet<string> all = new HashSet<string>(StringComparer.Ordinal);
			StringBuilder text = new StringBuilder();
			for (int i = 0; i < groups.Length; i++)
			{
				string[] alternatives = groups[i].Split('|');
				if (alternatives.Length < 1 || alternatives.Length > 8) return false;
				if (i > 0) text.Append(',');
				for (int j = 0; j < alternatives.Length; j++)
				{
					string value = alternatives[j].Trim();
					if (!Token(value, 128) || !all.Add(value)) return false;
					if (j > 0) text.Append('|');
					text.Append(value);
				}
			}
			Canonical = text.ToString();
			return true;
		}

		public static bool ProducersSatisfied(string Spec, ISet<string> Standing,
			out string MissingGroup)
		{
			MissingGroup = null;
			if (!TryProducerSpec(Spec, out string canonical)
				|| Standing == null) return false;
			string[] groups = canonical.Split(',');
			for (int i = 0; i < groups.Length; i++)
			{
				bool found = false;
				string[] alternatives = groups[i].Split('|');
				for (int j = 0; j < alternatives.Length; j++)
					if (Standing.Contains(alternatives[j])) { found = true; break; }
				if (!found)
				{
					MissingGroup = groups[i];
					return false;
				}
			}
			return true;
		}

		public static string EncodeManifest(KingdomPurposeManifest Manifest)
		{
			if (!ValidManifest(Manifest)) return null;
			return Encode(new string[ManifestFieldCount]
			{
				Manifest.BuildKey, ((int)Manifest.Kind).ToString(CultureInfo.InvariantCulture),
				((int)Manifest.Site).ToString(CultureInfo.InvariantCulture), Manifest.CargoKey,
				Manifest.CargoName, ((int)Manifest.CargoMaterial).ToString(CultureInfo.InvariantCulture),
				Manifest.CargoWater.ToString(CultureInfo.InvariantCulture), Manifest.CargoCostClaim,
				Manifest.OriginSettlementId, Manifest.OriginCity, Manifest.OriginZoneId,
				Manifest.SourceGateKey, Manifest.DestinationSettlementId, Manifest.DestinationCity,
				Manifest.DestinationZoneId, Manifest.DestinationGateKey, Manifest.ProducerProof,
				Manifest.Effect, "purpose-manifest"
			});
		}

		public static bool TryDecodeManifest(string Receipt, out KingdomPurposeManifest Manifest)
		{
			Manifest = null;
			if (!TryDecode(Receipt, ManifestFieldCount, out string[] f)
				|| f[18] != "purpose-manifest"
				|| !int.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture, out int kind)
				|| !int.TryParse(f[2], NumberStyles.None, CultureInfo.InvariantCulture, out int site)
				|| !int.TryParse(f[5], NumberStyles.None, CultureInfo.InvariantCulture, out int material)
				|| !int.TryParse(f[6], NumberStyles.None, CultureInfo.InvariantCulture, out int water))
				return false;
			Manifest = new KingdomPurposeManifest
			{
				BuildKey = f[0], Kind = (KingdomPurposeKind)kind, Site = (KingdomPurposeSite)site,
				CargoKey = f[3], CargoName = f[4], CargoMaterial = (KingdomMaterial)material,
				CargoWater = water, CargoCostClaim = f[7], OriginSettlementId = f[8],
				OriginCity = f[9], OriginZoneId = f[10], SourceGateKey = f[11],
				DestinationSettlementId = f[12], DestinationCity = f[13],
				DestinationZoneId = f[14], DestinationGateKey = f[15], ProducerProof = f[16],
				Effect = f[17]
			};
			return ValidManifest(Manifest) && EncodeManifest(Manifest) == Receipt;
		}

		public static bool ValidManifest(KingdomPurposeManifest M)
		{
			if (M == null || !Token(M.BuildKey, 128) || M.Kind <= KingdomPurposeKind.None
				|| M.Kind > KingdomPurposeKind.Chrome || M.Site <= KingdomPurposeSite.None
				|| M.Site > KingdomPurposeSite.RuinEnrollment || !Token(M.CargoKey, 128)
				|| !Text(M.CargoName, 1, 180) || (int)M.CargoMaterial < 0
				|| (int)M.CargoMaterial >= KingdomMaterialRules.MaterialCount
				|| M.CargoWater < 1 || M.CargoWater > 100000
				|| !Text(M.CargoCostClaim, 1, 4096)
				|| !KingdomMaterialDebitCost.TryParseClaim(M.CargoCostClaim,
					out KingdomMaterialDebitCost claim) || claim.IsEmpty
				|| claim.Materials.Get(M.CargoMaterial) < 1
				|| !Identity(M.OriginSettlementId) || !Text(M.OriginCity, 1, 180)
				|| !Text(M.OriginZoneId, 1, 512) || !Text(M.SourceGateKey, 1, 768)
				|| !Identity(M.DestinationSettlementId) || !Text(M.DestinationCity, 1, 180)
				|| !Text(M.DestinationZoneId, 1, 512) || !Text(M.DestinationGateKey, 1, 768)
				|| M.OriginSettlementId == M.DestinationSettlementId
				|| M.OriginZoneId == M.DestinationZoneId || M.SourceGateKey == M.DestinationGateKey
				|| !TryProducerSpec(M.ProducerProof, out string producer)
				|| producer != M.ProducerProof || !Text(M.Effect, 1, 360)) return false;
			return true;
		}

		/// <summary>
		/// Whether an already delivered object answers the purpose currently being offered. A merge
		/// may change a recipe or purpose prospectively, but it cannot silently reinterpret an old
		/// cargo receipt as the new thing; the producer must dispatch a matching physical output.
		/// </summary>
		public static bool ManifestMatchesDefinition(KingdomPurposeManifest Manifest,
			KingdomPurposeDefinition Definition)
		{
			if (!ValidManifest(Manifest) || Definition == null || Definition.PortfolioOnly
				|| !Token(Definition.BuildKey, 128)
				|| Definition.Kind <= KingdomPurposeKind.None
				|| Definition.Kind > KingdomPurposeKind.Chrome
				|| Definition.Site <= KingdomPurposeSite.None
				|| Definition.Site > KingdomPurposeSite.RuinEnrollment
				|| !Token(Definition.CargoKey, 128) || !Text(Definition.CargoName, 1, 180)
				|| (int)Definition.CargoMaterial < 0
				|| (int)Definition.CargoMaterial >= KingdomMaterialRules.MaterialCount
				|| Definition.CargoWater < 1 || Definition.CargoWater > 100000
				|| Definition.CargoCost == null || Definition.CargoCost.IsEmpty()
				|| Definition.CargoCost.Get(Definition.CargoMaterial) < 1
				|| !TryProducerSpec(Definition.ProducerSpec, out string producer)
				|| producer != Definition.ProducerSpec || !Text(Definition.Effect, 1, 360)) return false;
			return Manifest.BuildKey == Definition.BuildKey
				&& Manifest.Kind == Definition.Kind && Manifest.Site == Definition.Site
				&& Manifest.CargoKey == Definition.CargoKey
				&& Manifest.CargoName == Definition.CargoName
				&& Manifest.CargoMaterial == Definition.CargoMaterial
				&& Manifest.CargoWater == Definition.CargoWater
				&& Manifest.CargoCostClaim == new KingdomMaterialDebitCost(
					Definition.CargoCost).ToClaimString()
				&& Manifest.ProducerProof == Definition.ProducerSpec
				&& Manifest.Effect == Definition.Effect;
		}
	}
}
