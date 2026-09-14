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
				for (int i = 0; i < 5; i++)
				{
					var work = Create("r_KingdomAirWellField");
					RequireChainStoreId(work);
					var producer = work.GetPart<LiquidProducer>();
					var liquid = work.GetPart<LiquidVolume>();
					Require(producer != null && producer.VariableRate == "32-64"
						&& producer.FillSelfOnly && liquid != null && liquid.Volume == 0,
						"synthetic producer does not retain its real water-production contract");
					work.SetIntProperty(KingdomUpgrade.BuiltProperty, 1);
					work.SetStringProperty(KingdomUpgrade.BuildKeyProperty, "airwellfield");
					work.SetIntProperty("KingdomStores", 1);
					work.RequirePart<r_KingdomImprovement>().Held = true;
					var cell = ChainSupplyCell();
					Require(ReferenceEquals(cell.AddObject(work, NoStack: true), work)
						&& work.CurrentCell == cell, "synthetic producer lost exact placement");
					ChainProducers.Add(work);
				}
			}

			private void RequireChainWaterSupport(KingdomSurvey Survey)
			{
				Require(ChainProducers.Count == 5, "synthetic producer roster changed");
				foreach (var work in ChainProducers)
					Require(GameObject.Validate(work) && work.CurrentZone == Zone
						&& Survey.Built.Contains(work) && KingdomUpgrade.IsFunctionallyBuilt(work)
						&& work.GetPart<LiquidProducer>()?.VariableRate == "32-64"
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
