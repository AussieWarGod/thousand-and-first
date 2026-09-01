using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Engine boundary for one exact player-designated room receipt.</summary>
	public static class KingdomAdoptionDesignation
	{
		public const int ReceiptSchema = 1;
		public const string SchemaProperty = "r_TAF_AdoptDesignationSchema";
		public const string ReceiptProperty = "r_TAF_AdoptDesignationReceipt";
		public const string RevisionProperty = "r_TAF_AdoptDesignationRevision";

		public static bool TryStamp(GameObject Root, Zone Z, string BuildingKey,
			KingdomAdoptRules.EnclosureMeasurement Enclosure,
			out KingdomAdoptionDesignationReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!GameObject.Validate(Root) || Z == null || Root.CurrentZone != Z
				|| Root.CurrentCell == null || !Enclosure.Bounded
				|| Enclosure.DoorCells < 1 || Enclosure.FloorCells == null)
				return Fail("an exact bounded room and marker are required", out Failure);
			string rootId = Root.ID;
			if (!KingdomData.TryGetBuilding(BuildingKey, out KingdomRules.BuildEntry entry)
				|| !KingdomPlots.TryGetSpec(BuildingKey, out KingdomPlotRules.PlotSpec spec)
				|| !KingdomAdoptRules.MeetsMinimumUsable(
					KingdomAdoptRules.ClassifyRole(entry.Category), spec.Size, Enclosure))
				return Fail("the exact room lacks its role and tier's usable floor", out Failure);
			if (!KingdomForeignFootprints.TryMatchExact(Z, Enclosure.FloorCells,
				out Api.KingdomForeignFootprint foreign, out Failure)) return false;
			if (!KingdomAdoptionDesignationRules.TryCreate(Z.ZoneID, rootId, BuildingKey,
				Enclosure.FloorCells, false, foreign?.ProviderId, foreign?.ProviderVersion,
				foreign?.Identity, foreign?.Revision,
				out Receipt, out Failure)) return false;
			if (!TryPublish(Root, Receipt, out Failure)) return false;
			return KingdomAdoptionOperation.TryStamp(Root, entry, out _, out Failure);
		}

		public static bool TryStampContainer(GameObject Root, Zone Z, string BuildingKey,
			out KingdomAdoptionDesignationReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!GameObject.Validate(Root) || Z == null || Root.CurrentZone != Z
				|| Root.CurrentCell == null)
				return Fail("an exact physical container is required", out Failure);
			ArchitecturePoint[] cell = { new ArchitecturePoint(Root.CurrentCell.X,
				Root.CurrentCell.Y) };
			if (!KingdomAdoptionDesignationRules.TryCreate(Z.ZoneID, Root.ID, BuildingKey,
				cell, true, null, null, null, null, out Receipt, out Failure)) return false;
			return TryPublish(Root, Receipt, out Failure);
		}

		public static bool TryStampOpenPlot(GameObject Root, Zone Z, string BuildingKey,
			IReadOnlyList<ArchitecturePoint> Cells,
			out KingdomAdoptionDesignationReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!GameObject.Validate(Root) || Z == null || Root.CurrentZone != Z
				|| Root.CurrentCell == null || Cells == null)
				return Fail("an exact open plot and civic marker are required", out Failure);
			if (!KingdomData.TryGetBuilding(BuildingKey, out KingdomRules.BuildEntry entry)
				|| !KingdomPlots.TryGetSpec(BuildingKey, out KingdomPlotRules.PlotSpec spec)
				|| !KingdomAdoptabilityRules.TryClassify(BuildingKey, entry.Category,
					spec.Size, spec.Open, out KingdomAdoptionTargetKind target, out Failure)
				|| target != KingdomAdoptionTargetKind.OpenPlot)
				return Failure != null ? false
					: Fail("the design has no open-plot adoption contract", out Failure);
			if (!KingdomAdoptionPlotRules.TryCenteredCells(Root.CurrentCell.X,
				Root.CurrentCell.Y, spec.Size, Z.Width, Z.Height, out _,
				out List<ArchitecturePoint> exact, out Failure)
				|| !KingdomAdoptRules.SameMembership(exact, Cells))
				return Failure != null ? false
					: Fail("the open plot cells disagree with their centred marker", out Failure);
			if (!KingdomAdoptionDesignationRules.TryCreate(Z.ZoneID, Root.ID, BuildingKey,
				exact, false, true, null, null, null, null, out Receipt, out Failure))
				return false;
			if (!TryPublish(Root, Receipt, out Failure)) return false;
			return KingdomAdoptionOperation.TryStamp(Root, entry, out _, out Failure);
		}

		public static bool TryPublish(GameObject Root,
			KingdomAdoptionDesignationReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Root) || Receipt == null || Root.ID != Receipt.RootId)
				return Fail("adoption designation target or identity is malformed", out Failure);
			string encoded = KingdomAdoptionDesignationRules.Encode(Receipt);
			if (encoded == null) return Fail("adoption designation receipt cannot be encoded", out Failure);
			KingdomAdoptionDesignationReceipt existing;
			if (Root.HasIntProperty(SchemaProperty))
				return TryRead(Root, out existing, out Failure)
					&& existing.Revision == Receipt.Revision;
			try
			{
				Root.RemoveIntProperty(SchemaProperty);
				Root.SetStringProperty(ReceiptProperty, encoded);
				Root.SetStringProperty(RevisionProperty, Receipt.Revision);
				Root.SetIntProperty(SchemaProperty, ReceiptSchema);
			}
			catch (Exception exception)
			{
				if (TryRead(Root, out existing, out _) && existing.Revision == Receipt.Revision)
					return true;
				return Fail("adoption designation publication remains retryable: "
					+ exception.Message, out Failure);
			}
			return TryRead(Root, out existing, out Failure)
				&& existing.Revision == Receipt.Revision;
		}

		public static bool TryRead(GameObject Root,
			out KingdomAdoptionDesignationReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (!GameObject.Validate(Root) || !Root.HasIntProperty(SchemaProperty)
				|| Root.HasStringProperty(SchemaProperty)
				|| Root.GetIntProperty(SchemaProperty) != ReceiptSchema
				|| !Root.HasStringProperty(ReceiptProperty) || Root.HasIntProperty(ReceiptProperty)
				|| !Root.HasStringProperty(RevisionProperty) || Root.HasIntProperty(RevisionProperty))
				return Fail("adoption designation receipt is absent or incomplete", out Failure);
			if (!KingdomAdoptionDesignationRules.TryDecode(Root.GetStringProperty(ReceiptProperty),
				out Receipt, out Failure)) return false;
			if (Receipt.RootId != Root.IDIfAssigned
				|| Receipt.Revision != Root.GetStringProperty(RevisionProperty))
				return Fail("adoption designation receipt disagrees with its marker", out Failure);
			return true;
		}

		/// <summary>Re-proves current local ground against one signed exact-room receipt.</summary>
		internal static bool TryReproveLocal(GameObject Root,
			KingdomAdoptionDesignationReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Root) || Receipt == null || Root.CurrentZone == null
				|| Root.CurrentCell == null || Root.CurrentZone.ZoneID != Receipt.ZoneId
				|| Root.IDIfAssigned != Receipt.RootId)
				return Fail("adoption designation marker is absent from its exact ground",
					out Failure);
			if (Receipt.ContainerOnly)
				return Receipt.Cells.Count == 1
					&& Receipt.Cells[0].X == Root.CurrentCell.X
					&& Receipt.Cells[0].Y == Root.CurrentCell.Y
					|| Fail("adopted container moved from its exact designated cell", out Failure);
			if (!KingdomData.TryGetBuilding(Receipt.BuildingKey,
				out KingdomRules.BuildEntry entry)
				|| !KingdomPlots.TryGetSpec(Receipt.BuildingKey,
					out KingdomPlotRules.PlotSpec spec))
				return Fail("adopted design or plot tier is no longer available", out Failure);
			if (Receipt.OpenPlot)
			{
				if (!KingdomAdoptabilityRules.TryClassify(Receipt.BuildingKey, entry.Category,
					spec.Size, spec.Open, out KingdomAdoptionTargetKind target, out Failure)
					|| target != KingdomAdoptionTargetKind.OpenPlot)
					return Failure != null ? false
						: Fail("adopted open-plot contract changed", out Failure);
				if (!KingdomAdoptionPlotRules.TryCenteredCells(Root.CurrentCell.X,
					Root.CurrentCell.Y, spec.Size, Root.CurrentZone.Width, Root.CurrentZone.Height,
					out _, out List<ArchitecturePoint> exact, out Failure)) return false;
				return KingdomAdoptRules.SameMembership(exact, Receipt.Cells)
					|| Fail("adopted open plot moved from its exact cells", out Failure);
			}
			KingdomAdoptRules.EnclosureMeasurement live = KingdomAdopt.MeasureExactRoom(
				Root.CurrentZone, Root.CurrentCell.X, Root.CurrentCell.Y);
			if (!live.Bounded || live.DoorCells < 1 || live.FloorCells == null
				|| !KingdomAdoptRules.MeetsMinimumUsable(
					KingdomAdoptRules.ClassifyRole(entry.Category), spec.Size, live))
				return Fail("adopted room no longer has enough safe usable floor", out Failure);
			if (!KingdomAdoptRules.SameMembership(live.FloorCells, Receipt.Cells))
				return Fail("adopted room is no longer the exact bounded room recorded", out Failure);
			return true;
		}

		public static void Clear(GameObject Root)
		{
			if (Root == null) return;
			KingdomAdoptionOperation.Clear(Root);
			ClearTyped(Root, SchemaProperty);
			ClearTyped(Root, RevisionProperty);
			ClearTyped(Root, ReceiptProperty);
		}

		private static void ClearTyped(GameObject Root, string Property)
		{
			Root.RemoveIntProperty(Property); Root.RemoveStringProperty(Property);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
