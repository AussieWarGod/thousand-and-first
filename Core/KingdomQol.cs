using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;
	using XRL.World.Parts.Mutation;

	/// <summary>
	/// The engine-coupled half of the quality-of-life vocabulary (<see cref="KingdomQolRules"/> is
	/// the whole of the matching, and is engine-free): the catalogue contract of what each design
	/// may <c>Provide</c>, the reads that turn a real creature into a
	/// <see cref="ResidentTruth"/>, and the two-line answers the rest of the mod asks for &mdash;
	/// will this person live here, will they live beside that one, and what is their being here
	/// worth.
	/// <para>
	/// <b>No state of its own.</b> Nothing in this file is serialized, and there is no per-city or
	/// realm-level field anywhere in the system. Live-building callers obtain tags through
	/// <c>TryPhysicalOfferOf</c> and the current benefit index; the key-only methods in this file
	/// describe catalogue ceilings for previews and authoring validation, never physical supply.
	/// Every resident answer is recomputed from the tags in hand,
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
	public static partial class KingdomQol
	{
		// --- Registry ---------------------------------------------------------------------

		// Keyed by building Key like every other registry beside the catalogue (STANDARDS 6): a
		// later file re-using a key owns that design's whole Provides list, and a file that
		// re-declares the design without naming Provides at all correctly leaves it with none.
		// Raw strings, parsed on read and cached, because the merge layer hands this the merged
		// attribute and merges happen before anything is parsed.
		private static readonly Dictionary<string, string> Declared = new Dictionary<string, string>();

		// One design does not have one offer. What a roof gives depends on the stratum it stands
		// in -- nothing underground admits sky, whatever its roof state says -- so the assembled
		// answer is cached per stratum rather than per key, and a design raised both above and
		// below the surface is answered correctly for each.
		private static readonly Dictionary<string, string[]> SurfaceOffers = new Dictionary<string, string[]>();

		private static readonly Dictionary<string, string[]> DeepOffers = new Dictionary<string, string[]>();

		private static Dictionary<string, string[]> OfferCacheFor(bool Underground)
		{
			return Underground ? DeepOffers : SurfaceOffers;
		}

		/// <summary>Forgets every registered <c>Provides</c>. Called by the registry loader before
		/// it re-reads the XML streams.</summary>
		public static void ClearProvides()
		{
			Declared.Clear();
			SurfaceOffers.Clear();
			DeepOffers.Clear();
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
			SurfaceOffers.Remove(Key);
			DeepOffers.Remove(Key);
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
		/// Everything a design is allowed to offer on the named stratum: its declared
		/// tags plus what its own roof gives (sky under canvas and open ground, shade under wall
		/// and rock), so an author who never wrote a <c>Provides</c> still houses a photosynthetic
		/// settler correctly.
		/// <para>
		/// The stratum is the caller's to supply because it is not a property of the design. An
		/// open plot cut into the deep is still an open plot &mdash; it raises no walls, exactly as
		/// <c>KingdomPlotRules.RoofOnGround</c> says &mdash; but the hill is over it, so it offers
		/// shade and never sky.
		/// </para>
		/// </summary>
		/// <param name="BuildingKey">The design's registry key. Blank offers nothing.</param>
		/// <param name="Underground">Whether this ground is below
		/// <c>KingdomRules.SurfaceZLevel</c>; <c>KingdomPlotRules.IsUnderground</c> is the read
		/// that answers it.</param>
		/// This is catalogue-preview authority only. Runtime building supply must use
		/// <c>TryPhysicalOfferOf</c>.
		/// <returns>Never null. Empty for a design that declares nothing and takes no ground.
		/// </returns>
		public static string[] CatalogueOfferOf(string BuildingKey, bool Underground)
		{
			if (string.IsNullOrEmpty(BuildingKey))
			{
				return KingdomQolRules.NoTags;
			}
			Dictionary<string, string[]> cache = OfferCacheFor(Underground);
			string[] cached;
			if (cache.TryGetValue(BuildingKey, out cached))
			{
				return cached;
			}
			KingdomPlotRules.PlotSpec spec;
			bool isPlot = KingdomPlots.TryGetSpec(BuildingKey, out spec) && spec != null;
			string[] offer = KingdomQolRules.DesignOffer(DeclaredProvides(BuildingKey),
				isPlot ? spec.Roof : KingdomPlotRules.RoofState.Walled, isPlot, Underground);
			cache[BuildingKey] = offer;
			return offer;
		}

		/// <summary>The same offer, for a caller holding the zone the design stands in &mdash;
		/// which is every caller that reads a settlement's own housing. A null zone reads as the
		/// surface.</summary>
		public static string[] CatalogueOfferOf(string BuildingKey, Zone Z)
		{
			return CatalogueOfferOf(BuildingKey,
				Z != null && KingdomPlotRules.IsUnderground(Z.Z));
		}

		/// <summary>What a design offers on the surface.</summary>
		public static string[] CatalogueOfferOf(string BuildingKey)
		{
			return CatalogueOfferOf(BuildingKey, Underground: false);
		}

		/// <summary>What a work standing on the ground offers, read off the design key it was
		/// raised under and the stratum it is standing in. A work with no key on it &mdash;
		/// anything the settlement did not raise &mdash; offers nothing, and a work in no zone at
		/// all is read as standing on the surface.</summary>
		public static string[] CatalogueOfferOf(GameObject Work)
		{
			if (Work == null)
			{
				return KingdomQolRules.NoTags;
			}
			return CatalogueOfferOf(Work.GetStringProperty(KingdomUpgrade.BuildKeyProperty),
				Work.CurrentZone);
		}

		[Obsolete("Catalogue preview only; use CatalogueOfferOf or TryPhysicalOfferOf.", true)]
		public static string[] OfferOf(string BuildingKey, bool Underground)
		{
			return CatalogueOfferOf(BuildingKey, Underground);
		}

		[Obsolete("Catalogue preview only; use CatalogueOfferOf or TryPhysicalOfferOf.", true)]
		public static string[] OfferOf(string BuildingKey, Zone Z)
		{
			return CatalogueOfferOf(BuildingKey, Z);
		}

		[Obsolete("Catalogue preview only; use CatalogueOfferOf or TryPhysicalOfferOf.", true)]
		public static string[] OfferOf(string BuildingKey)
		{
			return CatalogueOfferOf(BuildingKey);
		}

		[Obsolete("Catalogue preview only; use CatalogueOfferOf or TryPhysicalOfferOf.", true)]
		public static string[] OfferOf(GameObject Work)
		{
			return CatalogueOfferOf(Work);
		}
	}
}
