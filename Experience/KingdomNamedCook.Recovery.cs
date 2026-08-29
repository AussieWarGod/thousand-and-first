using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomNamedCook
	{
		internal static void ReconcileAll(KingdomSystem System, bool LoadZones)
		{
			if (System == null || !System.Founded) return;
			System.Collections.Generic.List<KingdomCityBook> books = System.OwnedCityBooks();
			for (int i = 0; i < books.Count; i++)
			{
				string failure;
				Reconcile(System, books[i], LoadZones, out failure);
				if (books[i]?.NamedCook?.Phase == KingdomNamedCookPhase.Quarantined)
					KingdomLog.Log("named cook: " + (failure ?? books[i].NamedCook.Fault));
			}
		}

		internal static void ReconcileZone(KingdomSystem System, Zone Zone)
		{
			if (System == null || !System.Founded || Zone == null) return;
			KingdomCityBook book = System.ClaimedZones != null
				&& System.ClaimedZones.Contains(Zone.ZoneID) ? System.City
				: System.FindNonSeatSettlementByZone(Zone.ZoneID)?.City;
			if (book == null) return;
			string failure;
			if (!RepairWitnessedLossOnActiveGround(System, book, Zone, out failure))
			{
				KingdomLog.Log("named cook: witnessed active-ground recovery waits ("
					+ (failure ?? "unknown failure") + ")"); return;
			}
			Reconcile(System, book, false, out failure);
			if (book.NamedCook?.Phase == KingdomNamedCookPhase.Quarantined)
				KingdomLog.Log("named cook: " + (failure ?? book.NamedCook.Fault));
		}

		private static bool RemoveProjection(KingdomCityBook Book, GameObject Body,
			KingdomNamedCookReceipt Authority, out string Failure)
		{
			Failure = null;
			r_KingdomNamedCook marker = Body?.GetPart<r_KingdomNamedCook>();
			TeachesDish teaching = Body?.GetPart<TeachesDish>();
			if (marker != null && !marker.Matches(Authority, Body))
				return Quarantine(Book, Authority, "Cook release found a different body marker.",
					out Failure);
			if (teaching != null && !ExactTeaching(teaching, Authority))
				return Quarantine(Book, Authority, "Cook release found a different native recipe.",
					out Failure);
			try
			{
				if (teaching != null) Body.RemovePart(teaching);
				if (marker != null) Body.RemovePart(marker);
			}
			catch (Exception ex)
			{
				Failure = "Native cook release threw " + ex.GetType().Name + ".";
				return false;
			}
			if (Body.GetPart<TeachesDish>() != null || Body.GetPart<r_KingdomNamedCook>() != null)
				return Quarantine(Book, Authority, "Native cook parts remained after exact release.",
					out Failure);
			long now = The.Game == null || The.Game.TimeTicks < Authority.DesignatedTick
				? Authority.DesignatedTick : The.Game.TimeTicks;
			KingdomNamedCookReceipt released = KingdomNamedCookRules.Released(Authority, now);
			if (released == null)
			{
				Failure = "The released cook receipt could not commit.";
				return false;
			}
			Book.NamedCook = released;
			return true;
		}

		private static bool Quarantine(KingdomCityBook Book,
			KingdomNamedCookReceipt Authority, string Reason, out string Failure)
		{
			Failure = Reason;
			if (Book != null)
				Book.NamedCook = KingdomNamedCookRules.Quarantined(Authority, Reason)
					?? new KingdomNamedCookReceipt();
			KingdomLog.Log("named cook: " + Reason);
			return false;
		}

		private static string Refusal(KingdomNamedCookVerdict Verdict)
		{
			switch (Verdict)
			{
			case KingdomNamedCookVerdict.Unfounded:
				return "No founded realm can appoint a cook.";
			case KingdomNamedCookVerdict.NotOwnedCity:
				return "That roll is not an exact owned city.";
			case KingdomNamedCookVerdict.NotStandingResident:
				return "That person is not a standing resident of this city.";
			case KingdomNamedCookVerdict.BodyNotExact:
				return "The exact bound resident body could not be resolved.";
			case KingdomNamedCookVerdict.PlayerOrFollower:
				return "The founder and player-led followers keep their own recipe authority.";
			case KingdomNamedCookVerdict.NativeRecipeAlreadyPresent:
				return "That resident already teaches a native recipe; it was not overwritten.";
			case KingdomNamedCookVerdict.ForeignCookMarker:
				return "A different named-cook receipt already marks that body.";
			case KingdomNamedCookVerdict.OpenReceipt:
				return "Release the current named-cook appointment first.";
			default:
				return "The exact resident or appointment identity is malformed.";
			}
		}
	}
}
