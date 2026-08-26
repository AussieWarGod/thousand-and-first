using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomZoningRules
	{
		// ==================================================================================
		// The creed stack (Addendum 16): who is here, what they hold, and what they once held.
		// ==================================================================================

		/// <summary>A <c>Builders</c> kind: somebody living here holds that creed today. Read off
		/// the city's own creed tally, so it comes and goes with the believers.</summary>
		public const string KindCreed = "creed";

		/// <summary>A <c>Builders</c> kind: somebody here has held that creed and LEFT it. The one
		/// fact no tally of present belief can answer, and the reason a settler's creed history is
		/// recorded at all (Addendum 16).</summary>
		public const string KindKept = "kept";

		/// <summary>
		/// Whether the roll satisfies one <c>Builders</c> requirement.
		/// <para>
		/// A requirement is <c>kind:name</c>, or <c>kind:name:count</c> when one of them is not
		/// enough. The kinds are <see cref="KindOrigin"/> (people from a country),
		/// <see cref="KindCreed"/> (people holding a creed today) and <see cref="KindKept"/>
		/// (people who hold it or once did &mdash; the aligned). A requirement written as a bare
		/// name, with no kind, is satisfied by any of the three, exactly as
		/// <see cref="Knows"/> lets a bare <c>Knowledge</c> name be satisfied by any kind.
		/// </para>
		/// <para>
		/// A kind this file does not know never matches, and the refusal names the requirement as
		/// the author wrote it. That is the same bargain <see cref="Knows"/> strikes for an
		/// invented knowledge kind, told the other way round: a knowledge kind can be supplied by
		/// a third party's own <c>Learn</c> call, and a people-kind cannot, so an unknown one here
		/// is a gate that will never open and the log has to be able to say which.
		/// </para>
		/// </summary>
		public static bool HasBuilders(BuilderRoll Roll, string Requirement)
		{
			string required = Fold(Requirement);
			if (required == null)
			{
				return true;
			}
			if (!Roll.Known)
			{
				return true;
			}
			string kind;
			string name;
			int wanted;
			if (!SplitBuilder(required, out kind, out name, out wanted))
			{
				return false;
			}
			if (kind == null)
			{
				return Roll.FromCountry(name) >= wanted || Roll.HoldingNow(name) >= wanted || Roll.Aligned(name) >= wanted;
			}
			if (kind == KindOrigin)
			{
				return Roll.FromCountry(name) >= wanted;
			}
			if (kind == KindCreed)
			{
				return Roll.HoldingNow(name) >= wanted;
			}
			if (kind == KindKept)
			{
				return Roll.Aligned(name) >= wanted;
			}
			return false;
		}

		/// <summary>Every requirement in a <c>Builders</c> list the roll does not satisfy, in the
		/// order the author wrote them. Empty when the city has all the hands it asks for.</summary>
		public static List<string> MissingBuilders(BuilderRoll Roll, string Required)
		{
			List<string> missing = new List<string>();
			if (!Gated(Required) || !Roll.Known)
			{
				return missing;
			}
			foreach (string token in Tokens(Required))
			{
				if (!HasBuilders(Roll, token) && !missing.Contains(token))
				{
					missing.Add(token);
				}
			}
			return missing;
		}

		/// <summary>One <c>Builders</c> requirement as prose: "somebody from the rust wells",
		/// "three who hold with the Barathrumites", "somebody who has ever held with the
		/// Mechanimists".</summary>
		public static string DescribeBuilder(string Requirement)
		{
			string required = Fold(Requirement);
			if (required == null)
			{
				return "";
			}
			string kind;
			string name;
			int wanted;
			if (!SplitBuilder(required, out kind, out name, out wanted))
			{
				return required;
			}
			bool one = wanted <= 1;
			string many = one ? "somebody" : (wanted + " people");
			string holds = one ? "holds" : "hold";
			if (kind == KindOrigin)
			{
				return many + " from " + name;
			}
			if (kind == KindCreed)
			{
				return many + " who " + holds + " with " + name;
			}
			if (kind == KindKept)
			{
				return many + " who " + holds + ", or " + (one ? "has" : "have") + " ever held, with " + name;
			}
			return many + " answering to " + required;
		}

		/// <summary>Every requirement of a list, read back as prose.</summary>
		public static List<string> DescribeBuilders(IEnumerable<string> Requirements)
		{
			List<string> said = new List<string>();
			if (Requirements == null)
			{
				return said;
			}
			foreach (string requirement in Requirements)
			{
				string one = DescribeBuilder(requirement);
				if (!string.IsNullOrEmpty(one) && !said.Contains(one))
				{
					said.Add(one);
				}
			}
			return said;
		}

		/// <summary>
		/// Whether anybody here ALIGNS with a creed: holds it, or has held it and left it. The
		/// alignment gate of Addendum 16 clause (4), and &mdash; through
		/// <see cref="NoPathToCreed"/> &mdash; the visibility law of Addendum 14 as it applies to
		/// creed-works.
		/// </summary>
		/// <returns>True for a design that names no creed, and true against
		/// <see cref="BuilderRoll.Unknown"/>.</returns>
		public static bool Aligned(BuilderRoll Roll, string Creed)
		{
			if (string.IsNullOrEmpty(Creed) || !Roll.Known)
			{
				return true;
			}
			return Roll.Aligned(Creed) > 0;
		}

		/// <summary>
		/// A city with no way to this design at all: it names a creed, nobody here holds that
		/// creed, and nobody here ever has. Addendum 14's visibility law &mdash; <i>you especially
		/// cannot see what you CAN'T unlock</i> &mdash; and the exact complement of
		/// <see cref="Aligned"/>, deliberately, so that "shown" and "buildable" can never drift
		/// apart into two rules.
		/// <para>
		/// A creed somebody once held is still a path: they can be turned back, and their
		/// household can be turned with them. Only a creed no one here has ever carried is a door
		/// with no key, and only that one is hidden.
		/// </para>
		/// </summary>
		public static bool NoPathToCreed(BuilderRoll Roll, string Creed)
		{
			return !Aligned(Roll, Creed);
		}

		/// <summary>
		/// Whether enough of the city holds a creed for a work of it to stand &mdash; Addendum 16
		/// clause (2), the AMOUNT.
		/// <para>
		/// The threshold is not chosen here. It is <c>KingdomCreedRules.DominantCreed</c>'s own
		/// arithmetic, minus one clause: at least <c>KingdomCreedRules.MinBelievers</c> people, and
		/// at least the asked share of everyone living there. What is dropped is the
		/// no-larger-rival test, and dropping it is the point &mdash; that test answers "what creed
		/// is this CITY", and a congregation large enough to raise its own shrine does not have to
		/// be the largest congregation in town.
		/// </para>
		/// </summary>
		/// <param name="Holding">People holding the creed now.</param>
		/// <param name="People">Everyone living in the city.</param>
		/// <param name="Percent">The share asked for. Zero and below ask for no share at all, and
		/// the believers floor goes with it: an author who writes <c>CreedShare="0"</c> has said
		/// one aligned builder is enough.</param>
		public static bool CreedShareMet(int Holding, int People, int Percent)
		{
			if (Percent <= 0)
			{
				return true;
			}
			if (Holding < KingdomCreedRules.MinBelievers || People <= 0)
			{
				return false;
			}
			return Holding * 100 >= People * Percent;
		}

		/// <summary>The share a city actually holds, in whole percent, for the sentence that names
		/// it. Zero for a city with nobody in it, which is the only honest answer.</summary>
		public static int ShareHeld(int Holding, int People)
		{
			if (Holding <= 0 || People <= 0)
			{
				return 0;
			}
			return Holding * 100 / People;
		}

		// A Builders token, split. `kind:name`, `kind:name:count`, or a bare `name` (kind null).
		// A count that is not a positive number is not a count -- it is part of the name, because
		// nothing stops a country or a faction from ending in a colon and a word.
		private static bool SplitBuilder(string Requirement, out string Kind, out string Name, out int Wanted)
		{
			Kind = null;
			Name = Requirement;
			Wanted = 1;
			if (string.IsNullOrEmpty(Requirement))
			{
				return false;
			}
			int last = Requirement.LastIndexOf(KindSeparator);
			if (last > 0 && last < Requirement.Length - 1)
			{
				int count;
				if (int.TryParse(Requirement.Substring(last + 1), out count) && count > 0)
				{
					Wanted = count;
					Name = Requirement.Substring(0, last);
				}
			}
			int first = Name.IndexOf(KindSeparator);
			if (first > 0 && first < Name.Length - 1)
			{
				Kind = Name.Substring(0, first);
				Name = Name.Substring(first + 1);
			}
			return !string.IsNullOrEmpty(Name);
		}

	}
}
