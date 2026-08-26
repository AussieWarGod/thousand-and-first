using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealRecord
	{
		private static string StateName(int State)
		{
			return (State >= 0 && State < KingdomRules.InheritedStateNames.Length) ? KingdomRules.InheritedStateNames[State] : "";
		}

		private static int IndexOf(string[] Names, string Value)
		{
			if (Value == null)
			{
				return -1;
			}
			for (int i = 0; i < Names.Length; i++)
			{
				if (Names[i] == Value)
				{
					return i;
				}
			}
			return -1;
		}

		private static bool HasDuplicate(List<string> Values)
		{
			HashSet<string> seen = new HashSet<string>();
			for (int i = 0; i < Values.Count; i++)
			{
				if (!seen.Add(Values[i]))
				{
					return true;
				}
			}
			return false;
		}

		private static List<long> Widen(List<int> Values)
		{
			List<long> wide = new List<long>(Values.Count);
			for (int i = 0; i < Values.Count; i++)
			{
				wide.Add(Values[i]);
			}
			return wide;
		}

		private static bool ReadText(KingdomSealBody Body, string Key, int MaxChars, out string Value, ref KingdomSealFault Fault, ref string Detail)
		{
			Value = "";
			if (Body.KindOf(Key) != KingdomSealKind.Text)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as text";
				return false;
			}
			string text = Body.Text(Key) ?? "";
			if (text.Length > MaxChars)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' is longer than " + MaxChars + " characters";
				return false;
			}
			if (!KingdomSealRules.IsSafeText(text))
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' carries something no name may carry";
				return false;
			}
			Value = text;
			return true;
		}

		private static bool ReadToken(KingdomSealBody Body, string Key, out string Value, ref KingdomSealFault Fault, ref string Detail)
		{
			Value = "";
			if (Body.KindOf(Key) != KingdomSealKind.Text)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as text";
				return false;
			}
			string text = Body.Text(Key) ?? "";
			if (text.Length > MaxIdChars || !KingdomSealRules.IsToken(text))
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' is not an identifier this build accepts";
				return false;
			}
			Value = text;
			return true;
		}

		private static bool ReadOptionalToken(KingdomSealBody Body, string Key, out string Value, ref KingdomSealFault Fault, ref string Detail)
		{
			Value = "";
			if (Body.KindOf(Key) != KingdomSealKind.Text)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as text";
				return false;
			}
			string text = Body.Text(Key) ?? "";
			if (text.Length > MaxIdChars || (text.Length > 0 && !KingdomSealRules.IsToken(text)))
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' is not an identifier this build accepts";
				return false;
			}
			Value = text;
			return true;
		}

		private static bool ReadBoundedToken(KingdomSealBody Body, string Key, int Maximum,
			out string Value, ref KingdomSealFault Fault, ref string Detail)
		{
			Value = "";
			if (Body.KindOf(Key) != KingdomSealKind.Text)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as text";
				return false;
			}
			string text = Body.Text(Key) ?? "";
			if (text.Length == 0 || text.Length > Maximum || !KingdomSealRules.IsToken(text))
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' is not a bounded identifier this build accepts";
				return false;
			}
			Value = text;
			return true;
		}

		private static bool ReadLong(KingdomSealBody Body, string Key, long Low, long High, out long Value, ref KingdomSealFault Fault, ref string Detail)
		{
			Value = 0L;
			if (Body.KindOf(Key) != KingdomSealKind.Number)
			{
				Fault = KingdomSealFault.WrongKind;
				Detail = "'" + Key + "' is not written as a number";
				return false;
			}
			long number = Body.Number(Key);
			if (number < Low || number > High)
			{
				Fault = KingdomSealFault.OutOfBounds;
				Detail = "'" + Key + "' is " + number + ", outside " + Low + " to " + High;
				return false;
			}
			Value = number;
			return true;
		}

		private static bool ReadInt(KingdomSealBody Body, string Key, int Low, int High, out int Value, ref KingdomSealFault Fault, ref string Detail)
		{
			Value = 0;
			long wide;
			if (!ReadLong(Body, Key, Low, High, out wide, ref Fault, ref Detail))
			{
				return false;
			}
			Value = (int)wide;
			return true;
		}

	}
}
