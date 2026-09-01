using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomLodging
	{
		public static string HomeDesignKeyOf(Zone Z, GameObject Resident)
		{
			if (!TryBenefitIndex(Z, null, out KingdomBenefitIndex benefits,
				out string failure))
			{
				LogBenefitFailure(Z, "home-key reading", failure);
				return null;
			}
			GameObject home = HomeOf(Z, Resident, benefits);
			return home == null ? null : HomeBuildingKey(home, benefits);
		}

		/// <summary>
		/// The closeness rung of the home this resident sleeps in &mdash; the design's declared
		/// <c>Closeness</c>, or the arithmetic on its beds against the ground its tier stands on.
		/// For a caller that needs to know how much of a difference a roof will hold: Addendum 5's
		/// osmosis scales on exactly this ladder, and it is the same ladder the cohabitation gate
		/// already reads, so the roof that will house a grudge and the roof that will cross one can
		/// never disagree.
		/// </summary>
		/// <param name="Z">The zone. Null reads as no home.</param>
		/// <param name="Resident">The resident. Null, and a resident with no assigned home, both
		/// read as <see cref="KingdomLodgingRules.Closeness.Packed"/> &mdash; the tightest rung,
		/// which is also the rung that converts nobody, so a missing answer can never accelerate
		/// anything.</param>
		public static KingdomLodgingRules.Closeness QuartersOf(Zone Z, GameObject Resident)
		{
			if (!TryBenefitIndex(Z, null, out KingdomBenefitIndex benefits,
				out string failure))
			{
				LogBenefitFailure(Z, "quarters reading", failure);
				return KingdomLodgingRules.Closeness.Packed;
			}
			GameObject home = HomeOf(Z, Resident, benefits);
			return home == null ? KingdomLodgingRules.Closeness.Packed
				: QuartersOf(home, benefits);
		}

		// --- The pass itself -------------------------------------------------------------

		// Returns the occupancy map it settled on, or null when the module has nothing to do here.
		// RunBrink is false for the arrival gate, which asks the same question without charging
		// anybody a pass of the grace Addendum 4b gives them.
		private static Dictionary<string, List<GameObject>> Settle(KingdomSystem System, Zone Z,
			bool RunBrink, KingdomSurvey Survey = null)
		{
			if (!Enabled || System == null || !System.Founded || Z == null)
			{
				return null;
			}
			List<GameObject> residents = ResidentsIn(Z);
			if (residents.Count == 0)
			{
				// Nobody to settle, and an EMPTY map rather than none at all: a settlement with no
				// citizens standing in it still has housing, or has none, and the arrival gate has
				// to be able to tell those two apart. A camp with no roof yet takes nobody, which
				// is the rule that shipped before this module and is unchanged by it.
				return new Dictionary<string, List<GameObject>>();
			}
			if (!TryBenefitIndex(Z, Survey, out KingdomBenefitIndex benefits,
				out string failure))
			{
				LogBenefitFailure(Z, "settlement pass", failure);
				return null;
			}
			List<GameObject> homes = HousingIn(Z, benefits);
			Dictionary<string, GameObject> homeByPlot = new Dictionary<string, GameObject>();
			for (int i = 0; i < homes.Count; i++)
			{
				string plotId = homes[i].GetStringProperty(KingdomPlots.PlotIdProperty);
				if (!string.IsNullOrEmpty(plotId) && !homeByPlot.ContainsKey(plotId))
				{
					homeByPlot[plotId] = homes[i];
				}
			}
			Dictionary<string, List<GameObject>> occupancy = new Dictionary<string, List<GameObject>>();
			List<GameObject> unassigned = new List<GameObject>();
			for (int i = 0; i < residents.Count; i++)
			{
				GameObject resident = residents[i];
				string plotId = resident.GetStringProperty(HomePlotIdProperty);
				if (!string.IsNullOrEmpty(plotId) && homeByPlot.TryGetValue(plotId,
					out GameObject assignedHome) && !IsCondemned(assignedHome))
				{
					AddOccupant(occupancy, plotId, resident);
					continue;
				}
				if (!string.IsNullOrEmpty(plotId))
				{
					// The plot they were assigned to is gone (struck, or never built after a
					// save from an older version). Something changed, so the stale pointer is
					// cleared rather than left dangling, and they are reconsidered below exactly
					// like a resident who never had a home.
					resident.SetStringProperty(HomePlotIdProperty, null);
					// Whoever they were living beside, they are not living beside them now, so
					// the cohabitation clock osmosis reads restarts here rather than crediting a
					// household that has stopped existing.
					KingdomConversion.ForgetCohabitation(resident);
				}
				unassigned.Add(resident);
			}
			for (int i = 0; i < unassigned.Count; i++)
			{
				AssignOne(System, Z, unassigned[i], homes, occupancy, benefits, RunBrink);
			}
			return occupancy;
		}

		private static void AssignOne(KingdomSystem System, Zone Z, GameObject Resident,
			List<GameObject> Homes, Dictionary<string, List<GameObject>> Occupancy,
			KingdomBenefitIndex Benefits, bool RunBrink)
		{
			GameObject winningHome;
			KingdomLodgingRules.UnhousedReason reason;
			KingdomLodgingRules.Closeness roomiestRefused;
			List<string> needs;
			string winningPlotId = ChooseHome(Z, Resident, Homes, Occupancy, Benefits,
				out winningHome, out reason, out roomiestRefused, out needs);
			string residentName = NameOf(Resident);
			if (winningPlotId == null)
			{
				AnnounceUnhoused(System, Resident, residentName, reason, roomiestRefused);
				if (RunBrink)
				{
					RunRoofBrink(System, Z, Resident, RollNameOf(Resident));
				}
				return;
			}
			Resident.SetStringProperty(HomePlotIdProperty, winningPlotId);
			// A new roof is a new household, so the cohabitation clock starts from tonight. They
			// do not inherit the days they spent under somebody else's roof, or outside.
			KingdomConversion.ForgetCohabitation(Resident);
			bool wasUnhoused = Resident.GetIntProperty(UnhousedAnnouncedProperty) == 1;
			if (wasUnhoused)
			{
				Resident.SetIntProperty(UnhousedAnnouncedProperty, 0);
			}
			// Housed is housed: the window a settler was spending is not banked, halved or
			// remembered. The brink is lifted outright, and if they lose their home again they get
			// the whole of it back, because the founder is being asked to act on THIS loss. Rule 2
			// -- the pressure is a fact re-derived every pass, so taking it off takes it off -- and
			// the unsaying is owed as loudly as the warning was.
			bool wasWarned = KingdomBrink.Of(Resident, BrinkKind.Roof).Warned;
			if (KingdomBrink.Lift(Resident, BrinkKind.Roof) && wasWarned)
			{
				// Only what was actually said is unsaid. A brink pre-recorded at a condemnation
				// the founder has not been told about yet has no warning to withdraw, and
				// withdrawing one they never heard is noise in the one lane that must not have any.
				KingdomBrink.Unsay(System, BrinkKind.Roof, residentName, KingdomWord.StandsIn(Z), System.SeatName);
			}
			AddOccupant(Occupancy, winningPlotId, Resident);
			KingdomLabCivicRuntime.ObserveRehoused(System, Z, Resident, winningPlotId);
			if (wasUnhoused)
			{
					KingdomRules.BuildEntry winEntry;
					TryGetBuiltEntry(winningHome, Benefits, out winEntry);
					string matched = KingdomLodgingRules.MatchedTag(needs,
						new List<string>(HomeTags(winningHome, Benefits)));
				string line = KingdomPresentation.Rich(residentName) + " found shelter: "
					+ KingdomLodgingRules.HomeSuffix((winEntry != null) ? winEntry.Name : null,
						matched) + ".";
				KingdomChronicle.Record(System, line);
			}
		}

		/// <summary>Shared pure chooser for the real settlement pass and admission's projected
		/// occupancy. Ordinary residents hold an eligible fine house behind every other eligible
		/// roof without turning it into a hard vacancy lock; the durable legendary-trader marker
		/// retains the prior generic choice law. All mutation remains in <see cref="AssignOne"/>.</summary>
		private static string ChooseHome(Zone Z, GameObject Resident, List<GameObject> Homes,
			Dictionary<string, List<GameObject>> Occupancy, KingdomBenefitIndex Benefits,
			out GameObject WinningHome,
			out KingdomLodgingRules.UnhousedReason Reason,
			out KingdomLodgingRules.Closeness RoomiestRefused, out List<string> Needs)
		{
			WinningHome = null;
			Reason = KingdomLodgingRules.UnhousedReason.Housed;
			RoomiestRefused = KingdomLodgingRules.Closeness.Packed;
			QolProfile profile = KingdomQol.ProfileOf(Resident);
			Needs = new List<string>(profile.Needs);
			List<string> refuses = new List<string>(profile.Refuses);
			List<string> selfTags = SelfTagsOf(profile);
			string creed = Resident.GetStringProperty(KingdomCreed.CreedProperty);
			bool anyRoofAtAll = Homes.Count > 0;
			bool anyStanding = false;
			bool anyMeetsNeeds = false;
			bool anyHasCapacity = false;
			bool anyWithoutRefusal = false;
			List<KingdomLodgingRules.LodgingCandidate> eligible =
				new List<KingdomLodgingRules.LodgingCandidate>();
			List<GameObject> eligibleHomes = new List<GameObject>();
			List<bool> eligibleFineHouses = new List<bool>();
			for (int i = 0; i < Homes.Count; i++)
			{
				GameObject home = Homes[i];
				string plotId = home.GetStringProperty(KingdomPlots.PlotIdProperty);
				if (string.IsNullOrEmpty(plotId)
					|| !TryHomeReading(home, Benefits, out KingdomBenefitReading reading,
						out string exactPlot)
					|| !string.Equals(plotId, exactPlot, StringComparison.Ordinal)) continue;
				int capacity = RoofCapacity(home, Benefits);
				if (capacity <= 0 || IsCondemned(home)) continue;
				anyStanding = true;
				List<string> provides = new List<string>(HomeTags(home, Benefits));
				if (!KingdomLodgingRules.MeetsNeeds(Needs, provides)) continue;
				anyMeetsNeeds = true;
				if (KingdomLabCivicRuntime.RefusesHome(The.Game?.GetSystem<KingdomSystem>(),
					Z, Resident, home, out string labRefusal))
				{
					RoomiestRefused = KingdomLodgingRules.Roomier(RoomiestRefused,
						QuartersOf(home, Benefits));
					continue;
				}
				List<GameObject> occupants;
				Occupancy.TryGetValue(plotId, out occupants);
				int occupantCount = occupants == null ? 0 : occupants.Count;
				if (!KingdomLodgingRules.HasFreeBed(capacity, occupantCount)) continue;
				anyHasCapacity = true;
				KingdomLodgingRules.Closeness quarters = KingdomFaith.EducatedCloseness(
					Z, QuartersOf(home, Benefits), home);
				if (occupants != null && AnyOccupantConflicts(refuses, selfTags, creed,
					occupants, quarters))
				{
					RoomiestRefused = KingdomLodgingRules.Roomier(RoomiestRefused, quarters);
					continue;
				}
				anyWithoutRefusal = true;
				eligible.Add(new KingdomLodgingRules.LodgingCandidate(
					plotId, capacity, occupantCount));
				eligibleHomes.Add(home);
				eligibleFineHouses.Add(string.Equals(reading.Designation.BuildingKey, "finehouse",
					StringComparison.Ordinal));
			}
			bool luxuryResident = Resident != null && Resident.GetIntProperty(
				KingdomGuestbook.LegendaryTraderResidentProperty) == 1;
			int chosen = luxuryResident ? KingdomLodgingRules.ChooseIndex(eligible)
				: KingdomLodgingRules.ChooseOrdinaryIndex(eligible, eligibleFineHouses);
			if (chosen < 0)
			{
				Reason = KingdomLodgingRules.Diagnose(anyRoofAtAll, anyMeetsNeeds,
					anyHasCapacity, anyWithoutRefusal, anyStanding);
				return null;
			}
			WinningHome = eligibleHomes[chosen];
			return eligible[chosen].PlotId;
		}

		// --- Addendum 4b: the brink, the window, and the leaving ---------------------------

		// The roof instance of the shared brink (KingdomBrinkRules). Losing every acceptable home
		// is the irreversible line: it is RECORDED with the tick it happened and then nothing
		// accrues, so a founder away a thousand days and a founder away ten find a settler
		// standing in exactly the same place -- that half of the doctrine did not move.
		//
		// What moved (Addendum 10(a)) is everything after the record. The word is PUSHED the
		// moment the loss is seen, wherever the founder is, and it names the arrest. From that
		// delivery the settler has KingdomLodgingRules.GraceDays of WORLD TIME, not two visits. If
		// that time runs out with them still unroofed they go, attended or not, and the leaving is
		// dated to the tick the window actually ran out on rather than to the pass that found it.
		// Nothing here fires unwarned: an unwarned brink has no deadline at all, so the pass that
		// discovers a loss can only ever say so.
	}
}
