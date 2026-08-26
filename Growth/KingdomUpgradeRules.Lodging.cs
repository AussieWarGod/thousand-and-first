using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomUpgradeRules
	{
		// ==================================================================================
		// The absorption law (brief, Addendum 3): auto-improve when the city can absorb the
		// disruption; OFFER when it cannot. Never a timer -- nothing below reads the clock, the
		// age of a work, or how long anything has stood. Every figure here is a quantity the
		// settlement holds or a property of the design, and the only duration that appears is the
		// build's own LABOUR, which sizes the loss and never causes the trigger.
		// ==================================================================================

		/// <summary>
		/// How well the people living in a house must be housed while it is rebuilt. Read off the
		/// refinement surface the catalogue already carries &mdash; the <c>luxury</c> the design
		/// lifts by, the same lane the fine houses are written in &mdash; against the housing
		/// family of <c>KingdomCeremonyRules.TasteCategories</c>, which is the vocabulary a
		/// notable states a taste in ("judges a place by its roofs before its walls").
		/// </summary>
		public enum LodgingStandard
		{
			/// <summary>An ordinary settler. A roof of any kind will do: a bunk in a tent-row, a
			/// corner of somebody else's hut.</summary>
			Settler = 0,

			/// <summary>Someone who was housed for their standing. Walls, a floor, and a door;
			/// canvas is not lodging for a notable.</summary>
			Notable = 1,

			/// <summary>A notable whose stated taste IS housing. Nothing below what they are being
			/// moved out of, which for the best house in a city means nothing at all.</summary>
			Discerning = 2
		}

		/// <summary>Shelter an ordinary settler tolerates: canvas counts, open ground does not.
		/// The same rank <c>KingdomPlotRules.ShelterRank</c> gives a <c>Soft</c> roof.</summary>
		public const int BunkShelter = 1;

		/// <summary>Shelter a notable tolerates: the rank of a walled or carved room.</summary>
		public const int RoomShelter = 2;

		/// <summary>Luxury a design must lift by before the person living in it is housed as a
		/// notable rather than as a settler.</summary>
		public const int NotableLuxury = 1;

		/// <summary>Luxury at which the resident is a notable who states a taste in housing, and
		/// will not be moved down at all.</summary>
		public const int DiscerningLuxury = 3;

		/// <summary>
		/// Whose standard a house is judged by, from the refinement the design lifts. A design
		/// that lifts no luxury houses settlers, however grand its walls; that is the whole point
		/// of denominating the lane in <c>luxury</c> rather than in stone.
		/// </summary>
		/// <param name="LuxuryCarried">The <c>luxury</c> amount in the design's <c>Carries</c>.
		/// </param>
		public static LodgingStandard StandardFor(int LuxuryCarried)
		{
			if (LuxuryCarried >= DiscerningLuxury)
			{
				return LodgingStandard.Discerning;
			}
			return (LuxuryCarried >= NotableLuxury) ? LodgingStandard.Notable : LodgingStandard.Settler;
		}

		/// <summary>
		/// The shelter rank lodging must reach for this resident. A discerning notable is measured
		/// against the roof they are being moved out of, and never against less than a room, so a
		/// design authored with an odd roof cannot lower their standard below a notable's.
		/// </summary>
		/// <param name="Standard">Whose standard, from <see cref="StandardFor"/>.</param>
		/// <param name="CurrentShelter">Rank of the roof they live under now.</param>
		public static int ShelterRequired(LodgingStandard Standard, int CurrentShelter)
		{
			switch (Standard)
			{
			case LodgingStandard.Settler:
				return BunkShelter;
			case LodgingStandard.Notable:
				return RoomShelter;
			default:
				return (CurrentShelter > RoomShelter) ? CurrentShelter : RoomShelter;
			}
		}

		/// <summary>
		/// Whether the people living in a house can be put somewhere they would tolerate for the
		/// rebuild. Three things, all of them present-tense: somebody has to be living there, there
		/// has to be room for them elsewhere, and that elsewhere has to meet their own standard.
		/// <para>
		/// An empty house displaces nobody and is always improvable. A house nobody can be moved
		/// out of is not refused forever &mdash; it is refused until the settlement builds the
		/// lodging, which is a thing the founder can go and do.
		/// </para>
		/// </summary>
		/// <param name="Residents">People the house holds, which is the <c>roof</c> it carries.
		/// </param>
		/// <param name="SpareLodging">Roof standing elsewhere in the settlement that nobody is
		/// using.</param>
		/// <param name="OfferedShelter">The best shelter rank among that spare lodging.</param>
		/// <param name="Standard">Whose standard, from <see cref="StandardFor"/>.</param>
		/// <param name="CurrentShelter">Rank of the roof they live under now.</param>
		public static bool CanDisplace(int Residents, int SpareLodging, int OfferedShelter, LodgingStandard Standard, int CurrentShelter)
		{
			if (Residents <= 0)
			{
				return true;
			}
			if (SpareLodging < Residents)
			{
				return false;
			}
			return OfferedShelter >= ShelterRequired(Standard, CurrentShelter);
		}

		/// <summary>
		/// The other half of tolerance, and the half Addendum 4 re-bases onto the shared
		/// quality-of-life vocabulary: whether the quarters on offer are somewhere these
		/// particular residents will live AT ALL. <see cref="CanDisplace"/> goes on answering how
		/// GOOD the lodging must be &mdash; that ladder is unchanged and its boundaries are
		/// unmoved &mdash; and this answers whether it is lodging for these people. A tent is
		/// tolerable lodging for a settler and no lodging whatever for the robot who needs a
		/// cradle, and no shelter rank can say so.
		/// <para>
		/// Exactly <c>KingdomQolRules.Judge</c>, asked once per resident: tolerance IS the Needs
		/// check against the temporary quarters, which is the sentence Addendum 4 writes. Nobody
		/// is moved and nobody is evicted by a refusal here &mdash; the rebuild is held, and the
		/// existing <see cref="UpgradeVerdict.NoTolerableLodging"/> line says so.
		/// </para>
		/// </summary>
		/// <param name="QuartersOffer">What the temporary quarters provide, from
		/// <c>KingdomQol.OfferOf</c>. Null provides nothing, which residents who need nothing
		/// still accept.</param>
		/// <param name="Residents">Profiles of the people who would be moved. Null or empty
		/// measures nobody and refuses nothing.</param>
		/// <param name="Tag">The tag of the first resident who refuses, for the founder's line.
		/// Empty when nobody does.</param>
		public static bool QuartersRefused(string[] QuartersOffer, IList<QolProfile> Residents, out string Tag)
		{
			Tag = "";
			if (Residents == null)
			{
				return false;
			}
			for (int i = 0; i < Residents.Count; i++)
			{
				string tag;
				if (KingdomQolRules.IsBlocked(KingdomQolRules.Judge(QuartersOffer, Residents[i], out tag)))
				{
					Tag = tag;
					return true;
				}
			}
			return false;
		}

	}
}
