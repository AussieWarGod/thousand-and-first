using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealRecord
	{
		private static bool ReadTexts(KingdomSealBody Body, string Key, int MaxItems, int MaxChars, out List<string> Values, ref KingdomSealFault Fault, ref string Detail)
		{
			Values = null;
			List<string> list = Body.TextList(Key);
			if (list == null)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as a list of text";
				return false;
			}
			if (list.Count > MaxItems)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' holds " + list.Count + " entries; no more than " + MaxItems + " may cross";
				return false;
			}
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].Length > MaxChars || !KingdomSealRules.IsSafeText(list[i]))
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "an entry of '" + Key + "' is too long, or carries something no line may carry";
					return false;
				}
			}
			Values = list;
			return true;
		}

		private static bool ReadTokens(KingdomSealBody Body, string Key, int MaxItems, out List<string> Values, ref KingdomSealFault Fault, ref string Detail)
		{
			Values = null;
			List<string> list = Body.TextList(Key);
			if (list == null)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as a list of text";
				return false;
			}
			if (list.Count > MaxItems)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' holds " + list.Count + " entries; no more than " + MaxItems + " may cross";
				return false;
			}
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].Length == 0 || list[i].Length > MaxIdChars || !KingdomSealRules.IsToken(list[i]))
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "an entry of '" + Key + "' is not an identifier this build accepts";
					return false;
				}
			}
			Values = list;
			return true;
		}

		private static bool ReadBoundedTokens(KingdomSealBody Body, string Key, int MaxItems,
			int MaxChars, out List<string> Values, ref KingdomSealFault Fault, ref string Detail)
		{
			Values = null;
			List<string> list = Body.TextList(Key);
			if (list == null)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as a list of text";
				return false;
			}
			if (list.Count > MaxItems)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' exceeds its row cap";
				return false;
			}
			for (int i = 0; i < list.Count; i++)
				if (string.IsNullOrEmpty(list[i]) || list[i].Length > MaxChars ||
					!KingdomSealRules.IsToken(list[i]))
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "an entry of '" + Key + "' is not a bounded canonical token";
					return false;
				}
			Values = list;
			return true;
		}

		private static bool ReadInts(KingdomSealBody Body, string Key, int MaxItems, int Low, int High, out List<int> Values, ref KingdomSealFault Fault, ref string Detail)
		{
			Values = null;
			List<long> list = Body.NumberList(Key);
			if (list == null)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as a list of numbers";
				return false;
			}
			if (list.Count > MaxItems)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' holds " + list.Count + " entries; no more than " + MaxItems + " may cross";
				return false;
			}
			List<int> narrow = new List<int>(list.Count);
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] < Low || list[i] > High)
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "an entry of '" + Key + "' is outside " + Low + " to " + High;
					return false;
				}
				narrow.Add((int)list[i]);
			}
			Values = narrow;
			return true;
		}

		private static string EncodeEvidence(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "";
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(Value);
			if (bytes.Length > 1024)
				throw new InvalidOperationException("Immutable identity evidence exceeds seal cap.");
			return Convert.ToBase64String(bytes);
		}

		private static bool TryDecodeEvidence(string Value, out string Evidence)
		{
			Evidence = "";
			if (string.IsNullOrEmpty(Value)) return true;
			if (Value.Length > 1400) return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(Value);
				if (bytes.Length > 1024) return false;
				Evidence = new UTF8Encoding(false, true).GetString(bytes);
				return true;
			}
			catch { Evidence = ""; return false; }
		}

		private static bool TryParseHex64(string Value, out ulong Parsed)
		{
			Parsed = 0UL;
			if (Value == null || Value.Length != 16) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				int digit = c >= '0' && c <= '9' ? c - '0' :
					(c >= 'a' && c <= 'f' ? c - 'a' + 10 : -1);
				if (digit < 0) { Parsed = 0UL; return false; }
				Parsed = (Parsed << 4) | (uint)digit;
			}
			return true;
		}

		/// <summary>One line naming this seal for a log or a tester. Never player-facing.</summary>
		public string Describe()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(StatusNames[(int)Status]).Append(' ').Append(LegacyId).Append(" lineage=").Append(LineageId).Append(" gen=").Append(Generation)
				.Append(" rev=").Append(Revision).Append(" origin=").Append(OriginGameId)
				.Append(" '").Append(SettlementName).Append("' vigour=").Append(Vigour)
				.Append(" roll=").Append(InterregnumRoll).Append(" state=").Append(StateName(InheritedState))
				.Append(" works=").Append(WorkKeys.Count).Append(" roll=").Append(RollNames.Count)
				.Append(" chronicle=").Append(Chronicle.Count);
			return sb.ToString();
		}
	}
}
