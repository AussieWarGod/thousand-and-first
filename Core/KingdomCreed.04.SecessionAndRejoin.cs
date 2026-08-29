using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomCreed
	{

		/// <summary>
		/// Eases the quarrel because the founder called a shared meal while it was running. Called
		/// from the meal itself rather than being a lever of its own: the founder already paid for
		/// the food, and this is the meal being worth more than its calories when there is
		/// something to mend.
		/// </summary>
		/// <param name="System">The kingdom. Does nothing for a one-city realm or a realm at
		/// peace, so a founder who holds one city never learns this exists.</param>
		public static void EaseForMeal(KingdomSystem System)
		{
			if (!Enabled || System == null || !System.Founded ||
				System.SettlementCount < 2 || System.Dissent <= 0)
			{
				return;
			}
			System.Dissent = KingdomCreedRules.ApplyDissent(System.Dissent, -KingdomCreedRules.MealEase);
			MessageQueue.AddPlayerMessage("{{G|Word of the meal reaches " + OtherCityName(System) + " before the plates are cold.}}");
			Rearm(System);
		}

		/// <summary>Whether the ground belongs to a city that left the realm.</summary>
		/// <param name="ZoneID">A zone id. Null and empty read as false.</param>
		public static bool SecededHolds(KingdomSystem System, string ZoneID)
		{
			return System != null && System.Seceded != null && !string.IsNullOrEmpty(ZoneID) && System.Seceded.ClaimedZones.Contains(ZoneID);
		}

		/// <summary>
		/// The unhappier of the realm's two cities leaves it.
		/// <para>
		/// Preconditions: the realm holds two cities whose creeds clash, and dissent has reached
		/// <see cref="KingdomCreedRules.DissentBreaking"/> — or <paramref name="Forced"/> is set,
		/// which is the debug path and skips the dissent requirement and nothing else. Side
		/// effects: the leaving city moves whole into <c>KingdomSystem.Seceded</c>, the realm keeps
		/// the other as its seat, dissent is cleared, both chronicle registers record the day in
		/// their own words, and a modal states what has changed. Failure mode: returns false with a
		/// founder-facing refusal and changes nothing.
		/// </para>
		/// <para>
		/// Nothing physical is touched, for the same reasons exile does not touch anything: no
		/// citizen's allegiance key moves, no faction is minted or unmade, no zone is stripped, no
		/// vessel loses its dedication. The seceded city's people still carry the realm's faction
		/// property and are not hostile. They simply are not yours, and the ground they stand on
		/// stops being in <c>ClaimedZones</c>, so every kingdom pass skips it.
		/// </para>
		/// </summary>
		public static bool Secede(KingdomSystem System, bool Forced, out string Refusal)
		{
			Refusal = "";
			if (System == null || !System.Founded)
			{
				Refusal = "There is no realm to leave.";
				return false;
			}
			if (!EnsureDissentPair(System, out KingdomCreedPairCity first,
				out KingdomCreedPairCity second))
			{
				Refusal = "The cities at odds cannot be identified exactly.";
				return false;
			}
			SecessionVerdict verdict = KingdomCreedRules.JudgeSecession(System.SettlementCount,
				HostilityBetween(first.Creed, second.Creed), System.Dissent, Forced);
			if (verdict != SecessionVerdict.Warranted)
			{
				Refusal = SecessionRefusal(verdict);
				return false;
			}
			bool secondLeaves = KingdomCreedRules.AwayIsTheLeaver(
				Feeling(first.Creed, second.Creed), Feeling(second.Creed, first.Creed),
				first.Population, second.Population);
			KingdomCreedPairCity leaving = secondLeaves ? second : first;
			KingdomCreedPairCity staying = secondLeaves ? first : second;
			// Read before anything moves: once the seat is exchanged, "the seated city" names the
			// other one and every string below would be about the wrong place.
			string keptName = staying.Name;
			string leaverName = leaving.Name;
			string leaverCreed = CreedName(leaving.Creed);
			int leaverPopulation = leaving.Population;
			KingdomRelocation.BeforeOwnershipLoss(System, leaving.Seated
				? System.ClaimedZones : leaving.Settlement?.ClaimedZones,
				"The city seceded while the heart's ring was called.");
			if (!leaving.Seated)
			{
				if (!System.TryRemoveNonSeatSettlement(leaving.Settlement, out Refusal))
					return false;
				System.Seceded = leaving.Settlement;
			}
			else
			{
				// Capture-then-Restore is the sanctioned exchange (see KingdomSystem.TrySeat): the
				// two share their containers for exactly as long as it takes to write the survivor
				// over the flat fields.
				KingdomSettlement leavingSeat = System.Capture();
				if (staying.Settlement == null ||
					!System.TryRemoveNonSeatSettlement(staying.Settlement, out Refusal))
					return false;
				try { System.Restore(staying.Settlement); }
				catch (global::System.Exception ex)
				{
					System.TryAddNonSeatSettlement(staying.Settlement, out string _);
					Refusal = "The surviving city could not take the seat: " + ex.Message;
					return false;
				}
				System.Seceded = leavingSeat;
			}
			System.SecededTick = The.Game.TimeTicks;
			System.Dissent = 0;
			System.DissentSpoken = 0;
			KingdomBrink.LiftCity(System, The.Game.TimeTicks);
			System.LastDissentTick = The.Game.TimeTicks;
			ClearDissentPair(System);
			KingdomChronicle.RecordDisputed(System,
				KingdomCreedRules.SecessionTelling(KingdomPresentation.Rich(leaverName), KingdomPresentation.Rich(keptName), leaverCreed),
				KingdomCreedRules.SecessionRumour(KingdomPresentation.Rich(leaverName), KingdomPresentation.Rich(KingdomChronicle.FounderName())));
			Popup.Show(KingdomCreedRules.SecessionNotice(KingdomPresentation.Rich(leaverName), KingdomPresentation.Rich(keptName), leaverCreed, leaverPopulation));
			KingdomLog.Log("secession: " + (leaverName ?? "-") + " left; realm keeps " + (keptName ?? "-"));
			return true;
		}

		/// <summary>
		/// Takes a seceded city back into the realm.
		/// <para>
		/// Preconditions: a city seceded, the realm has room for it, the founder is standing on its
		/// own ground, the clash that split them is no longer live, and the realm's standing with
		/// the city's creed is not contemptible — see
		/// <see cref="KingdomCreedRules.JudgeRejoin"/>. Side effects: the city returns as the
		/// realm's second, the seat moves into it because the founder is standing there, dissent is
		/// cleared, both registers record the day, and a modal states it. Failure mode: returns
		/// false with a founder-facing refusal and changes nothing.
		/// </para>
		/// <para>
		/// The cause has to be gone before the city is. A founder who walks back with the same
		/// clash still live is told exactly that and sent away — waiting it out is not a route,
		/// because a quarrel nobody did anything about has not ended.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Site">The zone the founder is standing in.</param>
		/// <param name="Refusal">Founder-facing reason, or empty on success.</param>
		public static bool TryRejoin(KingdomSystem System, Zone Site, out string Refusal)
		{
			Refusal = "";
			if (System == null || !System.Founded)
			{
				Refusal = "There is no realm for anyone to come back to.";
				return false;
			}
			string leaverCreed = CreedOf(System.Seceded);
			RejoinVerdict verdict = KingdomCreedRules.JudgeRejoin(
				System.Seceded != null,
				System.SettlementCount,
				Site != null && SecededHolds(System, Site.ZoneID),
				MaxHostilityWithRealm(System, leaverCreed),
				System.GetRegardForRealm(leaverCreed));
			string leaverName = (System.Seceded != null) ? System.Seceded.SettlementName : null;
			if (verdict != RejoinVerdict.Allowed)
			{
				Refusal = KingdomCreedRules.RejoinRefusal(verdict, KingdomPresentation.Rich(leaverName), CreedName(leaverCreed));
				return false;
			}
			if (!System.TryAddNonSeatSettlement(System.Seceded, out string topologyFailure))
			{
				Refusal = "The city cannot rejoin the exact realm topology: " + topologyFailure;
				return false;
			}
			KingdomGovernanceScope.Commit("rejoin city");
			System.Seceded = null;
			System.SecededTick = 0;
			System.Dissent = 0;
			System.DissentSpoken = 0;
			// A realm that has taken a city back is not standing at a brink, whatever the state
			// slot was left holding by a secession that has since been undone.
			KingdomBrink.LiftCity(System, The.Game.TimeTicks);
			System.LastDissentTick = The.Game.TimeTicks;
			ClearDissentPair(System);
			// The founder is standing in it, so it becomes the seat by the ordinary route rather
			// than a second one invented here.
			System.TrySeat(Site);
			KingdomChronicle.RecordDisputed(System,
				KingdomCreedRules.RejoinTelling(KingdomPresentation.Rich(leaverName)),
				KingdomCreedRules.RejoinRumour(KingdomPresentation.Rich(leaverName), KingdomPresentation.Rich(KingdomChronicle.FounderName())));
			Popup.Show(KingdomCreedRules.RejoinNotice(KingdomPresentation.Rich(leaverName), KingdomPresentation.Rich(System.KingdomDisplayName)));
			KingdomLog.Log("rejoin: " + (leaverName ?? "-") + " came back");
			return true;
		}

		/// <summary>The realm's temper between its two cities. Concord for anything else.</summary>
		public static CityTemper Temper(KingdomSystem System)
		{
			if (System == null || !System.Founded || System.SettlementCount < 2)
			{
				return CityTemper.Concord;
			}
			return KingdomCreedRules.ClassifyTemper(System.Dissent);
		}

		private static int MaxHostilityWithRealm(KingdomSystem System, string Creed)
		{
			int hostility = HostilityBetween(Creed, SeatCreed(System));
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++) hostility = global::System.Math.Max(
				hostility, HostilityBetween(Creed, CreedOf(nonSeat[i])));
			return hostility;
		}
	}
}
