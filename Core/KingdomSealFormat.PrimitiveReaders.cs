using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomSealFormat
	{
		private static bool ReadText(string S, ref int At, int MaxChars, out string Value)
		{
			Value = null;
			if (At >= S.Length || S[At] != '"')
			{
				return false;
			}
			At++;
			StringBuilder sb = new StringBuilder();
			while (true)
			{
				if (At >= S.Length)
				{
					return false;
				}
				char c = S[At++];
				if (c == '"')
				{
					if (sb.Length > MaxChars)
					{
						return false;
					}
					Value = sb.ToString();
					return true;
				}
				// A raw control character is a corrupt file, never a value: the writer escapes
				// every one of them, so meeting one means something else edited this.
				if (c < ' ')
				{
					return false;
				}
				if (c != '\\')
				{
					sb.Append(c);
					if (sb.Length > MaxChars)
					{
						return false;
					}
					continue;
				}
				if (At >= S.Length)
				{
					return false;
				}
				char escape = S[At++];
				if (escape == '"')
				{
					sb.Append('"');
				}
				else if (escape == '\\')
				{
					sb.Append('\\');
				}
				else if (escape == 'u')
				{
					if (At + 4 > S.Length)
					{
						return false;
					}
					int code = 0;
					for (int i = 0; i < 4; i++)
					{
						int digit = HexDigit(S[At + i]);
						if (digit < 0)
						{
							return false;
						}
						code = code * 16 + digit;
					}
					At += 4;
					sb.Append((char)code);
				}
				else
				{
					// Every other escape JSON defines is refused rather than honoured. The writer
					// emits none of them, so an input carrying one did not come from here.
					return false;
				}
				if (sb.Length > MaxChars)
				{
					return false;
				}
			}
		}

		private static bool ReadNumber(string S, ref int At, out long Value)
		{
			Value = 0L;
			int start = At;
			if (At < S.Length && S[At] == '-')
			{
				At++;
			}
			int digits = 0;
			while (At < S.Length && S[At] >= '0' && S[At] <= '9')
			{
				At++;
				digits++;
			}
			if (digits == 0)
			{
				return false;
			}
			// Leading zeros, a decimal point, and an exponent are all outside this grammar. Each
			// would give one value two spellings, and a canonical form cannot have two spellings.
			if (digits > 1 && S[At - digits] == '0')
			{
				return false;
			}
			if (At < S.Length && (S[At] == '.' || S[At] == 'e' || S[At] == 'E'))
			{
				return false;
			}
			return long.TryParse(S.Substring(start, At - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out Value);
		}

		private static int HexDigit(char C)
		{
			if (C >= '0' && C <= '9')
			{
				return C - '0';
			}
			if (C >= 'a' && C <= 'f')
			{
				return C - 'a' + 10;
			}
			if (C >= 'A' && C <= 'F')
			{
				return C - 'A' + 10;
			}
			return -1;
		}

		private static void SkipSpace(string S, ref int At)
		{
			while (At < S.Length && (S[At] == ' ' || S[At] == '\t'))
			{
				At++;
			}
		}

		private static bool Take(string S, ref int At, char C)
		{
			if (At < S.Length && S[At] == C)
			{
				At++;
				return true;
			}
			return false;
		}
	}
}
