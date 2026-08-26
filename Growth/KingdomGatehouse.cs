using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	/// <summary>Live projection and exact ownership for the typed gatehouse network.</summary>
	public static class KingdomGatehouse
	{
		public const int Schema = 1;
		public const string SchemaProperty = "KingdomGatehouseSchema";
		public const string PlanProperty = "KingdomGatehousePlan";
		public const string ReservationProperty = "KingdomGatehouseReservation";
		public const string SatelliteProperty = "KingdomGatehouseSatellite";
		public const string OwnerProperty = "KingdomGatehouseOwner";
		public const string IndexProperty = "KingdomGatehouseIndex";
		public const string SlotProperty = "KingdomGatehouseSlot";
		private const string SatelliteIdPrefix = "KingdomGatehouseSatelliteId";

		public static string SatelliteIdProperty(int Index)
		{
			return SatelliteIdPrefix + Index;
		}

		/// <summary>Resolve road/frontier grammar and audit every owned/path cell before debit.</summary>
		public static bool TryPlan(Zone Z, KingdomSystem System, out KingdomGatehousePlan Plan,
			out string Failure)
		{
			Plan = null;
			Failure = null;
			if (Z == null || System == null)
			{
				Failure = "The gatehouse needs claimed ground to measure its road and frontier.";
				return false;
			}
			KingdomRules.Frontier edges = KingdomRules.FrontierEdges(Z.ZoneID,
				System.ClaimedZones);
			if (edges == KingdomRules.Frontier.None)
			{
				Failure = "This ground has no frontier edge for a gatehouse to cross.";
				return false;
			}
			bool hasRite = KingdomPlots.TryRiteGround(Z, out int riteX, out int riteY);
			if (!KingdomPlotRules.TryHeart(KingdomLayout.ReadMarks(Z), hasRite, riteX, riteY,
				out int heartX, out int heartY))
			{
				Failure = "The settlement has no heart from which to measure a road to the frontier.";
				return false;
			}
			if (!KingdomGatehouseRules.TryPlan(Z.Width, Z.Height, edges, heartX, heartY,
				out Plan))
			{
				Failure = "The road reaches the frontier too near the zone edge for a gatehouse and its approaches.";
				return false;
			}
			return TryAudit(Z, Plan, null, null, out Failure);
		}

		/// <summary>Reserve the entire frozen footprint while the paid scaffold is standing.</summary>
		public static bool TryStageScaffold(GameObject Scaffold, KingdomGatehousePlan Plan)
		{
			if (!GameObject.Validate(Scaffold)
				|| !KingdomGatehouseRules.TryEncode(Plan, out string receipt)) return false;
			Scaffold.SetStringProperty(PlanProperty, receipt);
			Scaffold.SetIntProperty(ReservationProperty, Schema);
			KingdomPlots.StampRect(Scaffold, new KingdomPlotRules.PlotRect(
				Plan.X1, Plan.Y1, Plan.X2, Plan.Y2));
			return Scaffold.GetIntProperty(ReservationProperty) == Schema
				&& Scaffold.GetStringProperty(PlanProperty) == receipt
				&& KingdomPlots.TryReadRect(Scaffold, out KingdomPlotRules.PlotRect observed)
				&& SameRect(observed, Plan);
		}

		public static bool ScaffoldMatches(GameObject Scaffold, KingdomGatehousePlan Plan)
		{
			return GameObject.Validate(Scaffold)
				&& Scaffold.GetIntProperty(ReservationProperty) == Schema
				&& KingdomGatehouseRules.TryEncode(Plan, out string receipt)
				&& Scaffold.GetStringProperty(PlanProperty) == receipt
				&& KingdomPlots.TryReadRect(Scaffold, out KingdomPlotRules.PlotRect observed)
				&& SameRect(observed, Plan);
		}

		/// <summary>Re-audit immediately before projection; allows only its exact root/scaffold.</summary>
		public static bool TryAudit(Zone Z, KingdomGatehousePlan Plan, GameObject Root,
			GameObject Scaffold, out string Failure)
		{
			Failure = null;
			if (Z == null || Plan == null || !KingdomGatehouseRules.TryEncode(Plan, out _))
			{
				Failure = "The frozen gatehouse footprint cannot be read.";
				return false;
			}
			KingdomPlotRules.PlotRect proposed = new KingdomPlotRules.PlotRect(
				Plan.X1, Plan.Y1, Plan.X2, Plan.Y2);
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.PlotRoots)
			{
				if (!GameObject.Validate(item) || ReferenceEquals(item, Root)
					|| ReferenceEquals(item, Scaffold)) continue;
				if (KingdomPlots.TryReadRect(item, out KingdomPlotRules.PlotRect laid)
					&& KingdomPlotRules.Overlaps(proposed, laid))
				{
					Failure = "The frozen gatehouse footprint overlaps another reserved work at "
						+ item.CurrentCell.X + "," + item.CurrentCell.Y + ".";
					return false;
				}
			}
			for (int y = Plan.Y1; y <= Plan.Y2; y++)
			{
				for (int x = Plan.X1; x <= Plan.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (!AuditFootprintCell(cell, Root, Scaffold, out string blocker))
					{
						Failure = "The gatehouse footprint is blocked at " + x + "," + y
							+ (string.IsNullOrEmpty(blocker) ? "." : (" by " + blocker + "."));
						return false;
					}
				}
			}
			for (int i = 0; i < 2; i++)
			{
				if (!KingdomGatehouseRules.TryApproach(Plan, i, out KingdomGatehouseCell approach))
					return false;
				Cell cell = Z.GetCell(approach.X, approach.Y);
				if (cell == null || !cell.IsPassable() || cell.HasObjectWithPart("LiquidVolume"))
				{
					Failure = "The " + approach.Slot + " is not passable at "
						+ approach.X + "," + approach.Y + ".";
					return false;
				}
			}
			return true;
		}

		/// <summary>Read the final root's typed footprint without treating it as a plot design.</summary>
		public static bool TryReadPlan(GameObject Root, out KingdomGatehousePlan Plan,
			out string Failure)
		{
			Plan = null;
			Failure = null;
			if (!GameObject.Validate(Root) || Root.HasStringProperty(SchemaProperty)
				|| Root.GetIntProperty(SchemaProperty) != Schema
				|| !KingdomGatehouseRules.IsGatehouse(
					Root.GetStringProperty(KingdomUpgrade.BuildKeyProperty)))
			{
				Failure = "The gatehouse typed-network marker is absent or malformed.";
				return false;
			}
			if (!KingdomGatehouseRules.TryDecode(Root.GetStringProperty(PlanProperty), out Plan)
				|| Root.CurrentCell == null || Root.CurrentCell.X != Plan.GateX
				|| Root.CurrentCell.Y != Plan.GateY)
			{
				Failure = "The gatehouse's frozen road footprint cannot be read exactly.";
				Plan = null;
				return false;
			}
			return true;
		}

		/// <summary>Freeze the six exact owned satellite IDs for the non-plot strike receipt.</summary>
		public static bool TryFreezeStrikeTargets(GameObject Root, Zone Z,
			out KingdomGatehousePlan Plan, out List<KingdomStrikeTarget> Targets,
			out string Failure)
		{
			Targets = null;
			if (!TryReadPlan(Root, out Plan, out Failure) || Root.CurrentZone != Z)
				return false;
			if (!TryExactSatellites(Root, Z, Plan, out List<GameObject> satellites, out Failure))
				return false;
			Targets = new List<KingdomStrikeTarget>(KingdomGatehouseRules.SatelliteCount);
			for (int i = 0; i < satellites.Count; i++)
			{
				GameObject item = satellites[i];
				Targets.Add(new KingdomStrikeTarget
				{
					Id = item.ID,
					Blueprint = item.Blueprint,
					X = item.CurrentCell.X,
					Y = item.CurrentCell.Y
				});
			}
			return true;
		}

		public static bool IsOwnedSatellite(GameObject Item, string OwnerId, string Blueprint,
			int X, int Y, Zone Z)
		{
			if (!GameObject.Validate(Item) || Z == null || string.IsNullOrEmpty(OwnerId)
				|| Item.CurrentZone != Z || Item.CurrentCell != Z.GetCell(X, Y)
				|| Item.Blueprint != Blueprint || Item.GetIntProperty(SatelliteProperty) != 1
				|| Item.GetStringProperty(OwnerProperty) != OwnerId
				|| Item.GetIntProperty(IndexProperty) < 0
				|| Item.GetIntProperty(IndexProperty) >= KingdomGatehouseRules.SatelliteCount
				|| string.IsNullOrEmpty(Item.GetStringProperty(SlotProperty))
				|| Item.GetIntProperty(KingdomPlots.PlotPartProperty) != 0) return false;
			// Once the root's schema-last receipt exists, index and slot are immutable physical
			// facts too. During the live projection callback the root intentionally has no schema
			// yet, so the raw checks above are the only facts available until final verification.
			GameObject root = GameObject.FindByID(OwnerId);
			if (GameObject.Validate(root) && root.GetIntProperty(SchemaProperty) == Schema)
			{
				int index = Item.GetIntProperty(IndexProperty);
				if (!TryReadPlan(root, out KingdomGatehousePlan plan, out _)
					|| !KingdomGatehouseRules.TrySatellite(plan, index,
						out KingdomGatehouseCell expected)
					|| expected.X != X || expected.Y != Y || expected.Blueprint != Blueprint
					|| expected.Slot != Item.GetStringProperty(SlotProperty)) return false;
			}
			return true;
		}

		public static bool IsOwnedSatellite(GameObject Item, string OwnerId)
		{
			return GameObject.Validate(Item) && Item.GetIntProperty(SatelliteProperty) == 1
				&& Item.GetStringProperty(OwnerProperty) == OwnerId;
		}

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

		private static bool TryExactSatellites(GameObject Root, Zone Z,
			KingdomGatehousePlan Plan, out List<GameObject> Satellites, out string Failure)
		{
			Satellites = new List<GameObject>(KingdomGatehouseRules.SatelliteCount);
			Failure = null;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
			{
				string id = Root.GetStringProperty(SatelliteIdProperty(i));
				if (string.IsNullOrEmpty(id) || !ids.Add(id)
					|| !KingdomGatehouseRules.TrySatellite(Plan, i, out KingdomGatehouseCell spec))
				{
					Failure = "The gatehouse's exact satellite receipt is absent or duplicated.";
					return false;
				}
				GameObject item = GameObject.FindByID(id);
				if (!IsOwnedSatellite(item, Root.ID, spec.Blueprint, spec.X, spec.Y, Z)
					|| item.ID != id || item.GetIntProperty(IndexProperty) != i
					|| item.GetStringProperty(SlotProperty) != spec.Slot
					|| (i == 0 && (item.GetIntProperty(ReservationProperty) != Schema
						|| !KingdomPlots.TryReadRect(item, out KingdomPlotRules.PlotRect rect)
						|| !SameRect(rect, Plan)))
					|| (i != 0 && item.HasIntProperty(KingdomPlots.PlotX2Property)))
				{
					Failure = "A gatehouse satellite was removed, moved, replaced, or changed.";
					return false;
				}
				Satellites.Add(item);
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.GatehouseSatellites)
			{
				if (IsOwnedSatellite(item, Root.ID) && !ids.Contains(item.ID))
				{
					Failure = "A new or replacement satellite entered the gatehouse receipt.";
					return false;
				}
			}
			return true;
		}

		private static GameObject FindExactScaffold(Cell Cell, KingdomConstructionJob Job)
		{
			if (Cell == null || Job == null || string.IsNullOrEmpty(Job.SubjectId)) return null;
			GameObject found = null;
			foreach (GameObject item in Cell.GetObjects())
			{
				if (GameObject.Validate(item) && item.ID == Job.SubjectId
					&& item.HasPart("r_KingdomScaffold")
					&& KingdomConstruction.HasReceipt(item, Job))
				{
					if (found != null) return null;
					found = item;
				}
			}
			return found;
		}

		private static bool AuditFootprintCell(Cell Cell, GameObject Root, GameObject Scaffold,
			out string Blocker)
		{
			Blocker = null;
			if (Cell == null)
			{
				Blocker = "the edge of the zone";
				return false;
			}
			bool hasExpected = false;
			foreach (GameObject item in Cell.GetObjects())
			{
				if (ReferenceEquals(item, Root) || ReferenceEquals(item, Scaffold))
				{
					hasExpected = true;
					continue;
				}
				if (!GameObject.Validate(item)) continue;
				if (item.IsPlayer() || item.IsCreature)
				{
					Blocker = item.IsPlayer() ? "the founder" : item.ShortDisplayNameStripped;
					return false;
				}
				if (KingdomPlots.ReadObject(item) != KingdomPlotRules.GroundKind.Bare)
				{
					Blocker = item.ShortDisplayNameStripped ?? item.Blueprint;
					return false;
				}
			}
			if (!hasExpected && (!Cell.IsPassable() || Cell.HasObjectWithPart("LiquidVolume")))
			{
				Blocker = "impassable ground";
				return false;
			}
			return true;
		}

		private static void ClearRootReceipt(GameObject Root)
		{
			if (!GameObject.Validate(Root)) return;
			Root.RemoveIntProperty(SchemaProperty);
			Root.RemoveStringProperty(PlanProperty);
			for (int i = 0; i < KingdomGatehouseRules.SatelliteCount; i++)
				Root.RemoveStringProperty(SatelliteIdProperty(i));
		}

		private static bool SameRect(KingdomPlotRules.PlotRect Rect, KingdomGatehousePlan Plan)
		{
			return Plan != null && Rect.X1 == Plan.X1 && Rect.Y1 == Plan.Y1
				&& Rect.X2 == Plan.X2 && Rect.Y2 == Plan.Y2;
		}
	}
}

namespace XRL.World.Parts
{
	/// <summary>Stateless projection hook; all reload-safe state lives in named properties.</summary>
	[Serializable]
	public sealed class r_KingdomGatehouse : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == EnteredCellEvent.ID;
		}

		public override bool HandleEvent(EnteredCellEvent E)
		{
			KingdomGatehouse.MaterializeFromEnteredCell(ParentObject, E.Cell);
			return base.HandleEvent(E);
		}
	}
}
