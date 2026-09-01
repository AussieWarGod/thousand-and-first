using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityAmbientTransactionRules
	{
		public static bool Valid(KingdomPolityAmbientTransaction T, string CohortId,
			out string Failure)
		{
			Failure = null;
			if (T == null || T.Version != KingdomPolityAmbientTransaction.CurrentVersion ||
				!KingdomPolityRules.TypedId(T.TransactionId, "taf:ambient-transaction:v1:") ||
				!KingdomPolityDispatchRules.AmbientPurpose(T.Purpose) ||
				!KingdomPolityRules.SemanticId(T.SourcePolityId) ||
				!KingdomPolityRules.TypedId(T.SourceSettlementId, "taf:settlement:v1:") ||
				!SafeText(T.SourceSettlementName, true) || !SafeText(T.SourceZoneId, true) ||
				!KingdomPolityRules.TypedId(T.DestinationSettlementId, "taf:settlement:v1:") ||
				!SafeText(T.DestinationSettlementName, true) ||
				!SafeText(T.DestinationZoneId, true) || !SafeText(T.LocalLocusRef, false) ||
				!CanonicalFacts(T.FactRefs) || !SafeText(T.SafeDetail, true) ||
				!CanonicalOptionalRefs(T.ManifestRefs, MaximumManifestRows) ||
				!CanonicalOptionalRefs(T.PhysicalStockObjectIds, MaximumManifestRows) ||
				!OptionalSemantic(T.NewsRef) || T.PreparedTick < 0L ||
				T.FrozenDigest != FrozenDigest(T) || T.TransactionId !=
				KingdomPolityRules.ActivationId("taf:ambient-transaction:v1:",
					"polity-ambient-transaction-v1", CohortId, T.FrozenDigest) ||
				!SafeText(T.Fault, false)) return Fail(
					"ambient transaction is invalid, unbounded, or tampered", out Failure);
			bool local = T.Purpose == KingdomPolityCohortPurpose.Guard ||
				T.Purpose == KingdomPolityCohortPurpose.Patrol;
			if (local != !string.IsNullOrEmpty(T.LocalLocusRef) || local &&
				(!KingdomPolityRules.SemanticId(T.LocalLocusRef) ||
				 T.SourceSettlementId != T.DestinationSettlementId ||
				 T.SourceSettlementName != T.DestinationSettlementName ||
				 T.SourceZoneId != T.DestinationZoneId)) return Fail(
				"ambient local locus contract is incoherent", out Failure);
			if (!local && T.SourceSettlementId == T.DestinationSettlementId) return Fail(
				"travelling ambient purpose has only one endpoint", out Failure);
			if (T.ManifestRefs.Count != 0 || T.PhysicalStockObjectIds.Count != 0) return Fail(
				"ambient stock is not backed by an implemented physical custody seam", out Failure);
			if (T.Purpose == KingdomPolityCohortPurpose.Guard &&
				(T.FactRefs.Count != 1 || !HasPrefix(T.FactRefs, "taf:fact:witnessed:")) ||
				T.Purpose == KingdomPolityCohortPurpose.Patrol &&
				(T.FactRefs.Count != 1 || !HasPrefix(T.FactRefs, "taf:fact:route-condition:")) ||
				T.Purpose == KingdomPolityCohortPurpose.Courier &&
				(T.FactRefs.Count != 2 || string.IsNullOrEmpty(T.NewsRef) ||
				 !T.FactRefs.Contains(T.NewsRef)) ||
				T.Purpose == KingdomPolityCohortPurpose.Trader &&
				((T.FactRefs.Count != 2 && T.FactRefs.Count != 3) ||
				 string.IsNullOrEmpty(T.NewsRef) || !T.FactRefs.Contains(T.NewsRef) ||
				 T.SafeDetail != "No exact physical stock accompanies this visit; no trade is offered.") ||
				T.Purpose == KingdomPolityCohortPurpose.Migrant &&
				(T.FactRefs.Count != 3 || !string.IsNullOrEmpty(T.NewsRef) ||
				 T.SafeDetail != "A petitioner asks to enter this settlement; no resident is admitted by the visit.")) return Fail(
				"ambient purpose lacks its exact proof contract", out Failure);
			return ValidTerminal(T, out Failure);
		}

		internal static string FrozenDigest(KingdomPolityAmbientTransaction T)
		{
			List<string> v = new List<string> { T.Version.ToString(CultureInfo.InvariantCulture),
				((byte)T.Purpose).ToString(CultureInfo.InvariantCulture), T.SourcePolityId ?? "",
				T.SourceSettlementId ?? "", T.SourceSettlementName ?? "", T.SourceZoneId ?? "",
				T.DestinationSettlementId ?? "", T.DestinationSettlementName ?? "",
				T.DestinationZoneId ?? "", T.LocalLocusRef ?? "", T.SafeDetail ?? "",
				T.NewsRef ?? "", T.PreparedTick.ToString(CultureInfo.InvariantCulture) };
			Append(v, "facts", T.FactRefs); Append(v, "manifest", T.ManifestRefs);
			Append(v, "stock", T.PhysicalStockObjectIds);
			return KingdomPolityRules.ActivationDigest("polity-ambient-frozen-v1", v);
		}

		private static bool ValidTerminal(KingdomPolityAmbientTransaction T, out string Failure)
		{
			Failure = null;
			if (T.TerminalChoice == KingdomPolityAmbientTerminalChoice.None)
				return T.TerminalTick == 0L && string.IsNullOrEmpty(T.TerminalReceiptId) &&
					T.AdmissionHandoff == null || Fail("open ambient transaction claims a receipt", out Failure);
			if ((byte)T.TerminalChoice > 4 || T.TerminalTick < T.PreparedTick ||
				!KingdomPolityRules.TypedId(T.TerminalReceiptId, "taf:ambient-receipt:v1:"))
				return Fail("ambient terminal receipt is invalid", out Failure);
			bool migrant = T.Purpose == KingdomPolityCohortPurpose.Migrant;
			if (migrant != (T.TerminalChoice == KingdomPolityAmbientTerminalChoice.PetitionAccepted ||
				T.TerminalChoice == KingdomPolityAmbientTerminalChoice.PetitionRejected) ||
				migrant != (T.AdmissionHandoff != null) || migrant && !ValidHandoff(
					T.AdmissionHandoff, true)) return Fail("ambient terminal choice has wrong authority", out Failure);
			if (migrant && (T.AdmissionHandoff.RealmId != T.SourcePolityId ||
				T.AdmissionHandoff.PolityId != T.SourcePolityId ||
				T.AdmissionHandoff.TargetSettlementId != T.DestinationSettlementId ||
				T.AdmissionHandoff.CauseDigest != T.FrozenDigest ||
				T.AdmissionHandoff.DecidedTick != T.TerminalTick ||
				(T.TerminalChoice == KingdomPolityAmbientTerminalChoice.PetitionAccepted) !=
				 (T.AdmissionHandoff.Decision == KingdomPolityAdmissionDecision.Accepted)))
				return Fail("admission handoff does not bind the petition", out Failure);
			if (!migrant && T.TerminalChoice != (T.Purpose == KingdomPolityCohortPurpose.Trader
				? KingdomPolityAmbientTerminalChoice.AcknowledgedNoTrade
				: KingdomPolityAmbientTerminalChoice.Acknowledged)) return Fail(
				"ambient terminal choice does not match its purpose", out Failure);
			return T.TerminalReceiptId == Receipt(T) || Fail(
				"ambient terminal receipt does not bind the frozen choice", out Failure);
		}

		private static bool ValidHandoff(KingdomPolityAdmissionHandoff H, bool Terminal)
		{
			if (H == null || H.Version != KingdomPolityAdmissionHandoff.CurrentVersion ||
				!KingdomPolityRules.TypedId(H.HandoffId, "taf:admission-handoff:v1:") ||
				!KingdomPolityRules.TypedId(H.RealmId, "taf:realm:") ||
				!KingdomPolityRules.SemanticId(H.PolityId) ||
				!KingdomPolityRules.TypedId(H.CohortId, "taf:cohort:") ||
				!KingdomPolityRules.TypedId(H.MemberId, "taf:cohort-member:") ||
				!KingdomPolityRules.TypedId(H.TargetSettlementId, "taf:settlement:v1:") ||
				!KingdomPolityRules.SemanticId(H.SourceObjectId) || !SafeText(H.SourceZoneId, true) ||
				!SafeText(H.ProposedResidentName, true) || H.PreparedTick < 0L ||
				!KingdomPolityRules.Digest(H.CauseDigest) || !SafeText(H.Fault, false)) return false;
			string id = KingdomPolityRules.ActivationId("taf:admission-handoff:v1:",
				"polity-admission-handoff-v1", H.RealmId, H.PolityId, H.CohortId,
				H.MemberId, H.TargetSettlementId, H.SourceObjectId, H.SourceZoneId,
				H.ProposedResidentName, H.PreparedTick.ToString(CultureInfo.InvariantCulture),
				H.CauseDigest);
			if (H.HandoffId != id) return false;
			bool shape = Terminal ? (H.Decision == KingdomPolityAdmissionDecision.Accepted ||
				H.Decision == KingdomPolityAdmissionDecision.Rejected) && H.DecidedTick >= H.PreparedTick
				: H.Decision == KingdomPolityAdmissionDecision.Pending && H.DecidedTick == 0L;
			if (!shape) return false;
			if (!Terminal || H.Decision != KingdomPolityAdmissionDecision.Accepted)
				return H.AdmissionReceipt == null;
			return H.AdmissionReceipt == null ||
				KingdomPolityAdmissionReceiptRules.Valid(H.AdmissionReceipt, H);
		}

		private static string Receipt(KingdomPolityAmbientTransaction T)
		{
			return KingdomPolityRules.ActivationId("taf:ambient-receipt:v1:",
				"polity-ambient-terminal-v1", T.TransactionId, T.FrozenDigest,
				((byte)T.TerminalChoice).ToString(CultureInfo.InvariantCulture),
				T.TerminalTick.ToString(CultureInfo.InvariantCulture),
				T.AdmissionHandoff?.HandoffId ?? "");
		}

		private static bool CanonicalFacts(List<string> Values)
		{
			return CanonicalOptionalRefs(Values, MaximumFacts) && Values.Count > 0;
		}

		private static bool CanonicalOptionalRefs(List<string> Values, int Maximum)
		{
			if (Values == null || Values.Count > Maximum) return false;
			string prior = null;
			for (int i = 0; i < Values.Count; i++)
			{
				if (!KingdomPolityRules.SemanticId(Values[i]) || prior != null &&
					string.CompareOrdinal(prior, Values[i]) >= 0) return false;
				prior = Values[i];
			}
			return true;
		}

		private static bool OptionalSemantic(string Value)
		{
			return string.IsNullOrEmpty(Value) || KingdomPolityRules.SemanticId(Value);
		}

		private static bool HasPrefix(IList<string> Values, string Prefix)
		{
			for (int i = 0; i < Values.Count; i++)
				if (Values[i].StartsWith(Prefix, StringComparison.Ordinal)) return true;
			return false;
		}

		internal static bool SafeText(string Value, bool Required)
		{
			return KingdomPolityRules.Text(Value, Required) && (Value == null ||
				Value.IndexOf('{') < 0 && Value.IndexOf('}') < 0);
		}

		private static void Append(List<string> Target, string Label, IList<string> Values)
		{
			Target.Add(Label + "#" + Values.Count.ToString(CultureInfo.InvariantCulture));
			for (int i = 0; i < Values.Count; i++) Target.Add(Values[i]);
		}
	}
}
