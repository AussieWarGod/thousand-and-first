using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomSealRules
	{
		/// <summary>
		/// True when a string is safe to keep as prose: no control characters, no markup a later
		/// renderer would obey, no brace or ampersand sequence at all.
		/// <para>
		/// Qud's own markup is <c>{{colour|text}}</c> and <c>&amp;Y</c>. A settlement name carrying
		/// either would be obeyed by every string it is later concatenated into &mdash; including
		/// popups and the founding book. Names are sanitized at capture; this is the gate that
		/// proves it happened, and it is checked again on the way in from a file nobody here wrote.
		/// </para>
		/// </summary>
		public static bool IsSafeText(string Value)
		{
			if (Value == null)
			{
				return false;
			}
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (c < ' ' || c == '\u007F' || c == '{' || c == '}' || c == '&' || c == '\\')
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// A player-chosen name as it may be written into a seal: markup removed rather than
		/// escaped, whitespace collapsed, and cut to length on a word where it can be.
		/// </summary>
		/// <param name="Value">Anything, including null.</param>
		/// <param name="MaxChars">The cut. At or below zero returns empty.</param>
		/// <returns>A string satisfying <see cref="IsSafeText"/>; never null.</returns>
		public static string SanitizeText(string Value, int MaxChars)
		{
			if (Value == null || MaxChars <= 0)
			{
				return "";
			}
			StringBuilder sb = new StringBuilder(Value.Length);
			bool space = false;
			int i = 0;
			while (i < Value.Length)
			{
				char c = Value[i];
				// {{colour|text}} keeps its text and loses its tag. Dropping the braces alone would
				// leave the colour word and the bar behind, which reads as garbage in a name and is
				// worse than either keeping the markup or losing the whole thing.
				if (c == '{' && i + 1 < Value.Length && Value[i + 1] == '{')
				{
					i = OpenTag(Value, i);
					continue;
				}
				if (c == '{' || c == '}' || c == '\\')
				{
					i++;
					continue;
				}
				// A colour code is the ampersand and the letter after it. A trailing ampersand with
				// nothing to colour is simply dropped.
				if (c == '&')
				{
					i += (i + 1 < Value.Length) ? 2 : 1;
					continue;
				}
				i++;
				if (c < ' ' || c == '\u007F')
				{
					c = ' ';
				}
				if (c == ' ')
				{
					if (space || sb.Length == 0)
					{
						continue;
					}
					space = true;
					sb.Append(' ');
					continue;
				}
				space = false;
				sb.Append(c);
			}
			while (sb.Length > 0 && sb[sb.Length - 1] == ' ')
			{
				sb.Length--;
			}
			if (sb.Length <= MaxChars)
			{
				return sb.ToString();
			}
			string cut = sb.ToString(0, MaxChars);
			int lastSpace = cut.LastIndexOf(' ');
			if (lastSpace >= MaxChars / 2)
			{
				cut = cut.Substring(0, lastSpace);
			}
			return cut.TrimEnd();
		}

		/// <summary>
		/// Where reading resumes after a <c>{{</c>: past the tag's separator when it has one, past
		/// the braces when it does not. The scan is bounded by the string, so an unclosed tag costs
		/// the rest of the name rather than looping.
		/// </summary>
		private static int OpenTag(string Value, int At)
		{
			for (int i = At + 2; i < Value.Length; i++)
			{
				if (Value[i] == '|')
				{
					return i + 1;
				}
				if (Value[i] == '}' || Value[i] == '{')
				{
					return i;
				}
			}
			return Value.Length;
		}

		/// <summary>
		/// An identifier as it may be written into a seal: everything outside the token alphabet
		/// replaced with a dot, and cut to length. A caller that hands in a path gets a token, not
		/// a path.
		/// </summary>
		/// <returns>A string satisfying <see cref="IsToken"/>; never null; empty for null input.</returns>
		public static string SanitizeToken(string Value, int MaxChars)
		{
			if (Value == null || MaxChars <= 0)
			{
				return "";
			}
			StringBuilder sb = new StringBuilder(Value.Length);
			for (int i = 0; i < Value.Length && sb.Length < MaxChars; i++)
			{
				sb.Append((TokenAlphabet.IndexOf(Value[i]) >= 0) ? Value[i] : '.');
			}
			return sb.ToString();
		}

		/// <summary>
		/// The chronicle as a seal keeps it: the beginning and the end, with a scribe's note where
		/// the copy skips.
		/// <para>
		/// A book longer than the cap loses its middle rather than its head, because the founding
		/// is the half a stranger reads a dead town's book for. The note is written into the copy
		/// so the gap is visible rather than seamless &mdash; a chronicle that quietly omits is
		/// worse than one that says it is a copy.
		/// </para>
		/// </summary>
		/// <param name="Lines">The living register. Null is empty.</param>
		/// <param name="MaxLines">The cap; at or below two returns at most that many head lines.</param>
		/// <returns>A new list; never null; never longer than <paramref name="MaxLines"/>.</returns>
		public static List<string> PinChronicle(IList<string> Lines, int MaxLines)
		{
			List<string> kept = new List<string>();
			if (Lines == null || MaxLines <= 0)
			{
				return kept;
			}
			if (Lines.Count <= MaxLines)
			{
				for (int i = 0; i < Lines.Count; i++)
				{
					kept.Add(SanitizeText(Lines[i], KingdomSealRecord.MaxLineChars));
				}
				return kept;
			}
			if (MaxLines <= 2)
			{
				for (int i = 0; i < MaxLines; i++)
				{
					kept.Add(SanitizeText(Lines[i], KingdomSealRecord.MaxLineChars));
				}
				return kept;
			}
			int head = (MaxLines - 1) / 2;
			int tail = MaxLines - 1 - head;
			for (int i = 0; i < head; i++)
			{
				kept.Add(SanitizeText(Lines[i], KingdomSealRecord.MaxLineChars));
			}
			kept.Add("Here the copy skips " + (Lines.Count - head - tail) + " entries the book no longer holds.");
			for (int i = Lines.Count - tail; i < Lines.Count; i++)
			{
				kept.Add(SanitizeText(Lines[i], KingdomSealRecord.MaxLineChars));
			}
			return kept;
		}
	}
}
