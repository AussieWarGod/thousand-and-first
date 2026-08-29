using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomQolRules
	{
		public static string[] ParseTags(string Source)
		{
			if (string.IsNullOrEmpty(Source) || Source.Trim().Length == 0)
			{
				return NoTags;
			}
			List<string> tags = new List<string>();
			string[] parts = Source.Split(ListSeparator);
			for (int i = 0; i < parts.Length; i++)
			{
				string tag = Fold(parts[i]);
				if (tag.Length == 0 || (tag.Length == 1 && tag[0] == RemovePrefix))
				{
					continue;
				}
				if (!tags.Contains(tag))
				{
					tags.Add(tag);
				}
			}
			return (tags.Count == 0) ? NoTags : tags.ToArray();
		}

		/// <summary>Whether a set holds a tag. A null or empty set holds nothing; a blank tag is
		/// held by nothing.</summary>
		public static bool Has(string[] Set, string Tag)
		{
			string tag = Fold(Tag);
			if (Set == null || tag.Length == 0)
			{
				return false;
			}
			for (int i = 0; i < Set.Length; i++)
			{
				if (Fold(Set[i]) == tag)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Folds an authored refinement into a derived list: a plain tag is added, a tag written
		/// <c>-like-this</c> removes the derived tag of that name, and anything the refinement does
		/// not mention survives untouched.
		/// <para>
		/// Order inside the result is derived-first, then whatever the refinement added, so a
		/// reader of the log sees where each tag came from.
		/// </para>
		/// </summary>
		/// <param name="Derived">The list <see cref="Derive"/> produced. Null is empty.</param>
		/// <param name="Refinement">The authored list. Null or empty changes nothing.</param>
		/// <returns>A fresh array; neither argument is modified.</returns>
		public static string[] Merge(string[] Derived, string[] Refinement)
		{
			List<string> merged = new List<string>();
			if (Derived != null)
			{
				for (int i = 0; i < Derived.Length; i++)
				{
					string tag = Fold(Derived[i]);
					if (tag.Length != 0 && !merged.Contains(tag))
					{
						merged.Add(tag);
					}
				}
			}
			if (Refinement == null)
			{
				return (merged.Count == 0) ? NoTags : merged.ToArray();
			}
			for (int i = 0; i < Refinement.Length; i++)
			{
				string tag = Fold(Refinement[i]);
				if (tag.Length == 0)
				{
					continue;
				}
				if (tag[0] == RemovePrefix)
				{
					merged.Remove(tag.Substring(1));
					continue;
				}
				if (!merged.Contains(tag))
				{
					merged.Add(tag);
				}
			}
			return (merged.Count == 0) ? NoTags : merged.ToArray();
		}

		/// <summary>
		/// One species as an open QoL self-tag. No species is enumerated: a vanilla or modded value
		/// becomes <c>species:&lt;folded value&gt;</c> on first read. Blank, over-long, or roster-breaking
		/// values yield null rather than a malformed tag.
		/// </summary>
		/// <param name="Species">The exact open value from <c>GameObject.GetSpecies()</c>.</param>
		/// <returns>The folded species self-tag, or null when the value is unsafe.</returns>
		public static string SpeciesTag(string Species)
		{
			string species = Fold(Species);
			if (species == null || species.Length == 0 || species.Length > MaxSpeciesLength
				|| species.IndexOf('|') >= 0)
			{
				return null;
			}
			for (int i = 0; i < species.Length; i++)
			{
				if (char.IsControl(species[i]))
				{
					return null;
				}
			}
			return SpeciesNamespace + species;
		}

		/// <summary>
		/// Tags this resident presents to a housemate: the same Needs and Prefers the QoL lane has
		/// always read, plus their exact vanilla species. This makes an authored
		/// <c>r_TAF_Refuses="species:..."</c> immediately useful for any modded species without a
		/// second catalogue or a hardcoded compatibility table.
		/// </summary>
		/// <param name="Profile">The resident's fresh QoL profile. Null presents nothing.</param>
		/// <returns>A fresh tag set. Never null.</returns>
		public static string[] SelfTags(QolProfile Profile)
		{
			if (Profile == null)
			{
				return NoTags;
			}
			string[] tags = Merge(Profile.Needs, Profile.Prefers);
			string species = SpeciesTag(Profile.Species);
			return (species == null) ? tags : Merge(tags, new string[1] { species });
		}

		// --- Derive before authoring ----------------------------------------------------------

		/// <summary>
		/// The derivation table: what the game already knows about a creature, turned into what
		/// that creature asks of a place to live. The whole of the "correct resident before its
		/// author writes one tag" promise.
		/// <list type="bullet">
		/// <item><b>Robot</b> needs <see cref="TagCharge"/> and eats nothing. Vanilla's own
		/// <c>Robot</c> object removes the <c>Stomach</c> part and sets <c>Physics Organic=false</c>,
		/// and <c>Stomach.IsFamished()</c> returns false for anything carrying the <c>Robot</c>
		/// tag or property, so "does not eat" is read twice from the game and agreed both
		/// times.</item>
		/// <item><b>Water-bound</b> (and not flying) needs <see cref="TagOpenWater"/>, because
		/// <c>Brain.LimitToAquatic</c> makes every step it takes require a cell with aquatic
		/// support. Flying cancels it, exactly as that method does.</item>
		/// <item><b>Fungal</b> needs <see cref="TagDamp"/> and prefers <see cref="TagDark"/>. The
		/// need is the fungus's condition; the dark is a want, so a fungal settler can still take
		/// a lit room and simply be no happier for it.</item>
		/// <item><b>Photosynthetic</b> needs <see cref="TagSky"/>, which is
		/// <c>PhotosyntheticSkin.HasSunlight</c> stated as a placement constraint. Canvas admits
		/// sky (<c>KingdomPlotRules.AdmitsSky</c>), so a tent houses them, a sealed room does not,
		/// and nothing underground does whatever its roof says.</item>
		/// <item><b>Inorganic</b> eats and drinks nothing, whatever parts it happens to carry:
		/// <c>GameObject.IsAlive</c> requires <c>IsOrganic</c>, and the food and water ladders are
		/// a living body's.</item>
		/// </list>
		/// <para>
		/// Nothing here derives a <c>Refuses</c>. A refusal is a person's own line and is either
		/// authored on the blueprint or, for the ideological cases, read off the engine's faction
		/// feelings (<c>KingdomLodgingRules.Conflicts</c> against
		/// <c>KingdomLodgingRules.RefusalHostility</c> &mdash; the ladder, not the retired flat
		/// compatibility adapter) &mdash; it is not something a body
		/// plan implies.
		/// </para>
		/// </summary>
		/// <param name="Truth">What <c>KingdomQol</c> read off the creature.</param>
		/// <returns>A fresh profile. Never null, and never shares its arrays with another
		/// profile.</returns>
	}
}
