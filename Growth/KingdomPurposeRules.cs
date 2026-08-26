using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>The two presently shipped, physically realised city purposes.</summary>
	public enum KingdomPurposeKind : byte
	{
		None = 0,
		Flesh = 1,
		Chrome = 2
	}

	/// <summary>Physical ground predicate declared by a purposeful design.</summary>
	public enum KingdomPurposeSite : byte
	{
		None = 0,
		LivingSurgery = 1,
		RuinEnrollment = 2
	}

	/// <summary>
	/// One merged catalogue declaration. It describes the real consignment another city must
	/// produce and the distinct physical ground on which this purpose can be committed.
	/// </summary>
	public sealed class KingdomPurposeDefinition
	{
		public string BuildKey;
		public KingdomPurposeKind Kind;
		public KingdomPurposeSite Site;
		public string CargoKey;
		public string CargoName;
		public KingdomMaterial CargoMaterial;
		public int CargoWater;
		public KingdomMaterialTally CargoCost;
		public string ProducerSpec;
		public string Effect;

		public KingdomPurposeDefinition Copy()
		{
			return new KingdomPurposeDefinition
			{
				BuildKey = BuildKey,
				Kind = Kind,
				Site = Site,
				CargoKey = CargoKey,
				CargoName = CargoName,
				CargoMaterial = CargoMaterial,
				CargoWater = CargoWater,
				CargoCost = CargoCost?.Copy() ?? new KingdomMaterialTally(),
				ProducerSpec = ProducerSpec,
				Effect = Effect
			};
		}
	}

	/// <summary>
	/// Immutable manifest published before production is funded. The output ID is deliberately
	/// absent: construction publishes that identity before the first physical AddObject callback.
	/// </summary>
	public sealed class KingdomPurposeManifest
	{
		public const int Schema = 1;
		public string BuildKey;
		public KingdomPurposeKind Kind;
		public KingdomPurposeSite Site;
		public string CargoKey;
		public string CargoName;
		public KingdomMaterial CargoMaterial;
		public int CargoWater;
		public string CargoCostClaim;
		public string OriginSettlementId;
		public string OriginCity;
		public string OriginZoneId;
		public string SourceGateKey;
		public string DestinationSettlementId;
		public string DestinationCity;
		public string DestinationZoneId;
		public string DestinationGateKey;
		public string ProducerProof;
		public string Effect;
	}

	/// <summary>
	/// Frozen commitment shown before a purposeful building is debited. It binds one exact delivered
	/// cargo object and its terminal consignment receipt to one distinct site reading.
	/// </summary>
	public sealed class KingdomPurposeCommitment
	{
		public const int Schema = 1;
		public string Manifest;
		public string ConsignmentId;
		public string CargoItemId;
		public string SiteProof;
		public string SpecialistId;
		public string SpecialistName;
	}

	/// <summary>Engine-free schema, parser, and canonical receipt laws.</summary>
	public static class KingdomPurposeRules
	{
		public const int MaxReceiptChars = 4096;
		private const int ManifestFieldCount = 19;
		private const int CommitmentFieldCount = 5;

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
				Effect = Effect.Trim()
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
			if (!ValidManifest(Manifest) || Definition == null
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

		public static string EncodeCommitment(KingdomPurposeCommitment Commitment)
		{
			if (!ValidCommitment(Commitment)) return null;
			return Encode(new string[CommitmentFieldCount]
			{
				Commitment.Manifest, Commitment.ConsignmentId, Commitment.CargoItemId,
				Commitment.SiteProof, Encode(new string[2]
					{ Commitment.SpecialistId, Commitment.SpecialistName })
			});
		}

		public static bool TryDecodeCommitment(string Receipt,
			out KingdomPurposeCommitment Commitment)
		{
			Commitment = null;
			if (!TryDecode(Receipt, CommitmentFieldCount, out string[] f)
				|| !TryDecode(f[4], 2, out string[] specialist)) return false;
			Commitment = new KingdomPurposeCommitment
			{
				Manifest = f[0], ConsignmentId = f[1], CargoItemId = f[2],
				SiteProof = f[3], SpecialistId = specialist[0], SpecialistName = specialist[1]
			};
			return ValidCommitment(Commitment) && EncodeCommitment(Commitment) == Receipt;
		}

		public static bool ValidCommitment(KingdomPurposeCommitment C)
		{
			return C != null && TryDecodeManifest(C.Manifest, out _)
				&& Identity(C.ConsignmentId) && Identity(C.CargoItemId)
				&& Text(C.SiteProof, 1, 720) && Identity(C.SpecialistId)
				&& Text(C.SpecialistName, 1, 180);
		}

		public static string PurposeName(KingdomPurposeKind Kind)
		{
			return Kind == KingdomPurposeKind.Flesh ? "the flesh-city"
				: Kind == KingdomPurposeKind.Chrome ? "the chrome-city" : "no purpose";
		}

		private static bool TryKind(string Raw, out KingdomPurposeKind Kind)
		{
			string value = (Raw ?? "").Trim().ToLowerInvariant();
			Kind = value == "flesh" ? KingdomPurposeKind.Flesh
				: value == "chrome" ? KingdomPurposeKind.Chrome : KingdomPurposeKind.None;
			return Kind != KingdomPurposeKind.None;
		}

		private static bool TrySite(string Raw, out KingdomPurposeSite Site)
		{
			string value = (Raw ?? "").Trim().ToLowerInvariant();
			Site = value == "living-surgery" ? KingdomPurposeSite.LivingSurgery
				: value == "ruin-enrollment" ? KingdomPurposeSite.RuinEnrollment
				: KingdomPurposeSite.None;
			return Site != KingdomPurposeSite.None;
		}

		private static string Encode(IList<string> Fields)
		{
			StringBuilder text = new StringBuilder("v1");
			for (int i = 0; i < Fields.Count; i++)
			{
				string value = Fields[i] ?? "";
				text.Append(';').Append(value.Length.ToString(CultureInfo.InvariantCulture))
					.Append(':').Append(value);
				if (text.Length > MaxReceiptChars) return null;
			}
			return text.ToString();
		}

		private static bool TryDecode(string Text, int Count, out string[] Fields)
		{
			Fields = null;
			if (string.IsNullOrEmpty(Text) || Text.Length > MaxReceiptChars
				|| !Text.StartsWith("v1", StringComparison.Ordinal)) return false;
			string[] values = new string[Count];
			int at = 2;
			for (int i = 0; i < Count; i++)
			{
				if (at >= Text.Length || Text[at++] != ';') return false;
				int colon = Text.IndexOf(':', at);
				if (colon < at || colon - at > 8
					|| !int.TryParse(Text.Substring(at, colon - at), NumberStyles.None,
						CultureInfo.InvariantCulture, out int length)
					|| length < 0 || length > MaxReceiptChars || colon + 1 + length > Text.Length)
					return false;
				values[i] = Text.Substring(colon + 1, length);
				at = colon + 1 + length;
			}
			if (at != Text.Length) return false;
			Fields = values;
			return true;
		}

		private static bool Token(string Value, int Max)
		{
			if (!Text(Value, 1, Max)) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ':' || c == '.'))
					return false;
			}
			return true;
		}

		private static bool Identity(string Value)
		{
			return Text(Value, 1, 256) && Value.Trim() == Value;
		}

		private static bool Text(string Value, int Min, int Max)
		{
			return Value != null && Value.Length >= Min && Value.Length <= Max
				&& Value.IndexOf('\0') < 0;
		}

		private static bool Fail(string Message, out string Error)
		{
			Error = Message;
			return false;
		}
	}
}
