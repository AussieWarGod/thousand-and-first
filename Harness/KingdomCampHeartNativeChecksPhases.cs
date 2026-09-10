using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>Phase dispatch for <see cref="KingdomCampHeartNativeChecks"/>. Every phase reads
	/// what the REAL settlement pass did on the turns the persona's own <c>advance</c> spends:
	/// the pass assesses the heart, begins the improvement, commits the water and material debit,
	/// burns the labour, and hands the rung over. None of that is called from here.</summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			/// <summary>The exact custody the upgrade must not disturb, read before a single turn
			/// is spent: per unit, its identity, blueprint, exact holder and raw stack count.
			/// </summary>
			internal void RecordBefore()
			{
				int riteX, riteY;
				Require(KingdomPlots.TryRiteGround(Zone, out riteX, out riteY),
					"taf-camp-rite-absent: the founded camp has no provable rite ground");
				RiteX = riteX;
				RiteY = riteY;
				KingdomSurvey survey = Census();
				BeforeWater = survey.StoredWater;
				List<GameObject> bodies;
				List<KingdomCampHeartNativeCensus.Unit> units = ContentUnits(out bodies);
				RetainedBrush = Select(units, MintedBrush);
				RetainedBrushBodies = SelectBodies(bodies, MintedBrush);
				Require(RetainedBrush.Count == MintedBrushUnits,
					"taf-camp-store-retained-missing: the store does not hold all "
						+ MintedBrushUnits + " unasked units before the upgrade");
				Evidence.Append("\nbefore tick=").Append(Game.TimeTicks)
					.Append("; population=").Append(System.Population)
					.Append("; stage=").Append(System.Stage)
					.Append("; rung=").Append(KingdomPlots.HeartRung(Zone))
					.Append("; heart=").Append(HeartId)
					.Append("; store=").Append(StoreId)
					.Append('@').Append(Offset(StoreCell))
					.Append("; store raw custody census=")
					.Append(KingdomCampHeartNativeCensus.Describe(units))
					.Append("; retained unasked units=")
					.Append(KingdomCampHeartNativeCensus.Describe(RetainedBrush))
					.Append("; fire=").Append(FireId).Append('@').Append(Offset(FireCell))
					.Append("; stored water=").Append(BeforeWater);
			}

			internal void Check()
			{
				Require(Phase >= 1 && !Done, "taf-camp-setup-absent: setup did not run");
				Require(!KingdomScenarioAdvance.Pending,
					"taf-camp-turns-owed: turns are still owed");
				switch (Phase)
				{
					case 1: Phase1(); break;
					case 2: Phase2(); Done = true; Armed = false; break;
					default:
						Require(false, "taf-camp-phase-overrun: check ran past its final phase");
						break;
				}
			}

			/// <summary>The real pass began and FUNDED the rung-2 improvement: the production
			/// construction claim records the authored water and material as committed, the exact
			/// bill units are absent from the dedicated store, and every unit the bill did not
			/// ask for is still there unchanged, in the same holder, at the same raw count.
			/// </summary>
			private void Phase1()
			{
				Require(System.Stage >= GrowthStage.Steading,
					"taf-camp-stage-short: the settlement never reached the stage the waterstone "
						+ "asks for; stage=" + System.Stage);
				r_KingdomImprovement improvement = GameObject.Validate(Heart)
					? Heart.GetPart<r_KingdomImprovement>() : null;
				Begun = improvement != null && improvement.Working;
				Require(Begun, "taf-camp-improvement-absent: the real settlement pass never began "
					+ "the heart's rung-2 improvement");
				RequireBillDebited();
				List<GameObject> bodies;
				List<KingdomCampHeartNativeCensus.Unit> present = ContentUnits(out bodies);
				RequireAbsent(MintedStone, present, "stone");
				RequireAbsent(MintedTimber, present, "timber");
				RequireHeld(RetainedBrush, present, "brush");
				RequireSameBodies(RetainedBrushBodies, bodies, "brush");
				RequireStoreIdentity();
				KingdomSurvey survey = Census();
				Phase = 2;
				Evidence.Append("\nphase1 tick=").Append(Game.TimeTicks)
					.Append("; stage=").Append(System.Stage)
					.Append("; improvement working=").Append(Begun)
					.Append("; job=").Append(JobId)
					.Append("; committed water debit=").Append(BilledWater)
					.Append("; authored water cost=").Append(AuthoredWaterCost)
					.Append("; authored material bill=").Append(AuthoredMaterial)
					.Append("; committed material debit=")
					.Append(KingdomScenarioRules.Bounded(BilledMaterial))
					.Append("; store raw custody census=")
					.Append(KingdomCampHeartNativeCensus.Describe(present))
					.Append("; stored water=").Append(survey.StoredWater);
			}

			/// <summary>The rung stands. The SAME store object, in the SAME cell, still holding
			/// the same physical units the bill never asked for - same identity, same holder,
			/// same raw count - and the fire re-laid on the same rite-relative cell.</summary>
			private void Phase2()
			{
				GameObject standing = StandingHeart();
				Require(KingdomUpgrade.DesignKeyOf(standing) == SecondRungKey,
					"taf-camp-rung-unfinished: the heart did not finish its real paid climb to "
						+ "the waterstone; key=" + KingdomUpgrade.DesignKeyOf(standing));
				Require(!ReferenceEquals(standing, Heart) && standing.IDIfAssigned != HeartId,
					"taf-camp-successor-is-predecessor: no rung was raised");
				GameObject store;
				string failure;
				Require(KingdomArchitectureStamper.TryExactAnchoredComponent(standing, Zone,
					StorageRole, out store, out failure),
					failure ?? "taf-camp-store-unanchored: the raised rung has no anchored store");
				Require(ReferenceEquals(store, Store) && store.IDIfAssigned == StoreId,
					"taf-camp-store-replaced: the raised rung published a different store object: "
						+ StoreId + " -> " + store.IDIfAssigned);
				RequireStoreIdentity();
				List<GameObject> bodies;
				List<KingdomCampHeartNativeCensus.Unit> present = ContentUnits(out bodies);
				RequireHeld(RetainedBrush, present, "brush");
				RequireSameBodies(RetainedBrushBodies, bodies, "brush");
				RequireAbsent(MintedStone, present, "stone");
				RequireAbsent(MintedTimber, present, "timber");
				GameObject fire = FireIn(standing);
				Require(fire != null,
					"taf-camp-fire-absent: no camp fire stands inside the raised rung");
				Require(Offset(fire.CurrentCell) == Offset(FireCell),
					"taf-camp-fire-moved: the camp fire left its rite-relative cell: "
						+ Offset(FireCell) + " -> " + Offset(fire.CurrentCell));
				RequireHeartGroundNeverTaken(standing);
				Evidence.Append("\nphase2 tick=").Append(Game.TimeTicks)
					.Append("; standing=").Append(standing.IDIfAssigned)
					.Append("; key=").Append(KingdomUpgrade.DesignKeyOf(standing))
					.Append("; store=").Append(store.IDIfAssigned)
					.Append('@').Append(Offset(store.CurrentCell))
					.Append("; store raw custody census=")
					.Append(KingdomCampHeartNativeCensus.Describe(present))
					.Append("; retained unasked units=")
					.Append(KingdomCampHeartNativeCensus.Describe(RetainedBrush))
					.Append("; fire=").Append(fire.IDIfAssigned)
					.Append('@').Append(Offset(fire.CurrentCell))
					.Append("; zone rung read=").Append(KingdomPlots.HeartRung(Zone))
					.Append("; basin capacity read=").Append(BasinCapacity(standing))
					.Append("; real commission outcome=").Append(ClaimOutcome)
					.Append("; stockpile-reason-claimed=false");
			}

			/// <summary>The bodies in Present whose identity appears in Wanted, in Wanted's own
			/// order, refusing if any is absent. Retained BY REFERENCE, so a later replacement
			/// that reused the identity string cannot pass.</summary>
			private List<GameObject> SelectBodies(List<GameObject> Present, List<string> Wanted)
			{
				List<GameObject> chosen = new List<GameObject>();
				for (int i = 0; i < Wanted.Count; i++)
				{
					GameObject found = null;
					for (int j = 0; j < Present.Count; j++)
						if (Present[j] != null && Present[j].IDIfAssigned == Wanted[i])
							found = Present[j];
					Require(found != null, "taf-camp-store-unit-missing:" + Wanted[i]);
					chosen.Add(found);
				}
				return chosen;
			}

			/// <summary>The units in Present whose identity appears in Wanted, in Wanted's own
			/// order, refusing if any is absent.</summary>
			private List<KingdomCampHeartNativeCensus.Unit> Select(
				List<KingdomCampHeartNativeCensus.Unit> Present, List<string> Wanted)
			{
				List<KingdomCampHeartNativeCensus.Unit> chosen =
					new List<KingdomCampHeartNativeCensus.Unit>();
				for (int i = 0; i < Wanted.Count; i++)
				{
					KingdomCampHeartNativeCensus.Unit found = null;
					for (int j = 0; j < Present.Count; j++)
						if (Present[j] != null && Present[j].Id == Wanted[i]) found = Present[j];
					Require(found != null, "taf-camp-store-unit-missing:" + Wanted[i]);
					chosen.Add(found);
				}
				return chosen;
			}

			/// <summary>A cell as its rite-relative offset, which is the coordinate the authored
			/// camp promises stays fixed as the rungs grow around it.</summary>
			internal string Offset(Cell Cell)
			{
				return Cell == null ? "(none)"
					: "(" + (Cell.X - RiteX) + "," + (Cell.Y - RiteY) + ")";
			}

			/// <summary>The camp fire physically inside a heart root's own rect.</summary>
			internal GameObject FireIn(GameObject Root)
			{
				KingdomPlotRules.PlotRect rect;
				Require(KingdomPlots.TryReadRect(Root, out rect),
					"taf-camp-rect-unreadable: the standing heart's rect could not be read");
				GameObject found = null;
				for (int y = rect.Y1; y <= rect.Y2; y++)
					for (int x = rect.X1; x <= rect.X2; x++)
					{
						Cell cell = Zone.GetCell(x, y);
						if (cell == null) continue;
						foreach (GameObject item in cell.GetObjects())
							if (GameObject.Validate(item) && item.Blueprint == FireBlueprint)
							{
								Require(found == null, "taf-camp-fire-ambiguous: more than one "
									+ "camp fire stands in the heart");
								found = item;
							}
					}
				return found;
			}

			/// <summary>Reported, never asserted: what the first basin's capacity reads after the
			/// climb. Recorded so the journal carries the measurement instead of a claim.
			/// </summary>
			internal string BasinCapacity(GameObject Root)
			{
				GameObject basin;
				if (!KingdomArchitectureStamper.TryExactAnchoredComponent(Root, Zone,
					KingdomPlots.HeartBasinRole, out basin, out _)
					|| !GameObject.Validate(basin)) return "unresolved";
				LiquidVolume liquid = basin.GetPart<LiquidVolume>();
				return liquid == null ? "no-volume"
					: liquid.MaxVolume.ToString(
						global::System.Globalization.CultureInfo.InvariantCulture);
			}
		}
	}
}
