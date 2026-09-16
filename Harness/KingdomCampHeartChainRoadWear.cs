using System;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private GameObject ChainTrack;
			private Cell ChainTrackCell;
			private string ChainTrackId;

			private void ProveChainRoadWear()
			{
				long tick = Game.TimeTicks;
				int water = Census().StoredWater;
				Require(KingdomConstruction.TryRead(out var jobs, out string failure), failure);
				int jobCount = jobs.Count;
				Require(KingdomArchitectureRuntime.TryRead(ChainHeart, out var before, out failure), failure);
				Require(KingdomArchitectureRuntime.TryPrepareSuccessorForUpgrade(System, Zone, ChainHeart,
					before, ChainTo, out var successor, out failure), failure);
				var claim = new KingdomMaterialDebitCost(KingdomMaterials.UpgradeCostFor(ChainFrom));
				ChainTrackCell = ChainEnvelopeProbeCell(before, successor, false);
				Require(ChainTrackCell != null && KingdomRoads.FindOurFloor(ChainTrackCell, out _)
					== KingdomPhysicalLookupState.Absent, "road probe needs an unused annexed ground slot");
				RequireChainUpgradePreflight(successor, claim, true, null);
				string refusal = "plot-envelope growth would absorb public road ground at ";
				var foreign = Create("Floor");
				foreign.SetIntProperty(KingdomRoads.PathStateProperty, (int)KingdomRoadRules.WearState.Path);
				try
				{
					Require(ReferenceEquals(ChainTrackCell.AddObject(foreign, NoStack: true), foreign),
						"foreign road probe placement substituted its object");
					RequireChainUpgradePreflight(successor, claim, false, refusal);
				}
				finally { foreign.Obliterate(null, Silent: true); }
				foreach (var state in new[] { KingdomRoadRules.WearState.Worn,
					KingdomRoadRules.WearState.Trodden, KingdomRoadRules.WearState.Path })
				{
					Require(KingdomRoads.Lay(ChainTrackCell, state, null), "normal road-layer fixture refused");
					Require(KingdomRoads.FindOurFloor(ChainTrackCell, out ChainTrack)
						== KingdomPhysicalLookupState.Exact && KingdomRoads.IsExactUnpaidTrack(ChainTrackCell, ChainTrack),
						"normal unpaid road layer lacks exact identity");
					RequireChainUpgradePreflight(successor, claim, true, null);
				}
				ChainTrackId = ChainTrack.IDIfAssigned;
				Require(!string.IsNullOrEmpty(ChainTrackId), "retained track has no assigned identity");
				try
				{
					ChainTrack.SetIntProperty(KingdomRoads.PathStateProperty, (int)KingdomRoadRules.WearState.Paved);
					RequireChainUpgradePreflight(successor, claim, false, refusal);
				}
				finally { ChainTrack.SetIntProperty(KingdomRoads.PathStateProperty, (int)KingdomRoadRules.WearState.Path); }
				try
				{
					ChainTrack.SetStringProperty(KingdomConstruction.ReceiptProperty, "");
					RequireChainUpgradePreflight(successor, claim, false, refusal);
				}
				finally { ChainTrack.RemoveStringProperty(KingdomConstruction.ReceiptProperty); }
				try
				{
					ChainTrack.SetIntProperty(KingdomConstruction.ReceiptProperty, 0);
					RequireChainUpgradePreflight(successor, claim, false, refusal);
				}
				finally { ChainTrack.RemoveIntProperty(KingdomConstruction.ReceiptProperty); }
				var duplicate = Create(KingdomRoads.PathBlueprint);
				try
				{
					duplicate.SetIntProperty(KingdomRoads.PathStateProperty, (int)KingdomRoadRules.WearState.Path);
					duplicate.IDIfAssigned = ChainTrackId;
					Require(ReferenceEquals(ChainTrackCell.AddObject(duplicate, NoStack: true), duplicate),
						"duplicate road placement substituted its object");
					RequireChainUpgradePreflight(successor, claim, false, refusal);
				}
				finally { duplicate.Obliterate(null, Silent: true); }
				bool hadTally = Zone.HasZoneProperty(KingdomRoads.TallyProperty);
				string originalTally = Zone.GetZoneProperty(KingdomRoads.TallyProperty, null);
				try
				{
					var tally = KingdomRoads.ReadTally(Zone);
					Require(KingdomRoadRules.Accrue(tally, ChainTrackCell.X, ChainTrackCell.Y,
						KingdomRoadRules.MaxTraffic, out int total) && total == KingdomRoadRules.MaxTraffic,
						"maximum historic traffic was not represented");
					KingdomRoads.WriteTally(Zone, tally);
					RequireChainUpgradePreflight(successor, claim, true, null);
				}
				finally
				{
					if (hadTally) Zone.SetZoneProperty(KingdomRoads.TallyProperty, originalTally);
					else Zone.RemoveZoneProperty(KingdomRoads.TallyProperty);
				}
				RequireChainTrack();
				RequireChainUpgradePreflight(successor, claim, true, null);
				Require(Game.TimeTicks == tick && !KingdomSurvey.HasBoundPass && Census().StoredWater == water,
					"road preflight changed time, water or survey binding");
				Require(KingdomConstruction.TryRead(out jobs, out failure) && jobs.Count == jobCount, failure);
				foreach (var unit in ChainSupplied)
					Require(GameObject.Validate(unit) && ReferenceEquals(unit.InInventory, ChainStore),
						"road preflight spent a material unit");
				RequireChainCustody();
				Require(KingdomScenarioJournal.Append("camp-heart-chain-road-wear", true,
					"synthetic-road-layers=true; worn-trodden-path-admitted=true; paved-refused=true"
					+ "; foreign-blueprint-refused=true; string-receipt-refused=true; int-receipt-refused=true"
					+ "; duplicate-id-refused=true; maximum-tally-admitted=true; tally-restored=true; no-debit=true"
					+ "; retained-track=" + ChainTrackId + "; cell=" + ChainTrackCell.X + "," + ChainTrackCell.Y) == null,
					"road probe journal unavailable");
			}

			private void RequireChainTrack()
			{
				Require(GameObject.Validate(ChainTrack) && ChainTrack.IDIfAssigned == ChainTrackId
					&& KingdomRoads.IsExactUnpaidTrack(ChainTrackCell, ChainTrack)
					&& ChainTrack.GetIntProperty(KingdomRoads.PathStateProperty) == (int)KingdomRoadRules.WearState.Path,
					"heart growth lost, moved, demoted or appropriated its original unpaid track");
			}
		}
	}
}
