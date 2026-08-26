using System;
using System.Text;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomApiRules
	{
		/// <summary>Characters a colour code may run to before the opener is treated as ordinary
		/// text. The engine's own codes are one or two (<c>{{K|</c>, <c>{{rr|</c>).</summary>
		private const int MaxColourCode = 3;

		/// <summary>
		/// An extension-supplied line as the surfaces will carry it: no control characters, no runs
		/// of whitespace, no markup that could open a colour span the rest of the report never
		/// closes, and never longer than <see cref="MaxTextLength"/>.
		/// <para>
		/// <b>Colour is taken away entirely, opener and all.</b> Stripping the braces and leaving
		/// the code behind would put a literal <c>R|</c> in the founder's report, which is worse
		/// than either extreme &mdash; and letting the span through would recolour every line after
		/// it, which is how one mod's ask makes the whole board look like ours is broken.
		/// </para>
		/// <para>
		/// Cut at a word boundary with an ellipsis when it is too long, because a line cut mid-word
		/// reads as corruption and a line refused outright reads as a silent stall.
		/// </para>
		/// </summary>
		public static string Trim(string Text, int Limit)
		{
			if (string.IsNullOrEmpty(Text) || Limit <= 0)
			{
				return "";
			}
			StringBuilder builder = new StringBuilder(Text.Length);
			bool pendingSpace = false;
			for (int i = 0; i < Text.Length; i++)
			{
				char c = Text[i];
				if (c == '}')
				{
					continue;
				}
				if (c == '{')
				{
					i = AfterOpener(Text, i);
					continue;
				}
				if (c <= ' ')
				{
					if (builder.Length > 0)
					{
						pendingSpace = true;
					}
					continue;
				}
				if (pendingSpace)
				{
					builder.Append(' ');
					pendingSpace = false;
				}
				builder.Append(c);
			}
			string clean = builder.ToString();
			if (clean.Length <= Limit)
			{
				return clean;
			}
			// The ellipsis counts. Cutting to Limit and then appending one would return Limit+1
			// characters from a method whose whole contract is a ceiling.
			int room = Limit - 1;
			int cut = clean.LastIndexOf(' ', Math.Min(room, clean.Length - 1));
			if (cut < room / 2)
			{
				cut = room;
			}
			return clean.Substring(0, cut).TrimEnd() + "…";
		}

		/// <summary>
		/// The index of the last character of a colour opener beginning at <paramref name="at"/>,
		/// so the caller's loop skips the whole of it. A run of braces with no pipe close behind it
		/// is just braces, and only the braces are dropped.
		/// </summary>
		private static int AfterOpener(string text, int at)
		{
			int i = at;
			while (i < text.Length && text[i] == '{')
			{
				i++;
			}
			for (int j = i; j < text.Length && j - i <= MaxColourCode; j++)
			{
				if (text[j] == '|')
				{
					return j;
				}
			}
			return i - 1;
		}

		/// <summary>The ordinary clamp: a sentence for a founder to read.</summary>
		public static string Trim(string Text)
		{
			return Trim(Text, MaxTextLength);
		}

		/// <summary>A filing key, slugged and clamped. Empty is a refused ask or notice, never a
		/// blank one.</summary>
		public static string Kind(string Source)
		{
			string slug = Slug(Source);
			return (slug.Length <= MaxKindLength) ? slug : slug.Substring(0, MaxKindLength);
		}

	}
}
