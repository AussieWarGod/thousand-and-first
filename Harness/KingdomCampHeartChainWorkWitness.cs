using System;
using System.Collections.Generic;
using System.Text;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private string CaptureChainWorks(KingdomCityState City, KingdomSurvey Survey,
				Dictionary<string, string> Records)
			{
				var rows = new List<string[]>();
				for (int i = 0; i < City.WorkCount; i++)
				{
					Require(City.TryWork(i, out var row), "higher-heart work row missing");
					rows.Add(new[] { "row:" + ChainNumber(row.WorkId), row.ZoneId, row.DesignKey,
						ChainNumber(row.AnchorX), ChainNumber(row.AnchorY), ChainNumber(row.ConditionPercent),
						ChainNumber(row.CrewAssigned), ChainNumber(row.RanThroughTick), row.RunState.Kind.ToString(),
						ChainNumber(row.RunState.Stage), ChainNumber(row.RunState.Progress), ChainNumber(row.RunState.NextTick) });
				}
				Require(Survey.TryBenefits(out var benefits, out string failure), failure);
				foreach (var root in Survey.Built)
				{
					ExactGround(Zone, root);
					Require(KingdomUpgrade.IsFunctionallyBuilt(root), "higher-heart support work is not functional");
					bool authored = root.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
						|| root.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty);
					string layout = null;
					if (authored)
					{
						Require(KingdomArchitectureStamper.TryVerifyComplete(root, Zone, out failure)
							&& KingdomArchitectureRuntime.TryRead(root, out _, out failure), failure);
						layout = Convert.ToBase64String(new UTF8Encoding(false, true).GetBytes(
							root.GetStringProperty(KingdomArchitectureRuntime.SnapshotProperty)));
					}
					else Require(KingdomUpgrade.DesignKeyOf(root) == "airwellcourt",
						"higher-heart support has an undisclosed legacy work");
					rows.Add(new[] { "work:" + root.IDIfAssigned, root.Blueprint, KingdomUpgrade.DesignKeyOf(root),
						ChainNumber(root.CurrentCell.X), ChainNumber(root.CurrentCell.Y), layout,
						root.GetStringProperty(KingdomConstruction.ReceiptProperty),
						root.GetStringProperty(KingdomPlots.PlotIdProperty),
						ChainNumber(KingdomLodging.RoofCapacity(root, benefits)),
						root.GetPart<r_KingdomImprovement>()?.Held.ToString(),
						root.GetPart<LiquidProducer>()?.VariableRate,
						root.GetPart<LiquidProducer>()?.FillSelfOnly.ToString() });
				}
				int stakes = 0;
				foreach (var item in Survey.Objects)
				{
					if (item.GetIntProperty(KingdomPlots.HeartStakeProperty) != 1) continue;
					ExactGround(Zone, item);
					Require(KingdomPlots.IsExactFoundingHeartSurveyStake(System, Zone, item),
						"higher-heart stake no longer has exact founding authority");
					rows.Add(new[] { "stake:" + item.IDIfAssigned, item.Blueprint,
						ChainNumber(item.CurrentCell.X), ChainNumber(item.CurrentCell.Y),
						item.GetStringProperty(KingdomPlots.FoundingHeartOwnerProperty),
						ChainNumber(item.GetIntProperty(KingdomPlots.FoundingHeartSlotProperty)) });
					stakes++;
				}
				Require(stakes == 4, "higher-heart save must retain all four exact survey stakes");
				foreach (var liquid in Survey.Stores)
				{
					var holder = liquid.ParentObject; ExactGround(Zone, holder);
					rows.Add(new[] { "water:" + holder.IDIfAssigned, holder.Blueprint,
						ChainNumber(holder.CurrentCell.X), ChainNumber(holder.CurrentCell.Y),
						ChainNumber(liquid.Volume), ChainNumber(liquid.MaxVolume) });
					foreach (var component in liquid.ComponentLiquids)
						rows.Add(new[] { "liquid:" + holder.IDIfAssigned + ":" + component.Key,
							holder.IDIfAssigned, component.Key, ChainNumber(component.Value) });
				}
				foreach (var holder in Survey.Larders) CaptureChainInventory(holder, "larder", rows);
				foreach (var holder in Survey.MaterialStockpiles) CaptureChainInventory(holder, "stockpile", rows);
				return ChainFacts("support", rows, Records);
			}

			private void CaptureChainInventory(GameObject Holder, string Kind, List<string[]> Rows)
			{
				ExactGround(Zone, Holder);
				Require(Holder.Inventory != null, "higher-heart supply inventory absent");
				Rows.Add(new[] { Kind + ":" + Holder.IDIfAssigned, Holder.Blueprint,
					ChainNumber(Holder.CurrentCell.X), ChainNumber(Holder.CurrentCell.Y),
					ChainNumber(Holder.Inventory.Objects.Count) });
				for (int i = 0; i < Holder.Inventory.Objects.Count; i++)
				{
					var item = Holder.Inventory.Objects[i];
					Require(GameObject.Validate(item) && ReferenceEquals(item.Physics?._InInventory, Holder)
						&& item.Physics._CurrentCell == null && item.Physics._Equipped == null,
						"higher-heart supply has ambiguous physical custody");
					int count = KingdomMaterials.RawCensusCountOf(item);
					Require(count > 0, "higher-heart supply has invalid raw stack count");
					Rows.Add(new[] { Kind + ":" + Holder.IDIfAssigned + ":unit:" + ChainNumber(i),
						item.IDIfAssigned, item.Blueprint, ChainNumber(count) });
				}
			}
		}
	}
}
