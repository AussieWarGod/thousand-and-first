using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlanMarker
	{
		private static GameObject FindExactPlanMarker(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job,
			KingdomRules.BuildEntry Entry)
		{
			GameObject marker;
			if (KingdomConstruction.FindExactId(Z, Job?.SourceId ?? Job?.SubjectId,
				out marker) != KingdomPhysicalLookupState.Exact) return null;
			Cell cell = Z == null || Job == null ? null : Z.GetCell(Job.X, Job.Y);
			if (!KingdomConstruction.Owns(System, Z, Job)
				|| !KingdomConstruction.IsCurrent(Job)
				|| !IsExactPlanMarker(marker, Z, cell, Job, Entry, false)) return null;
			if (string.IsNullOrEmpty(marker.GetStringProperty(KingdomConstruction.ReceiptProperty)))
				KingdomConstruction.Bind(marker, Job);
			return IsExactPlanMarker(marker, Z, cell, Job, Entry, true) ? marker : null;
		}

		private static bool IsExactPlanMarker(GameObject Marker, Zone Z, Cell Cell,
			KingdomConstructionJob Job, KingdomRules.BuildEntry Entry, bool RequireReceipt)
		{
			if (!GameObject.Validate(Marker) || Z == null || Cell == null || Job == null
				|| Entry == null || Marker.IDIfAssigned != (Job.SourceId ?? Job.SubjectId)
				|| Marker.CurrentZone != Z
				|| Marker.CurrentCell != Cell || Cell != Z.GetCell(Job.X, Job.Y)) return false;
			r_KingdomPlanMarker marker = Marker.GetPart<r_KingdomPlanMarker>();
			string receipt = Marker.GetStringProperty(KingdomConstruction.ReceiptProperty);
			return marker != null && marker.DesignKey == Entry.Key
				&& (RequireReceipt ? receipt == Job.Id
					: string.IsNullOrEmpty(receipt) || receipt == Job.Id);
		}

		private static bool IsExactPlanScaffold(GameObject Scaffold, Zone Z, Cell Cell,
			KingdomConstructionJob Job, KingdomRules.BuildEntry Entry)
		{
			if (!GameObject.Validate(Scaffold) || Z == null || Cell == null || Job == null
				|| Entry == null || Scaffold.CurrentZone != Z || Scaffold.CurrentCell != Cell
				|| Cell != Z.GetCell(Job.X, Job.Y)
				|| Scaffold.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Entry.Key
				|| !KingdomConstruction.HasReceipt(Scaffold, Job)) return false;
			r_KingdomScaffold scaffold = Scaffold.GetPart<r_KingdomScaffold>();
			return scaffold != null && scaffold.TargetBlueprint == Entry.Blueprint
				&& (KingdomConstruction.BuildTruthMatches(Scaffold, Job)
					|| KingdomConstruction.LegacyProjectedBuildTruthMatches(
						Scaffold, Job, false));
		}

		private static GameObject FindPlanScaffold(Zone Z, KingdomConstructionJob Job,
			KingdomRules.BuildEntry Entry)
		{
			if (Z == null || Job == null || Entry == null) return null;
			Cell cell = Z.GetCell(Job.X, Job.Y);
			if (cell == null) return null;
			GameObject found = null;
			GameObject exact = null;
			int count = 0;
			foreach (GameObject item in cell.GetObjects())
			{
				if (IsExactPlanScaffold(item, Z, cell, Job, Entry))
				{
					count++;
					if (found == null) found = item;
					if (item.IDIfAssigned == Job.OutputId
						|| item.IDIfAssigned == Job.SubjectId) exact = item;
				}
			}
			GameObject global;
			return count == 1 && exact != null
				&& KingdomConstruction.FindExactId(Z, exact.IDIfAssigned, out global)
					== KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(global, exact) ? exact : null;
		}

		private static bool RemoveCreated(GameObject Object, Zone Z)
		{
			try
			{
				return !GameObject.Validate(Object)
					|| (Object.Obliterate(null, Silent: true) && !GameObject.Validate(Object));
			}
			catch
			{
				return false;
			}
			finally
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, Object);
			}
		}

		/// <summary>Persists physical marker absence before any callback-staled registry reproof.</summary>
		private static bool TryProveMarkerRemoval(KingdomSystem System, Zone Z,
			GameObject Scaffold, Cell Expected, KingdomRules.BuildEntry Entry, string MarkerId,
			ref KingdomConstructionJob Current, out string Failure)
		{
			Failure = "The planned scaffold changed during marker removal.";
			if (string.IsNullOrEmpty(MarkerId)
				|| KingdomConstruction.FindExactId(Z, Scaffold?.IDIfAssigned, out GameObject exact)
					!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(exact, Scaffold)
				|| !IsExactPlanScaffold(Scaffold, Z, Expected, Current, Entry)) return false;
			Scaffold.SetStringProperty(r_KingdomScaffold.RemovalProofProperty, MarkerId);
			if (!r_KingdomScaffold.HasRemovalProof(Scaffold, MarkerId))
			{
				Failure = "The planned scaffold did not retain marker-removal proof.";
				return false;
			}
			if (!KingdomConstruction.TryFind(Current.Id, out KingdomConstructionJob refreshed)
				|| !SamePlanProjection(Current, refreshed)
				|| refreshed.Phase != KingdomConstructionPhase.ProjectionPending
				|| !KingdomConstruction.Owns(System, Z, refreshed)
				|| !KingdomConstruction.IsCurrent(refreshed)
				|| !IsExactPlanScaffold(Scaffold, Z, Expected, refreshed, Entry)
				|| !r_KingdomScaffold.HasRemovalProof(Scaffold, MarkerId)) return false;
			Current = refreshed;
			return true;
		}

		private static bool SamePlanProjection(KingdomConstructionJob Expected,
			KingdomConstructionJob Observed)
		{
			return Expected != null && Observed != null && Expected.Id == Observed.Id
				&& Expected.OwnerKey == Observed.OwnerKey && Expected.ZoneId == Observed.ZoneId
				&& Expected.Route == Observed.Route && Expected.Projection == Observed.Projection
				&& Expected.X == Observed.X && Expected.Y == Observed.Y
				&& Expected.SubjectId == Observed.SubjectId && Expected.SourceId == Observed.SourceId
				&& Expected.OutputId == Observed.OutputId && Expected.TargetKey == Observed.TargetKey
				&& Expected.Payload == Observed.Payload
				&& Expected.BuildTruthSchema == Observed.BuildTruthSchema
				&& Expected.BuildHasPlot == Observed.BuildHasPlot
				&& Expected.BuildFrontier == Observed.BuildFrontier
				&& Expected.BuildDefence == Observed.BuildDefence;
		}

		private static int CountPlanScaffolds(Zone Z, KingdomConstructionJob Job,
			KingdomRules.BuildEntry Entry)
		{
			if (Z == null || Job == null || Entry == null) return 0;
			Cell cell = Z?.GetCell(Job.X, Job.Y);
			if (cell == null) return 0;
			int count = 0;
			foreach (GameObject item in cell.GetObjects())
			{
				if (IsExactPlanScaffold(item, Z, cell, Job, Entry))
					{
						if (item.IDIfAssigned != Job.OutputId
							&& item.IDIfAssigned != Job.SubjectId) return 2;
						count++;
					}
			}
			return count;
		}

	}
}
