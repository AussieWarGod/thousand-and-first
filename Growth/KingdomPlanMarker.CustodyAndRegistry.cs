using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPlanMarker
	{
		private static bool ExactAuthority(KingdomSystem System, Zone Z, string Owner)
		{
			return The.Game != null && System != null && System.Founded && Z != null
				&& ReferenceEquals(The.Game.GetSystem<KingdomSystem>(), System)
				&& System.ClaimedZones != null && System.ClaimedZones.Contains(Z.ZoneID)
				&& !string.IsNullOrEmpty(Owner) && KingdomConstruction.OwnerOf(System) == Owner;
		}

		private static bool BasicDirectGround(GameObject Marker, Zone Z, Cell Cell,
			out int DirectReferences)
		{
			DirectReferences = 0;
			if (!GameObject.Validate(Marker) || Z == null || Cell == null || Marker.Count != 1
				|| Marker.HasPart("Stacker") || Marker.InInventory != null || Marker.Equipped != null
				|| Marker.CurrentZone != Z || Marker.CurrentCell != Cell
				|| Cell.ParentZone != Z || Z.GetCell(Cell.X, Cell.Y) != Cell) return false;
			foreach (GameObject item in Cell.GetObjects())
				if (ReferenceEquals(item, Marker)) DirectReferences++;
			return DirectReferences == 1;
		}

		private static bool ExactDirectGround(GameObject Marker, Zone Z, Cell Cell, string Id)
		{
			bool basic = BasicDirectGround(Marker, Z, Cell, out int direct);
			KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(Z, Id,
				out GameObject exact);
			return KingdomConstructionRules.PlanMarkerDirectGroundProved(
				GameObject.Validate(Marker), Marker?.Count ?? 0,
				Marker != null && Marker.HasPart("Stacker"), Marker?.InInventory != null,
				Marker?.Equipped != null, basic && Marker.CurrentZone == Z,
				basic && Marker.CurrentCell == Cell, direct, state, ReferenceEquals(exact, Marker));
		}

		private static void ReadPlanShape(GameObject Marker, string Design, int StakeX, int StakeY,
			bool RequireWorld, KingdomPlanMarkerProof Proof)
		{
			Proof.PlannedSkinExact = !Marker.HasIntProperty(KingdomDesign.PlannedSkinProperty)
				&& (!Marker.HasStringProperty(KingdomDesign.PlannedSkinProperty)
					|| !string.IsNullOrEmpty(Marker.GetStringProperty(
						KingdomDesign.PlannedSkinProperty)));
			Proof.PlannedSkin = Marker.GetStringProperty(KingdomDesign.PlannedSkinProperty);
			Proof.PlotReceiptFragments = HasPlotReceiptFragments(Marker);
			if (!KingdomData.TryGetBuilding(Design, out KingdomRules.BuildEntry entry)
				|| !ExactPlotReceiptTypes(Marker)
				|| !KingdomPlots.TryReadFrozenPlan(Marker, entry, RequireWorld,
					out KingdomPlotRules.PlotRect rect, out string payload, out _, out _, out _, out _)
				|| !KingdomPlots.TryDecodePlotPayload(payload, out KingdomPlotRules.PlotRect decoded,
					out _, out KingdomArchitectureIntent architecture, out bool legacy, out _)
				|| legacy || architecture == null || architecture.BuildKey != Design
				|| decoded.X1 != rect.X1 || decoded.Y1 != rect.Y1
				|| decoded.X2 != rect.X2 || decoded.Y2 != rect.Y2
				|| rect.Contains(StakeX, StakeY)
				|| !rect.Contains(architecture.MainWorldX, architecture.MainWorldY)) return;
			Proof.PlotReceiptExact = true;
			Proof.PlotPayload = payload;
			Proof.PlotRect = rect;
			Proof.MainX = architecture.MainWorldX;
			Proof.MainY = architecture.MainWorldY;
		}

		private static bool TryBuildProof(KingdomSystem System, Zone Z, GameObject Marker,
			out KingdomPlanMarkerProof Proof, out string Failure)
		{
			Proof = null;
			Failure = "The plan lacks exact current provenance and direct-ground custody.";
			if (!TryReadProvenance(Marker, out string owner, out string zoneId,
				out int x, out int y, out string design) || zoneId != Z?.ZoneID
				|| !ExactAuthority(System, Z, owner) || x >= Z.Width || y >= Z.Height) return false;
			Cell cell = Z.GetCell(x, y);
			string id = Marker.IDIfAssigned;
			if (string.IsNullOrEmpty(id) || !ExactDirectGround(Marker, Z, cell, id)
				|| !TryCaptureFrozenBytes(Marker, out string frozen)) return false;
			KingdomPlanMarkerProof proof = new KingdomPlanMarkerProof
			{
				Zone = Z, Cell = cell, MarkerId = id, OwnerKey = owner, ZoneId = zoneId,
				DesignKey = design, StakeX = x, StakeY = y, FrozenBytes = frozen
			};
			proof.ReceiptShape = ReceiptShape(Marker, out proof.ReceiptId);
			ReadPlanShape(Marker, design, x, y, true, proof);
			Proof = proof;
			Failure = null;
			return true;
		}

		private static bool RouteMatches(KingdomPlanMarkerProof Proof,
			KingdomConstructionJob Job)
		{
			if (Proof == null || Job == null
				|| Job.BuildTruthSchema != KingdomConstructionRules.BuildTruthSchema
				|| Job.BuildHasPlot != (Job.Route == KingdomConstructionRoute.PlotPlan)) return false;
			bool coordinates = KingdomConstructionRules.PlanMarkerRouteCoordinatesValid(Job.Route,
				Proof.StakeX, Proof.StakeY, Proof.PlotReceiptExact,
				Proof.PlotReceiptExact && !Proof.PlotRect.Contains(Proof.StakeX, Proof.StakeY),
				Proof.MainX, Proof.MainY, Job.X, Job.Y);
			if (!coordinates) return false;
			if (Job.Route == KingdomConstructionRoute.PlanScaffold)
				return !Proof.PlotReceiptFragments && Proof.PlannedSkinExact
					&& Job.Payload == Proof.PlannedSkin;
			return Job.Route == KingdomConstructionRoute.PlotPlan
				&& Job.Payload == Proof.PlotPayload;
		}

		private static bool RegistryAllows(KingdomPlanMarkerProof Proof, bool HasReceipt,
			string Receipt, out string Failure)
		{
			if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out Failure))
				return false;
			if (KingdomConstructionRules.PlanMarkerCancellationAllowed(jobs, HasReceipt, Receipt,
				Proof.MarkerId, Proof.OwnerKey, Proof.ZoneId, Proof.DesignKey,
				job => RouteMatches(Proof, job))) return true;
			Failure = "This plan has active, paid, foreign, mismatched, or unproved construction work.";
			return false;
		}

		private static KingdomPlanReceiptShape ReceiptShape(GameObject Marker, out string Receipt)
		{
			Receipt = Marker?.GetStringProperty(KingdomConstruction.ReceiptProperty);
			return KingdomConstructionRules.PlanMarkerReceiptShape(
				Marker != null && Marker.HasStringProperty(KingdomConstruction.ReceiptProperty),
				Receipt, Marker != null && Marker.HasIntProperty(KingdomConstruction.ReceiptProperty));
		}

		internal static bool TryPrepareNewMarker(KingdomSystem System, GameObject Marker, Zone Z,
			Cell Cell, string Design, out string Frozen, out string Failure)
		{
			Frozen = null;
			string owner = KingdomConstruction.OwnerOf(System);
			Failure = "The new plan could not freeze exact ownership and design provenance.";
			if (!ExactAuthority(System, Z, owner) || !GameObject.Validate(Marker) || Cell == null
				|| Cell.ParentZone != Z || Marker.CurrentCell != null || Marker.CurrentZone != null
				|| Marker.InInventory != null || Marker.Equipped != null || Marker.Count != 1
				|| Marker.HasPart("Stacker") || string.IsNullOrEmpty(Marker.IDIfAssigned)
				|| ReceiptShape(Marker, out _) != KingdomPlanReceiptShape.Absent
				|| Marker.GetPart<r_KingdomPlanMarker>()?.DesignKey != Design
				|| !TryStampProvenance(Marker, owner, Z.ZoneID, Cell.X, Cell.Y, Design, out Failure))
				return false;
			KingdomPlanMarkerProof proof = new KingdomPlanMarkerProof();
			ReadPlanShape(Marker, Design, Cell.X, Cell.Y, false, proof);
			bool plotted = KingdomPlots.IsPlotDesign(Design);
			if (!proof.PlannedSkinExact || (plotted ? !proof.PlotReceiptExact
				: proof.PlotReceiptFragments) || !TryCaptureFrozenBytes(Marker, out Frozen)
				|| !KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out Failure)
				|| !KingdomConstructionRules.PlanMarkerRegistryUnreferenced(
					jobs, Marker.IDIfAssigned))
			{
				ClearProvenance(Marker);
				Frozen = null;
				return false;
			}
			Failure = null;
			return true;
		}

		internal static bool EnsureLegacyProvenance(KingdomSystem System, Zone Z, GameObject Marker)
		{
			if (TryBuildProof(System, Z, Marker, out _, out _)) return true;
			r_KingdomPlanMarker part = GameObject.Validate(Marker)
				? Marker.GetPart<r_KingdomPlanMarker>() : null;
			if (!GameObject.Validate(Marker) || HasProvenanceFragments(Marker)
				|| ReceiptShape(Marker, out _) != KingdomPlanReceiptShape.Absent
				|| part == null || string.IsNullOrEmpty(part.DesignKey)
				|| !KingdomData.TryGetBuilding(part.DesignKey, out _)
				|| !ExactAuthority(System, Z, KingdomConstruction.OwnerOf(System))
				|| !BasicDirectGround(Marker, Z, Marker.CurrentCell, out _)) return false;
			string id = Marker.IDIfAssigned;
			if (string.IsNullOrEmpty(id)) id = Marker.ID;
			if (string.IsNullOrEmpty(id) || Marker.IDIfAssigned != id
				|| !ExactDirectGround(Marker, Z, Marker.CurrentCell, id)
				|| !KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out _)
				|| !KingdomConstructionRules.PlanMarkerRegistryUnreferenced(jobs, id)
				|| !TryStampProvenance(Marker, KingdomConstruction.OwnerOf(System), Z.ZoneID,
					Marker.CurrentCell.X, Marker.CurrentCell.Y, part.DesignKey, out _)
				|| !TryBuildProof(System, Z, Marker, out _, out _)) return false;
			KingdomSurvey.ObserveChangedInActive(Z, Marker);
			return true;
		}

		internal static bool PlacementProved(KingdomSystem System, GameObject Marker, Zone Z,
			Cell Cell, string Frozen)
		{
			bool direct = TryBuildProof(System, Z, Marker,
				out KingdomPlanMarkerProof proof, out _) && ReferenceEquals(proof.Cell, Cell);
			bool registry = direct
				&& KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out _)
				&& KingdomConstructionRules.PlanMarkerRegistryUnreferenced(
					jobs, proof.MarkerId);
			return KingdomConstructionRules.PlanMarkerPlacementCommitAllowed(direct,
				direct && proof.FrozenBytes == Frozen, ReceiptShape(Marker, out _), registry,
				direct && AuthorityStillExact(System, proof));
		}

		internal static bool TryDiscardDetached(KingdomSystem System, Zone Z,
			GameObject Marker, string Frozen)
		{
			if (!ExactAuthority(System, Z, KingdomConstruction.OwnerOf(System))
				|| !GameObject.Validate(Marker) || Marker.CurrentCell != null || Marker.CurrentZone != null
				|| Marker.InInventory != null || Marker.Equipped != null || Marker.Count != 1
				|| Marker.HasPart("Stacker") || !TryCaptureFrozenBytes(Marker, out string observed)
				|| observed != Frozen
				|| ReceiptShape(Marker, out _) != KingdomPlanReceiptShape.Absent
				|| !KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out _)
				|| !KingdomConstructionRules.PlanMarkerRegistryUnreferenced(
					jobs, Marker.IDIfAssigned)) return false;
			try { Marker.Obliterate(null, Silent: true); }
			catch { return false; }
			return !GameObject.Validate(Marker);
		}

		internal static bool TryDiscardUnplaced(GameObject Marker)
		{
			if (!GameObject.Validate(Marker) || Marker.CurrentCell != null || Marker.CurrentZone != null
				|| Marker.InInventory != null || Marker.Equipped != null || Marker.Count != 1
				|| Marker.HasPart("Stacker")) return false;
			try { Marker.Obliterate(null, Silent: true); }
			catch { return false; }
			return !GameObject.Validate(Marker);
		}

		internal static bool PublicationAllowed(KingdomSystem System, Zone Z, GameObject Marker,
			out KingdomPlanMarkerProof Proof, out string Failure)
		{
			if (!TryBuildProof(System, Z, Marker, out Proof, out Failure)
				|| ReceiptShape(Marker, out _) != KingdomPlanReceiptShape.Absent) return false;
			if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out Failure)
				|| !KingdomConstructionRules.PlanMarkerRegistryUnreferenced(
					jobs, Proof.MarkerId))
			{
				if (Failure == null)
					Failure = "This plan is still named by durable construction state.";
				return false;
			}
			return true;
		}

		internal static bool PreparedJobMatches(KingdomPlanMarkerProof Proof,
			KingdomConstructionJob Job)
		{
			return KingdomConstructionRules.ValidJob(Job) && Proof != null
				&& Job.SourceId == Proof.MarkerId && Job.SubjectId == Proof.MarkerId
				&& Job.OwnerKey == Proof.OwnerKey && Job.ZoneId == Proof.ZoneId
				&& Job.TargetKey == Proof.DesignKey && RouteMatches(Proof, Job);
		}

		internal static bool TryReleaseCleanReceipt(KingdomSystem System, Zone Z,
			GameObject Marker, out string Failure)
		{
			if (!TryBuildProof(System, Z, Marker, out KingdomPlanMarkerProof proof, out Failure)
				|| ReceiptShape(Marker, out string receipt) != KingdomPlanReceiptShape.Exact
				|| !RegistryAllows(proof, true, receipt, out Failure)) return false;
			Marker.RemoveStringProperty(KingdomConstruction.ReceiptProperty);
			if (ReceiptShape(Marker, out _) != KingdomPlanReceiptShape.Absent
				|| !TryBuildProof(System, Z, Marker, out proof, out Failure)
				|| !RegistryAllows(proof, false, null, out Failure)) return false;
			return true;
		}

		internal static bool AuthorityStillExact(KingdomSystem System,
			KingdomPlanMarkerProof Proof)
		{
			return Proof != null && ExactAuthority(System, Proof.Zone, Proof.OwnerKey)
				&& Proof.Zone.ZoneID == Proof.ZoneId;
		}

		internal static bool ReproveSurvivor(KingdomSystem System, GameObject Marker,
			KingdomPlanMarkerProof Expected, out bool RegistrySafe, out string Failure)
		{
			Failure = null;
			KingdomPlanReceiptShape shape = ReceiptShape(Marker, out string receipt);
			RegistrySafe = shape == Expected.ReceiptShape && receipt == Expected.ReceiptId
				&& RegistryAllows(Expected, shape == KingdomPlanReceiptShape.Exact,
					receipt, out Failure);
			return TryBuildProof(System, Expected.Zone, Marker, out KingdomPlanMarkerProof observed,
				out _) && observed.MarkerId == Expected.MarkerId
				&& observed.FrozenBytes == Expected.FrozenBytes
				&& shape == Expected.ReceiptShape && receipt == Expected.ReceiptId;
		}
	}
}
