using System;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Conservation boundary between semantic routes and an owning inventory adapter.</summary>
	public static class KingdomPolityManifestRules
	{
		public static bool TryCreateErrandProof(string ProofId, string SourceAuthorityId,
			string ErrandId, out KingdomPolityManifestProof Proof, out string Failure)
		{
			Proof = new KingdomPolityManifestProof
			{
				ProofId = ProofId, SourceAuthorityId = SourceAuthorityId,
				ManifestOrErrandId = ErrandId,
				Kind = KingdomPolityManifestAuthorityKind.Errand
			};
			Proof.ProofDigest = Digest(Proof);
			if (TryValidate(Proof, out Failure)) return true;
			Proof = null; return false;
		}

		public static bool TryCreateCargoProof(string ProofId, string SourceAuthorityId,
			string ManifestId, string UnitKey, long SourceBefore, long SourceAfter, long Debited,
			long InCustody, long Delivered, long Returned, string DebitReceiptId,
			string DeliveryReceiptId, string ReturnReceiptId,
			out KingdomPolityManifestProof Proof, out string Failure)
		{
			Proof = new KingdomPolityManifestProof
			{
				ProofId = ProofId, SourceAuthorityId = SourceAuthorityId,
				ManifestOrErrandId = ManifestId,
				Kind = KingdomPolityManifestAuthorityKind.PhysicalCargo, UnitKey = UnitKey,
				SourceBefore = SourceBefore, SourceAfter = SourceAfter, Debited = Debited,
				InCustody = InCustody, Delivered = Delivered, Returned = Returned,
				DebitReceiptId = DebitReceiptId, DeliveryReceiptId = DeliveryReceiptId,
				ReturnReceiptId = ReturnReceiptId
			};
			Proof.ProofDigest = Digest(Proof);
			if (TryValidate(Proof, out Failure)) return true;
			Proof = null; return false;
		}

		public static bool TryValidate(KingdomPolityManifestProof Proof, out string Failure)
		{
			Failure = null;
			if (Proof == null || !KingdomPolityRules.TypedId(Proof.ProofId,
				"taf:manifest-proof:") || !KingdomPolityRules.SemanticId(Proof.SourceAuthorityId) ||
				!KingdomPolityRules.SemanticId(Proof.ManifestOrErrandId) ||
				Proof.Kind == KingdomPolityManifestAuthorityKind.None ||
				(byte)Proof.Kind > (byte)KingdomPolityManifestAuthorityKind.PhysicalCargo ||
				!KingdomPolityRules.Digest(Proof.ProofDigest) || Proof.ProofDigest != Digest(Proof))
				return Refuse("manifest proof identity or digest is invalid", out Failure);
			if (Proof.Kind == KingdomPolityManifestAuthorityKind.Errand)
			{
				if (!string.IsNullOrEmpty(Proof.UnitKey) || Proof.SourceBefore != 0L ||
					Proof.SourceAfter != 0L || Proof.Debited != 0L || Proof.InCustody != 0L ||
					Proof.Delivered != 0L || Proof.Returned != 0L ||
					!string.IsNullOrEmpty(Proof.DebitReceiptId) ||
					!string.IsNullOrEmpty(Proof.DeliveryReceiptId) ||
					!string.IsNullOrEmpty(Proof.ReturnReceiptId))
					return Refuse("zero-cargo errand proof is noncanonical", out Failure);
				return true;
			}
			if (!KingdomPolityRules.Text(Proof.UnitKey, true) ||
				!Quantity(Proof.SourceBefore) || !Quantity(Proof.SourceAfter) ||
				!Quantity(Proof.Debited) || !Quantity(Proof.InCustody) ||
				!Quantity(Proof.Delivered) || !Quantity(Proof.Returned) || Proof.Debited < 1L ||
				Proof.SourceBefore != Proof.SourceAfter + Proof.Debited ||
				Proof.Debited != Proof.InCustody + Proof.Delivered + Proof.Returned ||
				!KingdomPolityRules.SemanticId(Proof.DebitReceiptId))
				return Refuse("physical manifest quantities are not exactly conserved", out Failure);
			if ((Proof.Delivered > 0L) != !string.IsNullOrEmpty(Proof.DeliveryReceiptId) ||
				(Proof.Returned > 0L) != !string.IsNullOrEmpty(Proof.ReturnReceiptId) ||
				(!string.IsNullOrEmpty(Proof.DeliveryReceiptId) &&
				 !KingdomPolityRules.SemanticId(Proof.DeliveryReceiptId)) ||
				(!string.IsNullOrEmpty(Proof.ReturnReceiptId) &&
				 !KingdomPolityRules.SemanticId(Proof.ReturnReceiptId)))
				return Refuse("physical manifest outcome receipts are incoherent", out Failure);
			return true;
		}

		public static bool IsDepartable(KingdomPolityManifestProof Proof, string ManifestId,
			out string Failure)
		{
			if (!TryValidate(Proof, out Failure) || Proof.ManifestOrErrandId != ManifestId) return false;
			if (Proof.Kind == KingdomPolityManifestAuthorityKind.PhysicalCargo &&
				(Proof.InCustody != Proof.Debited || Proof.Delivered != 0L || Proof.Returned != 0L))
				return Refuse("departing cargo is not wholly in exact custody", out Failure);
			return true;
		}

		public static bool IsSemanticEntitlement(KingdomPolityManifestProof Proof,
			string ManifestId, out string Failure)
		{
			if (!TryValidate(Proof, out Failure) || Proof.ManifestOrErrandId != ManifestId) return false;
			if (Proof.Kind == KingdomPolityManifestAuthorityKind.PhysicalCargo &&
				(Proof.InCustody != Proof.Debited || Proof.Delivered != 0L || Proof.Returned != 0L))
				return Refuse("semantic delivery cannot claim a physical inventory mutation", out Failure);
			return true;
		}

		public static bool IsLoadedDelivery(KingdomPolityManifestProof Proof, string ManifestId,
			out string Failure)
		{
			if (!TryValidate(Proof, out Failure) || Proof.ManifestOrErrandId != ManifestId) return false;
			if (Proof.Kind == KingdomPolityManifestAuthorityKind.PhysicalCargo &&
				(Proof.Delivered != Proof.Debited || Proof.InCustody != 0L || Proof.Returned != 0L))
				return Refuse("loaded endpoint has not reconciled the exact physical delivery", out Failure);
			return true;
		}

		private static bool Quantity(long Value)
		{
			return Value >= 0L && Value <= KingdomPolityRules.MaxValueBudget;
		}

		private static string Digest(KingdomPolityManifestProof P)
		{
			return KingdomPolityRules.ActivationDigest("polity-manifest-proof-v1", P.ProofId ?? "",
				P.SourceAuthorityId ?? "", P.ManifestOrErrandId ?? "", ((byte)P.Kind).ToString(
				CultureInfo.InvariantCulture), P.UnitKey ?? "", N(P.SourceBefore), N(P.SourceAfter),
				N(P.Debited), N(P.InCustody), N(P.Delivered), N(P.Returned),
				P.DebitReceiptId ?? "", P.DeliveryReceiptId ?? "", P.ReturnReceiptId ?? "");
		}

		private static string N(long Value) { return Value.ToString(CultureInfo.InvariantCulture); }
		private static bool Refuse(string Reason, out string Failure)
		{
			Failure = Reason; return false;
		}
	}
}
