using System.Collections.Generic;
using XRL.World;


namespace ThousandAndFirst
{
	using XRL.World.Parts;
	using XRL.World.Parts.Mutation;

	public static partial class KingdomQol
	{
		// --- The questions the rest of the mod asks ------------------------------------------

		/// <summary>
		/// Whether this person lives in this design, and which tag decided it.
		/// </summary>
		/// <param name="Resident">The settler, notable, or guest. Null asks nothing and matches.
		/// </param>
		/// <param name="BuildingKey">The design's registry key.</param>
		/// <param name="Tag">The tag refused, or the first need missing. Empty on a match.</param>
		public static QolVerdict PreviewJudge(GameObject Resident, string BuildingKey,
			out string Tag)
		{
			return KingdomQolRules.Judge(CatalogueOfferOf(BuildingKey),
				ProfileOf(Resident), out Tag);
		}

		/// <summary>The plain yes-or-no, for callers that have nothing to say about a no.</summary>
		public static bool PreviewWillLive(GameObject Resident, string BuildingKey)
		{
			string tag;
			return KingdomQolRules.IsMatch(PreviewJudge(Resident, BuildingKey, out tag));
		}

		/// <summary>
		/// Whether this person would tolerate these quarters &mdash; the same question as
		/// <see cref="PreviewWillLive"/>, asked of temporary lodging during a rebuild. Addendum 3's
		/// "tolerable displacement" re-based onto this vocabulary as Addendum 4 directs: tolerance
		/// IS the Needs check against the quarters on offer, and the shelter-rank ladder in
		/// <c>KingdomUpgradeRules</c> goes on deciding how GOOD the lodging must be.
		/// </summary>
		/// <param name="Resident">The person being moved.</param>
		/// <param name="QuartersKey">The design key of the lodging on offer. Blank quarters offer
		/// nothing, which a person who needs nothing still tolerates.</param>
		/// <param name="Tag">The tag that decided it, for the founder's line.</param>
		public static bool PreviewTolerates(GameObject Resident, string QuartersKey,
			out string Tag)
		{
			return KingdomQolRules.IsMatch(PreviewJudge(Resident, QuartersKey, out Tag));
		}

		/// <summary>
		/// Equilibrium points this person's met Prefers are worth in this building: small, capped,
		/// and routed through the tastes machinery so there is one balance to keep rather than two.
		/// Never negative, and an unmet Prefers is worth exactly nothing rather than a penalty.
		/// </summary>
		public static int PreviewPreferShade(GameObject Resident, string BuildingKey)
		{
			return KingdomQolRules.PreferShade(CatalogueOfferOf(BuildingKey),
				ProfileOf(Resident));
		}

		/// <summary>
		/// The met/unmet flags for this person's Prefers in this building, as
		/// <c>KingdomCeremonyRules.TasteShade</c> takes them, for a caller that is already
		/// assembling a taste list and wants these folded into it.
		/// </summary>
		public static List<bool> PreviewPreferFlags(GameObject Resident, string BuildingKey)
		{
			return KingdomQolRules.PreferFlags(CatalogueOfferOf(BuildingKey),
				ProfileOf(Resident));
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
		[System.Obsolete("Retired before public release; use KingdomLodging with the home's explicit closeness rung.", true)]
		public static QolVerdict JudgeCohabitation(GameObject Newcomer, GameObject Resident, out string Tag)
		{
			int hostility = 0;
			if (Newcomer != null && Resident != null && KingdomCreed.Enabled)
			{
				hostility = KingdomCreed.HostilityBetween(
					Newcomer.GetStringProperty(KingdomCreed.CreedProperty),
					Resident.GetStringProperty(KingdomCreed.CreedProperty));
			}
			Tag = "";
			if (hostility >= 100)
			{
				return QolVerdict.Refused;
			}
			QolVerdict verdict = KingdomQolRules.Judge(HouseholdOf(Resident),
				ProfileOf(Newcomer), out Tag);
			if (verdict == QolVerdict.NeedUnmet)
			{
				Tag = "";
				return QolVerdict.Match;
			}
			return verdict;
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
		public static string PreviewFirstTolerable(GameObject Resident,
			IEnumerable<string> Keys, out string Tag)
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
				if (KingdomQolRules.IsMatch(KingdomQolRules.Judge(
					CatalogueOfferOf(key), profile, out tag)))
				{
					Tag = "";
					return key;
				}
				Tag = tag;
			}
			return null;
		}

		[System.Obsolete("Catalogue preview only; use PreviewJudge or a physical benefit reading.", true)]
		public static QolVerdict Judge(GameObject Resident, string BuildingKey, out string Tag)
		{
			return PreviewJudge(Resident, BuildingKey, out Tag);
		}

		[System.Obsolete("Catalogue preview only; use PreviewWillLive or a physical benefit reading.", true)]
		public static bool WillLive(GameObject Resident, string BuildingKey)
		{
			return PreviewWillLive(Resident, BuildingKey);
		}

		[System.Obsolete("Catalogue preview only; use PreviewTolerates or a physical benefit reading.", true)]
		public static bool Tolerates(GameObject Resident, string QuartersKey, out string Tag)
		{
			return PreviewTolerates(Resident, QuartersKey, out Tag);
		}

		[System.Obsolete("Catalogue preview only; use PreviewPreferShade or a physical benefit reading.", true)]
		public static int PreferShade(GameObject Resident, string BuildingKey)
		{
			return PreviewPreferShade(Resident, BuildingKey);
		}

		[System.Obsolete("Catalogue preview only; use PreviewPreferFlags or a physical benefit reading.", true)]
		public static List<bool> PreferFlags(GameObject Resident, string BuildingKey)
		{
			return PreviewPreferFlags(Resident, BuildingKey);
		}

		[System.Obsolete("Catalogue preview only; use PreviewFirstTolerable or a physical benefit reading.", true)]
		public static string FirstTolerable(GameObject Resident,
			IEnumerable<string> Keys, out string Tag)
		{
			return PreviewFirstTolerable(Resident, Keys, out Tag);
		}
	}
}
