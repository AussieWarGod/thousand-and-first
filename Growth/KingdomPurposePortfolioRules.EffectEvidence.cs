using System;

namespace ThousandAndFirst
{
	internal enum KingdomPurposeEffectCallbackKind : byte
	{
		Invalid = 0,
		RefineRaw = 1,
		HarvestCrop = 2,
		RefinedProduct = 3,
		HarvestSeed = 4,
		HarvestStaple = 5
	}

	internal enum KingdomPurposeEffectProductRole : byte
	{
		Invalid = 0,
		Refined = 1,
		Seed = 2,
		Staple = 3
	}

	internal sealed class KingdomPurposeEffectAttempt
	{
		internal string Receipt;
		internal int Step;
		internal KingdomPurposeEffectCallbackKind Callback;
		internal string ObjectId;
		internal int BeforeCount;
		internal int BeforeTotal;
		internal int ExpectedProgress;
		internal string BeforeRosterDigest;
		internal string AfterRosterDigest;
	}

	internal struct KingdomPurposeEffectProductRecord
	{
		internal int Refined;
		internal int Seed;
		internal int Staple;
	}

	public static partial class KingdomPurposePortfolioRules
	{
		private const string EffectReceiptTag = "purpose-effect-physical";
		private const string EffectAttemptTag = "purpose-effect-attempt";
		private const string EffectRecordTag = "purpose-effect-products";
		private const string EffectFaultTag = "purpose-effect-fault";
		private const string EffectProductTag = "purpose-effect-product";

		internal static bool TryEffectReceipt(string PairId, long PairEpoch,
			string OperationId, KingdomPurposeKind Kind, out string Receipt)
		{
			Receipt = null;
			if (!Id(PairId) || PairEpoch < 1L || !Id(OperationId)
				|| !EffectIsOwed(Kind)) return false;
			return (Receipt = EncodeFields(new string[] { EffectReceiptTag, PairId,
				N(PairEpoch), OperationId, N((int)Kind) })) != null;
		}

		internal static bool TryEffectProductReceipt(string EffectReceipt,
			KingdomPurposeEffectProductRole Role, out string Receipt)
		{
			Receipt = null;
			if (string.IsNullOrEmpty(EffectReceipt)
				|| Role <= KingdomPurposeEffectProductRole.Invalid
				|| Role > KingdomPurposeEffectProductRole.Staple) return false;
			return (Receipt = EncodeFields(new string[] { EffectProductTag,
				EffectReceipt, N((int)Role) })) != null;
		}

		internal static bool TryEffectAttempt(string Receipt, int Step,
			KingdomPurposeEffectCallbackKind Callback, string ObjectId, int BeforeCount,
			int BeforeTotal, int ExpectedProgress, string BeforeRosterDigest,
			string AfterRosterDigest, out string Witness)
		{
			Witness = null;
			if (string.IsNullOrEmpty(Receipt) || Step < 0 || Step >= PurposeEffectExempt
				|| Callback <= KingdomPurposeEffectCallbackKind.Invalid
				|| Callback > KingdomPurposeEffectCallbackKind.HarvestStaple
				|| !Id(ObjectId) || BeforeCount < 0 || BeforeTotal < 0
				|| ExpectedProgress < 0 || !EffectRosterDigest(BeforeRosterDigest)
				|| !EffectRosterDigest(AfterRosterDigest)
				|| BeforeRosterDigest == AfterRosterDigest) return false;
			return (Witness = EncodeFields(new string[] { EffectAttemptTag, Receipt,
				N(Step), N((int)Callback), ObjectId, N(BeforeCount), N(BeforeTotal),
				N(ExpectedProgress), BeforeRosterDigest, AfterRosterDigest })) != null;
		}

