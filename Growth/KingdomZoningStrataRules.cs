using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomZoningRules
	{
		// ==================================================================================
		// Strata (Addendum 15): the catalogue has more than one set, and a design says which.
		// ==================================================================================

		/// <summary>Ground with the weather over it. Every design written before strata existed
		/// belongs here, and the token is the one <see cref="StrataAdmits"/> falls back to when a
		/// design says nothing about the sky.</summary>
		public const string StratumSurface = "surface";

		/// <summary>Under the rock: carved works whose enclosure is free because the rock is the
		/// wall, and a food lane grown rather than sown.</summary>
		public const string StratumDeep = "deep";

		/// <summary>Ground that is another building's roof. A FILTERED SUBSET of
		/// <see cref="StratumSurface"/> rather than a set of its own (Addendum 15), which is
		/// exactly how <see cref="StrataAdmits"/> reads it.</summary>
		public const string StratumSky = "sky";

		/// <summary>Inside the shell. Named here so the vocabulary is whole and the crown wave adds
		/// records rather than tokens &mdash; Addendum 15 holds the arcology SET back until the
		/// capital exists, and naming a token ships no set.</summary>
		public const string StratumArcology = "arcology";

		/// <summary>
		/// The stratum a design LIVES in: the first welcomed token of its <c>Strata</c> list.
		/// A design that declares nothing, or nothing but refusals, lives on the surface &mdash;
		/// which is where the whole catalogue lived before this attribute existed.
		/// </summary>
		public static string HomeStratum(string Strata)
		{
			foreach (string token in Tokens(Strata))
			{
				if (token[0] != NegationPrefix && token != AnyToken)
				{
					return token;
				}
			}
			return StratumSurface;
		}

		/// <summary>
		/// Every stratum a design may stand in beside its home, in the order the author wrote them.
		/// Empty for a design that shares nowhere, and empty for one that declares nothing &mdash;
		/// a design with no <c>Strata</c> stands everywhere by default rather than by sharing, and
		/// saying otherwise would put every entry in the catalogue in every set.
		/// </summary>
		public static List<string> StrataShared(string Strata)
		{
			List<string> shared = new List<string>();
			string home = null;
			foreach (string token in Tokens(Strata))
			{
				if (token[0] == NegationPrefix || token == AnyToken)
				{
					continue;
				}
				if (home == null)
				{
					home = token;
					continue;
				}
				if (!shared.Contains(token))
				{
					shared.Add(token);
				}
			}
			return shared;
		}

		/// <summary>
		/// Whether a design may stand in one stratum. The tag idiom
		/// (<see cref="TagAccepts"/>) with one clause added, and the clause is Addendum 15's
		/// second ruling: <b>the sky is a filtered subset of the surface</b>. A design that never
		/// mentions the sky is admitted there exactly as it is admitted to the surface, so the sky
		/// set is the surface set minus what filters itself out with <c>!sky</c> rather than a
		/// second enumeration somebody has to keep in step.
		/// <para>
		/// An absent list admits every stratum. That is the whole back-compatibility promise: the
		/// weep-tap declares nothing and goes on being cut into rock, and no shipped entry &mdash;
		/// ours or a third party's &mdash; loses ground the day this attribute lands.
		/// </para>
		/// </summary>
		/// <param name="Strata">The design's <c>Strata</c> attribute. Null and empty admit
		/// everything.</param>
		/// <param name="Stratum">The ground's stratum, from <see cref="StratumOfGround"/>. Null is
		/// admitted, so a caller who cannot name the ground is never the reason for a refusal.</param>
		public static bool StrataAdmits(string Strata, string Stratum)
		{
			List<string> tokens = Tokens(Strata);
			if (tokens.Count == 0)
			{
				return true;
			}
			string stratum = Fold(Stratum);
			if (stratum == null)
			{
				return true;
			}
			if (stratum == StratumSky && !Mentions(tokens, StratumSky))
			{
				return TagAccepts(Strata, StratumSurface);
			}
			return TagAccepts(Strata, stratum);
		}

		/// <summary>
		/// The stratum a piece of ground is: the two the ground itself can name today. The sky and
		/// the arcology are ground some other building carries, so nothing derives them from a zone
		/// id and this never returns them.
		/// </summary>
		/// <param name="Underground">Whether the ground is below <c>KingdomRules.SurfaceZLevel</c>
		/// (<c>KingdomPlotRules.IsUnderground</c>).</param>
		public static string StratumOfGround(bool Underground)
		{
			return Underground ? StratumDeep : StratumSurface;
		}

		/// <summary>
		/// What the founder calls a stratum. A token this build does not ship comes back as the
		/// author wrote it, because the strata set is open the way every tag set here is: a third
		/// party's seabed names itself in the refusal rather than reading as a blank.
		/// <para>
		/// Deliberately not the same words as <see cref="StratumName(bool)"/>, which is older and
		/// answers a different question: that one names the WEATHER a design was refused for want
		/// of, and this one names the GROUND a design belongs to. A carved cell wants no weather
		/// and is still refused on the surface.
		/// </para>
		/// </summary>
		public static string StratumName(string Stratum)
		{
			string stratum = Fold(Stratum);
			if (stratum == null || stratum == StratumSurface)
			{
				return "open ground";
			}
			if (stratum == StratumDeep)
			{
				return "the deep";
			}
			if (stratum == StratumSky)
			{
				return "the sky";
			}
			if (stratum == StratumArcology)
			{
				return "the arcology";
			}
			return stratum;
		}

		/// <summary>
		/// A <c>Strata</c> list read back as founder-facing prose: "the deep", "the deep or open
		/// ground", "open ground, but never the sky". The sentence a refusal owes the founder
		/// (STANDARDS 7b) &mdash; it names every stratum that WOULD take the design, not merely the
		/// one that would not.
		/// </summary>
		/// <returns>Null when the list gates nothing, so a caller can drop the whole clause.</returns>
		public static string DescribeStrata(string Strata)
		{
			List<string> tokens = Tokens(Strata);
			if (tokens.Count == 0)
			{
				return null;
			}
			List<string> welcomed = new List<string>();
			List<string> refused = new List<string>();
			bool takesAll = false;
			for (int i = 0; i < tokens.Count; i++)
			{
				string token = tokens[i];
				if (token[0] == NegationPrefix)
				{
					string name = token.Substring(1).Trim();
					if (name.Length > 0 && !refused.Contains(StratumName(name)))
					{
						refused.Add(StratumName(name));
					}
					continue;
				}
				if (token == AnyToken)
				{
					takesAll = true;
					continue;
				}
				if (!welcomed.Contains(StratumName(token)))
				{
					welcomed.Add(StratumName(token));
				}
			}
			if (welcomed.Count == 0 && refused.Count == 0)
			{
				return null;
			}
			if (welcomed.Count == 0)
			{
				return "any stratum but " + JoinOr(refused);
			}
			string said = takesAll ? ("any stratum, or " + JoinOr(welcomed)) : JoinOr(welcomed);
			return (refused.Count == 0) ? said : (said + ", but never " + JoinOr(refused));
		}

		// Whether a list says anything at all about one stratum, welcome or refusal. The sky
		// clause turns on this rather than on TagAccepts: a design that has NOT mentioned the sky
		// is the one that inherits the surface's answer, and one that wrote `!sky` has mentioned it.
		private static bool Mentions(List<string> Tokens, string Stratum)
		{
			for (int i = 0; i < Tokens.Count; i++)
			{
				string token = Tokens[i];
				string name = (token[0] == NegationPrefix) ? token.Substring(1).Trim() : token;
				if (name == Stratum)
				{
					return true;
				}
			}
			return false;
		}

	}
}
