using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomQolRules
	{
		public static QolProfile Derive(ResidentTruth Truth)
		{
			List<string> needs = new List<string>();
			List<string> prefers = new List<string>();
			if (Truth.Robot)
			{
				needs.Add(TagCharge);
			}
			if (Truth.Aquatic && !Truth.Flying)
			{
				needs.Add(TagOpenWater);
			}
			if (Truth.Fungal)
			{
				needs.Add(TagDamp);
				prefers.Add(TagDark);
			}
			if (Truth.Photosynthetic)
			{
				needs.Add(TagSky);
			}
			bool feeds = Truth.HasStomach && !Truth.Robot && !Truth.Inorganic;
			return new QolProfile
			{
				Species = (Truth.Species ?? "").Trim(),
				Needs = (needs.Count == 0) ? NoTags : needs.ToArray(),
				Prefers = (prefers.Count == 0) ? NoTags : prefers.ToArray(),
				Refuses = NoTags,
				EatsFood = feeds,
				DrinksWater = feeds
			};
		}

		/// <summary>
		/// Applies a creature blueprint's authored refinement to a derived profile. Each list
		/// merges by <see cref="Merge"/>, so authoring adds to the derivation rather than
		/// replacing it, and a mod that disagrees with a derived tag removes it by name with the
		/// <see cref="RemovePrefix"/>.
		/// <para>
		/// Vanilla's blueprint tags are a dictionary, so a child blueprint that re-declares
		/// <c>r_TAF_Needs</c> overrides its parent's whole string rather than appending to it. That
		/// is the game's mechanism and it is left alone; the removal prefix is what makes it
		/// workable, and MODDING.md says so.
		/// </para>
		/// </summary>
		/// <param name="Derived">From <see cref="Derive"/>. Null reads as
		/// <see cref="QolProfile.Ordinary"/>.</param>
		/// <param name="Needs">Raw <c>r_TAF_Needs</c>.</param>
		/// <param name="Prefers">Raw <c>r_TAF_Prefers</c>.</param>
		/// <param name="Refuses">Raw <c>r_TAF_Refuses</c>.</param>
		/// <returns>A fresh profile; the argument is not modified.</returns>
		public static QolProfile Refine(QolProfile Derived, string Needs, string Prefers, string Refuses)
		{
			QolProfile derived = Derived ?? QolProfile.Ordinary;
			return new QolProfile
			{
				Species = derived.Species ?? "",
				Needs = Merge(derived.Needs, ParseTags(Needs)),
				Prefers = Merge(derived.Prefers, ParseTags(Prefers)),
				Refuses = Merge(derived.Refuses, ParseTags(Refuses)),
				EatsFood = derived.EatsFood,
				DrinksWater = derived.DrinksWater
			};
		}

		/// <summary>
		/// What sharing a roof with this resident does to the room, when the blueprint has not said
		/// otherwise: a household keeps the conditions its people need. The fungal settler's cellar
		/// is damp, the water-bound one's is flooded, the robot's has a cradle in it.
		/// <para>
		/// This is what makes cohabitation one rule rather than a second system &mdash; a
		/// neighbour's household is judged with exactly the same <see cref="Judge"/> a building is.
		/// </para>
		/// </summary>
		/// <param name="Profile">The resident. Null provides nothing.</param>
		/// <param name="Authored">Raw <c>r_TAF_Provides</c> from the blueprint, which refines this
		/// the same way everything else refines: adds, and removes with the prefix.</param>
		public static string[] HouseholdProvides(QolProfile Profile, string Authored = null)
		{
			string[] derived = SelfTags(Profile);
			return Merge(derived, ParseTags(Authored));
		}

		// --- The building's side --------------------------------------------------------------

		/// <summary>
		/// What a tier's roof provides on its own, before any author writes a <c>Provides</c>: sky
		/// for anything weather reaches under, dark for anything it does not. Read straight off
		/// <c>KingdomPlotRules.AdmitsSky</c>, so canvas gives sky and a carved room does not, and
		/// the two can never drift apart.
		/// <para>
		/// <b>Sky is weather-reach, and weather reaches nothing under rock.</b> Underground every
		/// roof reads dark, the open plot included. That is not a contradiction of
		/// <c>KingdomPlotRules.RoofOnGround</c>, which correctly leaves an open plot open down
		/// there: Open is a claim about walls, and a field cut into the deep raises none, exactly
		/// as it raises none in the sun. It simply has a hill over it. Reading the roof state
		/// alone would have a photosynthetic settler housed in a cellar-field on the strength of a
		/// sky that is several hundred feet of rock away.
		/// </para>
		/// </summary>
		/// <param name="Roof">The tier's roof state, as declared.</param>
		/// <param name="Underground">Whether this ground is below
		/// <c>KingdomRules.SurfaceZLevel</c>. Handed in rather than derived: this file has no
		/// engine to ask, and <c>KingdomPlotRules.IsUnderground</c> is the one read that answers
		/// it.</param>
		public static string[] ProvidedByRoof(KingdomPlotRules.RoofState Roof, bool Underground)
		{
			return new string[1] { (!Underground && KingdomPlotRules.AdmitsSky(Roof)) ? TagSky : TagDark };
		}

		/// <summary>What a roof provides on the surface, where weather does reach.
		/// <see cref="ProvidedByRoof(KingdomPlotRules.RoofState, bool)"/> is the whole rule.
		/// </summary>
		public static string[] ProvidedByRoof(KingdomPlotRules.RoofState Roof)
		{
			return ProvidedByRoof(Roof, Underground: false);
		}

		/// <summary>
		/// Everything one design offers a resident: what its author declared, plus what its roof
		/// gives whether they thought about it or not.
		/// </summary>
		/// <param name="Declared">Raw <c>Provides</c> from the registry.</param>
		/// <param name="Roof">The tier's roof state.</param>
		/// <param name="IsPlot">False for a design that takes no ground at all, whose roof state is
		/// not a claim about anything; such a design offers only what it declared.</param>
		/// <param name="Underground">The stratum this design is standing in, for
		/// <see cref="ProvidedByRoof(KingdomPlotRules.RoofState, bool)"/>. One design raised on two
		/// strata offers two different things, which is why the caller says where.</param>
	}
}