		internal static bool TryReadEffectAttempt(string Witness, string Receipt,
			out KingdomPurposeEffectAttempt Attempt)
		{
			Attempt = null;
			if (string.IsNullOrEmpty(Witness) || string.IsNullOrEmpty(Receipt)
				|| !TryDecodeFields(Witness, 10, out string[] f)
				|| f[0] != EffectAttemptTag || f[1] != Receipt
				|| !Int(f[2], out int step) || step < 0 || step >= PurposeEffectExempt
				|| !Int(f[3], out int callback)
				|| callback <= (int)KingdomPurposeEffectCallbackKind.Invalid
				|| callback > (int)KingdomPurposeEffectCallbackKind.HarvestStaple
				|| !Id(f[4]) || !Int(f[5], out int before) || before < 0
				|| !Int(f[6], out int total) || total < 0
				|| !Int(f[7], out int progress) || progress < 0
				|| !EffectRosterDigest(f[8]) || !EffectRosterDigest(f[9])
				|| f[8] == f[9]) return false;
			Attempt = new KingdomPurposeEffectAttempt
			{
				Receipt = Receipt, Step = step,
				Callback = (KingdomPurposeEffectCallbackKind)callback,
					ObjectId = f[4], BeforeCount = before, BeforeTotal = total,
					ExpectedProgress = progress, BeforeRosterDigest = f[8],
					AfterRosterDigest = f[9]
			};
			return EncodeEffectAttempt(Attempt) == Witness;
		}

		internal static string EncodeEffectAttempt(KingdomPurposeEffectAttempt Attempt)
		{
			if (Attempt == null) return null;
			return TryEffectAttempt(Attempt.Receipt, Attempt.Step, Attempt.Callback,
				Attempt.ObjectId, Attempt.BeforeCount, Attempt.BeforeTotal,
				Attempt.ExpectedProgress, Attempt.BeforeRosterDigest,
				Attempt.AfterRosterDigest, out string encoded) ? encoded : null;
		}

		internal static bool EffectRosterDigest(string Digest)
		{
			if (Digest == null || Digest.Length != 64) return false;
			for (int i = 0; i < Digest.Length; i++)
				if (Digest[i] < '0' || Digest[i] > '9'
					&& (Digest[i] < 'a' || Digest[i] > 'f')) return false;
			return true;
		}

		internal static bool TryEffectProductRecord(string Receipt,
			KingdomPurposeEffectProductRecord Record, out string Encoded)
		{
			Encoded = null;
			if (string.IsNullOrEmpty(Receipt) || Record.Refined < 0 || Record.Refined > 1
				|| Record.Seed < 0 || Record.Seed > PurposeEffectSeedUnits
				|| Record.Staple < 0 || Record.Staple > PurposeEffectStapleUnits) return false;
			return (Encoded = EncodeFields(new string[] { EffectRecordTag, Receipt,
				N(Record.Refined), N(Record.Seed), N(Record.Staple) })) != null;
		}

		internal static bool TryReadEffectProductRecord(string Encoded, string Receipt,
			out KingdomPurposeEffectProductRecord Record)
		{
			Record = new KingdomPurposeEffectProductRecord();
			if (string.IsNullOrEmpty(Encoded) || string.IsNullOrEmpty(Receipt)
				|| !TryDecodeFields(Encoded, 5, out string[] f)
				|| f[0] != EffectRecordTag || f[1] != Receipt
				|| !Int(f[2], out Record.Refined) || Record.Refined < 0 || Record.Refined > 1
				|| !Int(f[3], out Record.Seed) || Record.Seed < 0
				|| Record.Seed > PurposeEffectSeedUnits
				|| !Int(f[4], out Record.Staple) || Record.Staple < 0
				|| Record.Staple > PurposeEffectStapleUnits) return false;
			return TryEffectProductRecord(Receipt, Record, out string roundTrip)
				&& roundTrip == Encoded;
		}

		internal static bool TryEffectFault(string Receipt, int Step, string Observation,
			out string Witness)
		{
			Witness = null;
			if (string.IsNullOrEmpty(Receipt) || Step < 0 || Step >= PurposeEffectExempt
				|| string.IsNullOrEmpty(Observation) || Observation.Length > 256
				|| Observation.IndexOf('\0') >= 0) return false;
			return (Witness = EncodeFields(new string[] { EffectFaultTag, Receipt,
				N(Step), Observation })) != null;
		}

		internal static int EffectIndex(int RawIndex)
		{
			return RawIndex == 0 ? 1 : RawIndex;
		}

		internal static bool EffectMarkerIsOurs(string Receipt, int Prefilter,
			bool IndexPresent, int MarkPrefilter, bool MarkPresent, string MarkReceipt)
		{
			return !string.IsNullOrEmpty(Receipt) && Prefilter != 0 && IndexPresent
				&& MarkPresent && MarkPrefilter == Prefilter
				&& string.Equals(MarkReceipt, Receipt, StringComparison.Ordinal);
		}

		internal static bool EffectMarkerIsPresent(bool IndexPresent, bool MarkPresent)
		{
			return IndexPresent || MarkPresent;
		}
	}
}
