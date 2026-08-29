using System;
using System.Text;

namespace ThousandAndFirst
{
	public enum KingdomJointOwnerState : byte
	{
		Absent = 0,
		Valid = 1,
		Invalid = 2
	}

	/// <summary>One independently validated semantic owner in the D9 view.</summary>
	[Serializable]
	public sealed class KingdomJointCivicOwnerView
	{
		public string OwnerKey = "";
		public int SourceVersion;
		public string SourceReceiptId = "";
		public string Text = "";
		public string Failure = "";
		public KingdomJointOwnerState State;

		public KingdomJointCivicOwnerView Copy()
		{
			return new KingdomJointCivicOwnerView
			{
				OwnerKey = OwnerKey,
				SourceVersion = SourceVersion,
				SourceReceiptId = SourceReceiptId,
				Text = Text,
				Failure = Failure,
				State = State
			};
		}
	}

	[Serializable]
	public sealed class KingdomJointCivicView
	{
		public KingdomJointCivicOwnerView Creed;
		public KingdomJointCivicOwnerView Covenant;
		public KingdomJointCivicOwnerView Moot;
		public KingdomJointCivicOwnerView Enclave;
	}

	/// <summary>Pure four-owner fan-in. It performs no owner lookup or mutation.</summary>
	public static class KingdomJointCivicViewRules
	{
		public const int MaxReceiptIdBytes = 128;
		public const int MaxReportBytes = 4096;
		public const int MaxFailureBytes = 512;

		public static bool TryBuild(KingdomJointCivicOwnerView Creed,
			KingdomJointCivicOwnerView Covenant, KingdomJointCivicOwnerView Moot,
			KingdomJointCivicOwnerView Enclave, out KingdomJointCivicView View,
			out string Failure)
		{
			View = null;
			Failure = null;
			if (!Valid(Creed, "creed") || !Valid(Covenant, "covenant")
				|| !Valid(Moot, "moot") || !Valid(Enclave, "enclave"))
			{
				Failure = "joint civic owner evidence is malformed";
				return false;
			}
			View = new KingdomJointCivicView
			{
				Creed = Creed.Copy(),
				Covenant = Covenant.Copy(),
				Moot = Moot.Copy(),
				Enclave = Enclave.Copy()
			};
			return true;
		}

		/// <summary>
		/// Whether one piece of ground is owned by exactly one settlement, and by the settlement an
		/// authority claims.
		/// <para>
		/// Ownership has to be unique before it can be evidence. If the seat and a non-seat
		/// settlement both claim a zone, choosing one of them lets a topology fault decide which
		/// settlement an enclave belongs to, and the answer looks equally confident either way; if
		/// neither claims it, the realm simply does not hold that ground and has nothing to say
		/// about what stands on it. Both are refusals, and they are different refusals because they
		/// are different problems.
		/// </para>
		/// <para>
		/// Every fact arrives as an argument and none is looked up, which is what keeps this
		/// judgement testable without a game and stops it from ever loading a zone to make an
		/// answer come out true.
		/// </para>
		/// </summary>
		/// <param name="Seated">Whether the realm's seat claims this exact zone.</param>
		/// <param name="SeatSettlementId">The seat's settlement id, when it is the owner.</param>
		/// <param name="NonSeatSettlementId">The non-seat settlement's id, or null when none
		/// claims this ground.</param>
		/// <param name="ClaimedSettlementId">The settlement the authority names.</param>
		public static bool TryProveOwnedGround(bool Seated, string SeatSettlementId,
			string NonSeatSettlementId, string ClaimedSettlementId, out string Failure)
		{
			if (Seated && NonSeatSettlementId != null)
				return Refuse("this ground is claimed by more than one settlement", out Failure);
			if (!Seated && NonSeatSettlementId == null)
				return Refuse("this is not ground the realm owns", out Failure);
			string owner = Seated ? SeatSettlementId : NonSeatSettlementId;
			if (string.IsNullOrEmpty(owner))
				return Refuse("the settlement that owns this ground has no identity", out Failure);
			if (!KingdomJointCivicViewRules.SemanticId(owner))
				return Refuse("the settlement that owns this ground is not canonically named",
					out Failure);
			if (!string.Equals(owner, ClaimedSettlementId, StringComparison.Ordinal))
				return Refuse("this names a settlement other than the one that owns its ground",
					out Failure);
			Failure = "";
			return true;
		}

		private static bool Refuse(string Text, out string Failure)
		{
			Failure = Text;
			return false;
		}

		internal static bool Valid(KingdomJointCivicOwnerView Value, string OwnerKey)
		{
			if (Value == null || Value.OwnerKey != OwnerKey
				|| !Enum.IsDefined(typeof(KingdomJointOwnerState), Value.State)) return false;
			if (Value.State == KingdomJointOwnerState.Valid)
			{
				return Value.SourceVersion > 0 && SemanticId(Value.SourceReceiptId)
					&& Report(Value.Text) && string.IsNullOrEmpty(Value.Failure);
			}
			return Value.SourceVersion == 0 && string.IsNullOrEmpty(Value.SourceReceiptId)
				&& string.IsNullOrEmpty(Value.Text) && FailureText(Value.Failure);
		}

		internal static bool SemanticId(string Value)
		{
			return Value != null && Value.StartsWith("taf:", StringComparison.Ordinal)
				&& Utf8(Value, MaxReceiptIdBytes);
		}

		internal static bool Report(string Value)
		{
			return !string.IsNullOrWhiteSpace(Value) && Utf8(Value, MaxReportBytes);
		}

		internal static bool FailureText(string Value)
		{
			return !string.IsNullOrWhiteSpace(Value) && Utf8(Value, MaxFailureBytes);
		}

		private static bool Utf8(string Value, int Maximum)
		{
			try
			{
				return Value != null && Value.IndexOf('\0') < 0
					&& new UTF8Encoding(false, true).GetByteCount(Value) <= Maximum;
			}
			catch (EncoderFallbackException) { return false; }
		}
	}
}
