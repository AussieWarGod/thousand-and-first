using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static bool TryArchitectureDestination(KingdomSystem System, Zone Zone,
			GameObject Root, KingdomPlotRules.PlotRect Destination, out string Failure)
		{
			Failure = null;
			bool marker = Root != null
				&& (Root.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
					|| Root.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty));
			if (!marker) return true;
			if (!KingdomArchitectureRuntime.TryRead(Root,
				out KingdomArchitectureIntent before, out Failure)) return false;
			if (Zone == null || Destination.X1 < 0 || Destination.Y1 < 0
				|| Destination.X2 >= Zone.Width || Destination.Y2 >= Zone.Height
				|| Destination.Width != before.Rect.Width
				|| Destination.Height != before.Rect.Height)
			{
				Failure = "That ground cannot carry the plot's exact frozen architecture.";
				return false;
			}
			int dx = Destination.X1 - before.Rect.X1;
			int dy = Destination.Y1 - before.Rect.Y1;
			KingdomArchitectureIntent shifted = KingdomArchitectureIntent.CreateRaw(
				before.SchemaVersion, before.BuildKey, before.PlanKey, before.BindingKey,
				before.TierKey, before.VariantKey, before.PaletteKey, before.LotType,
				before.LotSize, before.Facing, before.EncodedSnapshot, before.SnapshotHash,
				Destination, before.MainWorldX + dx, before.MainWorldY + dy);
			if (!KingdomArchitectureRuntime.TryDecode(shifted,
				out ArchitectureLayoutSnapshot snapshot, out Failure)) return false;
			for (int i = 0; i < snapshot.Placements.Count; i++)
				if (!KingdomArchitectureRuntime.TryWorldPlacement(snapshot, Destination,
					snapshot.Placements[i], out _, out _, out Failure)) return false;
			return true;
		}

		private static bool TryFreezeArchitecture(KingdomSystem System, Zone Zone,
			GameObject Root, KingdomPlotRules.PlotRect Source,
			KingdomPlotRules.PlotRect Destination, IList<KingdomRelocationRow> Rows,
			out KingdomRelocationArchitecture FrozenReceipt, out string Failure)
		{
			FrozenReceipt = null; Failure = null;
			bool marker = Root != null
				&& (Root.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
					|| Root.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty));
			if (!marker) return true;
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomArchitectureRuntime.TryRead(Root, out intent, out Failure)
				|| !KingdomArchitectureRuntime.TryDecode(intent, out snapshot, out Failure)
				|| intent.Rect.X1 != Source.X1 || intent.Rect.Y1 != Source.Y1
				|| intent.Rect.X2 != Source.X2 || intent.Rect.Y2 != Source.Y2
				|| Root.CurrentCell != Zone.GetCell(intent.MainWorldX, intent.MainWorldY)
				|| Root.GetStringProperty(KingdomArchitectureStamper.LotIdProperty)
					!= Root.GetStringProperty(KingdomPlots.PlotIdProperty)
				|| Root.GetStringProperty(KingdomArchitectureStamper.HashProperty)
					!= intent.SnapshotHash
				|| Root.GetIntProperty(KingdomArchitectureStamper.NextLayerProperty) != 3)
			{
				if (Failure == null) Failure = "The plot's frozen architecture authority is incomplete.";
				return false;
			}
			if (!TryArchitectureDestination(System, Zone, Root, Destination, out Failure)
				|| !ExactArchitectureRows(Zone, intent, snapshot, Rows, out Failure)) return false;
			FrozenReceipt = new KingdomRelocationArchitecture
			{
				Schema = intent.SchemaVersion, BuildKey = intent.BuildKey,
				PlanKey = intent.PlanKey, BindingKey = intent.BindingKey,
				TierKey = intent.TierKey, VariantKey = intent.VariantKey,
				PaletteKey = intent.PaletteKey, LotType = intent.LotType,
				LotSize = (int)intent.LotSize, Facing = (int)intent.Facing,
				Snapshot = intent.EncodedSnapshot, Hash = intent.SnapshotHash,
				MainX = intent.MainWorldX, MainY = intent.MainWorldY
			};
			return true;
		}

		private static bool ExactArchitectureRows(Zone Zone, KingdomArchitectureIntent Intent,
			ArchitectureLayoutSnapshot Snapshot, IList<KingdomRelocationRow> Rows,
			out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (placement.ExistingAuthority)
				{
					Failure = "A plot bound to immutable existing ground cannot be relocated.";
					return false;
				}
				if (!KingdomArchitectureRuntime.TryWorldPlacement(Snapshot, Intent.Rect,
					placement, out int x, out int y, out Failure)) return false;
				int count = 0;
				for (int r = 0; r < Rows.Count; r++)
				{
					KingdomRelocationRow row = Rows[r];
					if (row.Blueprint != placement.Blueprint) continue;
					GameObject exact;
					if (KingdomConstruction.FindExactId(Zone, row.ObjectId, out exact)
						!= KingdomPhysicalLookupState.Exact || exact == null) continue;
					if (exact.GetStringProperty(KingdomArchitectureStamper.ComponentSlotProperty)
						== placement.Slot
						&& exact.GetStringProperty(KingdomArchitectureStamper.ComponentHashProperty)
							== Intent.SnapshotHash
						&& exact.CurrentCell == Zone.GetCell(x, y)) count++;
				}
				if (count != 1)
				{
					Failure = "Frozen architecture slot " + placement.Slot
						+ " is absent, duplicated, or displaced.";
					return false;
				}
			}
			return true;
		}

		private static KingdomArchitectureIntent ArchitectureIntent(
			KingdomRelocationMove Move, bool Destination)
		{
			KingdomRelocationArchitecture a = Move?.Architecture;
			if (a == null) return null;
			KingdomRelocationRect rect = Destination ? Move.Destination : Move.Source;
			int dx = rect.X1 - Move.Source.X1;
			int dy = rect.Y1 - Move.Source.Y1;
			return KingdomArchitectureIntent.CreateRaw(a.Schema, a.BuildKey, a.PlanKey,
				a.BindingKey, a.TierKey, a.VariantKey, a.PaletteKey, a.LotType,
				(ArchitectureLotSize)a.LotSize, (ArchitectureFacing)a.Facing,
				a.Snapshot, a.Hash, Runtime(rect), a.MainX + dx, a.MainY + dy);
		}
	}
}
