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

		/// <summary>Whether the founder may hold a rite of shared water here and now.</summary>
		public static bool RiteAvailable(KingdomSystem System)
		{
			return Enabled && System != null && System.Founded
				&& System.SettlementCount >= KingdomSettlement.MaxSettlements
				&& KingdomCreedRules.RiteCost(Temper(System)) > 0;
		}

		/// <summary>
		/// The rite of shared water: the seated city pours for the other city's people and drinks
		/// with them.
		/// <para>
		/// Preconditions: the realm holds two cities at odds, the founder is on the seated city's
		/// ground, its dedicated stores can bear
		/// <see cref="KingdomCreedRules.RiteCost"/>, and the rite is off cooldown. Side effects:
		/// drams are drained from the dedicated stores, dissent eases, and the day is chronicled.
		/// Failure mode: returns false with a founder-facing reason and changes nothing.
		/// </para>
		/// <para>
		/// Water is what founds this realm and water is what holds it together; this is the lever
		/// the mod is actually about. It cannot outrun the quarrel on its own at the worst
		/// hostility the game's data holds — it holds the line and gains slowly, at eighty drams
		/// every three days, which is the price of not choosing.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Z">The zone the founder is standing in.</param>
		/// <param name="Failure">Founder-facing reason, or empty on success.</param>
		public static bool HoldRite(KingdomSystem System, Zone Z, out string Failure)
		{
			Failure = "";
			if (!Enabled || System == null || !System.Founded || System.SettlementCount < KingdomSettlement.MaxSettlements)
			{
				Failure = "A rite of shared water is held between two cities. This realm has one.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "The rite is held on the realm's own ground.";
				return false;
			}
			CityTemper temper = Temper(System);
			int cost = KingdomCreedRules.RiteCost(temper);
			if (cost <= 0)
			{
				Failure = "There is nothing between your two cities that a basin of water would mend. Do not go looking for one.";
				return false;
			}
			if (!KingdomCreedRules.RiteReady(System.LastRiteTick, The.Game.TimeTicks))
			{
				Failure = "You poured for them too recently. A rite held every day is a habit, and a habit mends nothing.";
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			KingdomWaterDebit debit;
			if (!survey.TryReserveExactWater(cost, out debit))
			{
				Failure = "The rite requires exactly {{C|" + cost
					+ " drams}} from the dedicated stores, and they cannot provide it.";
				return false;
			}
			// Last safe point before the realm's cadence, dissent or history changes. This receipt
			// either drains the whole stated cost or restores every vessel it touched.
			if (!debit.Commit())
			{
				Failure = "The dedicated stores could not yield exactly {{C|" + cost
					+ " drams}}. No rite was held.";
				return false;
			}
			System.LastRiteTick = The.Game.TimeTicks;
			KingdomGovernanceScope.Commit("hold shared rite");
			System.Dissent = KingdomCreedRules.ApplyDissent(System.Dissent, -KingdomCreedRules.RiteEase(temper));
			string awayName = (System.Away != null) ? System.Away.SettlementName : null;
			KingdomChronicle.Record(System, KingdomCreedRules.RiteTelling(KingdomPresentation.Rich(System.SeatName), KingdomPresentation.Rich(awayName), cost));
			Popup.Show(KingdomCreedRules.RiteNotice(Temper(System), KingdomPresentation.Rich(awayName)));
			Rearm(System);
			return true;
		}

		/// <summary>
		/// The creeds the founder may declare the realm's own: whatever its two cities hold. Never
		/// null; empty when neither city has a creed, in which case there is nothing to choose
		/// between and the Charter should not offer the choice.
		/// </summary>
		public static List<string> DeclarableCreeds(KingdomSystem System)
		{
			List<string> creeds = new List<string>();
			if (!Enabled || System == null || !System.Founded)
			{
				return creeds;
			}
			string here = SeatCreed(System);
			string there = AwayCreed(System);
			if (!string.IsNullOrEmpty(here))
			{
				creeds.Add(here);
			}
			if (!string.IsNullOrEmpty(there) && there != here)
			{
				creeds.Add(there);
			}
			return creeds;
		}

		/// <summary>
		/// Declares one creed the realm's own, or takes the declaration back.
		/// <para>
		/// Side effects: sets <c>KingdomSystem.DeclaredCreed</c>, which raises that creed's weight
		/// in every future arrival to both cities. When the other city holds a creed, that faction
		/// is slighted: the realm's standing with it falls by
		/// <see cref="KingdomCreedRules.DeclarationStandingCost"/> — mirrored into the world, so
		/// its people think less of the realm everywhere — and dissent rises immediately by
		/// <see cref="KingdomCreedRules.DeclarationShock"/>. Failure mode: returns false with a
		/// founder-facing reason and changes nothing.
		/// </para>
		/// <para>
		/// This is the fast lever and the expensive one. It does not ease the quarrel; it decides
		/// it, by making one city's creed the one arriving settlers carry until the other city's
		/// creed thins below dominance and the clash simply stops being true.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="CreedFactionName">A creed from <see cref="DeclarableCreeds"/>, or null/empty
		/// to recant.</param>
		/// <param name="Failure">Founder-facing reason, or empty on success.</param>
		public static bool Declare(KingdomSystem System, string CreedFactionName, out string Failure)
		{
			Failure = "";
			if (!Enabled || System == null || !System.Founded)
			{
				Failure = "There is no realm to declare anything about.";
				return false;
			}
			if (string.IsNullOrEmpty(CreedFactionName))
			{
				if (string.IsNullOrEmpty(System.DeclaredCreed))
				{
					Failure = "The realm has said nothing about itself. There is nothing to unsay.";
					return false;
				}
				System.DeclaredCreed = null;
				KingdomGovernanceScope.Commit("recant creed");
				KingdomChronicle.Record(System, KingdomCreedRules.RecantTelling(KingdomPresentation.Rich(System.KingdomDisplayName)));
				Popup.Show("You unsay it. The roads go back to carrying whoever they were carrying before, and nobody is owed an explanation.");
				return true;
			}
			List<string> declarable = DeclarableCreeds(System);
			if (!declarable.Contains(CreedFactionName))
			{
				Failure = "A realm declares for what its own people already hold. Nobody here holds with " + CreedName(CreedFactionName) + ".";
				return false;
			}
			if (CreedFactionName == System.DeclaredCreed)
			{
				Failure = "You have said it already, and saying it twice is not louder.";
				return false;
			}
			string slighted = null;
			for (int i = 0; i < declarable.Count; i++)
			{
				if (declarable[i] != CreedFactionName)
				{
					slighted = declarable[i];
				}
			}
			System.DeclaredCreed = CreedFactionName;
			KingdomGovernanceScope.Commit("declare creed");
			if (!string.IsNullOrEmpty(slighted))
			{
				System.AdjustStanding(slighted, KingdomCreedRules.DeclarationStandingCost);
				string provocation = KingdomLifecycleRules.ChildId(System.LifecycleBook.SettlementId,
					"creed-declaration-" + The.Game.TimeTicks + "-" + slighted + "-" + CreedFactionName, 0);
				KingdomRaids.RecordProvocation(System, slighted, "creed-declaration-slight",
					provocation, KingdomPresentation.Rich(System.KingdomDisplayName) + " publicly declared for "
						+ CreedName(CreedFactionName) + " over " + CreedName(slighted),
					The.Player?.CurrentZone?.ZoneID, 1);
				System.Dissent = KingdomCreedRules.ApplyDissent(System.Dissent, KingdomCreedRules.DeclarationShock);
			}
			KingdomChronicle.Record(System, KingdomCreedRules.DeclarationTelling(KingdomPresentation.Rich(System.KingdomDisplayName), CreedName(CreedFactionName)));
			Popup.Show(KingdomCreedRules.DeclarationNotice(CreedName(CreedFactionName), CreedName(slighted)));
			return true;
		}
	}
}
