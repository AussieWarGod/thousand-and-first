using System;
using System.Collections.Generic;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomAnnexe
	{
		/// <summary>
		/// F4's friction, riding the petitions surface that already ships rather than building
		/// anything parallel: a named person, waiting at the Charter, about a thing they actually
		/// mind.
		/// <para>
		/// The speaker is a Mechanimist and that inverts the lab's shape on purpose. The hall's
		/// petitioner is offended BY what is done there; the annexe's holds with the creed the act
		/// belongs to and minds the MANNER of it &mdash; chrome in Qud is borrowed from Shekhinah
		/// and repaid down the Sacred Well (<c>B/Books.xml:165,170,171</c>), and a city handing it
		/// out on its own authority has settled nothing with anybody. There is no correct answer,
		/// which is the point.
		/// </para>
		/// <para>
		/// The trigger arithmetic is <see cref="KingdomLabRules.SpeaksAgainstHall"/>, consumed
		/// rather than copied: a tenth of the city, a minority rather than a majority, and once is
		/// the whole of it. The latch is set only when a petition was really raised, so a founder
		/// who happened to be carrying another petition still gets this one next time.
		/// </para>
		/// </summary>
		private static void Speak(KingdomSystem Realm)
		{
			bool spoken = The.Game != null && The.Game.GetIntGameState(SpokenState) == 1;
			int holding = CreedCount(Realm, KingdomAnnexeRules.Creditors);
			if (!KingdomLabRules.SpeaksAgainstHall(holding, Realm.Population, spoken))
			{
				return;
			}
			if (KingdomPetitions.Raise(Realm, KingdomRules.PetitionKind.Chrome, KingdomAnnexeRules.Creditors))
			{
				The.Game?.SetIntGameState(SpokenState, 1);
				KingdomLog.Log("annexe: chrome debt spoken about (" + KingdomAnnexeRules.Creditors + " x" + holding + ")");
			}
		}

		private static int CreedCount(KingdomSystem Realm, string Creed)
		{
			int count;
			return (Realm.CreedCounts != null && Creed != null && Realm.CreedCounts.TryGetValue(Creed, out count)) ? count : 0;
		}

		// ==================================================================================
		// Reading the world
		// ==================================================================================

		/// <summary>
		/// Everyone standing where the register can see them: the annexe's own cell and the ring
		/// around it &mdash; vanilla's own reach for exactly this act
		/// (<c>CyberneticsTerminal.GetAuthorizedSubjects</c> walks the terminal's cell and its
		/// adjacent cells), plus the founder unconditionally, which is that same method's own
		/// first line. Anyone already enrolled, already Kin by birth, or not one of the city's own
		/// is dropped here, so a refusal the founder can do nothing about is never offered as a
		/// row.
		/// </summary>
		private static List<GameObject> Candidates(KingdomSystem Realm, GameObject Building, GameObject Actor)
		{
			List<GameObject> found = new List<GameObject>();
			if (Actor != null && Admits(Realm, Actor))
			{
				found.Add(Actor);
			}
			Cell cell = Building?.CurrentCell;
			if (cell == null)
			{
				return found;
			}
			Gather(Realm, found, cell);
			foreach (Cell adjacent in cell.GetLocalAdjacentCells())
			{
				Gather(Realm, found, adjacent);
			}
			return found;
		}

		private static void Gather(KingdomSystem Realm, List<GameObject> Found, Cell Where)
		{
			if (Where == null)
			{
				return;
			}
			foreach (GameObject item in Where.GetObjects())
			{
				if (item != null && !Found.Contains(item) && Admits(Realm, item))
				{
					Found.Add(item);
				}
			}
		}

		/// <summary>Whether a body is one the register could write down at all.</summary>
		private static bool Admits(KingdomSystem Realm, GameObject Who)
		{
			if (Who == null || !Who.IsCreature || Who.Body == null || KinByBirth(Who))
			{
				return false;
			}
			if (!Who.IsPlayer() && !KingdomCitizenship.BelongsTo(Realm, Who))
			{
				return false;
			}
			return !HeldBy(Realm, Who.GeneID);
		}

		private static KingdomEnrolVerdict JudgeFor(KingdomSystem Realm, GameObject Building, GameObject Who)
		{
			bool ours = Who != null && (Who.IsPlayer()
				|| KingdomCitizenship.BelongsTo(Realm, Who));
			return KingdomAnnexeRules.Judge(
				Founded: Realm != null && Realm.Founded,
				Annexe: Building != null && Building.HasPart("r_KingdomBecomingAnnexe"),
				Staffed: !string.IsNullOrEmpty(KeeperAt(Realm, Building)),
				Ours: ours,
				AlreadyKin: KinByBirth(Who),
				AlreadyEnrolled: Who != null && HeldBy(Realm, Who.GeneID),
				StoredWater: StoredWater(Realm, Building));
		}

		/// <summary>
		/// The city this building actually stands in, for the heading over its own book.
		/// <para>
		/// Not <c>Realm.SeatName</c>, which is what this used to read. The seat is the settlement
		/// that owns physical truth and <b>retargeting it is never automatic</b>
		/// (FOUNDATION-CONTRACT), so a founder standing in front of a register in their other city
		/// could be shown the seat's name over the seat's rolls and reasonably conclude they were
		/// reading a different city's book. The ground is asked instead, exactly as the arch asks
		/// it, and the seat is only the answer when the ground could not be named at all.
		/// </para>
		/// </summary>
		private static string CityAt(KingdomSystem Realm, GameObject Building)
		{
			Zone zone = Building?.CurrentZone;
			string city = (zone == null) ? null : KingdomCrown.CityOf(Realm, zone.ZoneID);
			return string.IsNullOrEmpty(city) ? Realm.SeatName : city;
		}

		private static int StoredWater(KingdomSystem Realm, GameObject Building)
		{
			Zone zone = Building?.CurrentZone;
			return (zone == null) ? 0 : KingdomSurvey.Take(zone, Realm).StoredWater;
		}

		/// <summary>
		/// Whoever keeps the book, or null when nobody does. Derived from the crew the lodging
		/// machinery already placed &mdash; the annexe assigns nobody, exactly as Addendum 6 says
		/// a great work never does, and exactly as the grafting hall's savant is read.
		/// </summary>
		private static string KeeperAt(KingdomSystem Realm, GameObject Building)
		{
			Zone zone = Building?.CurrentZone;
			if (Realm == null || !Realm.Founded || zone == null) return null;
			GameObject best = null;
			string bestId = null;
			KingdomSurvey survey = KingdomSurvey.Take(zone, Realm);
			for (int i = 0; survey != null && survey.Settlers != null
				&& i < survey.Settlers.Count; i++)
			{
				GameObject candidate = survey.Settlers[i];
				if (!KingdomPurpose.IsLodgedSpecialist(zone, candidate,
					Psyberneticist: true)) continue;
				string id = candidate.IDIfAssigned;
				if (string.IsNullOrEmpty(id)) continue;
				if (best == null || string.CompareOrdinal(id, bestId) < 0)
				{
					best = candidate;
					bestId = id;
				}
			}
			return GameObject.Validate(best) ? PlainName(best) : null;
		}

		/// <summary>One plain persisted/display snapshot; Qud formatting enters only at a sink.</summary>
		private static string PlainName(GameObject Who)
		{
			return GameObject.Validate(Who) ? (Who.BaseDisplayNameStripped ?? "") : "";
		}

		/// <summary>
		/// Row labels for the register: the book stores ids, and the NAMES live on the people, so
		/// the zone the annexe stands in is read once per redraw to put a face to each id. Anybody
		/// who has moved on gets the honest line rather than a number.
		/// </summary>
		private static List<string> RollNames(GameObject Building, List<string> Rolls)
		{
			Dictionary<string, string> known = new Dictionary<string, string>();
			Zone zone = Building?.CurrentZone;
			if (zone != null)
			{
				foreach (GameObject item in zone.GetObjects())
				{
					r_KingdomEnrolled record = (item == null) ? null : item.GetPart<r_KingdomEnrolled>();
					if (record != null && !string.IsNullOrEmpty(record.Who) && !known.ContainsKey(record.Who))
					{
						known[record.Who] = string.IsNullOrEmpty(record.Named)
							? PlainName(item) : record.Named;
					}
				}
			}
			List<string> names = new List<string>();
			for (int i = 0; i < Rolls.Count; i++)
			{
				string name;
				names.Add(known.TryGetValue(Rolls[i], out name) ? name : "somebody who is not here today");
			}
			return names;
		}	}
}
