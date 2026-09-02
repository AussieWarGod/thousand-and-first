using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomLocus
	{
		/// <summary>
		/// Offers the settlement's own water to a guest, spent exactly from its dedicated stores.
		/// Called
		/// from <see cref="XRL.World.Parts.r_KingdomGuest"/>'s inventory action; a no-op if the
		/// guest has already been offered water or is no longer present.
		/// </summary>
		/// <param name="Guest">The guest object the player targeted.</param>
		public static void OfferGuestWater(GameObject Guest)
		{
			if (Guest == null || Guest.GetIntProperty("KingdomGuest") != 1 || Guest.GetIntProperty("KingdomGuestOffered") == 1)
			{
				return;
			}
			Zone zone = Guest.CurrentZone;
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (!KingdomMaster.NewWorkAllowed(system))
			{
				Popup.Show("Settlement simulation is paused; the guest can be helped after it resumes.");
				return;
			}
			if (zone == null || !system.Founded || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				return;
			}
			int cost = KingdomLocusRules.GuestWaterCostDrams;
			string measure = cost + ((cost == 1) ? " dram" : " drams");
			KingdomSurvey survey = KingdomSurvey.Take(zone);
			if (survey.StoredWater < cost)
			{
				Popup.Show("Offering water to "
					+ KingdomPresentation.Rich(PlainGuestName(Guest))
					+ " requires exactly {{C|"
					+ measure + "}} from the dedicated stores, and they cannot provide it.");
				return;
			}
			string guestName = PlainGuestName(Guest);
			string shownGuestName = KingdomPresentation.Rich(guestName);
			bool causal = Guest.GetIntProperty(CausalPilgrimProperty) == 1;
			string cause = causal ? Guest.GetStringProperty(PilgrimCauseProperty) : null;
			string shownCause = KingdomPresentation.Rich(cause);
			string realm = KingdomPresentation.Rich(system.KingdomDisplayName);
			string chronicle = causal
				? KingdomLocusRules.PilgrimChronicleLine(shownGuestName,
					KingdomPresentation.Rich(system.City.PilgrimPlaceName), shownCause,
					Greeted: true)
				: (!system.FirstGuestGreeted
					? realm + " gave water to its first guest since its founding, and the traveller went on speaking well of it"
					: KingdomLocusRules.GuestChronicleLine(true, realm));
			string ledger = causal
				? shownGuestName + " received water and went on speaking of "
					+ shownCause + "."
				: shownGuestName + " received " + measure + " and continued along the road.";
			string message = "{{C|" + realm + " offered " + measure
				+ " to " + shownGuestName + ".}}";
			long next = KingdomLocusRules.NextGuestDueTick(The.Game.TimeTicks);
			bool milestone = !system.FirstGuestGreeted;
			if (!KingdomGuestLifecycle.PublishOfferWater(system, Guest, The.Game.TimeTicks,
				next, chronicle, ledger, message, milestone))
			{
				Popup.Show("The offering could not complete. Its exact lifecycle receipt remains open; no second offering can begin.");
				return;
			}
			Popup.Show(KingdomLocusRules.GuestThanks(shownGuestName, realm));
		}

		/// <summary>Plain lifecycle name; rich output is a separate projection.</summary>
		private static string PlainGuestName(GameObject guest)
		{
			if (!GameObject.Validate(guest)) return "A traveller";
			string named = guest.GetStringProperty("KingdomName");
			if (string.IsNullOrEmpty(named)) named = guest.BaseDisplayNameStripped;
			return string.IsNullOrEmpty(named) ? "A traveller" : named;
		}

		private static bool DepartGuest(KingdomSystem System, GameObject Guest, bool Greeted)
		{
			if (Guest.GetIntProperty(CausalPilgrimProperty) == 1)
			{
				long depart = System.GuestDepartTick;
				if (depart <= 0L && !KingdomLocusRules.TryPilgrimWindow(
					System.City.PilgrimCauseTick, out _, out depart)) return false;
				return ResolvePilgrim(System, System.City, Guest, Greeted, depart);
			}
			string name = PlainGuestName(Guest);
			string shownName = KingdomPresentation.Rich(name);
			bool milestone = Greeted && !System.FirstGuestGreeted;
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			string line = milestone
				? realm + " gave water to its first guest since its founding, and the traveller went on speaking well of it"
				: KingdomLocusRules.GuestChronicleLine(Greeted, realm);
			string ledger = Greeted
				? shownName + " received water and continued along the road."
				: KingdomLocusRules.GuestLedgerNote(shownName,
					KingdomRules.ElapsedDays(The.Game.TimeTicks - System.GuestDepartTick));
			string message = Greeted ? "{{C|" + shownName
				+ " continued along the road.}}" : null;
			return KingdomGuestLifecycle.PublishDeparture(System, Guest,
				KingdomLifecycleLane.PlainGuest, The.Game.TimeTicks,
				KingdomLocusRules.NextGuestDueTick(The.Game.TimeTicks), Greeted,
				line, ledger, message, null, milestone);
		}
	}
}
