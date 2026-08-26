using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomSealFormat
	{
		private static bool ParsePayload(string Payload, out KingdomSealBody Body, out KingdomSealFault Fault, out string Detail)
		{
			Body = null;
			Fault = KingdomSealFault.Malformed;
			Detail = "the seal's record is malformed";
			int at = 0;
			KingdomSealBody body = new KingdomSealBody();
			HashSet<string> seen = new HashSet<string>();
			SkipSpace(Payload, ref at);
			if (!Take(Payload, ref at, '{'))
			{
				return false;
			}
			SkipSpace(Payload, ref at);
			if (Take(Payload, ref at, '}'))
			{
				SkipSpace(Payload, ref at);
				if (at != Payload.Length)
				{
					Fault = KingdomSealFault.TrailingData;
					Detail = "there is more after the seal's record";
					return false;
				}
				Body = body;
				Fault = KingdomSealFault.None;
				Detail = "";
				return true;
			}
			while (true)
			{
				Fault = KingdomSealFault.Malformed;
				Detail = "the seal's record is malformed";
				if (body.Count >= MaxKeys)
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "the seal's record carries too many fields";
					return false;
				}
				SkipSpace(Payload, ref at);
				string key;
				if (!ReadText(Payload, ref at, MaxKeyChars, out key) || key.Length == 0)
				{
					Detail = "a key in the seal's record is malformed";
					return false;
				}
				if (!seen.Add(key))
				{
					Fault = KingdomSealFault.DuplicateKey;
					Detail = "the key '" + key + "' appears twice";
					return false;
				}
				SkipSpace(Payload, ref at);
				if (!Take(Payload, ref at, ':'))
				{
					return false;
				}
				SkipSpace(Payload, ref at);
				if (!ReadValue(Payload, ref at, body, key, out Fault, out Detail))
				{
					return false;
				}
				SkipSpace(Payload, ref at);
				if (Take(Payload, ref at, ','))
				{
					continue;
				}
				if (Take(Payload, ref at, '}'))
				{
					break;
				}
				Fault = KingdomSealFault.Malformed;
				Detail = "the seal's record does not close";
				return false;
			}
			SkipSpace(Payload, ref at);
			if (at != Payload.Length)
			{
				Fault = KingdomSealFault.TrailingData;
				Detail = "there is more after the seal's record";
				return false;
			}
			Body = body;
			Fault = KingdomSealFault.None;
			Detail = "";
			return true;
		}

		private static bool ReadValue(string S, ref int At, KingdomSealBody Body, string Key, out KingdomSealFault Fault, out string Detail)
		{
			Fault = KingdomSealFault.Malformed;
			Detail = "the value of '" + Key + "' is malformed";
			if (At >= S.Length)
			{
				return false;
			}
			char c = S[At];
			if (c == '"')
			{
				string text;
				if (!ReadText(S, ref At, MaxValueChars, out text))
				{
					return false;
				}
				Body.Adopt(Key, KingdomSealKind.Text, text, 0L, null, null);
				Fault = KingdomSealFault.None;
				Detail = "";
				return true;
			}
			if (c == '-' || (c >= '0' && c <= '9'))
			{
				long number;
				if (!ReadNumber(S, ref At, out number))
				{
					return false;
				}
				Body.Adopt(Key, KingdomSealKind.Number, null, number, null, null);
				Fault = KingdomSealFault.None;
				Detail = "";
				return true;
			}
			if (c != '[')
			{
				return false;
			}
			At++;
			SkipSpace(S, ref At);
			if (At >= S.Length)
			{
				return false;
			}
			if (Take(S, ref At, ']'))
			{
				Body.Adopt(Key, KingdomSealKind.EmptyList, null, 0L, null, null);
				Fault = KingdomSealFault.None;
				Detail = "";
				return true;
			}
			bool textList = S[At] == '"';
			List<string> texts = textList ? new List<string>() : null;
			List<long> numbers = textList ? null : new List<long>();
			while (true)
			{
				SkipSpace(S, ref At);
				if (At >= S.Length)
				{
					return false;
				}
				if (textList)
				{
					string item;
					if (S[At] != '"' || !ReadText(S, ref At, MaxValueChars, out item))
					{
						return false;
					}
					texts.Add(item);
				}
				else
				{
					long item;
					if (!ReadNumber(S, ref At, out item))
					{
						return false;
					}
					numbers.Add(item);
				}
				if ((textList ? texts.Count : numbers.Count) > MaxArrayItems)
				{
					Fault = KingdomSealFault.OutOfBounds;
					Detail = "the list in '" + Key + "' carries too many entries";
					return false;
				}
				SkipSpace(S, ref At);
				if (Take(S, ref At, ','))
				{
					continue;
				}
				if (Take(S, ref At, ']'))
				{
					break;
				}
				return false;
			}
			Body.Adopt(Key, textList ? KingdomSealKind.TextList : KingdomSealKind.NumberList, null, 0L, texts, numbers);
			Fault = KingdomSealFault.None;
			Detail = "";
			return true;
		}
	}
}
