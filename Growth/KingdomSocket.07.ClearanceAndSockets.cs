using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomSocket
	{
		internal const int SocketLotSchema = 1;
		internal const string SocketLotSchemaProperty = "r_TAF_SocketLotSchema";
		internal const string SocketLotTypeProperty = "r_TAF_SocketLotType";
		internal const string SocketLotSizeProperty = "r_TAF_SocketLotSize";
		internal const string SocketLotFacingProperty = "r_TAF_SocketLotFacing";

		private static bool HasStrikePlotParts(Zone Z, KingdomPlotRules.PlotRect Rect,
			string PlotId)
		{
			KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
			if (active != null)
			{
				for (int i = 0; i < active.PlotParts.Count; i++)
				{
					GameObject item = active.PlotParts[i];
					Cell cell = item?.CurrentCell;
					if (GameObject.Validate(item) && cell != null
						&& cell.X >= Rect.X1 && cell.X <= Rect.X2
						&& cell.Y >= Rect.Y1 && cell.Y <= Rect.Y2
						&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == PlotId)
						return true;
				}
				return false;
			}
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null) continue;
					List<GameObject> objects = cell.GetObjects();
					for (int i = 0; i < objects.Count; i++)
					{
						GameObject item = objects[i];
						if (GameObject.Validate(item)
							&& item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1
							&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == PlotId)
							return true;
					}
				}
			}
			return false;
		}

		private static bool ExactConversionOutput(GameObject Output, Zone Z,
			KingdomConstructionJob Job)
		{
			if (Z == null || Job == null || !KingdomPlots.TryDecodePlotPayload(Job.Payload,
				out KingdomPlotRules.PlotRect rect, out _,
				out KingdomArchitectureIntent architecture, out bool legacyArchitecture, out _)
				|| (!legacyArchitecture && (architecture == null
					|| architecture.BuildKey != Job.TargetKey
					|| Job.X != architecture.MainWorldX || Job.Y != architecture.MainWorldY))
				|| (legacyArchitecture && (Job.X != rect.CenterX || Job.Y != rect.CenterY))
				|| !GameObject.Validate(Output) || Output.IDIfAssigned != Job.OutputId
				|| Output.CurrentZone != Z || !KingdomConstruction.HasReceipt(Output, Job)
				|| !KingdomPlots.ExpectedArchitectureReceipt(Output, Z.GetCell(Job.X, Job.Y),
					Job.TargetKey, architecture, legacyArchitecture))
				return false;
			r_KingdomPlotWorks works = Output.GetPart<r_KingdomPlotWorks>();
			return (works != null && works.DesignKey == Job.TargetKey)
				|| (Output.GetIntProperty("KingdomBuilt") == 1
					&& Output.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == Job.TargetKey);
		}

		private static bool ExactSocketOutput(GameObject Output, Zone Z,
			KingdomPlotRules.PlotRect Rect, KingdomStrikeIntent Intent,
			KingdomConstructionJob Job)
		{
			r_KingdomSocket socket = GameObject.Validate(Output)
				? Output.GetPart<r_KingdomSocket>() : null;
			if (socket == null || Output.IDIfAssigned != Job.OutputId || Output.CurrentZone != Z
				|| Output.CurrentCell != Z.GetCell(Rect.CenterX, Rect.CenterY)
				|| socket.LastDesignKey != Intent.BuildKey
				|| !KingdomConstruction.HasReceipt(Output, Job)
				|| !KingdomPlots.TryReadRect(Output, out var observed)
				|| !SocketLotMatches(Output, Intent)) return false;
			return observed.X1 == Rect.X1 && observed.Y1 == Rect.Y1
				&& observed.X2 == Rect.X2 && observed.Y2 == Rect.Y2;
		}

		/// <summary>Compatibility event hook. It never mutates strike topology; the durable
		/// construction inspector alone resumes the published callback receipt.</summary>
		public static bool OnCleared(KingdomSystem System, Zone Z, GameObject Building)
		{
			if (System == null || Z == null || !GameObject.Validate(Building)
				|| Building.CurrentZone != Z) return false;
			string receipt = Building.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(receipt)
				|| !KingdomConstruction.TryFind(receipt, out var construction)
				|| !KingdomConstruction.Owns(System, Z, construction)
				|| KingdomConstructionRules.IsTerminal(construction.Phase)
				|| (construction.Route != KingdomConstructionRoute.Strike
					&& construction.Route != KingdomConstructionRoute.SocketConvert)
				|| construction.SourceId != Building.IDIfAssigned
				|| construction.PhysicalPhase == KingdomPhysicalPhase.None
				|| !KingdomConstructionRules.TryDecodeStrikeIntent(
					construction.PhysicalReceipt, out var intent)) return false;
			if (intent.HasPlot)
			{
				if (!KingdomPlots.TryReadRect(Building, out var rect)
					|| rect.X1 != intent.X1 || rect.Y1 != intent.Y1
					|| rect.X2 != intent.X2 || rect.Y2 != intent.Y2
					|| Building.GetStringProperty(KingdomPlots.PlotIdProperty) != intent.PlotId)
					return false;
			}
			// Legacy hook owns no destructive mutation. Durable strike inspector alone may advance.
			KingdomMaterials.InspectConstruction(System, Z, construction);
			return false;
		}

		/// <summary>Reads a schema-last typed-lot promise without consulting current catalogue data.
		/// A marker with none of the four properties is an honest save-era untyped socket; a partial,
		/// wrong-typed, or unknown-schema promise is malformed and freezes rebuilding.</summary>
		internal static bool TryReadSocketLot(GameObject Marker, out string LotType,
			out ArchitectureLotSize LotSize, out ArchitectureFacing Facing, out bool Legacy,
			out string Failure)
		{
			LotType = null;
			LotSize = default(ArchitectureLotSize);
			Facing = default(ArchitectureFacing);
			Legacy = false;
			Failure = null;
			if (!GameObject.Validate(Marker))
			{
				Failure = "There is no cleared lot there.";
				return false;
			}
			bool schemaInt = Marker.HasIntProperty(SocketLotSchemaProperty);
			bool schemaString = Marker.HasStringProperty(SocketLotSchemaProperty);
			bool typeString = Marker.HasStringProperty(SocketLotTypeProperty);
			bool typeInt = Marker.HasIntProperty(SocketLotTypeProperty);
			bool sizeInt = Marker.HasIntProperty(SocketLotSizeProperty);
			bool sizeString = Marker.HasStringProperty(SocketLotSizeProperty);
			bool facingInt = Marker.HasIntProperty(SocketLotFacingProperty);
			bool facingString = Marker.HasStringProperty(SocketLotFacingProperty);
			if (!schemaInt && !schemaString && !typeString && !typeInt
				&& !sizeInt && !sizeString && !facingInt && !facingString)
			{
				Legacy = true;
				return true;
			}
			int rawSize = Marker.GetIntProperty(SocketLotSizeProperty);
			int rawFacing = Marker.GetIntProperty(SocketLotFacingProperty);
			string type = Marker.GetStringProperty(SocketLotTypeProperty);
			if (!schemaInt || schemaString || Marker.GetIntProperty(SocketLotSchemaProperty)
					!= SocketLotSchema || !typeString || typeInt || !sizeInt || sizeString
				|| !facingInt || facingString || string.IsNullOrEmpty(type)
				|| type.Length > KingdomArchitectureRules.MaxKeyChars || type != type.Trim()
				|| rawSize < (int)ArchitectureLotSize.Small
				|| rawSize > (int)ArchitectureLotSize.Huge
				|| rawFacing < (int)ArchitectureFacing.North
				|| rawFacing > (int)ArchitectureFacing.West
				|| !KingdomArchitectureRules.TryClassifySetChange(type,
					(ArchitectureLotSize)rawSize, type, (ArchitectureLotSize)rawSize,
					out ArchitectureSetChange exact)
				|| exact != ArchitectureSetChange.SameSet)
			{
				Failure = "The cleared lot's typed-lot receipt is incomplete, contradictory, or unknown.";
				return false;
			}
			LotType = type;
			LotSize = (ArchitectureLotSize)rawSize;
			Facing = (ArchitectureFacing)rawFacing;
			return true;
		}

		internal static bool TryStampSocketLot(GameObject Marker, KingdomStrikeIntent Intent,
			out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Marker) || Intent == null)
			{
				Failure = "The cleared lot has no exact typed-lot target.";
				return false;
			}
			if (!Intent.HasTypedLot) return true;
			if (!Intent.HasPlot || string.IsNullOrEmpty(Intent.LotType)
				|| Intent.LotType.Length > KingdomArchitectureRules.MaxKeyChars
				|| (int)Intent.LotSize < (int)ArchitectureLotSize.Small
				|| (int)Intent.LotSize > (int)ArchitectureLotSize.Huge
				|| (int)Intent.Facing < (int)ArchitectureFacing.North
				|| (int)Intent.Facing > (int)ArchitectureFacing.West)
			{
				Failure = "The strike's typed-lot promise is malformed.";
				return false;
			}
			try
			{
				Marker.RemoveIntProperty(SocketLotSchemaProperty);
				Marker.RemoveStringProperty(SocketLotSchemaProperty);
				Marker.SetStringProperty(SocketLotTypeProperty, Intent.LotType);
				Marker.SetIntProperty(SocketLotSizeProperty, (int)Intent.LotSize);
				Marker.SetIntProperty(SocketLotFacingProperty, (int)Intent.Facing);
				Marker.SetIntProperty(SocketLotSchemaProperty, SocketLotSchema);
			}
			catch (Exception exception)
			{
				try { Marker.RemoveIntProperty(SocketLotSchemaProperty); } catch { }
				Failure = "The cleared lot's typed-lot receipt could not be written: "
					+ exception.Message;
				return false;
			}
			if (!TryReadSocketLot(Marker, out string type, out ArchitectureLotSize size,
				out ArchitectureFacing facing, out bool legacy, out Failure)
				|| legacy || type != Intent.LotType || size != Intent.LotSize
				|| facing != Intent.Facing)
			{
				if (Failure == null) Failure = "The cleared lot's typed-lot receipt changed while it was written.";
				return false;
			}
			return true;
		}

		internal static bool SocketLotMatches(GameObject Marker, KingdomStrikeIntent Intent)
		{
			if (Intent == null || !TryReadSocketLot(Marker, out string type,
				out ArchitectureLotSize size, out ArchitectureFacing facing,
				out bool legacy, out _)) return false;
			return Intent.HasTypedLot
				? !legacy && type == Intent.LotType && size == Intent.LotSize
					&& facing == Intent.Facing
				: legacy;
		}

		internal static bool SocketAcceptsArchitecture(GameObject Marker,
			KingdomArchitectureIntent Architecture, out string Failure)
		{
			if (!TryReadSocketLot(Marker, out string type, out ArchitectureLotSize size,
				out ArchitectureFacing facing, out bool legacy, out Failure)) return false;
			if (legacy) return true;
			if (Architecture == null || Architecture.LotType != type
				|| Architecture.LotSize != size || Architecture.Facing != facing)
			{
				Failure = "The prepared plan changes the cleared lot's frozen type, size, or facing. Rebuild this exact lot, or order a full re-type while a predecessor still stands so strike and fresh siting can be frozen together.";
				return false;
			}
			return true;
		}

		internal static string SocketLotLabel(GameObject Marker)
		{
			if (!TryReadSocketLot(Marker, out string type, out ArchitectureLotSize size,
				out ArchitectureFacing facing, out bool legacy, out _))
				return "a cleared lot with an unreadable typed-lot receipt";
			if (legacy)
			{
				if (KingdomPlots.TryReadRect(Marker, out KingdomPlotRules.PlotRect rect)
					&& KingdomSocketRules.TryActualSize(rect.Width, rect.Height,
						out KingdomPlotRules.PlotSize actual))
					return "a legacy cleared lot (" + SocketSizeName((ArchitectureLotSize)(int)actual)
						+ "; type and facing unrecorded)";
				return "a legacy cleared lot (type, size, and facing unrecorded)";
			}
			return "a cleared " + type + " lot (" + SocketSizeName(size) + ", facing "
				+ facing.ToString().ToLowerInvariant() + ")";
		}

		private static string SocketSizeName(ArchitectureLotSize Size)
		{
			switch (Size)
			{
			case ArchitectureLotSize.Small: return "S";
			case ArchitectureLotSize.Medium: return "M";
			case ArchitectureLotSize.Large: return "L";
			case ArchitectureLotSize.Huge: return "XL";
			default: return "?";
			}
		}
	}
}
