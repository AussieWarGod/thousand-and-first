using System.Collections.Generic;
using XRL.World;


namespace ThousandAndFirst
{
	using XRL.World.Parts;
	using XRL.World.Parts.Mutation;

	public static partial class KingdomQol
	{
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
			truth.Species = Resident.GetSpecies();
			Brain brain = Resident.GetPart<Brain>();
			truth.Aquatic = Resident.HasPart<Aquatic>() || (brain != null && brain.Aquatic);
			truth.Flying = Resident.IsFlying;
			truth.BroadBodied = Resident.HasTagOrProperty("Gigantic");
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
	}
}
