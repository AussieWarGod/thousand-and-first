using System;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>Per-receipt authority-hash basis provenance and its envelope tail.
	/// Each receipt carries two integers: the intent cut (BeforeGraph, BeforeArchiveGraph, and
	/// the AfterArchiveGraph copy of it) and the settle cut (AfterGraph). 0 is unresolved and is
	/// never promoted to a guess, so legacy receipts written by envelopes 2-8 keep it. The tail
	/// is fourteen Int32 appended after DirectionalStandingDigest under envelope version 9 and is
	/// present only at that version or later; the v2-v8 receipt frames written by WriteCallback
	/// do not move. Nothing here enters the TAA1 or TAG1 stream and no stored hash is rewritten.
	/// </summary>
	public sealed partial class KingdomRealmArchive
	{
		private static bool RefuseBasis(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}

		/// <summary>Pure shape rule for one receipt's persisted basis pair. Both integers must lie
		/// in 0..KingdomArchivedSettlementCodec.CurrentVersion; a receipt that never began carries
		/// no basis at all; a receipt that began but has not settled carries no settle basis. A
		/// settled receipt may legitimately hold 0 in either slot, because a basis is only ever
		/// pinned when it was proved, never inferred from the nested payload version.</summary>
		internal static bool ValidBasisShape(KingdomRealmCallbackReceipt Value, out string Failure)
		{
			if (Value == null) return RefuseBasis("callback receipt is absent", out Failure);
			return ValidBasisShape(Value.Phase, Value.IntentSettlementSchema,
				Value.SettledSettlementSchema, out Failure);
		}

		/// <summary>Shape rule applied to a candidate pair that has not been published onto
		/// <paramref name="Value"/> yet, so a malformed tail is refused before any field moves.
		/// </summary>
		private static bool ValidBasisShape(KingdomRealmCallbackReceipt Value, int Intent,
			int Settled, out string Failure)
		{
			if (Value == null) return RefuseBasis("callback receipt is absent", out Failure);
			return ValidBasisShape(Value.Phase, Intent, Settled, out Failure);
		}

		/// <summary>The rule itself, over a phase and a pair. No allocation, no reflection, no
		/// cache, and a fixed failure string per refusal so a corrupt tail cannot echo its own
		/// contents into an operator-visible fault.</summary>
		internal static bool ValidBasisShape(KingdomRealmCallbackPhase Phase, int Intent,
			int Settled, out string Failure)
		{
			Failure = null;
			if (!Enum.IsDefined(typeof(KingdomRealmCallbackPhase), Phase))
				return RefuseBasis("callback receipt phase is noncanonical", out Failure);
			if (Intent < 0 || Intent > KingdomArchivedSettlementCodec.CurrentVersion ||
				Settled < 0 || Settled > KingdomArchivedSettlementCodec.CurrentVersion)
				return RefuseBasis("callback hash basis version is out of range", out Failure);
			if (Phase == KingdomRealmCallbackPhase.None && (Intent != 0 || Settled != 0))
				return RefuseBasis("unresolved callback carries a hash basis", out Failure);
			if (Phase != KingdomRealmCallbackPhase.Settled && Settled != 0)
				return RefuseBasis("unsettled callback carries a settle hash basis", out Failure);
			return true;
		}

#if !TAF_TESTS
		/// <summary>Writes the fourteen-integer tail in receipt order, intent then settled per
		/// receipt. The receipts are passed explicitly so the wire order is stated here rather
		/// than inherited from field declaration order.</summary>
		private static void WriteHashBasisTail(SerializationWriter Writer,
			KingdomRealmCallbackReceipt ExileChronicleReceipt,
			KingdomRealmCallbackReceipt ExileAbilityReceipt,
			KingdomRealmCallbackReceipt ReturnChronicleReceipt,
			KingdomRealmCallbackReceipt ReturnReputationReceipt,
			KingdomRealmCallbackReceipt ReturnFeelingsReceipt,
			KingdomRealmCallbackReceipt ReturnSeatReceipt,
			KingdomRealmCallbackReceipt ReturnAbilityReceipt)
		{
			Writer.Write(ExileChronicleReceipt.IntentSettlementSchema);
			Writer.Write(ExileChronicleReceipt.SettledSettlementSchema);
			Writer.Write(ExileAbilityReceipt.IntentSettlementSchema);
			Writer.Write(ExileAbilityReceipt.SettledSettlementSchema);
			Writer.Write(ReturnChronicleReceipt.IntentSettlementSchema);
			Writer.Write(ReturnChronicleReceipt.SettledSettlementSchema);
			Writer.Write(ReturnReputationReceipt.IntentSettlementSchema);
			Writer.Write(ReturnReputationReceipt.SettledSettlementSchema);
			Writer.Write(ReturnFeelingsReceipt.IntentSettlementSchema);
			Writer.Write(ReturnFeelingsReceipt.SettledSettlementSchema);
			Writer.Write(ReturnSeatReceipt.IntentSettlementSchema);
			Writer.Write(ReturnSeatReceipt.SettledSettlementSchema);
			Writer.Write(ReturnAbilityReceipt.IntentSettlementSchema);
			Writer.Write(ReturnAbilityReceipt.SettledSettlementSchema);
		}

		/// <summary>Reads the tail for envelope <paramref name="Version"/>. Below HashBasisVersion
		/// there is no tail: the reader consumes nothing and every basis stays at the 0 its
		/// receipt was constructed with. At or above it, all fourteen integers are read into
		/// locals and every pair is shape-checked before a single field is published, so a short
		/// or malformed tail leaves the receipts exactly as they were. A false return is reported
		/// to ReadCore, whose existing poison/reset/rethrow contract owns the failure.</summary>
		private static bool TryReadHashBasisTail(SerializationReader Reader, int Version,
			KingdomRealmCallbackReceipt ExileChronicleReceipt,
			KingdomRealmCallbackReceipt ExileAbilityReceipt,
			KingdomRealmCallbackReceipt ReturnChronicleReceipt,
			KingdomRealmCallbackReceipt ReturnReputationReceipt,
			KingdomRealmCallbackReceipt ReturnFeelingsReceipt,
			KingdomRealmCallbackReceipt ReturnSeatReceipt,
			KingdomRealmCallbackReceipt ReturnAbilityReceipt, out string Failure)
		{
			Failure = null;
			if (Version < HashBasisVersion) return true;
			int exileChronicleIntent = Reader.ReadInt32();
			int exileChronicleSettled = Reader.ReadInt32();
			int exileAbilityIntent = Reader.ReadInt32();
			int exileAbilitySettled = Reader.ReadInt32();
			int returnChronicleIntent = Reader.ReadInt32();
			int returnChronicleSettled = Reader.ReadInt32();
			int returnReputationIntent = Reader.ReadInt32();
			int returnReputationSettled = Reader.ReadInt32();
			int returnFeelingsIntent = Reader.ReadInt32();
			int returnFeelingsSettled = Reader.ReadInt32();
			int returnSeatIntent = Reader.ReadInt32();
			int returnSeatSettled = Reader.ReadInt32();
			int returnAbilityIntent = Reader.ReadInt32();
			int returnAbilitySettled = Reader.ReadInt32();
			if (!ValidBasisShape(ExileChronicleReceipt, exileChronicleIntent,
					exileChronicleSettled, out Failure) ||
				!ValidBasisShape(ExileAbilityReceipt, exileAbilityIntent,
					exileAbilitySettled, out Failure) ||
				!ValidBasisShape(ReturnChronicleReceipt, returnChronicleIntent,
					returnChronicleSettled, out Failure) ||
				!ValidBasisShape(ReturnReputationReceipt, returnReputationIntent,
					returnReputationSettled, out Failure) ||
				!ValidBasisShape(ReturnFeelingsReceipt, returnFeelingsIntent,
					returnFeelingsSettled, out Failure) ||
				!ValidBasisShape(ReturnSeatReceipt, returnSeatIntent,
					returnSeatSettled, out Failure) ||
				!ValidBasisShape(ReturnAbilityReceipt, returnAbilityIntent,
					returnAbilitySettled, out Failure)) return false;
			ExileChronicleReceipt.IntentSettlementSchema = exileChronicleIntent;
			ExileChronicleReceipt.SettledSettlementSchema = exileChronicleSettled;
			ExileAbilityReceipt.IntentSettlementSchema = exileAbilityIntent;
			ExileAbilityReceipt.SettledSettlementSchema = exileAbilitySettled;
			ReturnChronicleReceipt.IntentSettlementSchema = returnChronicleIntent;
			ReturnChronicleReceipt.SettledSettlementSchema = returnChronicleSettled;
			ReturnReputationReceipt.IntentSettlementSchema = returnReputationIntent;
			ReturnReputationReceipt.SettledSettlementSchema = returnReputationSettled;
			ReturnFeelingsReceipt.IntentSettlementSchema = returnFeelingsIntent;
			ReturnFeelingsReceipt.SettledSettlementSchema = returnFeelingsSettled;
			ReturnSeatReceipt.IntentSettlementSchema = returnSeatIntent;
			ReturnSeatReceipt.SettledSettlementSchema = returnSeatSettled;
			ReturnAbilityReceipt.IntentSettlementSchema = returnAbilityIntent;
			ReturnAbilityReceipt.SettledSettlementSchema = returnAbilitySettled;
			return true;
		}

		/// <summary>Returns every basis to unresolved. Called on the pre-directional read path,
		/// where receipts decoded from a v2-v6 envelope carry no provenance, and again at the end
		/// of the poison reset so the invariant holds there whether or not the receipts were
		/// replaced with fresh instances.</summary>
		private void ClearHashBasis()
		{
			ExileChronicle.IntentSettlementSchema = 0;
			ExileChronicle.SettledSettlementSchema = 0;
			ExileAbility.IntentSettlementSchema = 0;
			ExileAbility.SettledSettlementSchema = 0;
			ReturnChronicle.IntentSettlementSchema = 0;
			ReturnChronicle.SettledSettlementSchema = 0;
			ReturnReputation.IntentSettlementSchema = 0;
			ReturnReputation.SettledSettlementSchema = 0;
			ReturnFeelings.IntentSettlementSchema = 0;
			ReturnFeelings.SettledSettlementSchema = 0;
			ReturnSeat.IntentSettlementSchema = 0;
			ReturnSeat.SettledSettlementSchema = 0;
			ReturnAbility.IntentSettlementSchema = 0;
			ReturnAbility.SettledSettlementSchema = 0;
		}

#endif
	}
}
