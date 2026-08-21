using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;
	using XRL.World.Parts.Mutation;

	/// <summary>
	/// The engine-coupled half of the quality-of-life vocabulary (<see cref="KingdomQolRules"/> is
	/// the whole of the matching, and is engine-free): the registry of what each design
	/// <c>Provides</c>, the reads that turn a real creature into a
	/// <see cref="ResidentTruth"/>, and the two-line answers the rest of the mod asks for &mdash;
	/// will this person live here, will they live beside that one, and what is their being here
	/// worth.
	/// <para>
	/// <b>No state of its own.</b> Nothing in this file is serialized, and there is no per-city or
	/// realm-level field anywhere in the system: every answer is recomputed from the tags in hand,
	/// which is what makes "nothing decays, nothing accumulates" a property of the code rather than
	/// a promise about it. The only tables here are the registry, cleared and refilled by the
	/// loader's single pass, and a parse cache keyed by blueprint.
	/// </para>
	/// <para>
	/// <b>What it never does.</b> Move anybody, evict anybody, or destroy anything. A refused match
	/// is a match that does not happen &mdash; the settler stays where they are and the founder is
	/// told why (STANDARDS 7b). The protection law is not even in reach from here.
	/// </para>
	/// </summary>
	public static class KingdomQol
	{
		// --- Registry ---------------------------------------------------------------------

		// Keyed by building Key like every other registry beside the catalogue (STANDARDS 6): a
		// later file re-using a key owns that design's whole Provides list, and a file that
		// re-declares the design without naming Provides at all correctly leaves it with none.
		// Raw strings, parsed on read and cached, because the merge layer hands this the merged
		// attribute and merges happen before anything is parsed.
		private static readonly Dictionary<string, string> Declared = new Dictionary<string, string>();

		private static readonly Dictionary<string, string[]> OfferCache = new Dictionary<string, string[]>();

		/// <summary>Forgets every registered <c>Provides</c>. Called by the registry loader before
		/// it re-reads the XML streams.</summary>
		public static void ClearProvides()
		{
			Declared.Clear();
			OfferCache.Clear();
		}

		/// <summary>
		/// Registers one entry's <c>Provides</c> as the registry parses it. Call once per
		/// <c>&lt;building&gt;</c> element that parsed successfully, with the merged raw attribute;
		/// null or blank registers "provides nothing declared", which is every design written
		/// before this vocabulary existed.
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Blank keys are ignored.</param>
		/// <param name="Provides">Raw <c>Provides</c> attribute: a comma list of open namespaced
		/// tags. Never refused &mdash; a tag this build has never heard of is somebody else's
		/// vocabulary, and it waits for its consumer.</param>
		public static void RegisterProvides(string Key, string Provides)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			OfferCache.Remove(Key);
			if (string.IsNullOrEmpty(Provides) || Provides.Trim().Length == 0)
			{
				Declared.Remove(Key);
				return;
			}
			Declared[Key] = Provides;
			string[] tags = KingdomQolRules.ParseTags(Provides);
			for (int i = 0; i < tags.Length; i++)
			{
				if (!KingdomQolRules.IsNamespaced(tags[i]))
				{
					// A note, never a fault: an un-namespaced tag works exactly as well and is
					// merely likelier to mean something else in somebody else's file.
					KingdomLog.Log("KingdomBuildings: building " + Key + " provides \"" + tags[i]
						+ "\", which carries no namespace of its own.");
				}
			}
		}

		/// <summary>What one design was registered as providing, as written.</summary>
		public static string DeclaredProvides(string Key)
		{
			string declared;
			return (!string.IsNullOrEmpty(Key) && Declared.TryGetValue(Key, out declared)) ? declared : null;
		}

		/// <summary>
		/// Everything a design offers a resident: its declared tags plus what its own roof gives
		/// (sky under canvas and open ground, shade under wall and rock), so an author who never
		/// wrote a <c>Provides</c> still houses a photosynthetic settler correctly.
		/// </summary>
		/// <returns>Never null. Empty for a design that declares nothing and takes no ground.
		/// </returns>
		public static string[] OfferOf(string BuildingKey)
		{
			if (string.IsNullOrEmpty(BuildingKey))
			{
				return KingdomQolRules.NoTags;
			}
			string[] cached;
			if (OfferCache.TryGetValue(BuildingKey, out cached))
			{
				return cached;
			}
			KingdomPlotRules.PlotSpec spec;
			bool isPlot = KingdomPlots.TryGetSpec(BuildingKey, out spec) && spec != null;
			string[] offer = KingdomQolRules.DesignOffer(DeclaredProvides(BuildingKey),
				isPlot ? spec.Roof : KingdomPlotRules.RoofState.Walled, isPlot);
			OfferCache[BuildingKey] = offer;
			return offer;
		}

		/// <summary>What a work standing on the ground offers, read off the design key it was
		/// raised under. A work with no key on it &mdash; anything the settlement did not raise
		/// &mdash; offers nothing.</summary>
		public static string[] OfferOf(GameObject Work)
		{
			if (Work == null)
			{
				return KingdomQolRules.NoTags;
			}
			return OfferOf(Work.GetStringProperty(KingdomUpgrade.BuildKeyProperty));
		}

		// --- Reading a creature ------------------------------------------------------------

		/// <summary>
		/// What the game already knows about this creature. Every read here is one vanilla itself
		/// performs somewhere, named in <see cref="ResidentTruth"/>'s own fields, so a creature
		/// another mod ships answers correctly without that mod knowing this system exists.
		/// </summary>
		public static ResidentTruth TruthOf(GameObject Resident)
		{
			ResidentTruth truth = default(ResidentTruth);
			if (Resident == null)
			{
				return truth;
			}
			truth.Robot = Resident.HasPart<Robot>() || Resident.HasTagOrProperty("Robot");
			Brain brain = Resident.GetPart<Brain>();
			truth.Aquatic = Resident.HasPart<Aquatic>() || (brain != null && brain.Aquatic);
			truth.Flying = Resident.IsFlying;
			truth.Fungal = Resident.HasTagOrProperty("LiveFungus");
			truth.Photosynthetic = Resident.HasPart<PhotosyntheticSkin>();
			truth.Inorganic = Resident.HasPart<Inorganic>() || !Resident.IsOrganic;
			truth.HasStomach = Resident.HasPart<Stomach>();
			return truth;
		}

		// The authored half of a profile is a blueprint's four tag strings, which never change
		// while the game runs, and parsing them is the only repeated cost in this file. Cached by
		// blueprint name, and ONLY the blueprint's own strings: an object carrying its own string
		// property for one of them bypasses the cache for that one, because two settlers of one
		// blueprint may certainly disagree about what they will live beside.
		private sealed class AuthoredTags
		{
			public string Needs;

			public string Prefers;

			public string Refuses;

			public string Provides;
		}

		private static readonly Dictionary<string, AuthoredTags> AuthoredCache = new Dictionary<string, AuthoredTags>();

		private static AuthoredTags AuthoredOf(GameObject Resident)
		{
			AuthoredTags authored = null;
			string blueprint = (Resident == null) ? null : Resident.Blueprint;
			if (!string.IsNullOrEmpty(blueprint) && AuthoredCache.TryGetValue(blueprint, out authored) && authored != null)
			{
				return Overlay(Resident, authored);
			}
			authored = new AuthoredTags
			{
				Needs = TagOnly(Resident, KingdomQolRules.NeedsTagName),
				Prefers = TagOnly(Resident, KingdomQolRules.PrefersTagName),
				Refuses = TagOnly(Resident, KingdomQolRules.RefusesTagName),
				Provides = TagOnly(Resident, KingdomQolRules.ProvidesTagName)
			};
			if (!string.IsNullOrEmpty(blueprint))
			{
				AuthoredCache[blueprint] = authored;
			}
			return Overlay(Resident, authored);
		}

		// GetTag reads the blueprint's own dictionary and never the object's properties, which is
		// exactly the half that is safe to cache.
		private static string TagOnly(GameObject Resident, string Name)
		{
			return (Resident == null) ? null : Resident.GetTag(Name);
		}

		// A string property set on one object wins over its blueprint's tag, the way
		// GameObject.GetPropertyOrTag orders them everywhere else in the game.
		private static AuthoredTags Overlay(GameObject Resident, AuthoredTags Blueprint)
		{
			if (Resident == null)
			{
				return Blueprint;
			}
			bool any = Resident.HasStringProperty(KingdomQolRules.NeedsTagName)
				|| Resident.HasStringProperty(KingdomQolRules.PrefersTagName)
				|| Resident.HasStringProperty(KingdomQolRules.RefusesTagName)
				|| Resident.HasStringProperty(KingdomQolRules.ProvidesTagName);
			if (!any)
			{
				return Blueprint;
			}
			return new AuthoredTags
			{
				Needs = Resident.GetPropertyOrTag(KingdomQolRules.NeedsTagName, Blueprint.Needs),
				Prefers = Resident.GetPropertyOrTag(KingdomQolRules.PrefersTagName, Blueprint.Prefers),
				Refuses = Resident.GetPropertyOrTag(KingdomQolRules.RefusesTagName, Blueprint.Refuses),
				Provides = Resident.GetPropertyOrTag(KingdomQolRules.ProvidesTagName, Blueprint.Provides)
			};
		}

		/// <summary>
		/// What this resident asks of a place: derived from vanilla truth first, then refined by
		/// whatever their blueprint authored. A creature from any mod is a correct resident here
		/// before its author has written one tag of ours.
		/// </summary>
		/// <returns>Never null. <see cref="QolProfile.Ordinary"/> for a null object.</returns>
		public static QolProfile ProfileOf(GameObject Resident)
		{
			if (Resident == null)
			{
				return QolProfile.Ordinary;
			}
			AuthoredTags authored = AuthoredOf(Resident);
			return KingdomQolRules.Refine(KingdomQolRules.Derive(TruthOf(Resident)),
				authored.Needs, authored.Prefers, authored.Refuses);
		}

		/// <summary>What sharing a roof with this person does to the room: the conditions they
		/// keep, refined by anything their blueprint says they bring.</summary>
		public static string[] HouseholdOf(GameObject Resident)
		{
			if (Resident == null)
			{
				return KingdomQolRules.NoTags;
			}
			return KingdomQolRules.HouseholdProvides(ProfileOf(Resident), AuthoredOf(Resident).Provides);
		}

		// --- The questions the rest of the mod asks ------------------------------------------

		/// <summary>
		/// Whether this person lives in this design, and which tag decided it.
		/// </summary>
		/// <param name="Resident">The settler, notable, or guest. Null asks nothing and matches.
		/// </param>
		/// <param name="BuildingKey">The design's registry key.</param>
		/// <param name="Tag">The tag refused, or the first need missing. Empty on a match.</param>
		public static QolVerdict Judge(GameObject Resident, string BuildingKey, out string Tag)
		{
			return KingdomQolRules.Judge(OfferOf(BuildingKey), ProfileOf(Resident), out Tag);
		}

		/// <summary>The plain yes-or-no, for callers that have nothing to say about a no.</summary>
		public static bool WillLive(GameObject Resident, string BuildingKey)
		{
			string tag;
			return KingdomQolRules.IsMatch(Judge(Resident, BuildingKey, out tag));
		}

		/// <summary>
		/// Whether this person would tolerate these quarters &mdash; the same question as
		/// <see cref="WillLive"/>, asked of temporary lodging during a rebuild. Addendum 3's
		/// "tolerable displacement" re-based onto this vocabulary as Addendum 4 directs: tolerance
		/// IS the Needs check against the quarters on offer, and the shelter-rank ladder in
		/// <c>KingdomUpgradeRules</c> goes on deciding how GOOD the lodging must be.
		/// </summary>
		/// <param name="Resident">The person being moved.</param>
		/// <param name="QuartersKey">The design key of the lodging on offer. Blank quarters offer
		/// nothing, which a person who needs nothing still tolerates.</param>
		/// <param name="Tag">The tag that decided it, for the founder's line.</param>
		public static bool Tolerates(GameObject Resident, string QuartersKey, out string Tag)
		{
			return KingdomQolRules.IsMatch(Judge(Resident, QuartersKey, out Tag));
		}

		/// <summary>
		/// Equilibrium points this person's met Prefers are worth in this building: small, capped,
		/// and routed through the tastes machinery so there is one balance to keep rather than two.
		/// Never negative, and an unmet Prefers is worth exactly nothing rather than a penalty.
		/// </summary>
		public static int PreferShade(GameObject Resident, string BuildingKey)
		{
			return KingdomQolRules.PreferShade(OfferOf(BuildingKey), ProfileOf(Resident));
		}

		/// <summary>
		/// The met/unmet flags for this person's Prefers in this building, as
		/// <c>KingdomCeremonyRules.TasteShade</c> takes them, for a caller that is already
		/// assembling a taste list and wants these folded into it.
		/// </summary>
		public static List<bool> PreferFlags(GameObject Resident, string BuildingKey)
		{
			return KingdomQolRules.PreferFlags(OfferOf(BuildingKey), ProfileOf(Resident));
		}

		/// <summary>
		/// Whether two people share a roof, <b>on the superseded flat floor</b>: this wrapper is
		/// the old path and <c>KingdomLodging</c> is the live one (the closeness ladder, Addendum
		/// 4c, under the fault-line ceiling, 4d). The ideological half comes from the engine's own
		/// faction feelings through <c>KingdomCreed</c> &mdash; no grudge table lives here either
		/// &mdash; and everything else is the tag vocabulary, judged against what the household
		/// already there keeps.
		/// </summary>
		/// <param name="Newcomer">The person moving in.</param>
		/// <param name="Resident">The person already there.</param>
		/// <param name="Tag">The tag refused, or empty when the creeds decided it.</param>
		public static QolVerdict JudgeCohabitation(GameObject Newcomer, GameObject Resident, out string Tag)
		{
			int hostility = 0;
			if (Newcomer != null && Resident != null && KingdomCreed.Enabled)
			{
				hostility = KingdomCreed.HostilityBetween(
					Newcomer.GetStringProperty(KingdomCreed.CreedProperty),
					Resident.GetStringProperty(KingdomCreed.CreedProperty));
			}
			return KingdomQolRules.JudgeCohabitation(ProfileOf(Newcomer), HouseholdOf(Resident), hostility, out Tag);
		}

		/// <summary>The plain yes-or-no for cohabitation.</summary>
		public static bool CanCohabit(GameObject Newcomer, GameObject Resident)
		{
			string tag;
			return KingdomQolRules.IsMatch(JudgeCohabitation(Newcomer, Resident, out tag));
		}

		/// <summary>
		/// The first design in a list this person would actually live in, for a caller choosing
		/// among the settlement's standing housing.
		/// </summary>
		/// <param name="Resident">The person.</param>
		/// <param name="Keys">Design keys to try, in the caller's own order of preference.</param>
		/// <param name="Tag">On failure, the tag that refused the LAST candidate tried, so a
		/// settlement with one kind of housing names the thing it is missing. Empty on success or
		/// on an empty list.</param>
		/// <returns>The key that matched, or null when none did.</returns>
		public static string FirstTolerable(GameObject Resident, IEnumerable<string> Keys, out string Tag)
		{
			Tag = "";
			if (Keys == null)
			{
				return null;
			}
			QolProfile profile = ProfileOf(Resident);
			foreach (string key in Keys)
			{
				string tag;
				if (KingdomQolRules.IsMatch(KingdomQolRules.Judge(OfferOf(key), profile, out tag)))
				{
					Tag = "";
					return key;
				}
				Tag = tag;
			}
			return null;
		}
	}
}
