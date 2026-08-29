using System;
using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRules
	{
		public const int CurrentFormatVersion = 4;
		public const int MaxSettlements = 3;
		public const int MaxAudienceReceipts = MaxSettlements;
		public const int MaxTransientBodySlots = 16;
		public const int MaxBodyReservations = MaxTransientBodySlots;
		public const int MaxBodiesPerReservation = 7;
		public const int MaxOfficeReceipts = MaxSettlements;
		public const int MaxRemembranceReceipts = MaxSettlements;
		public const int MaxVoiceReceipts = KingdomCivicVoiceRules.MaxReceipts;
		public const int MaxFirstFeastReceipts = MaxSettlements;
		public const int MaxFaultTextBytes = 256;
		public const int MaxCivicTextBytes = 96;
		public const int HeaderByteBudget = 448;
		public const int OptionByteBudget = 32;
		public const int AudienceRowByteBudget = 444;
		public const int BodyReservationRowByteBudget = 444;
		public const int CivicRowByteBudget = 736;
		public const int VoiceRowByteBudget = 960;
		/// <summary>Exact row maximum is 1,890 bytes; 30 bytes remain per declared row.</summary>
		public const int FirstFeastRowByteBudget = 2584;
		public const int MaxDeclaredPayloadBytes = HeaderByteBudget + (3 * OptionByteBudget)
			+ (MaxAudienceReceipts * AudienceRowByteBudget)
			+ (MaxBodyReservations * BodyReservationRowByteBudget)
			+ (MaxOfficeReceipts * CivicRowByteBudget)
			+ (MaxRemembranceReceipts * CivicRowByteBudget)
			+ (MaxVoiceReceipts * VoiceRowByteBudget)
			+ (MaxFirstFeastReceipts * FirstFeastRowByteBudget);

		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static bool TryValidate(KingdomExperienceLedger Ledger, out string Failure)
		{
			Failure = null;
			if (Ledger == null) return Fail("experience ledger is null", out Failure);
			if (Ledger.SchemaState != KingdomExperienceSchemaState.Compatible)
				return Fail("experience schema is not compatible", out Failure);
			if (Ledger.FormatVersion != CurrentFormatVersion || Ledger.MigratedFromVersion != 0
				|| Ledger.SchemaFault != null
				|| Ledger.OpaqueWireVersion != 0 || Ledger.OpaqueFuturePayload != null
				|| Ledger.Revision < 0L)
				return Fail("experience ledger header is invalid", out Failure);
			if (!ValidOption(Ledger.Story, KingdomExperienceOptionKind.CivicStory)
				|| !ValidOption(Ledger.Knowledge, KingdomExperienceOptionKind.CivicKnowledge)
				|| !ValidOption(Ledger.Ambient, KingdomExperienceOptionKind.AmbientUse))
				return Fail("experience option evidence is invalid", out Failure);
			if (!Ledger.IdentityBound)
			{
				if (!string.IsNullOrEmpty(Ledger.RealmId) || Ledger.Revision != 0L
					|| !Unobserved(Ledger.Story) || !Unobserved(Ledger.Knowledge)
					|| !Unobserved(Ledger.Ambient) || HasRows(Ledger))
					return Fail("unbound experience ledger carries authority", out Failure);
			}
			else if (!TypedId(Ledger.RealmId, "taf:realm:"))
				return Fail("experience realm identity is invalid", out Failure);
			if (Ledger.Audiences == null || Ledger.Audiences.Count > MaxAudienceReceipts
				|| Ledger.BodyReservations == null
				|| Ledger.BodyReservations.Count > MaxBodyReservations
				|| Ledger.Offices == null || Ledger.Offices.Count > MaxOfficeReceipts
				|| Ledger.Remembrances == null
				|| Ledger.Remembrances.Count > MaxRemembranceReceipts
				|| Ledger.Voices == null || Ledger.Voices.Count > MaxVoiceReceipts
				|| Ledger.FirstFeasts == null
				|| Ledger.FirstFeasts.Count > MaxFirstFeastReceipts)
				return Fail("experience collection exceeds capacity", out Failure);
			if (!ValidateAudiences(Ledger, out Failure)
				|| !ValidateBodies(Ledger, out Failure)
				|| !ValidateCivicRows(Ledger, out Failure)
				|| !ValidateVoices(Ledger, out Failure)
				|| !ValidateFirstFeasts(Ledger, out Failure)) return false;
			return true;
		}

		private static bool ValidateAudiences(KingdomExperienceLedger L, out string Failure)
		{
			Failure = null; string prior = null;
			for (int i = 0; i < L.Audiences.Count; i++)
			{
				KingdomExperienceAudienceReceipt r = L.Audiences[i];
				if (r == null || !TypedId(r.ReservationId, "taf:experience-audience:")
					|| !string.Equals(r.RealmId, L.RealmId, StringComparison.Ordinal)
					|| !After(prior, r.ReservationId) || !KernelSemanticId.IsValid(r.SettlementId)
					|| !KernelSemanticId.IsValid(r.SourceId) || !DefinedLane(r.Lane)
					|| !DefinedOption(r.OptionKind) || r.CauseTick < 0L
					|| r.ReservedTick < r.CauseTick || r.EnableEpoch < 1L
					|| !ReceiptOptionValid(L, r.OptionKind, r.CauseTick, r.ReservedTick,
						r.EnableEpoch))
					return Fail("experience audience receipt is invalid", out Failure);
				for (int j = 0; j < i; j++)
					if (L.Audiences[j].SettlementId == r.SettlementId)
						return Fail("settlement has more than one optional audience", out Failure);
				prior = r.ReservationId;
			}
			return true;
		}

		private static bool ValidateBodies(KingdomExperienceLedger L, out string Failure)
		{
			Failure = null; string prior = null; int total = 0;
			for (int i = 0; i < L.BodyReservations.Count; i++)
			{
				KingdomExperienceBodyReservation r = L.BodyReservations[i];
				if (r == null || !TypedId(r.ReservationId, "taf:experience-body:")
					|| !string.Equals(r.RealmId, L.RealmId, StringComparison.Ordinal)
					|| !After(prior, r.ReservationId) || !KernelSemanticId.IsValid(r.SettlementId)
					|| !KernelSemanticId.IsValid(r.SourceId) || !DefinedLane(r.Lane)
					|| !DefinedOption(r.OptionKind) || r.CauseTick < 0L
					|| r.ReservedTick < r.CauseTick || r.EnableEpoch < 1L
					|| r.BodyCount < 1 || r.BodyCount > MaxBodiesPerReservation
					|| total > MaxTransientBodySlots - r.BodyCount
					|| !ReceiptOptionValid(L, r.OptionKind, r.CauseTick, r.ReservedTick,
						r.EnableEpoch))
					return Fail("experience body reservation is invalid", out Failure);
				total += r.BodyCount; prior = r.ReservationId;
			}
			return true;
		}

		private static bool ReceiptOptionValid(KingdomExperienceLedger L,
			KingdomExperienceOptionKind Kind, long CauseTick, long ReservedTick, long Epoch)
		{
			KingdomExperienceOptionReceipt option = OptionFor(L, Kind);
			if (option == null || option.State == KingdomExperienceOptionState.Unobserved
				|| Epoch < 1L || Epoch > option.EnableEpoch) return false;
			if (option.State == KingdomExperienceOptionState.Enabled
				&& Epoch == option.EnableEpoch)
				return CauseTick >= option.FutureCauseFloorTick
					&& ReservedTick >= option.ObservedTick;
			// Disabled and prior-epoch rows are retirement leases only. Their source owner must
			// release them after exact semantic/projection cleanup; request validation never uses
			// this branch, so stale evidence cannot authorize new work.
			return ReservedTick <= option.ObservedTick;
		}

		internal static bool ValidOption(KingdomExperienceOptionReceipt O,
			KingdomExperienceOptionKind Expected)
		{
			if (O == null || O.Kind != Expected || O.ObservedTick < 0L || O.EnableEpoch < 0L)
				return false;
			if (O.State == KingdomExperienceOptionState.Unobserved)
				return O.ObservedTick == 0L && O.EnableEpoch == 0L
					&& O.FutureCauseFloorTick == long.MaxValue;
			if (O.State == KingdomExperienceOptionState.Disabled)
				return O.FutureCauseFloorTick == long.MaxValue;
			if (O.State == KingdomExperienceOptionState.Enabled)
				return O.EnableEpoch >= 1L && O.FutureCauseFloorTick >= 0L
					&& O.FutureCauseFloorTick <= O.ObservedTick;
			return false;
		}

		internal static bool TypedId(string Value, string Prefix)
		{
			return KernelSemanticId.IsValid(Value) && Value.StartsWith(Prefix,
				StringComparison.Ordinal) && Value.Length > Prefix.Length;
		}

		internal static bool Text(string Value, bool Required)
		{
			if (Value == null) return !Required;
			if (Required && Value.Length == 0) return false;
			try { if (StrictUtf8.GetByteCount(Value) > MaxFaultTextBytes) return false; }
			catch (EncoderFallbackException) { return false; }
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return false;
			return true;
		}

		internal static bool VoiceText(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return false;
			try { if (StrictUtf8.GetByteCount(Value) > KingdomCivicVoiceRules.MaxFactsBytes)
				return false; }
			catch (EncoderFallbackException) { return false; }
			for (int i = 0; i < Value.Length; i++)
				if (char.IsControl(Value[i]) && Value[i] != '\n') return false;
			return true;
		}

		internal static bool DefinedLane(KingdomExperienceLane Value)
		{
			return Value >= KingdomExperienceLane.CivicVoices
				&& Value <= KingdomExperienceLane.PolityCohort;
		}

		internal static bool DefinedOption(KingdomExperienceOptionKind Value)
		{
			return Value >= KingdomExperienceOptionKind.CivicStory
				&& Value <= KingdomExperienceOptionKind.AmbientUse;
		}

		internal static bool After(string Prior, string Value)
		{
			return Prior == null || string.CompareOrdinal(Prior, Value) < 0;
		}

		internal static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}

		private static bool HasRows(KingdomExperienceLedger L)
		{
			return (L.Audiences != null && L.Audiences.Count > 0)
				|| (L.BodyReservations != null && L.BodyReservations.Count > 0)
				|| (L.Offices != null && L.Offices.Count > 0)
				|| (L.Remembrances != null && L.Remembrances.Count > 0)
				|| (L.Voices != null && L.Voices.Count > 0)
				|| (L.FirstFeasts != null && L.FirstFeasts.Count > 0);
		}

		private static bool Unobserved(KingdomExperienceOptionReceipt O)
		{
			return O != null && O.State == KingdomExperienceOptionState.Unobserved;
		}
	}
}
