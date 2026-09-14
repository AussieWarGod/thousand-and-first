using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private void ProveChainSurveyStakes()
			{
				long tick = Game.TimeTicks;
				RequireChainFoundingRecovery("before survey-stake probes");
				Require(KingdomArchitectureRuntime.TryRead(ChainHeart, out var before, out string failure), failure);
				Require(KingdomArchitectureRuntime.TryPrepareSuccessorForUpgrade(System, Zone, ChainHeart,
					before, ChainTo, out var successor, out failure), failure);
				var claim = new KingdomMaterialDebitCost(KingdomMaterials.UpgradeCostFor(ChainFrom));
				var stakes = new List<GameObject>();
				foreach (var item in Census().Objects)
					if (item.GetIntProperty(KingdomPlots.HeartStakeProperty) == 1)
					{
						Require(KingdomPlots.IsExactFoundingHeartSurveyStake(System, Zone, item)
							&& successor.Rect.Contains(item.CurrentCell.X, item.CurrentCell.Y)
							&& !before.Rect.Contains(item.CurrentCell.X, item.CurrentCell.Y),
							"tier-four stake lacks exact founding or annexed-ground authority");
						stakes.Add(item);
					}
				Require(stakes.Count == 4, "tier-four probe did not witness all four survey stakes");
				RequireChainUpgradePreflight(successor, claim, true, null);
				var original = stakes[0];
				Cell at = original.CurrentCell;
				string id = original.IDIfAssigned;
				string owner = original.GetStringProperty(KingdomPlots.FoundingHeartOwnerProperty);
				try
				{
					original.SetStringProperty(KingdomPlots.FoundingHeartOwnerProperty, "foreign-probe-owner");
					Require(!KingdomPlots.IsExactFoundingHeartSurveyStake(System, Zone, original),
						"foreign-owned stake admitted");
					RequireChainUpgradePreflight(successor, claim, false, "founding-heart ground occupies plot-envelope growth at ");
				}
				finally { original.SetStringProperty(KingdomPlots.FoundingHeartOwnerProperty, owner); }
				for (int duplicate = 0; duplicate < 2; duplicate++)
				{
					var fake = Create(KingdomPlots.SurveyStakeBlueprint);
					string probeId = fake.ID;
					try
					{
						fake.SetIntProperty(KingdomPlots.HeartStakeProperty, 1);
						if (duplicate == 1)
						{
							fake.IDIfAssigned = id;
							fake.SetStringProperty(KingdomPlots.FoundingHeartOwnerProperty, owner);
							fake.SetIntProperty(KingdomPlots.FoundingHeartSlotProperty,
								original.GetIntProperty(KingdomPlots.FoundingHeartSlotProperty));
						}
						Require(ReferenceEquals(at.AddObject(fake, NoStack: true), fake), "fake stake placement substituted its identity");
						Require(!KingdomPlots.IsExactFoundingHeartSurveyStake(System, Zone, fake),
							"unbound or duplicate survey stake admitted");
						RequireChainUpgradePreflight(successor, claim, false, "founding-heart ground occupies plot-envelope growth at ");
					}
					finally
					{
						// Native destruction retains a tombstone. Never retire a probe under a
						// borrowed founding identity or transaction owner.
						fake.IDIfAssigned = probeId;
						fake.RemoveStringProperty(KingdomPlots.FoundingHeartOwnerProperty);
						fake.RemoveIntProperty(KingdomPlots.FoundingHeartSlotProperty);
						fake.Obliterate(null, Silent: true);
					}
					Require(original.CurrentCell == at && original.IDIfAssigned == id
						&& KingdomPlots.IsExactFoundingHeartSurveyStake(System, Zone, original),
						"fake stake cleanup lost the original marker authority");
				}
				RequireChainUpgradePreflight(successor, claim, true, null);
				foreach (var stake in stakes)
					Require(KingdomPlots.IsExactFoundingHeartSurveyStake(System, Zone, stake),
						"a real survey stake was lost or changed by preflight");
				Require(Game.TimeTicks == tick && !KingdomSurvey.HasBoundPass,
					"survey stake probes changed time or left a bound survey");
				RequireChainCustody();
				RequireChainFoundingRecovery("after survey-stake probes");
				Require(KingdomScenarioJournal.Append("camp-heart-chain-survey-stakes", true,
					"four-exact-stakes=true; founding-recovery-restored=true; foreign-owner-refused=true; unbound-marker-refused=true"
					+ "; duplicate-id-refused=true; original-markers-retained=true; no-turns=true") == null,
					"survey stake probe journal unavailable");
			}
		}
	}
}
