using System;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		/// <summary>Exact plan-marker tombstone; separate from later works-removal proof.</summary>
		public const string PlotPlanMarkerRemovalProofProperty = "r_TAF_PlotPlanMarkerRemoved";

		private static bool HasPlotPlanMarkerRemovalProof(GameObject Output, string MarkerId)
		{
			return GameObject.Validate(Output) && !string.IsNullOrEmpty(MarkerId)
				&& Output.GetStringProperty(PlotPlanMarkerRemovalProofProperty) == MarkerId;
		}

		/// <summary>Writes physical absence before callback-staled registry authority is re-read.</summary>
		private static bool TryProvePlotPlanMarkerRemoval(KingdomSystem System, Zone Z,
			GameObject Output, bool Final, string MarkerId,
			ref KingdomConstructionJob Current, out string Failure)
		{
			Failure = "The plot-plan output changed during marker removal.";
			if (Current == null || Current.Route != KingdomConstructionRoute.PlotPlan
				|| string.IsNullOrEmpty(MarkerId)
				|| FindConstructionResult(Z, Current, Final, out GameObject exact)
					!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(exact, Output)) return false;
			Output.SetStringProperty(PlotPlanMarkerRemovalProofProperty, MarkerId);
			if (!HasPlotPlanMarkerRemovalProof(Output, MarkerId))
			{
				Failure = "The plot output did not retain exact plan-marker removal proof.";
				return false;
			}
			if (!KingdomConstruction.TryFind(Current.Id, out KingdomConstructionJob refreshed)
				|| !SamePlotPlanProjection(Current, refreshed)
				|| refreshed.Phase != KingdomConstructionPhase.ProjectionPending
				|| !KingdomConstruction.Owns(System, Z, refreshed)
				|| !KingdomConstruction.IsCurrent(refreshed)
				|| FindConstructionResult(Z, refreshed, Final, out exact)
					!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(exact, Output)
				|| !HasPlotPlanMarkerRemovalProof(Output, MarkerId)) return false;
			Current = refreshed;
			return true;
		}

		private static bool TryCopyPlotPlanMarkerRemovalProof(GameObject Source,
			GameObject Destination)
		{
			string proof = Source?.GetStringProperty(PlotPlanMarkerRemovalProofProperty);
			if (string.IsNullOrEmpty(proof)) return true;
			Destination.SetStringProperty(PlotPlanMarkerRemovalProofProperty, proof);
			return Destination.GetStringProperty(PlotPlanMarkerRemovalProofProperty) == proof;
		}

		private static bool PlotPlanMarkerRemovalProofMatches(GameObject Source,
			GameObject Destination)
		{
			string proof = Source?.GetStringProperty(PlotPlanMarkerRemovalProofProperty);
			return string.IsNullOrEmpty(proof)
				|| HasPlotPlanMarkerRemovalProof(Destination, proof);
		}

		private static bool SamePlotPlanProjection(KingdomConstructionJob Expected,
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
	}
}
