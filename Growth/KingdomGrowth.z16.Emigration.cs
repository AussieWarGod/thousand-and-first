using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{

		/// <param name="System">The realm.</param>
		/// <param name="Z">The zone they walk out of.</param>
		/// <param name="Survey">The pass's survey, or null.</param>
		/// <param name="Leaver">A particular settler, for a departure that is about THEM &mdash;
		/// Addendum 4b's settler who has no home they would live in. Null takes whoever the zone
		/// offers first, which is the drought's own indifference and is right for it.</param>
		/// <param name="Cause">The clause both registers name the departure by. Null is the
		/// drought, which is what this machinery was built for and reads exactly as it always
		/// did.</param>
		/// <param name="Chronicled">Whether this departure gets its own line in both registers and
		/// the ledger. True for every ordinary departure, and for the sampled ones of a long
		/// subsidence slide; false for the ones a slide is carrying in its summary line instead
		/// (<c>KingdomSubsidenceRules.TellsDeparture</c>). The person still leaves, the ledger's
		/// departure COUNT still rises, and the log still records it &mdash; what is saved is a
		/// chronicle entry, because a City falling to Camp would otherwise spend a quarter of the
		/// two-hundred-entry register on one event.</param>
		/// <param name="Note">The same departure in the ledger's shorter voice. Null falls back to
		/// <paramref name="Cause"/>, which is what a caller with only one phrasing wants, and
		/// what every caller written before the two registers wanted different lengths passed.</param>
		public static bool Emigrate(KingdomSystem System, Zone Z, KingdomSurvey Survey = null, GameObject Leaver = null, string Cause = null, bool Chronicled = true, string Note = null)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return false;
			if (Survey == null) Survey = KingdomSurvey.ActiveFor(Z);
			if (Simulation.City.KingdomResidents.OnRollCount(System)
				<= KingdomRules.LoyalCoreSettlers)
			{
				return false;
			}
			GameObject leaver = null;
			if (Leaver != null)
			{
				// A named departure still answers to the same law as any other: the settlement
				// never empties itself, and a settler the machinery would not take is one who
				// stays and is asked again next pass.
				if (KingdomCitizenship.BelongsTo(System, Leaver)
					&& Leaver.GetIntProperty("KingdomBorn") == 1 && Leaver.GetIntProperty("VillageMerchant") == 0 && !Leaver.IsPlayer() && !Leaver.IsPlayerLed()
					&& !Simulation.City.KingdomPhysicalHappenings.IsStaged(Leaver))
				{
					leaver = Leaver;
				}
			}
			else
			{
				IEnumerable<GameObject> candidates = Survey != null
					? (IEnumerable<GameObject>)Survey.Settlers : KingdomSurvey.ObjectsFor(Z);
				foreach (GameObject item in candidates)
				{
					if (KingdomCitizenship.BelongsTo(System, item)
						&& item.GetIntProperty("KingdomBorn") == 1 && item.GetIntProperty("VillageMerchant") == 0 && !item.IsPlayer() && !item.IsPlayerLed()
						&& !Simulation.City.KingdomPhysicalHappenings.IsStaged(item))
					{
						leaver = item;
						break;
					}
				}
			}
			if (leaver == null)
			{
				return false;
			}
			string citizenshipFailure;
			if (!KingdomCitizenship.CanRemove(System, leaver, out citizenshipFailure))
			{
				KingdomLog.Log("emigrate: exact citizenship removal refused ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				return false;
			}
			if (!KingdomCitizenship.TryRemove(System, leaver,
				KingdomCitizenshipRemovalReason.Emigration, out citizenshipFailure))
			{
				KingdomLog.Log("emigrate: exact citizenship removal did not commit ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				return false;
			}
			Simulation.City.KingdomResidentRow former;
			if (!Simulation.City.KingdomResidents.TryDepart(System, leaver, out former))
			{
				Simulation.City.KingdomCityBook stillBook;
				int stillResidentId;
				if (Simulation.City.KingdomResidents.TryLocate(System, leaver,
					out stillBook, out stillResidentId))
				{
					KingdomCitizenship.TryRestoreEmigrationAfterCleanRefusal(System, leaver,
						out citizenshipFailure);
				}
				else
				{
					KingdomLog.Log("emigrate: resident carriers need repair after citizenship "
						+ "committed; the body remains alive and is not obliterated");
				}
				return false;
			}
			string name = string.IsNullOrEmpty(former.Name) ? leaver.BaseDisplayNameStripped : former.Name;
			string origin = former.Origin;
			KingdomResidentIdentity.Forget(System, leaver);
			KingdomCreed.Forget(System, leaver);
			try { leaver.Obliterate(); }
			finally { Survey?.ObserveCurrentTopology(leaver); }
			// Both registers name the person and the cause. The default clause is the drought's,
			// word for word as it always read; a caller that hands one in replaces it in both
			// places at once, so the chronicle and the ledger can never disagree about why
			// somebody left.
			string chronicled = string.IsNullOrEmpty(Cause) ? "for wetter country, the cisterns having run dry" : Cause;
			string noted = string.IsNullOrEmpty(Note) ? (string.IsNullOrEmpty(Cause) ? "for wetter country" : Cause) : Note;
			// The count is never sampled, only the telling: a founder who reads the ledger's
			// departure tally gets the true number however the story of it was told.
			System.Ledger.Departures++;
			if (Chronicled)
			{
				string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
				string named = KingdomPresentation.Rich(XRL.Language.Grammar.A(name));
				string namedAtStart = KingdomPresentation.Rich(
					XRL.Language.Grammar.A(name, Capitalize: true));
				KingdomChronicle.Record(System, named + " left " + realm + " " + chronicled);
				System.Ledger.Note(KingdomVoices.Say(System, VoiceOccasion.CitizenLost,
					"{{R|" + namedAtStart + " left " + realm + " " + noted + ".}}"));
			}
			if (KingdomLog.Enabled) KingdomLog.Log("emigrate: pop now " + System.Population + " origin=" + (origin ?? "-") + " cause=" + (Cause ?? "drought"));
			return true;
		}
	}
}
