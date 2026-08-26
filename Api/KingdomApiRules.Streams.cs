using System;
using System.Text;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomApiRules
	{
		/// <summary>
		/// The draw stream one extension lane runs on, in the kernel's own grammar.
		/// <para>
		/// Owning a stream per (mod, lane) is what makes an extension's draws unable to shift
		/// ours or another extension's: ordinal ownership is one <c>(EventStreamId,
		/// EventKindCode)</c> lane, so two mods at ordinal zero cannot collide (&sect;2.4).
		/// </para>
		/// </summary>
		/// <param name="ModName">The owning mod's immutable manifest ID. Slugged.</param>
		/// <param name="Lane">The extension's own name for this stream. Slugged.</param>
		/// <param name="Stream">The stream id, or empty on refusal.</param>
		/// <returns>False when either part slugs away to nothing or the pair will not fit the
		/// kernel's 128-byte identifier. The caller refuses the draw; it never invents a
		/// substitute stream, because a substituted stream is a silently different random
		/// sequence.</returns>
		public static bool TryStream(string ModName, string Lane, out string Stream)
		{
			Stream = "";
			string mod = Slug(ModName);
			string lane = Slug(Lane);
			if (string.IsNullOrEmpty(mod) || string.IsNullOrEmpty(lane))
			{
				return false;
			}
			string candidate = StreamPrefix + mod + ":" + lane;
			if (candidate.Length > MaxStreamLength)
			{
				return false;
			}
			Stream = candidate;
			return true;
		}

		/// <summary>
		/// A name reduced to the kernel identifier alphabet: lowercase, digits, and the three
		/// separators <c>. _ -</c>. Everything else becomes a hyphen, runs of hyphens collapse,
		/// and leading and trailing hyphens are dropped.
		/// </summary>
		/// <returns>The slug, or empty when nothing survives.</returns>
		public static string Slug(string Source)
		{
			if (string.IsNullOrEmpty(Source))
			{
				return "";
			}
			StringBuilder builder = new StringBuilder(Source.Length);
			bool pendingSeparator = false;
			for (int i = 0; i < Source.Length; i++)
			{
				char c = Source[i];
				if (c >= 'A' && c <= 'Z')
				{
					c = (char)(c + 32);
				}
				bool plain = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_';
				if (plain)
				{
					if (pendingSeparator && builder.Length > 0)
					{
						builder.Append('-');
					}
					pendingSeparator = false;
					builder.Append(c);
					continue;
				}
				if (builder.Length > 0)
				{
					pendingSeparator = true;
				}
			}
			return builder.ToString();
		}

	}
}
