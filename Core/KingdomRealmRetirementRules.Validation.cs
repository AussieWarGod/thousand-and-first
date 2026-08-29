using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomRealmRetirementRules
	{
		public const int MaxIdChars = 512;
		public const int MaxDetailChars = 1024;
		public const string AuthorityRecordId = "taf:realm-authority:v1";
		public const string FenceRecordId = "r_TAF_RealmIdentityFence_v1";

		public static bool Valid(KingdomRealmRetirementState State, out string Failure)
		{
			Failure = null;
			if (State == null || State.Version != KingdomRealmRetirementState.CurrentVersion
				|| !Enum.IsDefined(typeof(KingdomRealmRetirementPhase), State.Phase)
				|| State.Phase == KingdomRealmRetirementPhase.None || State.Revision <= 0
				|| !LowerHex(State.ReceiptId, 32) || !Text(State.RealmId, false)
				|| !Text(State.FactionId, false) || !Text(State.GameId, false)
				|| State.RealmIncarnation < 0L || State.StartedTick < 0L
				|| State.UpdatedTick < State.StartedTick || !Digest(State.AuthorityDigest)
				|| !Detail(State.Fault) || State.Locators == null || State.Records == null
				|| State.Locators.Count == 0
				|| State.Locators.Count > KingdomRealmRetirementState.MaxLocators
				|| State.Records.Count > KingdomRealmRetirementState.MaxRecords)
				return Fail("retirement header is malformed or outside its bounds", out Failure);
			string priorZone = null;
			for (int i = 0; i < State.Locators.Count; i++)
			{
				KingdomRemovalLocator row = State.Locators[i];
				if (row == null || !Text(row.ZoneId, false) || !Text(row.SettlementId, true)
					|| !Enum.IsDefined(typeof(KingdomRemovalLocatorState), row.State)
					|| row.Revision < 0 || row.CleanedTick < 0L || row.ObjectCount < 0
					|| !OptionalDigest(row.EvidenceDigest)
					|| (row.State == KingdomRemovalLocatorState.Cleaned
						? row.CleanedTick < State.StartedTick || row.EvidenceDigest == null
						: row.CleanedTick != 0L || row.EvidenceDigest != null)
					|| (priorZone != null && string.CompareOrdinal(priorZone, row.ZoneId) >= 0))
					return Fail("retirement locator rows are malformed, duplicate, or unsorted", out Failure);
				if (row.State == KingdomRemovalLocatorState.Cleaned
					&& (row.Revision <= 0 || row.EvidenceDigest == null))
					return Fail("cleaned ground lacks committed evidence", out Failure);
				priorZone = row.ZoneId;
			}
			string priorRecord = null;
			for (int i = 0; i < State.Records.Count; i++)
			{
				KingdomRemovalRecord row = State.Records[i];
				string key = RecordKey(row);
				if (row == null || !Enum.IsDefined(typeof(KingdomRemovalProjectionKind), row.Kind)
					|| !Enum.IsDefined(typeof(KingdomRemovalDisposition), row.Disposition)
					|| !Text(row.Id, false) || !OptionalDigest(row.BeforeDigest)
					|| !OptionalDigest(row.AfterDigest) || !Detail(row.Detail)
					|| (priorRecord != null && string.CompareOrdinal(priorRecord, key) >= 0))
					return Fail("retirement projection rows are malformed, duplicate, or unsorted", out Failure);
				priorRecord = key;
			}
			if ((State.Phase == KingdomRealmRetirementPhase.ReadyForFence
				|| State.Phase == KingdomRealmRetirementPhase.FenceCommitted
				|| State.Phase == KingdomRealmRetirementPhase.PreparedForRemoval)
				&& !CanCommitFence(State, out Failure)) return false;
			if ((State.Phase == KingdomRealmRetirementPhase.FenceCommitted
				|| State.Phase == KingdomRealmRetirementPhase.PreparedForRemoval)
				&& !HasFenceCommitRecord(State))
				return Fail("prepared removal lacks exact base-fence evidence", out Failure);
			if (!KingdomRealmRetirementCodec.FitsPayload(State))
				return Fail("retirement receipt exceeds its exact wire bounds", out Failure);
			return true;
		}

		public static bool CanCommitFence(KingdomRealmRetirementState State,
			out string Failure)
		{
			Failure = null;
			if (State == null) return Fail("retirement receipt is absent", out Failure);
			for (int i = 0; i < State.Locators.Count; i++)
				if (State.Locators[i].State != KingdomRemovalLocatorState.Cleaned)
					return Fail("tracked ground still requires an attended cleanup", out Failure);
			for (int i = 0; i < State.Records.Count; i++)
			{
				KingdomRemovalDisposition d = State.Records[i].Disposition;
				if (d == KingdomRemovalDisposition.Pending || d == KingdomRemovalDisposition.Blocked
					|| d == KingdomRemovalDisposition.Diverged)
					return Fail("an owned projection is pending, blocked, or diverged", out Failure);
			}
			if (!HasRecord(State, KingdomRemovalProjectionKind.Authority,
				AuthorityRecordId, KingdomRemovalDisposition.Closed))
				return Fail("realm authority closure has not been receipted", out Failure);
			return true;
		}

		public static bool HasFenceCommitRecord(KingdomRealmRetirementState State)
		{
			if (State == null) return false;
			for (int i = 0; i < State.Records.Count; i++)
			{
				KingdomRemovalRecord row = State.Records[i];
				if (row != null && row.Kind == KingdomRemovalProjectionKind.GlobalState
					&& row.Id == FenceRecordId
					&& row.Disposition == KingdomRemovalDisposition.Preserved
					&& Digest(row.BeforeDigest) && Digest(row.AfterDigest)
					&& row.BeforeDigest != row.AfterDigest) return true;
			}
			return false;
		}

		public static bool AllTrackedProjectionsClosed(KingdomRealmRetirementState State)
		{
			if (!CanCommitFence(State, out string _)) return false;
			for (int i = 0; i < State.Records.Count; i++)
				if (State.Records[i].Disposition == KingdomRemovalDisposition.TerminalIntent
					|| State.Records[i].Disposition == KingdomRemovalDisposition.PriorUnknown
					|| State.Records[i].Disposition == KingdomRemovalDisposition.Untracked)
					return false;
			return true;
		}

		/// <summary>Conditional preparation may retain disclosed legacy unknowns.</summary>
		public static bool KnownProjectionClosurePermitsPreparation(
			KingdomRealmRetirementState State, out string Failure)
		{
			return CanCommitFence(State, out Failure);
		}

		/// <summary>No tracked row admits prior-unknown or untracked residue.</summary>
		public static bool CleanRemovalProvable(KingdomRealmRetirementState State)
		{
			return AllTrackedProjectionsClosed(State);
		}

		internal static string RecordKey(KingdomRemovalRecord Row)
		{
			return Row == null ? null : ((int)Row.Kind).ToString("D3") + "\u001f" + Row.Id;
		}

		private static bool HasRecord(KingdomRealmRetirementState State,
			KingdomRemovalProjectionKind Kind, string Id,
			KingdomRemovalDisposition Disposition)
		{
			for (int i = 0; i < State.Records.Count; i++)
			{
				KingdomRemovalRecord row = State.Records[i];
				if (row != null && row.Kind == Kind && row.Id == Id
					&& row.Disposition == Disposition) return true;
			}
			return false;
		}

		internal static bool Text(string Value, bool Empty)
		{
			return Value != null && Value.Length <= MaxIdChars
				&& (Empty || Value.Length > 0);
		}

		internal static bool Detail(string Value)
		{
			return Value != null && Value.Length <= MaxDetailChars;
		}

		internal static bool Digest(string Value)
		{
			return LowerHex(Value, 64);
		}

		internal static bool OptionalDigest(string Value)
		{
			return Value == null || Digest(Value);
		}

		internal static bool LowerHex(string Value, int Length)
		{
			if (Value == null || Value.Length != Length) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
			}
			return true;
		}

		internal static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
