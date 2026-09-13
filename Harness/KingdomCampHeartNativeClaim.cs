using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// PR #107 native case 6, as far as the production plot-claim path actually reaches: a REAL
	/// commission is put to the settlement while the camp heart stands, and the heart's own ground
	/// is proved untakeable.
	/// <para>
	/// NO STOCKPILE-SPECIFIC REASON IS CLAIMED. #107's wording, "New plot touching heart rect
	/// refuses for stockpile reason", is NOT asserted here, because production records no such
	/// reason on this path: <c>KingdomPlots.TryFindRect</c> drops a rect that crowds an existing
	/// plot with a bare <c>continue</c> and no recorded reason
	/// (<c>Growth/KingdomPlot2.08.Siting.cs:135</c>) BEFORE the ground is ever read at <c>:136</c>,
	/// and the founder-facing answer when nothing fits is <c>KingdomPlotRules.RefuseRoom</c>
	/// (<c>Growth/KingdomPlotRefusalRules.cs:40</c>, emitted at
	/// <c>Growth/KingdomPlot2.08.Siting.cs:162</c>). What IS asserted is what production really
	/// does: the commission never takes the heart's reserved ground, the production crowding
	/// predicate refuses every rect that would cover the camp store's own cell, and the store and
	/// fire are untouched by the attempt.
	/// </para>
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		internal const string ClaimKey = "tent";

		private sealed partial class Frame
		{
			internal string ClaimOutcome;

			/// <summary>Puts one REAL commission to the settlement and proves the heart's ground
			/// was not taken, whichever way production answered.</summary>
			internal void RequireHeartGroundNeverTaken(GameObject Standing)
			{
				KingdomPlotRules.PlotRect heart;
				Require(KingdomPlots.TryReadRect(Standing, out heart),
					"taf-camp-claim-noheartrect: the standing heart's rect could not be read");
				RequireStoreCellIsCrowdedOut(heart);
				KingdomRules.BuildEntry entry = null;
				Require(KingdomData.TryGetBuilding(ClaimKey, out entry) && entry != null,
					"taf-camp-claim-nodesign: the catalogue has no '" + ClaimKey + "' design");
				List<KingdomPlotRules.PlotRect> before = KingdomPlots.ReadPlots(Zone);
				string failure;
				bool commissioned = KingdomPlots.Commission(System, Zone, entry, null,
					out failure);
				List<KingdomPlotRules.PlotRect> after = KingdomPlots.ReadPlots(Zone);
				if (commissioned)
				{
					Require(after.Count == before.Count + 1,
						"taf-camp-claim-unstaked: the commission reported success but staked "
							+ (after.Count - before.Count) + " plot(s)");
					KingdomPlotRules.PlotRect laid = Added(before, after);
					Require(!KingdomPlotRules.Overlaps(laid,
						KingdomPlotRules.Reserved(heart)),
						"taf-camp-claim-took-heart-ground: the commissioned plot lies on the "
							+ "heart's reserved rect");
					ClaimOutcome = "staked-clear-of-the-heart";
				}
				else
				{
					Require(!string.IsNullOrEmpty(failure),
						"taf-camp-claim-silent: the commission refused without a reason");
					Require(after.Count == before.Count,
						"taf-camp-claim-staked-on-refusal: a refused commission staked ground");
					// Recorded verbatim, and NOT required to name the stockpile: production
					// records no stockpile reason on this path.
					ClaimOutcome = "refused: " + KingdomScenarioRules.Bounded(failure);
				}
				RequireStoreIdentity();
				Require(GameObject.Validate(Fire) && ReferenceEquals(Fire.CurrentCell, FireCell),
					"taf-camp-claim-fire-disturbed: the commission moved the camp fire");
			}

			/// <summary>The PRODUCTION crowding predicate, asked about the exact rect that would
			/// cover the camp store's own cell. This is the rule that keeps a new plot off the
			/// heart's ground, and it is the rule that records no reason when it fires.</summary>
			private void RequireStoreCellIsCrowdedOut(KingdomPlotRules.PlotRect Heart)
			{
				Require(StoreCell != null,
					"taf-camp-claim-nostorecell: the camp store has no cell");
				KingdomPlotRules.PlotRect overStore = new KingdomPlotRules.PlotRect(
					StoreCell.X, StoreCell.Y, StoreCell.X, StoreCell.Y);
				Require(KingdomPlotRules.Overlaps(overStore, Heart),
					"taf-camp-claim-store-outside-heart: the camp store does not stand inside the "
						+ "heart's own rect");
				List<KingdomPlotRules.PlotRect> standing =
					new List<KingdomPlotRules.PlotRect> { Heart };
				Require(KingdomPlotRules.CrowdsExisting(overStore, standing),
					"taf-camp-claim-store-not-crowded: production would let a plot take the cell "
						+ "the camp store stands on");
			}

			/// <summary>The one rect present after the commission that was not present before.
			/// </summary>
			private KingdomPlotRules.PlotRect Added(List<KingdomPlotRules.PlotRect> Before,
				List<KingdomPlotRules.PlotRect> After)
			{
				for (int i = 0; i < After.Count; i++)
				{
					bool known = false;
					for (int j = 0; j < Before.Count; j++)
						if (Same(After[i], Before[j])) known = true;
					if (!known) return After[i];
				}
				Require(false, "taf-camp-claim-noaddedrect: no newly staked rect was found");
				return default(KingdomPlotRules.PlotRect);
			}

			private static bool Same(KingdomPlotRules.PlotRect A, KingdomPlotRules.PlotRect B)
			{
				return A.X1 == B.X1 && A.Y1 == B.Y1 && A.X2 == B.X2 && A.Y2 == B.Y2;
			}
		}
	}
}
