using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomDelveLink
	{

		private static bool TryDerive(KingdomArchitectureIntent Architecture, Zone Head,
			string RootId, string LotId, out Derived Result, out string Failure)
		{
			Result = null;
			Failure = null;
			if (Architecture == null || Head == null || Head.ZoneID == null
				|| !KingdomDelveRules.IsDelve(Architecture.BuildKey))
				return Fail("delve link has no frozen delve architecture or exact head zone", out Failure);
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomArchitectureRuntime.TryDecode(Architecture, out snapshot, out Failure)) return false;
			ArchitecturePlacement down = null;
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = snapshot.Placements[i];
				bool anyStair = placement.Blueprint == DownBlueprint
					|| placement.Blueprint == UpBlueprint || placement.Blueprint == "StairsDown"
					|| placement.Blueprint == "StairsUp";
				if (!anyStair) continue;
				if (down != null || placement.Blueprint != DownBlueprint
					|| placement.Layer != ArchitectureLayer.Object
					|| !(placement.StatefulAnchor == "travel:down"
						|| (placement.StatefulAnchor != null
							&& placement.StatefulAnchor.StartsWith("travel:down@",
								StringComparison.Ordinal))))
					return Fail("frozen delve must own exactly one stateful Down and no same-map Up",
						out Failure);
				down = placement;
			}
			if (down == null)
				return Fail("frozen delve has no runtime-owned Down placement", out Failure);
			int x;
			int y;
			if (!KingdomArchitectureRuntime.TryWorldPlacement(snapshot, Architecture.Rect, down,
				out x, out y, out Failure)) return false;
			string foot;
			if (!KingdomDelveRules.TryFootZoneId(Head.ZoneID, out foot))
				return Fail("head zone has no canonical one-stratum foot", out Failure);
			Derived result = new Derived
			{
				Architecture = Architecture,
				Snapshot = snapshot,
				Down = down,
				HeadZoneId = Head.ZoneID,
				FootZoneId = foot,
				RootId = RootId,
				LotId = LotId,
				X = x,
				Y = y
			};
			if (RootId != null)
			{
				if (!KingdomDelveLinkRules.TryToken(result.HeadZoneId, result.FootZoneId,
					result.X, result.Y, RootId, LotId, Architecture.SnapshotHash,
					down.Slot, out result.Token, out Failure)) return false;
			}
			Result = result;
			return true;
		}

		private static bool TryLoadBuiltFoot(Zone Head, Derived Derived, out Zone Foot,
			out string Failure)
		{
			Foot = null;
			Failure = null;
			if (The.ZoneManager == null || !The.ZoneManager.IsZoneBuilt(Derived.FootZoneId))
				return Fail("the exact lower zone is no longer built", out Failure);
			try { Foot = The.ZoneManager.GetZone(Derived.FootZoneId); }
			catch (Exception exception)
			{
				return Fail("the already-built lower zone could not be loaded: " + exception.Message,
					out Failure);
			}
			if (!ExactZonePair(Head, Foot, Derived))
				return Fail("loaded lower zone does not match the frozen shaft column", out Failure);
			return true;
		}

		private static bool ExactZonePair(Zone Head, Zone Foot, Derived Derived)
		{
			return Head != null && Foot != null && Head.ZoneID == Derived.HeadZoneId
				&& Foot.ZoneID == Derived.FootZoneId
				&& Head.Built && Foot.Built
				&& Head.Width == Foot.Width && Head.Height == Foot.Height
				&& Derived.X >= 0 && Derived.X < Head.Width && Derived.X < Foot.Width
				&& Derived.Y >= 0 && Derived.Y < Head.Height && Derived.Y < Foot.Height
				&& KingdomDelveRules.IsShaftPair(Head.ZoneID, Foot.ZoneID);
		}

		private static bool TrySafeFoot(KingdomSystem System, Zone Foot, Derived Derived,
			GameObject ExpectedEndpoint, out string Failure)
		{
			Failure = null;
			if (Foot == null) return Fail("lower landing zone is absent", out Failure);
			Cell cell = Foot.GetCell(Derived.X, Derived.Y);
			if (cell == null || !cell.IsPassable() || cell.HasOpenLiquidVolume() || cell.HasWall()
				|| cell.HasObjectWithPart("StairsDown")
				|| (ExpectedEndpoint == null && cell.HasObjectWithPart("StairsUp")))
				return Fail("the exact lower landing contains wall, liquid, or foreign stairs",
					out Failure);
			if (System != null && KingdomConstruction.HasActiveAt(System, Foot, cell))
				return Fail("active paid construction reserves the exact lower landing", out Failure);
			List<GameObject> objects = cell.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (!GameObject.Validate(item) || ReferenceEquals(item, ExpectedEndpoint)) continue;
				if (item.IsPlayer() || item.IsCreature)
					return Fail("a living occupant stands on the exact lower landing", out Failure);
				if (item.Inventory != null || item.GetPart<LiquidVolume>() != null
					|| item.IsTakeable() || item.IsOwned() || item.IsWall() || item.IsDoor()
					|| item.GetIntProperty("KingdomBuilt") == 1
					|| item.GetIntProperty("KingdomCitizen") == 1
					|| item.GetIntProperty("KingdomStores") == 1
					|| item.GetIntProperty("KingdomLarder") == 1
					|| item.GetIntProperty(KingdomMaterials.StockpileProperty) == 1
					|| item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1)
					return Fail("protected, stateful, liquid, or third-party property occupies the lower landing",
						out Failure);
				GameObjectBlueprint blueprint = item.GetBlueprint();
				if (blueprint == null || !blueprint.InheritsFrom("Floor"))
					return Fail("the lower landing contains non-floor object "
						+ (item.Blueprint ?? "<unknown>"), out Failure);
			}
			return true;
		}

		private static bool TryFindHeadEndpoint(Zone Head, Derived Derived,
			out GameObject Endpoint, out string Failure)
		{
			Endpoint = null;
			Failure = null;
			int count = 0;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Head) ?? KingdomSurvey.Take(Head);
			List<GameObject> objects = survey.ArchitectureComponents;
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (!GameObject.Validate(item)
					|| item.GetStringProperty(KingdomPlots.PlotIdProperty) != Derived.LotId
					|| item.GetStringProperty(KingdomArchitectureStamper.ComponentSlotProperty)
						!= Derived.Down.Slot) continue;
				count++;
				Endpoint = item;
			}
			StairsDown stairs = Endpoint == null ? null : Endpoint.GetPart<StairsDown>();
			if (count != 1 || Endpoint.Blueprint != DownBlueprint
				|| Endpoint.CurrentCell != Head.GetCell(Derived.X, Derived.Y)
				|| Endpoint.GetIntProperty(KingdomArchitectureStamper.ComponentSchemaProperty)
					!= KingdomArchitectureStamper.ComponentSchema
				|| Endpoint.GetIntProperty(KingdomArchitectureStamper.ComponentLayerProperty)
					!= (int)ArchitectureLayer.Object
				|| Endpoint.GetStringProperty(KingdomArchitectureStamper.ComponentAnchorProperty)
					!= Derived.Down.StatefulAnchor
				|| Endpoint.GetStringProperty(KingdomArchitectureStamper.ComponentHashProperty)
					!= Derived.Architecture.SnapshotHash
				|| stairs == null || !stairs.Connected || stairs.ConnectionObject != UpBlueprint)
			{
				Endpoint = null;
				return Fail("authored delve Down is absent, duplicated, moved, or changed", out Failure);
			}
			return true;
		}
	}
}
