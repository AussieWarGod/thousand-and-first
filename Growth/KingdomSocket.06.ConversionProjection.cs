using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomSocket
	{
		private static bool ProjectConvertOrder(KingdomSystem System, Zone Z,
			GameObject Building, string NewKey, string NewSkinKey, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			if (!GameObject.Validate(Building) || Building.CurrentZone != Z)
			{
				KingdomConstruction.FinishProjection(ref Updated, false, false,
					"The conversion's predecessor is absent.");
				return false;
			}
			if (KingdomConstruction.HasReceipt(Building, Job)
				&& Building.GetIntProperty(KingdomMaterials.StrikeEffortProperty) > 0
				&& Building.GetStringProperty(PendingConvertKeyProperty) == NewKey)
			{
				KingdomConstruction.FinishProjection(ref Updated, true, true);
				return true;
			}
			if (!KingdomConstructionRules.TryReadBuildTruth(Job, out bool hasPlot,
				out bool frontier, out _) || !hasPlot || frontier)
			{
				Failure = "The unprojected legacy conversion predates frozen plotted build effects.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (!KingdomConstruction.BeginProjection(ref Updated, out Failure))
			{
				return false;
			}
			if (!KingdomMaterials.OrderStrikeForConstruction(System, Z, Building, Updated,
				out Updated, out Failure))
			{
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			Building.SetStringProperty(PendingConvertKeyProperty, NewKey);
			Building.SetStringProperty(PendingConvertSkinProperty, NewSkinKey, RemoveIfNull: true);
			KingdomConstruction.Bind(Building, Updated);
			if (Building.CurrentZone != Z
				|| Building.GetIntProperty(KingdomMaterials.StrikeEffortProperty) <= 0
				|| Building.GetStringProperty(PendingConvertKeyProperty) != NewKey)
			{
				Failure = "The conversion strike order could not be verified.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			KingdomConstruction.FinishProjection(ref Updated, true, true);
			return true;
		}

		// ==================================================================================
		// The strike's own completion: leave a socket, or finish a conversion
		// ==================================================================================

		/// <summary>
		/// Projects only after the durable strike receipt proves the exact predecessor and every
		/// receipt-matched plot part absent. A pending reload adopts only the frozen output ID.
		/// </summary>
		internal static bool ResumeStrikeSuccessor(KingdomSystem System, Zone Z,
			KingdomStrikeIntent Intent, bool FreshAttempt, ref KingdomConstructionJob Job,
			out bool Converted, out string Failure)
		{
			Converted = Job != null && Job.Route == KingdomConstructionRoute.SocketConvert;
			Failure = null;
			if (System == null || Z == null || Intent == null || Job == null
				|| !KingdomConstruction.Owns(System, Z, Job)
				|| Job.PhysicalPhase != KingdomPhysicalPhase.SuccessorPending)
			{
				Failure = "The strike successor receipt is not current.";
				return false;
			}
			GameObject source;
			KingdomPhysicalLookupState sourceState = KingdomConstruction.FindExactId(
				Z, Job.SourceId, out source);
			if (sourceState != KingdomPhysicalLookupState.Absent)
			{
				Failure = sourceState == KingdomPhysicalLookupState.Ambiguous
					? "The strike predecessor ID is duplicated in its loaded owner zone."
					: "The strike successor waits on exact predecessor absence.";
				return false;
			}
			if (!Intent.HasPlot)
			{
				if (Converted)
				{
					Failure = "A conversion receipt has no frozen plot rectangle.";
					return false;
				}
				return string.IsNullOrEmpty(Job.OutputId);
			}
			KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(Intent.X1,
				Intent.Y1, Intent.X2, Intent.Y2);
			if (HasStrikePlotParts(Z, rect, Intent.PlotId))
			{
				Failure = "The strike successor waits on exact plot-part absence.";
				return false;
			}
			GameObject receiptObject;
			KingdomPhysicalLookupState receiptState = KingdomConstruction.FindReceipt(
				Z, Job, out receiptObject);
			if (receiptState == KingdomPhysicalLookupState.Ambiguous)
			{
				Failure = "More than one physical object carries the strike successor receipt.";
				return false;
			}
			if (!string.IsNullOrEmpty(Job.OutputId))
			{
				GameObject exact;
				KingdomPhysicalLookupState exactState = KingdomConstruction.FindExactId(
					Z, Job.OutputId, out exact);
				if (exactState == KingdomPhysicalLookupState.Exact
					&& GameObject.Validate(exact) && exact.IDIfAssigned == Job.OutputId
					&& exact.CurrentZone == Z && KingdomConstruction.HasReceipt(exact, Job))
				{
					if (Converted && ExactConversionOutput(exact, Z, Job)) return true;
					if (!Converted && ExactSocketOutput(exact, Z, rect, Intent, Job)) return true;
				}
				Failure = "The frozen strike successor ID is absent, replaced, or malformed.";
				return false;
			}
			if (!FreshAttempt)
			{
				Failure = "An interrupted strike successor has no frozen generated ID.";
				return false;
			}
			if (receiptState == KingdomPhysicalLookupState.Exact)
			{
				Failure = "A foreign object already carries the strike successor receipt.";
				return false;
			}
			if (Converted)
			{
				if (!KingdomPlots.TryDecodePlotPayload(Job.Payload, out var paidRect,
					out var skinKey, out KingdomArchitectureIntent architecture,
					out bool legacyArchitecture, out _)
					|| (!legacyArchitecture && (architecture == null
						|| architecture.BuildKey != Job.TargetKey
						|| Job.X != architecture.MainWorldX
						|| Job.Y != architecture.MainWorldY))
					|| (legacyArchitecture
					&& (Job.X != paidRect.CenterX || Job.Y != paidRect.CenterY))
					|| !KingdomData.TryGetBuilding(Job.TargetKey, out var entry)
					|| !KingdomPlots.TryGetSpec(Job.TargetKey, out var spec))
				{
					Failure = "The paid conversion no longer matches its frozen fresh site.";
					return false;
				}
				if (!KingdomPlots.ProjectOnRect(System, Z, paidRect, entry, spec, skinKey, Job,
					out GameObject works, out KingdomConstructionJob updated, out Failure))
				{
					Job = updated;
					return false;
				}
				Job = updated;
				if (!KingdomConstruction.Owns(System, Z, Job)
					|| KingdomConstruction.FindExactId(Z, Job.OutputId, out var exactWorks)
						!= KingdomPhysicalLookupState.Exact
					|| !ReferenceEquals(exactWorks, works)
					|| !ExactConversionOutput(works, Z, Job))
				{
					Failure = "The conversion callback did not retain its exact generated works.";
					return false;
				}
				if (Job.SubjectId != works.IDIfAssigned
					&& !KingdomConstruction.UpdateSubject(ref Job, works.IDIfAssigned))
				{
					Failure = "The conversion works identity could not replace the absent predecessor.";
					return false;
				}
				return true;
			}

			Cell cell = Z.GetCell(rect.CenterX, rect.CenterY);
			GameObject marker = cell == null ? null : GameObject.Create(SocketBlueprint);
			r_KingdomSocket part = marker?.GetPart<r_KingdomSocket>();
			if (!GameObject.Validate(marker) || part == null)
			{
				marker?.Obliterate(null, Silent: true);
				Failure = "The exact cleared-plot marker could not be created.";
				return false;
			}
			if (!KingdomConstruction.Owns(System, Z, Job)
				|| !KingdomConstruction.IsCurrent(Job)
				|| KingdomConstruction.FindExactId(Z, Job.SourceId, out _)
					!= KingdomPhysicalLookupState.Absent
				|| KingdomConstruction.FindReceipt(Z, Job, out _)
					!= KingdomPhysicalLookupState.Absent
				|| HasStrikePlotParts(Z, rect, Intent.PlotId))
			{
				marker.Obliterate(null, Silent: true);
				Failure = "Strike successor authority or frozen topology changed during creation.";
				return false;
			}
			part.LastDesignKey = Intent.BuildKey;
			KingdomPlots.StampRect(marker, rect);
			if (!TryStampSocketLot(marker, Intent, out Failure))
			{
				marker.Obliterate(null, Silent: true);
				return false;
			}
			if (!KingdomConstruction.UpdateOutput(ref Job, marker.ID))
			{
				marker.Obliterate(null, Silent: true);
				Failure = "The cleared-plot marker ID could not be published before insertion.";
				return false;
			}
			KingdomConstruction.Bind(marker, Job);
			GameObject accepted;
			try { accepted = cell.AddObject(marker); }
			catch (Exception ex)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, marker);
				Failure = "Cleared-plot marker insertion threw: " + ex.Message;
				return false;
			}
			KingdomSurvey.ObserveAddResultInActive(Z, marker, accepted);
			if (!ReferenceEquals(accepted, marker)
				|| !KingdomConstruction.IsCurrent(Job) || !KingdomConstruction.Owns(System, Z, Job)
				|| KingdomConstruction.FindExactId(Z, Job.OutputId, out var exactMarker)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactMarker, marker)
				|| !ExactSocketOutput(marker, Z, rect, Intent, Job))
			{
				Failure = "The cleared-plot marker was vetoed, replaced, or moved.";
				return false;
			}
			return true;
		}
	}
}
