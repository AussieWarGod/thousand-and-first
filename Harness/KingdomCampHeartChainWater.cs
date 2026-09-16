using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private readonly List<GameObject> ChainProducers = new List<GameObject>();

			private void SeedChainWaterSupport()
			{
				// Standalone legacy producer roots are synthetic prerequisites, not evidence that
				// authored waterworks can be commissioned. Their real LiquidProducer remains active.
				foreach (var rect in KingdomCampHeartChainGrid.WaterCourts())
				{
					var work = Create("r_KingdomAirWellCourt");
					RequireChainStoreId(work);
					var producer = work.GetPart<LiquidProducer>();
					var liquid = work.GetPart<LiquidVolume>();
					Require(producer != null && producer.VariableRate == "60-100"
						&& producer.FillSelfOnly && liquid != null && liquid.Volume == 0,
						"synthetic producer does not retain its real water-production contract");
					work.SetIntProperty(KingdomUpgrade.BuiltProperty, 1);
					work.SetStringProperty(KingdomUpgrade.BuildKeyProperty, "airwellcourt");
					work.SetIntProperty("KingdomStores", 1);
					work.RequirePart<r_KingdomImprovement>().Held = true;
					Require(KingdomInheritanceSpatialRules.TryLegacyRect("airwellcourt",
						rect.X1 + 3, rect.Y1 + 2, out var legacy)
						&& legacy.X1 == rect.X1 && legacy.Y1 == rect.Y1
						&& legacy.X2 == rect.X2 && legacy.Y2 == rect.Y2,
						"producer reservation differs from its persisted legacy footprint");
					var cell = Zone.GetCell(rect.X1 + 3, rect.Y1 + 2);
					Require(cell != null && cell.IsEmpty() && cell.IsPassable()
						&& !cell.HasOpenLiquidVolume(), "reserved producer root cell is occupied");
					var point = new KingdomPlotRules.PlotRect(cell.X, cell.Y, cell.X, cell.Y);
					var plots = KingdomPlots.ReadPlots(Zone);
					Require(KingdomPlots.TryHeartRectFor(Zone, 4, out var heart), "future heart absent");
					plots.Add(heart);
					foreach (var plot in plots)
						Require(KingdomCampHeartChainGrid.ClearsPaidApproach(point, plot),
							"reserved producer root blocks a paid approach");
					Require(ReferenceEquals(cell.AddObject(work, NoStack: true), work)
						&& work.CurrentCell == cell, "synthetic producer lost exact placement");
					ChainProducers.Add(work);
				}
			}

			private void RequireChainSpatialPreflight()
			{
				// The ordinary check-out must have recorded every new fixture work. Otherwise a
				// capture of the old smaller book could hide the producer footprint being tested.
				foreach (var work in ChainProducers)
					Require(System.City.WorkIds.Contains(Simulation.City.KingdomCityRules.StableId(work.IDIfAssigned)),
						"city book has not observed a water court before spatial preflight");
				foreach (var work in ChainHomes)
					Require(System.City.WorkIds.Contains(Simulation.City.KingdomCityRules.StableId(work.IDIfAssigned)),
						"city book has not observed a home before spatial preflight");
				var seal = Game.GetSystem<KingdomSeal>();
				Require(seal != null, "city fixture has no seal authority");
				bool ok = seal.NativeSpatialCapturePreflight(out string result, out string failure);
				Require(KingdomScenarioJournal.Append("camp-heart-chain-spatial", ok, result) == null,
					"spatial preflight could not be journalled");
				Require(ok, "city fixture spatial preflight refused: " + failure);
			}

			private void RequireChainWaterSupport(KingdomSurvey Survey)
			{
				Require(ChainProducers.Count == 8, "synthetic producer roster changed");
				foreach (var work in ChainProducers)
					Require(GameObject.Validate(work) && work.CurrentZone == Zone
						&& Survey.Built.Contains(work) && KingdomUpgrade.IsFunctionallyBuilt(work)
						&& work.GetPart<LiquidProducer>()?.VariableRate == "60-100"
						&& work.GetPart<LiquidProducer>().FillSelfOnly,
						"synthetic civic water producer disappeared or changed");
				bool wasBound = KingdomSurvey.HasBoundPass;
				Require(KingdomSurvey.TryBindLocalOperation(Zone, System, out var scope,
					out string failure), failure);
				using (scope)
				{
					var support = KingdomSubsidence.ScopedSupports(System, Zone, KingdomSurvey.ActiveFor(Zone));
					int level = KingdomSubsidenceRules.SupportedLevel(support, GrowthStage.City, System.Shade);
					Require(level >= 50, "city fixture cannot sustain fifty residents: level=" + level
						+ "; water-support=" + support.Water + "; roof-support=" + support.Roof);
				}
				Require(KingdomSurvey.HasBoundPass == wasBound, "support preflight changed its caller survey lifetime");
			}
		}
	}
}
