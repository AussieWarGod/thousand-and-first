using System;
using System.Text;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomApiRules
	{
		/// <summary>A frozen identity name: trimmed, control-free, and bounded. Case is preserved
		/// because culture and species are vanilla display vocabularies rather than filing keys.</summary>
		/// <param name="Source">The engine or extension supplied identity name. Null is empty.</param>
		/// <returns>A bounded display value. Never null.</returns>
		public static string IdentityName(string Source)
		{
			if (string.IsNullOrWhiteSpace(Source))
			{
				return "";
			}
			StringBuilder builder = new StringBuilder();
			bool spacing = false;
			string source = Source.Trim();
			for (int i = 0; i < source.Length && builder.Length < MaxIdentityNameLength; i++)
			{
				char c = source[i];
				if (char.IsControl(c) || char.IsWhiteSpace(c))
				{
					spacing = builder.Length > 0;
					continue;
				}
				if (spacing && builder.Length < MaxIdentityNameLength)
				{
					builder.Append(' ');
				}
				spacing = false;
				if (builder.Length < MaxIdentityNameLength)
				{
					builder.Append(c);
				}
			}
			return builder.ToString();
		}

		/// <summary>One work-kind label handed to an identity source. Empty stays empty; every other
		/// value uses the same bounded filing grammar as asks and notices.</summary>
		/// <param name="Source">The existing work lane's label.</param>
		/// <returns>The bounded canonical label, or empty.</returns>
		public static string IdentityWorkKind(string Source)
		{
			return Kind(Source);
		}

		/// <summary>
		/// Canonicalizes one extension-supplied roster key and proves the source owns its namespace.
		/// Unqualified input is filed under the owning mod. Qualified input is accepted only when its
		/// kind is that same mod slug. Blank, malformed, foreign, or over-long input is dropped.
		/// </summary>
		/// <param name="ModName">The registered owning mod's immutable manifest ID.</param>
		/// <param name="Source">The source's proposed key.</param>
		/// <returns>The owned canonical key, or null when it is unsafe or foreign.</returns>
		public static string IdentityKey(string ModName, string Source)
		{
			string owner = Kind(ModName);
			if (owner.Length == 0 || string.IsNullOrWhiteSpace(Source))
			{
				return null;
			}
			string raw = Source.Trim();
			if (raw.Length > MaxIdentityKeyLength)
			{
				return null;
			}
			string source = IdentityName(raw).ToLowerInvariant();
			if (source.IndexOf('|') >= 0 || source.Length == 0)
			{
				return null;
			}
			int colon = source.IndexOf(':');
			string name;
			if (colon < 0)
			{
				name = source;
			}
			else
			{
				if (colon == 0 || colon == source.Length - 1
					|| !string.Equals(source.Substring(0, colon), owner, StringComparison.Ordinal))
				{
					return null;
				}
				name = source.Substring(colon + 1);
			}
			if (name.Length == 0 || name.IndexOf('|') >= 0)
			{
				return null;
			}
			string key = owner + ":" + name;
			return (key.Length <= MaxIdentityKeyLength) ? key : null;
		}

		/// <summary>Clamps one affinity answer into the Addendum 17 band.</summary>
		/// <param name="Percent">The source's answer, where 100 is neutral.</param>
		/// <returns>The answer clamped to 70&ndash;130.</returns>
		public static int IdentityAffinity(int Percent)
		{
			if (Percent < MinIdentityAffinity) return MinIdentityAffinity;
			if (Percent > MaxIdentityAffinity) return MaxIdentityAffinity;
			return Percent;
		}

		/// <summary>Finalizes an order-independent sum of source deltas around neutral 100. The sum
		/// stays unclamped until every source has spoken; otherwise early saturation would let source
		/// order change a mixed positive/negative answer.</summary>
		internal static int IdentityAffinityFromDelta(long Delta)
		{
			if (Delta <= MinIdentityAffinity - 100L) return MinIdentityAffinity;
			if (Delta >= MaxIdentityAffinity - 100L) return MaxIdentityAffinity;
			return 100 + (int)Delta;
		}
	}
}
