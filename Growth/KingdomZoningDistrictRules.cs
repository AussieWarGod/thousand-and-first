using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomZoningRules
	{
		/// <summary>Whether a category is one undistricted ground always accepts.</summary>
		/// <param name="Category">A <c>BuildEntry.Category</c>. Null, empty, and any category a
		/// third party invents are not open &mdash; only the three named ones are.</param>
		public static bool IsOpenCategory(string Category)
		{
			string category = Fold(Category);
			if (category == null)
			{
				return false;
			}
			for (int i = 0; i < OpenCategories.Length; i++)
			{
				if (OpenCategories[i] == category)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// The districts whose names already imply they would take this category &mdash; the
		/// vinelands take food and storage, the forgeworks craft and power, and so on. Advice
		/// only: nothing in <see cref="Judge"/> consults this, so a design is refused ground only
		/// by what its own entry declares.
		/// </summary>
		/// <param name="Category">A <c>BuildEntry.Category</c>.</param>
		/// <returns>A comma list of district keys, or null for a category no district claims.</returns>
		public static string NaturalDistricts(string Category)
		{
			string category = Fold(Category);
			if (category == null)
			{
				return null;
			}
			// "defence" and "defense" both ship in the wild; KingdomLayoutRules.PurposeOf already
			// accepts either and a gate that disagreed with the plan would be a trap.
			if (category == "defence")
			{
				category = "defense";
			}
			for (int i = 0; i < MappedCategories.Length; i++)
			{
				if (MappedCategories[i] == category)
				{
					return MappedDistricts[i];
				}
			}
			return null;
		}

		/// <summary>
		/// Whether ground carrying <paramref name="TileDistrict"/> will accept a design.
		/// <para>
		/// A design that names no districts stands anywhere. A design that names districts stands
		/// on ground carrying one of them, on ground with no district at all if it named
		/// <see cref="UndistrictedToken"/>, and on ground with no district at all if its category
		/// is one of the <see cref="OpenCategories"/>. Everything else is refused.
		/// </para>
		/// <para>
		/// A district key this mod does not recognise is treated as a district all the same,
		/// never as open ground: a third party's quarter must be able to refuse a forge exactly
		/// the way ours does, and a design that wants to stand there names it and is let in.
		/// </para>
		/// </summary>
		/// <param name="TileDistrict">The district key on the zone, or null/empty for ground the
		/// founder has never designated.</param>
		/// <param name="RequiredDistricts">The entry's <c>Districts</c> attribute; null, empty,
		/// or <see cref="AnyToken"/> accepts everything.</param>
		/// <param name="Category">The entry's <c>Category</c>, for the open-ground clause.</param>
		public static bool DistrictAccepts(string TileDistrict, string RequiredDistricts, string Category)
		{
			if (!Gated(RequiredDistricts))
			{
				return true;
			}
			string tile = Fold(TileDistrict);
			if (tile != null)
			{
				return ListContains(RequiredDistricts, tile);
			}
			return ListContains(RequiredDistricts, UndistrictedToken) || IsOpenCategory(Category);
		}

		// ==================================================================================
		// Tags: the one list idiom every open-ended set in the catalogue is matched by.
		// ==================================================================================

		/// <summary>
		/// Whether a tag list accepts one value. The whole of what <c>Styles</c> means, and the
		/// shape Addendum 16 rules every open-ended catalogue set into: a comma list of tags,
		/// <see cref="AnyToken"/> for "all of them", and <see cref="NegationPrefix"/> for "all of
		/// them except this".
		/// <para>
		/// Three rules, in this order, and the order is the contract:
		/// </para>
		/// <list type="number">
		/// <item>An empty or absent list accepts everything. That is what keeps every entry
		/// written before a tag existed working untouched (STANDARDS &sect;6).</item>
		/// <item>A negation that matches refuses, whatever else the list says. An author who
		/// writes both a welcome and a refusal for the same tag meant the refusal &mdash; nobody
		/// writes <c>!x</c> by accident.</item>
		/// <item>Otherwise the list accepts when it names <see cref="AnyToken"/>, when it names
		/// the value, or when it names nothing but refusals &mdash; because a list of pure
		/// refusals is "everywhere except", and reading it as "nowhere" would gate the design out
		/// of the game.</item>
		/// </list>
		/// <para>
		/// Case-folded on both sides, unlike the exact comparison <c>Styles</c> shipped with. A
		/// tag is data an author types twice in two files, and <c>Verdant</c> silently matching
		/// nothing was a trap rather than a rule.
		/// </para>
		/// </summary>
		/// <param name="Tags">The authored list. Null and empty accept everything.</param>
		/// <param name="Value">The one tag being tested. Null is accepted only by a list that
		/// gates nothing, so a caller with nothing to test is never told it may not.</param>
		public static bool TagAccepts(string Tags, string Value)
		{
			List<string> tokens = Tokens(Tags);
			if (tokens.Count == 0)
			{
				return true;
			}
			string value = Fold(Value);
			bool welcomed = false;
			bool anyWelcome = false;
			for (int i = 0; i < tokens.Count; i++)
			{
				string token = tokens[i];
				if (token[0] == NegationPrefix)
				{
					string refused = token.Substring(1).Trim();
					if (refused.Length == 0)
					{
						continue;
					}
					if (refused == value || refused == AnyToken)
					{
						return false;
					}
					continue;
				}
				anyWelcome = true;
				if (token == AnyToken || token == value)
				{
					welcomed = true;
				}
			}
			return welcomed || !anyWelcome;
		}

		/// <summary>
		/// A tag list read back as prose: "the fungal or the eater city", "every style but the
		/// eater's". Names come back exactly as the author folded them, because a tag set is open
		/// and there is no table here to look a third party's tag up in.
		/// </summary>
		/// <returns>Null when the list gates nothing, so a caller can drop the whole clause.</returns>
		public static string DescribeTags(string Tags)
		{
			List<string> tokens = Tokens(Tags);
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
					if (name.Length > 0)
					{
						refused.Add(name);
					}
					continue;
				}
				if (token == AnyToken)
				{
					takesAll = true;
					continue;
				}
				welcomed.Add(token);
			}
			if (welcomed.Count == 0 && refused.Count == 0)
			{
				return null;
			}
			if (welcomed.Count == 0)
			{
				return "anything but " + JoinOr(refused);
			}
			string said = takesAll ? ("anything, or " + JoinOr(welcomed)) : JoinOr(welcomed);
			return (refused.Count == 0) ? said : (said + ", but never " + JoinOr(refused));
		}

		/// <summary>
		/// A <c>Districts</c> list read back as founder-facing prose: "the vinelands or the
		/// bazaar", "the forgeworks or ground with no district yet". Names come from
		/// <c>KingdomRules.DistrictName</c>, so a third party's district reads as whatever key
		/// they chose rather than as a blank.
		/// </summary>
		/// <returns>Null when the list gates nothing, so a caller can skip the whole clause.</returns>
		public static string DescribeDistricts(string RequiredDistricts)
		{
			if (!Gated(RequiredDistricts))
			{
				return null;
			}
			List<string> names = new List<string>();
			foreach (string token in Tokens(RequiredDistricts))
			{
				string name = (token == UndistrictedToken) ? "ground with no district yet" : ("the " + KingdomRules.DistrictName(token));
				if (!names.Contains(name))
				{
					names.Add(name);
				}
			}
			return JoinOr(names);
		}

	}
}
