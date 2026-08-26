using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		internal static void MaterializeFromEnteredCell(GameObject Root, Cell Cell)
		{
			if (!GameObject.Validate(Root) || Cell == null || Root.CurrentCell != Cell) return;
			if (Root.GetIntProperty(SchemaProperty) == Schema
				&& !Root.HasStringProperty(SchemaProperty)) return; // Reload: never recreate outputs.
			string receiptId = Root.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(receiptId) || !KingdomConstruction.TryFind(receiptId,
				out KingdomConstructionJob job) || job.TargetKey != KingdomGatehouseRules.BuildKey
				|| job.OutputId != Root.ID || job.X != Cell.X || job.Y != Cell.Y
				|| !KingdomGatehouseRules.TryDecode(job.Payload, out KingdomGatehousePlan plan))
				throw new InvalidOperationException(
					"The gatehouse entered without its exact frozen construction plan.");
			GameObject scaffold = FindExactScaffold(Cell, job);
			string failure = null;
			if (!GameObject.Validate(scaffold) || !ScaffoldMatches(scaffold, plan)
				|| !TryAudit(Cell.ParentZone, plan, Root, scaffold, out failure))
				throw new InvalidOperationException(failure
					?? "The gatehouse footprint changed before final projection.");

			List<GameObject> created = new List<GameObject>();
			try
			{
				for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
				{
					if (!KingdomGatehouseRules.TrySatellite(plan, i, out KingdomGatehouseCell spec))
						throw new InvalidOperationException("The frozen gatehouse topology is incomplete.");
					GameObject item = GameObject.Create(spec.Blueprint);
					if (!GameObject.Validate(item) || item.Blueprint != spec.Blueprint)
						throw new InvalidOperationException("A gatehouse satellite blueprint could not be created exactly.");
					item.SetIntProperty(SatelliteProperty, 1);
					item.SetStringProperty(OwnerProperty, Root.ID);
					item.SetIntProperty(IndexProperty, i);
					item.SetStringProperty(SlotProperty, spec.Slot);
					// The first owned stone jamb is the physical overlap marker. It is not
					// KingdomBuilt and not a plot part, so socket/change-plot UI can never treat
					// the gatehouse as a stakeable plot; ReadPlots still sees this one exact rect.
					if (i == 0)
					{
						KingdomPlots.StampRect(item, new KingdomPlotRules.PlotRect(
							plan.X1, plan.Y1, plan.X2, plan.Y2));
						item.SetIntProperty(ReservationProperty, Schema);
					}
					Root.SetStringProperty(SatelliteIdProperty(i), item.ID);
					created.Add(item);
					GameObject accepted = Cell.ParentZone.GetCell(spec.X, spec.Y).AddObject(item);
					KingdomSurvey.ObserveAddResultInActive(Cell.ParentZone, item, accepted);
					if (!ReferenceEquals(accepted, item)
						|| !IsOwnedSatellite(item, Root.ID, spec.Blueprint,
							spec.X, spec.Y, Cell.ParentZone))
						throw new InvalidOperationException("A gatehouse satellite changed during AddObject.");
				}
				if (!KingdomGatehouseRules.TryEncode(plan, out string encoded))
					throw new InvalidOperationException("The gatehouse plan could not be frozen on its root.");
				Root.SetStringProperty(PlanProperty, encoded);
				for (int i = 0; i < created.Count; i++)
					Root.SetStringProperty(SatelliteIdProperty(i), created[i].ID);
				Root.SetIntProperty(SchemaProperty, Schema); // final commit marker
				if (!TryReadPlan(Root, out KingdomGatehousePlan read, out failure)
					|| !TryExactSatellites(Root, Cell.ParentZone, read, out _, out failure))
					throw new InvalidOperationException(failure
						?? "The completed gatehouse receipt did not read back exactly.");
			}
			catch
			{
				for (int i = created.Count - 1; i >= 0; i--)
				{
					try { created[i].Obliterate(null, Silent: true); }
					catch { }
					// Obliterate may throw after applying its effect, or may be vetoed and leave the
					// satellite standing. Publish the proved live topology rather than assuming absence.
					KingdomSurvey.ObserveCurrentTopologyInActive(Cell.ParentZone, created[i]);
				}
				ClearRootReceipt(Root);
				throw;
			}
		}
	}
}
