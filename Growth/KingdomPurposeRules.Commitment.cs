using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomPurposeRules
	{

		public static string EncodeCommitment(KingdomPurposeCommitment Commitment)
		{
			if (!ValidCommitment(Commitment)) return null;
			return Encode(new string[CommitmentFieldCount]
			{
				Commitment.Manifest, Commitment.ConsignmentId, Commitment.CargoItemId,
				Commitment.SiteProof, Encode(new string[2]
					{ Commitment.SpecialistId, Commitment.SpecialistName }),
				Commitment.PortfolioPairId,
				Commitment.PortfolioEpoch == 0L ? "" : Commitment.PortfolioEpoch.ToString(
					CultureInfo.InvariantCulture), Commitment.PortfolioOperationId,
				Commitment.ReciprocalCargoItemId, Commitment.ReciprocalCargoReceipt,
				Commitment.InitialBuildKey
			});
		}

		public static bool TryDecodeCommitment(string Receipt,
			out KingdomPurposeCommitment Commitment)
		{
			Commitment = null;
			if (TryDecode(Receipt, LegacyCommitmentFieldCount, out string[] legacy)
				&& TryDecode(legacy[4], 2, out string[] oldSpecialist))
			{
				Commitment = new KingdomPurposeCommitment
				{
					Manifest = legacy[0], ConsignmentId = legacy[1],
					CargoItemId = legacy[2], SiteProof = legacy[3],
					SpecialistId = oldSpecialist[0], SpecialistName = oldSpecialist[1]
				};
				return ValidCommitment(Commitment);
			}
			if (TryDecode(Receipt, PortfolioCommitmentFieldCount, out string[] prior)
				&& TryDecode(prior[4], 2, out string[] priorSpecialist)
				&& (string.IsNullOrEmpty(prior[6]) || long.TryParse(prior[6],
					NumberStyles.None, CultureInfo.InvariantCulture, out _)))
			{
				long priorEpoch = string.IsNullOrEmpty(prior[6]) ? 0L : long.Parse(prior[6],
					CultureInfo.InvariantCulture);
				Commitment = new KingdomPurposeCommitment
				{
					Manifest = prior[0], ConsignmentId = prior[1], CargoItemId = prior[2],
					SiteProof = prior[3], SpecialistId = priorSpecialist[0],
					SpecialistName = priorSpecialist[1], PortfolioPairId = prior[5],
					PortfolioEpoch = priorEpoch, PortfolioOperationId = prior[7],
					ReciprocalCargoItemId = prior[8], ReciprocalCargoReceipt = prior[9]
				};
				return ValidCommitment(Commitment)
					&& EncodePortfolioCommitmentV2(Commitment) == Receipt;
			}
			if (!TryDecode(Receipt, CommitmentFieldCount, out string[] f)
				|| !TryDecode(f[4], 2, out string[] specialist)
				|| (!string.IsNullOrEmpty(f[6]) && !long.TryParse(f[6],
					NumberStyles.None, CultureInfo.InvariantCulture, out _))) return false;
			long epoch = string.IsNullOrEmpty(f[6]) ? 0L : long.Parse(f[6],
				CultureInfo.InvariantCulture);
			Commitment = new KingdomPurposeCommitment
			{
				Manifest = f[0], ConsignmentId = f[1], CargoItemId = f[2],
				SiteProof = f[3], SpecialistId = specialist[0], SpecialistName = specialist[1],
				PortfolioPairId = f[5], PortfolioEpoch = epoch,
				PortfolioOperationId = f[7], ReciprocalCargoItemId = f[8],
				ReciprocalCargoReceipt = f[9], InitialBuildKey = f[10]
			};
			return ValidCommitment(Commitment) && EncodeCommitment(Commitment) == Receipt;
		}

		private static string EncodePortfolioCommitmentV2(KingdomPurposeCommitment Commitment)
		{
			if (!ValidCommitment(Commitment)
				|| !string.IsNullOrEmpty(Commitment.InitialBuildKey)) return null;
			return Encode(new string[PortfolioCommitmentFieldCount]
			{
				Commitment.Manifest, Commitment.ConsignmentId, Commitment.CargoItemId,
				Commitment.SiteProof, Encode(new string[2]
					{ Commitment.SpecialistId, Commitment.SpecialistName }),
				Commitment.PortfolioPairId,
				Commitment.PortfolioEpoch == 0L ? "" : Commitment.PortfolioEpoch.ToString(
					CultureInfo.InvariantCulture), Commitment.PortfolioOperationId,
				Commitment.ReciprocalCargoItemId, Commitment.ReciprocalCargoReceipt
			});
		}

		public static bool ValidCommitment(KingdomPurposeCommitment C)
		{
			if (C == null || !Text(C.SiteProof, 1, 720) || !Identity(C.SpecialistId)
				|| !Text(C.SpecialistName, 1, 180)) return false;
			bool legacy = !string.IsNullOrEmpty(C.Manifest)
				|| !string.IsNullOrEmpty(C.ConsignmentId) || !string.IsNullOrEmpty(C.CargoItemId);
			if (legacy != (TryDecodeManifest(C.Manifest, out _)
				&& Identity(C.ConsignmentId) && Identity(C.CargoItemId))) return false;
			bool reciprocal = !string.IsNullOrEmpty(C.PortfolioPairId)
				|| C.PortfolioEpoch != 0L || !string.IsNullOrEmpty(C.PortfolioOperationId)
				|| !string.IsNullOrEmpty(C.ReciprocalCargoItemId)
				|| !string.IsNullOrEmpty(C.ReciprocalCargoReceipt);
			bool initial = !string.IsNullOrEmpty(C.InitialBuildKey);
			if (initial)
				return !legacy && !reciprocal && Token(C.InitialBuildKey, 128);
			if (!legacy && !reciprocal) return false;
			if (!reciprocal) return true;
			return Identity(C.PortfolioPairId) && C.PortfolioEpoch >= 1L
				&& Identity(C.PortfolioOperationId) && Identity(C.ReciprocalCargoItemId)
				&& KingdomPurposePortfolioRules.TryDecodeCargo(C.ReciprocalCargoReceipt,
					out KingdomPurposeCargoReceipt cargo)
				&& cargo.PairId == C.PortfolioPairId && cargo.PairEpoch == C.PortfolioEpoch
				&& cargo.OperationId == C.PortfolioOperationId
				&& cargo.ObjectId == C.ReciprocalCargoItemId
				&& (!legacy || C.CargoItemId != C.ReciprocalCargoItemId);
		}

		public static string PurposeName(KingdomPurposeKind Kind)
		{
			return Kind == KingdomPurposeKind.Flesh ? "the flesh-city"
				: Kind == KingdomPurposeKind.Chrome ? "the chrome-city"
				: Kind == KingdomPurposeKind.Deep ? "the Deep-Bore city"
				: Kind == KingdomPurposeKind.Forge ? "the Great Foundry city"
				: Kind == KingdomPurposeKind.Harvest ? "the Granary-Colossus city"
				: "no purpose";
		}

		private static bool TryKind(string Raw, out KingdomPurposeKind Kind)
		{
			string value = (Raw ?? "").Trim().ToLowerInvariant();
			Kind = value == "flesh" ? KingdomPurposeKind.Flesh
				: value == "chrome" ? KingdomPurposeKind.Chrome
				: value == "deep" ? KingdomPurposeKind.Deep
				: value == "forge" ? KingdomPurposeKind.Forge
				: value == "harvest" ? KingdomPurposeKind.Harvest
				: KingdomPurposeKind.None;
			return Kind != KingdomPurposeKind.None;
		}

		private static bool TrySite(string Raw, out KingdomPurposeSite Site)
		{
			string value = (Raw ?? "").Trim().ToLowerInvariant();
			Site = value == "living-surgery" ? KingdomPurposeSite.LivingSurgery
				: value == "ruin-enrollment" ? KingdomPurposeSite.RuinEnrollment
				: value == "deep-delve" ? KingdomPurposeSite.DeepDelve
				: value == "forge-quench" ? KingdomPurposeSite.ForgeQuench
				: value == "harvest-water" ? KingdomPurposeSite.HarvestWater
				: KingdomPurposeSite.None;
			return Site != KingdomPurposeSite.None;
		}

		private static bool PortfolioSiteMatches(KingdomPurposeKind Kind,
			KingdomPurposeSite Site)
		{
			return (Kind == KingdomPurposeKind.Deep && Site == KingdomPurposeSite.DeepDelve)
				|| (Kind == KingdomPurposeKind.Forge && Site == KingdomPurposeSite.ForgeQuench)
				|| (Kind == KingdomPurposeKind.Harvest && Site == KingdomPurposeSite.HarvestWater);
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
