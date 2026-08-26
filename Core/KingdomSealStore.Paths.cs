using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealStore
	{
		private static string TempPath(string PathValue)
		{
			return PathValue + ".writing." + Guid.NewGuid().ToString("N");
		}

		internal string StagePath(string Origin, char Slot)
		{
			if (!KingdomSealReceipt.ValidId(Origin) || (Slot != 'a' && Slot != 'b'))
			{
				throw new ArgumentException("A stage path requires one safe origin and slot.");
			}
			return Path.Combine(_root, StagesFolder, Origin + "." + Slot + SealExtension);
		}

		internal string LegacyPath(string Legacy)
		{
			if (!KingdomSealReceipt.ValidId(Legacy))
			{
				throw new ArgumentException("A legacy path requires one safe legacy id.");
			}
			return Path.Combine(_root, LegaciesFolder, Legacy + SealExtension);
		}

		internal string ReceiptPath(string Legacy, string Target)
		{
			if (!KingdomSealReceipt.ValidId(Legacy) || !KingdomSealReceipt.ValidId(Target))
			{
				throw new ArgumentException("A receipt path requires one safe tuple.");
			}
			return Path.Combine(_root, ReceiptsFolder, ReceiptFileName(Legacy, Target));
		}

		private string ClaimPath(string Legacy, string Target)
		{
			if (!KingdomSealReceipt.ValidId(Legacy) || !KingdomSealReceipt.ValidId(Target))
			{
				throw new ArgumentException("A live-claim path requires one safe tuple.");
			}
			return Path.Combine(_root, ClaimsFolder, ReceiptFileName(Legacy, Target) + ".live");
		}

		private static string ReceiptFileName(string Legacy, string Target)
		{
			return Legacy.Length.ToString(CultureInfo.InvariantCulture) + "_" + Legacy
				+ Target.Length.ToString(CultureInfo.InvariantCulture) + "_" + Target + ReceiptExtension;
		}

		private static bool TryParseReceiptTuple(string FileName, out string Legacy, out string Target)
		{
			Legacy = "";
			Target = "";
			if (FileName == null || !FileName.EndsWith(ReceiptExtension, StringComparison.Ordinal))
			{
				return false;
			}
			string stem = FileName.Substring(0, FileName.Length - ReceiptExtension.Length);
			int at = 0;
			int legacyLength;
			if (!ReadLength(stem, ref at, out legacyLength) || legacyLength <= 0
				|| at + legacyLength > stem.Length)
			{
				return false;
			}
			Legacy = stem.Substring(at, legacyLength);
			at += legacyLength;
			int targetLength;
			if (!ReadLength(stem, ref at, out targetLength) || targetLength <= 0
				|| at + targetLength != stem.Length)
			{
				Legacy = "";
				return false;
			}
			Target = stem.Substring(at, targetLength);
			return KingdomSealReceipt.ValidId(Legacy) && KingdomSealReceipt.ValidId(Target);
		}

		private static bool ReadLength(string Value, ref int At, out int Length)
		{
			Length = 0;
			int start = At;
			while (At < Value.Length && Value[At] >= '0' && Value[At] <= '9')
			{
				if (At - start >= 3)
				{
					return false;
				}
				Length = Length * 10 + (Value[At] - '0');
				At++;
			}
			if (At == start || At >= Value.Length || Value[At] != '_')
			{
				return false;
			}
			At++;
			return Length <= KingdomSealRecord.MaxIdChars;
		}
	}
}
