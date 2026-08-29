using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomWaterRite
	{
		// ==================================================================================
		// The facts, gathered off real people and real buildings
		// ==================================================================================

		// Everything one row of the Charter's list needs, decided once so the label, the refusal
		// line and the rite itself can never disagree about the same person.
		private sealed class RiteOffer
		{
			public WaterRiteBar Bar;

			public int Drams;

			public WaterRiteFacts Facts;

			public string ShrineCreed;
		}

		private static RiteOffer OfferFor(KingdomSystem System, Zone Z, GameObject Resident, string RealmCreed, int Stored)
		{
			RiteOffer offer = new RiteOffer();
			string shrineCreed;
			offer.Facts = FactsFor(Z, Resident, RealmCreed, out shrineCreed);
			offer.ShrineCreed = shrineCreed;
			offer.Drams = KingdomWaterRiteRules.Cost(KingdomWaterRiteRules.Distance(offer.Facts));
			offer.Bar = BarFor(System, Resident, offer.Facts, offer.Drams, Stored);
			return offer;
		}

		private static WaterRiteFacts FactsFor(Zone Z, GameObject Resident, string RealmCreed, out string ShrineCreed)
		{
			string theirs = Resident.GetStringProperty(KingdomCreed.CreedProperty);
			QolProfile profile = KingdomQol.ProfileOf(Resident);
			// One vocabulary, and no new tag: a creature whose Refuses names the faith tag will not
			// have belief put to them by anybody, exactly as an authored Refuses is absolute at
			// every closeness rung; one whose Prefers names it is somebody for whom belief is a
			// thing they think about, and so not a thing they trade over a bowl. A mod ships an
			// unconvertible zealot by writing r_TAF_Refuses="taf:faith" on a blueprint, and needs
			// nothing from this file to do it.
			string faith = KingdomCeremonyRules.CategoryTag("faith");
			ShrineCreed = RivalShrineNear(Z, Resident, RealmCreed);
			return new WaterRiteFacts(
				KingdomCreed.HostilityBetween(theirs, RealmCreed),
				SharedDaysOf(Resident),
				!string.IsNullOrEmpty(theirs),
				!string.IsNullOrEmpty(ShrineCreed),
				KingdomQolRules.Has(profile.Prefers, faith),
				KingdomQolRules.Has(profile.Refuses, faith),
				RealmCreed);
		}

		private static WaterRiteBar BarFor(KingdomSystem System, GameObject Resident, WaterRiteFacts Facts, int Drams, int Stored)
		{
			if (KingdomWaterRiteRules.SameCreed(Resident.GetStringProperty(KingdomCreed.CreedProperty), Facts.RealmCreed))
			{
				return WaterRiteBar.NothingBetweenYou;
			}
			if (!CouldWalkAway(System, Resident))
			{
				return WaterRiteBar.NoRoadOut;
			}
			string closed = Resident.GetStringProperty(AskedTooOftenCreedProperty);
			if (!string.IsNullOrEmpty(closed) && KingdomWaterRiteRules.SameCreed(closed, Facts.RealmCreed))
			{
				return WaterRiteBar.AskedTooOften;
			}
			WaterRiteStamp stamp;
			if (TryReadStamp(Resident, out stamp) && !KingdomWaterRiteRules.SomethingChanged(stamp, Facts))
			{
				return WaterRiteBar.AlreadyAnswered;
			}
			// The same cadence, from the same constant, as the rite of shared water between two
			// cities: one definition of "you poured too recently", never two that can drift.
			if (!KingdomCreedRules.RiteReady(System.LastSoulRiteTick, (The.Game != null) ? The.Game.TimeTicks : 0L))
			{
				return WaterRiteBar.PouredTooRecently;
			}
			WaterRiteBar baseline = (Stored < Drams)
				? WaterRiteBar.StoresCannotBear : WaterRiteBar.Ready;
			int residentId = Simulation.City.KingdomResidents.IdOf(Resident);
			return KingdomWaterRiteRules.PreserveEligibilityAcrossCivicTitle(baseline,
				residentId > 0 && residentId == System.OfficeHolderResidentId);
		}

		// A yes from somebody with nowhere to go is not a yes, so the rite is only ever put to
		// somebody the settlement's own emigration machinery would actually take: one of its own
		// arrivals, and not the last of the loyal core. KingdomGrowth.Emigrate's own conditions,
		// asked before the question rather than discovered after it.
		private static bool CouldWalkAway(KingdomSystem System, GameObject Resident)
		{
			if (Resident.GetIntProperty("KingdomBorn") != 1 || Resident.IsPlayer() || Resident.IsPlayerLed())
			{
				return false;
			}
			return System.Population > KingdomRules.LoyalCoreSettlers;
		}

		// A shrine consecrated to anything other than the realm's creed, standing within sight of
		// this settler's own door. Read off KingdomFaith's own property rather than a second name
		// for the same fact, so a settlement with the faith module switched off simply has no
		// object carrying it and this reads false.
		private static string RivalShrineNear(Zone Z, GameObject Resident, string RealmCreed)
		{
			Cell door = DoorOf(Z, Resident);
			if (Z == null || door == null)
			{
				return null;
			}
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1)
				{
					continue;
				}
				string consecrated = item.GetStringProperty(KingdomFaith.ShrineCreedProperty);
				if (string.IsNullOrEmpty(consecrated) || KingdomWaterRiteRules.SameCreed(consecrated, RealmCreed))
				{
					continue;
				}
				Cell cell = item.CurrentCell;
				if (cell != null && KingdomWaterRiteRules.WithinQuarter(cell.X - door.X, cell.Y - door.Y))
				{
					return consecrated;
				}
			}
			return null;
		}

		// Their own door, which is the only reading of "their quarter" the code can honestly make
		// (Addendum 4d: quarters emerge from the layout grammar and no code knows the word). A
		// settler with no home is judged from where they are standing, which for somebody sleeping
		// in the open is the same thing.
		private static Cell DoorOf(Zone Z, GameObject Resident)
		{
			if (Z == null || Resident == null)
			{
				return null;
			}
			string plotId = Resident.GetStringProperty(KingdomLodging.HomePlotIdProperty);
			if (!string.IsNullOrEmpty(plotId))
			{
				foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
				{
					if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1 && item.GetStringProperty(KingdomPlots.PlotIdProperty) == plotId)
					{
						Cell home = item.CurrentCell;
						if (home != null)
						{
							return home;
						}
					}
				}
			}
			return Resident.CurrentCell;
		}

		private static string RealmCreed(KingdomSystem System)
		{
			return string.IsNullOrEmpty(System.DeclaredCreed) ? KingdomCreed.SeatCreed(System) : System.DeclaredCreed;
		}

	}
}
