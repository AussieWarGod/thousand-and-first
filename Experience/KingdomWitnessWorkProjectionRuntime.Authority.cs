using System;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Engine-free carrier-claim guard shared by projection and recovery tests.</summary>
	internal static partial class KingdomWitnessWorkProjectionRuntime
	{
		internal static bool TryRequireUnclaimed(KingdomWitnessWorkBook Authority,
			string ObjectId, out string Failure)
		{
			Failure = null;
			if (!KingdomWitnessWorkRules.TryValidate(Authority, out Failure)
				|| !TypedId(ObjectId)) return false;
			for (int i = 0; i < Authority.Rows.Count; i++)
				if (Authority.Rows[i].CarrierObjectId == ObjectId)
				{
					Failure = "That exact surface is already bound to another witness-work receipt.";
					return false;
				}
			return true;
		}

		private static bool TypedId(string Value)
		{
			if (string.IsNullOrEmpty(Value) || !Value.StartsWith("taf:",
				StringComparison.Ordinal) || Value.IndexOf('\0') >= 0) return false;
			try
			{
				return new UTF8Encoding(false, true).GetByteCount(Value)
					<= KingdomWitnessWorkRules.MaxIdBytes;
			}
			catch (EncoderFallbackException) { return false; }
		}
	}
}
