using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// What one attempt to raise an outpost of a great work came to.
	/// </summary>
	public enum KingdomSatelliteVerdict : byte
	{
		/// <summary>Nothing in the way. Either the design is not an outpost at all, or the realm
		/// keeps the work it is an outpost OF and this city keeps no other.</summary>
		Allowed = 0,

		/// <summary>Nowhere in the realm does the parent work stand, so there is nothing for this
		/// to be an outpost of.</summary>
		RefusedNoParent = 1,

		/// <summary>This city already keeps one. One to a city is the whole of the rule
		/// (END-STATE-CITIES-RESEARCH &sect;5.5) &mdash; a second would answer the ground the first
		/// one answers.</summary>
		RefusedCityKeeps = 2
	}

	/// <summary>
	/// The satellites: cheap one-per-city outposts that carry a SLICE of what a megastructure does,
	/// so a realm's great works are felt everywhere rather than hoarded in the one city that has
	/// them.
	/// <para>
	/// <b>Anno's Palace and its local departments, transferred</b> (END-STATE-CITIES-RESEARCH
	/// &sect;5.5). The precedent is exact: one grand structure, and an outpost per island carrying a
	/// single slice of its function. What the transfer buys us is the answer to Addendum 22 A2's
	/// hardest clause &mdash; lower-rung outposts of the body-institutions may sit in the capital,
	/// top rungs and once-ever ceremonies stay sited &mdash; without a second doctrine: an outpost
	/// is a small building with a small verb, and the great verb stays where the great work is.
	/// </para>
	/// <para>
	/// <b>The verb reduction is not enforced here and must not be.</b> Nothing in this file turns a
	/// verb off. The outposts carry the SHIPPED parts of the rungs they are allowed to have &mdash;
	/// the hall's surgery is a slab and a vat and nothing else, so <c>KingdomLab.RungAt</c> already
	/// answers rung 1 for it without being told &mdash; and the annexe's own enrolment gate already
	/// asks whether the building it is standing in is the annexe
	/// (<c>KingdomAnnexe.JudgeFor</c>'s <c>Annexe:</c> argument), so a registry office refuses the
	/// ceremony through the gate that already existed. What is here is only the CARDINALITY and the
	/// words, because those are what nothing else knows.
	/// </para>
	/// <para>
	/// Engine-free. The derivation of "does the realm keep the parent anywhere" is engine-coupled
	/// and lives in <c>KingdomSatellite</c>, exactly as <c>KingdomZoning.KeptMegastructure</c> sits
	/// apart from <c>KingdomLabRules.JudgePurpose</c>.
	/// </para>
	/// </summary>
	public static class KingdomSatelliteRules
	{
		/// <summary>
		/// The building-record attribute that makes a design an outpost: the registry KEY of the
		/// great work it is an outpost of, written out whole (<c>Satellite="chimerictheatre"</c>).
		/// <para>
		/// A key rather than a flag, because the gate has to ask a question about a particular work
		/// &mdash; "does the realm keep a theatre?" &mdash; and a boolean could only ask whether the
		/// realm keeps anything at all. It is also what lets a third-party file declare an outpost
		/// of a third-party megastructure without a line of our code changing (STANDARDS &sect;6).
		/// </para>
		/// </summary>
		public const string SatelliteAttribute = "Satellite";

		/// <summary>The catalogue key of the theatre's outpost: rungs 0 and 1 of the lab ladder and
		/// not one step further.</summary>
		public const string SurgeryKey = "hallsurgery";

		/// <summary>The catalogue key of the annexe's outpost: the book may be READ here. It is not
		/// written here, and the ceremony is not held here.</summary>
		public const string RegistryOfficeKey = "registryoffice";

		/// <summary>
		/// The highest lab rung an outpost of the theatre reaches, and it is stated as a fact about
		/// this file rather than enforced by it: the surgery carries the slab's part and the vat's
		/// part and no other, so the ladder derivation answers this on its own. Held here so the
		/// ruling has a name a test can pin (Addendum 22 A2: rungs 0-1, never the hall's table and
		/// never Class III).
		/// </summary>
		public const int SurgeryCeilingRung = 1;

		/// <summary>
		/// Whether an outpost of the annexe may hold the enrolment ceremony. It may not, ever, and
		/// this is a ruling rather than a tuning knob: Addendum 22 A2 keeps once-ever ceremonies in
		/// the city that raised the great work, and enrolment is the most once-ever act the mod has
		/// &mdash; it rewrites what a body is allowed to be for the rest of a run.
		/// </summary>
		public const bool OfficeEnrols = false;

		/// <summary>Whether a design's <c>Satellite</c> declaration names a parent. Whitespace and
		/// absence both mean "not an outpost", which is every design in the catalogue but two.</summary>
		public static bool IsSatellite(string Declared)
		{
			return !string.IsNullOrEmpty(Declared) && Declared.Trim().Length > 0;
		}

		/// <summary>The parent key a declaration names, trimmed, or null when it names none.</summary>
		public static string ParentOf(string Declared)
		{
			return IsSatellite(Declared) ? Declared.Trim() : null;
		}

		/// <summary>
		/// Whether this city may raise this outpost.
		/// <para>
		/// <b>The parent is asked for REALM-wide and the outpost is counted CITY-wide</b>, and the
		/// asymmetry is the whole design. Realm-wide because &sect;5.5's point is that the capital's
		/// projects are felt in cities that did not undertake them; city-wide because &sect;5.6's
		/// requirement is that a satellite city stays low-attention, and a city that could stack
		/// four registry offices would be a city with a chore in it.
		/// </para>
		/// <para>
		/// <b>Re-raising the one you already keep is allowed</b>, exactly as re-keying a
		/// megastructure is (<c>KingdomLabRules.JudgePurpose</c>): a founder mending or re-siting
		/// the office they already have has not asked for a second one.
		/// </para>
		/// </summary>
		/// <param name="Satellite">Whether the design being zoned declares a parent.</param>
		/// <param name="RealmKeepsParent">Whether the parent work stands anywhere in the realm.
		/// A derivation that could not read the realm passes true, because a cardinality rule that
		/// cannot see must let the founder build (the fails-open bargain the purpose gate makes).</param>
		/// <param name="CityKeeps">The key of the outpost OF THE SAME PARENT this city already keeps,
		/// or null when it keeps none. Per-parent rather than per-city-total, because Anno's own
		/// rule is one department per island per palace module and because a city that could keep a
		/// surgery or a registry office but never both would be a city choosing between two great
		/// works it did not raise.</param>
		/// <param name="Key">The design being zoned.</param>
		public static KingdomSatelliteVerdict Judge(bool Satellite, bool RealmKeepsParent, string CityKeeps, string Key)
		{
			if (!Satellite)
			{
				return KingdomSatelliteVerdict.Allowed;
			}
			if (!RealmKeepsParent)
			{
				return KingdomSatelliteVerdict.RefusedNoParent;
			}
			if (string.IsNullOrEmpty(CityKeeps) || string.Equals(CityKeeps, Key, StringComparison.OrdinalIgnoreCase))
			{
				return KingdomSatelliteVerdict.Allowed;
			}
			return KingdomSatelliteVerdict.RefusedCityKeeps;
		}

		/// <summary>
		/// The refusal for a realm that keeps no such work anywhere. Names the great work rather
		/// than the rule, and names it as the founder reads it (STANDARDS 7b).
		/// </summary>
		/// <param name="ParentName">What the parent work is called, as the founder reads it.</param>
		public static string NoParentRefusalLine(string ParentName)
		{
			return "An outpost answers to something. Nowhere in the realm does " + Named(ParentName)
				+ " stand, so there is nothing for this to be an outpost of. Raise it in one of your cities first; it need not be this one.";
		}

		/// <summary>The refusal for a city that already keeps one. Names the building in the way.</summary>
		/// <param name="KeptName">What this city already keeps, as the founder reads it.</param>
		public static string CityKeepsRefusalLine(string KeptName)
		{
			return "This city already keeps " + Named(KeptName)
				+ ", and one to a city is the whole of it. A second would answer the same doors the first one answers.";
		}

		/// <summary>
		/// The line an outpost carries in its own description. It is written to CLOSE a door out
		/// loud rather than leave a founder to discover it at the moment they wanted the great verb
		/// &mdash; &sect;1.5's lesson, which is that what players will not forgive is the
		/// consequence nobody told them about.
		/// </summary>
		/// <param name="Slice">What this outpost does, in a phrase.</param>
		/// <param name="Withheld">What it does not do, in a phrase.</param>
		/// <param name="ParentCity">The city keeping the great work, or null when nothing could
		/// tell.</param>
		public static string DescriptionLine(string Slice, string Withheld, string ParentCity)
		{
			string where = string.IsNullOrEmpty(ParentCity) ? "the city that raised the great work" : Named(ParentCity);
			return "\n{{rules|An outpost. " + Slice + " here; " + Withheld + " at " + where + ".}}";
		}

		/// <summary>The slice the hall's surgery carries, and the slice it does not.</summary>
		public static string SurgerySlice()
		{
			return "A carcass is dressed and a part is kept";
		}

		/// <summary>The other half of the surgery's sentence.</summary>
		public static string SurgeryWithheld()
		{
			return "grafting is done";
		}

		/// <summary>The slice the registry office carries.</summary>
		public static string OfficeSlice()
		{
			return "The rolls are read";
		}

		/// <summary>The other half of the office's sentence, and it is the important half.</summary>
		public static string OfficeWithheld()
		{
			return "a name is entered on them";
		}

		/// <summary>A name as a founder would say it, or an honest word when nothing named one.</summary>
		public static string Named(string Text)
		{
			return string.IsNullOrEmpty(Text) ? "the great work" : Text.Trim();
		}
	}
}
