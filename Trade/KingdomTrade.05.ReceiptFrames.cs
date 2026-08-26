using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		private static void RefreshPhysicalTopologies(TradePhysicalFrame Physical)
		{
			if (Physical == null) return;
			LoadedTopologyWitness current = CaptureLoadedTopology();
			for (int i = 0; i < Physical.Water.Count; i++) Physical.Water[i].Topology = current;
			for (int i = 0; i < Physical.Materials.Count; i++) Physical.Materials[i].Topology = current;
		}

		private static bool ExactReferences<T>(List<T> Current, T[] Expected) where T : class
		{
			if (Current == null || Expected == null || Current.Count != Expected.Length) return false;
			for (int i = 0; i < Expected.Length; i++)
				if (!ReferenceEquals(Current[i], Expected[i])) return false;
			return true;
		}

		private static bool ExactReceiptRows(TradeLiveFrame Frame)
		{
			if (Frame?.WaterLegs == null || Frame.MaterialOutputs == null
				|| Frame.WaterRows == null || Frame.MaterialRows == null
				|| Frame.WaterLegs.Count != Frame.WaterRows.Length
				|| Frame.MaterialOutputs.Count != Frame.MaterialRows.Length) return false;
			for (int i = 0; i < Frame.WaterRows.Length; i++)
				if (!ReferenceEquals(Frame.WaterLegs[i], Frame.WaterRows[i])) return false;
			for (int i = 0; i < Frame.MaterialRows.Length; i++)
				if (!ReferenceEquals(Frame.MaterialOutputs[i], Frame.MaterialRows[i])) return false;
			return true;
		}

		private static void RefreshReceiptRows(TradeLiveFrame Frame)
		{
			if (Frame == null) return;
			Frame.WaterRows = Frame.WaterLegs?.ToArray();
			Frame.MaterialRows = Frame.MaterialOutputs?.ToArray();
		}

		private static WaterWitness CaptureWaterWitness(KingdomTradeWaterLeg Leg,
			GameObject Owner, LiquidVolume Vessel)
		{
			if (Leg == null || Owner == null || Vessel == null
				|| Vessel.ComponentLiquids == null) return null;
			return new WaterWitness
			{
				Leg = Leg,
				Owner = Owner,
				Vessel = Vessel,
				Cell = Owner.CurrentCell,
				Dictionary = Vessel.ComponentLiquids,
				BeforeComponents = new Dictionary<string, int>(Vessel.ComponentLiquids),
				OwnerId = Leg.OwnerId,
				ZoneId = Leg.ZoneId,
				Capacity = Leg.Capacity,
				Before = Leg.Before,
				Delta = Leg.Delta,
				After = Leg.After,
				BeforeComposition = Leg.BeforeComposition,
				AfterComposition = Leg.AfterComposition,
				Topology = CaptureLoadedTopology()
			};
		}

		private static bool ExactWaterReceipt(WaterWitness Witness)
		{
			return Witness != null && Witness.Leg != null
				&& string.Equals(Witness.Leg.OwnerId, Witness.OwnerId,
					StringComparison.Ordinal)
				&& string.Equals(Witness.Leg.ZoneId, Witness.ZoneId,
					StringComparison.Ordinal)
				&& Witness.Leg.Capacity == Witness.Capacity
				&& Witness.Leg.Before == Witness.Before
				&& Witness.Leg.Delta == Witness.Delta
				&& Witness.Leg.After == Witness.After
				&& string.Equals(Witness.Leg.BeforeComposition,
					Witness.BeforeComposition, StringComparison.Ordinal)
				&& string.Equals(Witness.Leg.AfterComposition,
					Witness.AfterComposition, StringComparison.Ordinal);
		}

		private static MaterialWitness CaptureMaterialWitness(
			KingdomTradeMaterialOutput Output, GameObject Item,
			GameObject Destination, InventoryWitness Inventory)
		{
			if (Output == null) return null;
			return new MaterialWitness
			{
				Output = Output,
				Item = Item,
				Destination = Destination,
				Inventory = Inventory,
				OutputId = Output.OutputId,
				Marker = Output.Marker,
				Blueprint = Output.Blueprint,
				Count = Output.Count,
				DestinationOwnerId = Output.DestinationOwnerId,
				ZoneId = Output.ZoneId,
				Topology = CaptureLoadedTopology()
			};
		}

		private static bool ExactMaterialReceipt(MaterialWitness Witness)
		{
			return Witness != null && Witness.Output != null
				&& string.Equals(Witness.Output.OutputId, Witness.OutputId,
					StringComparison.Ordinal)
				&& string.Equals(Witness.Output.Marker, Witness.Marker,
					StringComparison.Ordinal)
				&& string.Equals(Witness.Output.Blueprint, Witness.Blueprint,
					StringComparison.Ordinal)
				&& Witness.Output.Count == Witness.Count
				&& string.Equals(Witness.Output.DestinationOwnerId,
					Witness.DestinationOwnerId, StringComparison.Ordinal)
				&& string.Equals(Witness.Output.ZoneId, Witness.ZoneId,
					StringComparison.Ordinal);
		}

		private static ProjectionRowWitness[] CaptureProjectionRows(
			List<KingdomTradeProjectionRow> Rows)
		{
			if (Rows == null) return null;
			ProjectionRowWitness[] values = new ProjectionRowWitness[Rows.Count];
			for (int i = 0; i < Rows.Count; i++)
			{
				KingdomTradeProjectionRow row = Rows[i];
				if (row == null) return null;
				values[i] = new ProjectionRowWitness
				{
					Row = row, OperationSequence = row.OperationSequence,
					SettlementId = row.SettlementId, ZoneId = row.ZoneId,
					ProjectionId = row.ProjectionId, ObjectId = row.ObjectId,
					Quarantined = row.Quarantined, Fault = row.Fault
				};
			}
			return values;
		}

		private static bool ExactProjectionRows(TradeLiveFrame Frame)
		{
			if (Frame == null || !ReferenceEquals(Frame.Book?.Projections,
					Frame.ProjectionRows) || Frame.ProjectionRows == null
				|| Frame.ProjectionRowValues == null
				|| Frame.ProjectionRows.Count != Frame.ProjectionRowValues.Length) return false;
			for (int i = 0; i < Frame.ProjectionRowValues.Length; i++)
			{
				ProjectionRowWitness expected = Frame.ProjectionRowValues[i];
				KingdomTradeProjectionRow row = Frame.ProjectionRows[i];
				if (expected == null || !ReferenceEquals(row, expected.Row)
					|| row.OperationSequence != expected.OperationSequence
					|| !string.Equals(row.SettlementId, expected.SettlementId,
						StringComparison.Ordinal)
					|| !string.Equals(row.ZoneId, expected.ZoneId, StringComparison.Ordinal)
					|| !string.Equals(row.ProjectionId, expected.ProjectionId,
						StringComparison.Ordinal)
					|| !string.Equals(row.ObjectId, expected.ObjectId, StringComparison.Ordinal)
					|| row.Quarantined != expected.Quarantined
					|| !string.Equals(row.Fault, expected.Fault, StringComparison.Ordinal)) return false;
			}
			return true;
		}

		private static void RefreshProjectionRows(TradeLiveFrame Frame)
		{
			if (Frame == null) return;
			Frame.ProjectionRows = Frame.Book?.Projections;
			Frame.ProjectionRowValues = CaptureProjectionRows(Frame.ProjectionRows);
		}

		private static ManifestWitness CaptureManifest(KingdomTradeManifestState Row)
		{
			return Row == null ? null : new ManifestWitness
			{
				Row = Row, OperationSequence = Row.OperationSequence,
				OperationId = Row.OperationId, Id = Row.Id,
				OriginId = Row.OriginId, OriginName = Row.OriginName,
				DestinationId = Row.DestinationId, DestinationName = Row.DestinationName,
				OriginalDrams = Row.OriginalDrams, EscrowDrams = Row.EscrowDrams,
				LoadedTick = Row.LoadedTick, DeadlineTick = Row.DeadlineTick,
				TurnedBack = Row.TurnedBack, Status = Row.Status, Fault = Row.Fault
			};
		}

		private static bool ExactBookDomain(TradeLiveFrame Frame)
		{
			if (Frame == null || Frame.Book == null
				|| Frame.Book.RetainedEscrowDrams != Frame.RetainedEscrow
				|| !string.Equals(Frame.Book.ActiveProjectionId,
					Frame.LegacyProjectionId, StringComparison.Ordinal)
				|| !string.Equals(Frame.Book.ActiveProjectionObjectId,
					Frame.LegacyProjectionObjectId, StringComparison.Ordinal)) return false;
			KingdomTradeManifestState row = Frame.Book.Manifest;
			ManifestWitness expected = Frame.Manifest;
			if (row == null || expected == null) return row == null && expected == null;
			return ReferenceEquals(row, expected.Row)
				&& row.OperationSequence == expected.OperationSequence
				&& string.Equals(row.OperationId, expected.OperationId, StringComparison.Ordinal)
				&& string.Equals(row.Id, expected.Id, StringComparison.Ordinal)
				&& string.Equals(row.OriginId, expected.OriginId, StringComparison.Ordinal)
				&& string.Equals(row.OriginName, expected.OriginName, StringComparison.Ordinal)
				&& string.Equals(row.DestinationId, expected.DestinationId, StringComparison.Ordinal)
				&& string.Equals(row.DestinationName, expected.DestinationName, StringComparison.Ordinal)
				&& row.OriginalDrams == expected.OriginalDrams
				&& row.EscrowDrams == expected.EscrowDrams
				&& row.LoadedTick == expected.LoadedTick
				&& row.DeadlineTick == expected.DeadlineTick
				&& row.TurnedBack == expected.TurnedBack && row.Status == expected.Status
				&& string.Equals(row.Fault, expected.Fault, StringComparison.Ordinal);
		}

		private static void RefreshBookDomain(TradeLiveFrame Frame)
		{
			if (Frame == null || Frame.Book == null) return;
			Frame.Manifest = CaptureManifest(Frame.Book.Manifest);
			Frame.RetainedEscrow = Frame.Book.RetainedEscrowDrams;
			Frame.LegacyProjectionId = Frame.Book.ActiveProjectionId;
			Frame.LegacyProjectionObjectId = Frame.Book.ActiveProjectionObjectId;
		}

	}
}
