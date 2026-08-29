using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		private static bool ProjectImprovement(KingdomSystem System, GameObject Work,
			KingdomRules.BuildEntry Successor, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			Cell cell = Work?.CurrentCell;
			Zone zone = cell?.ParentZone;
			KingdomArchitectureIntent architecture = null;
			bool authored = false;
			string architectureFailure = null;
			if (Successor == null || !TryReadImprovementArchitecture(Work, Job,
				out architecture, out authored, out architectureFailure)
				|| !EnsureExactImprovementPredecessor(System, zone, Work, Job))
			{
				Failure = architectureFailure
					?? "The paid predecessor no longer matches its exact recorded identity.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			r_KingdomImprovement existing = Work.GetPart<r_KingdomImprovement>();
			GameObject exactScaffold;
			KingdomPhysicalLookupState scaffoldState = FindImprovementScaffold(
				cell, Successor, Job, architecture, authored, out exactScaffold);
			if (scaffoldState == KingdomPhysicalLookupState.Ambiguous)
			{
				Failure = "The improvement scaffold is duplicated, moved, replaced, or malformed.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			GameObject paidScaffold = existing != null && existing.Working
				? (scaffoldState == KingdomPhysicalLookupState.Exact
					&& ReferenceEquals(exactScaffold, existing.Scaffold)
						? exactScaffold : null)
				: (scaffoldState == KingdomPhysicalLookupState.Exact ? exactScaffold : null);
			if (paidScaffold != null)
			{
				r_KingdomScaffold paidPart = paidScaffold.GetPart<r_KingdomScaffold>();
				if (paidPart == null || !paidPart.TryValidateInitialDurableWork(
					Updated, Job.UpdatedTick, out Failure))
				{
					Failure = Failure ?? "The interrupted improvement scaffold lost its initial labour proof.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				r_KingdomImprovement recovered = Work.RequirePart<r_KingdomImprovement>();
				recovered.SuccessorKey = Successor.Key;
				recovered.SuccessorBlueprint = Successor.Blueprint;
				recovered.Working = true;
				recovered.Scaffold = paidScaffold;
				recovered.WorkCompleteTick = Job.DueTick;
				KingdomConstruction.Bind(Work, Job);
				KingdomSurvey.ObserveChangedInActive(zone, Work);
				if (!KingdomConstruction.FinishProjection(ref Updated, true, true))
				{
					Failure = "The paid improvement scaffold stands, but Working did not persist.";
					return false;
				}
				return true;
			}
			if (existing != null && existing.Working
				&& scaffoldState != KingdomPhysicalLookupState.Exact)
			{
				Failure = "The linked improvement scaffold lacks exact frozen output identity proof.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (!KingdomConstructionRules.TryReadBuildTruth(Job, out _, out _, out _))
			{
				Failure = "The unprojected legacy improvement predates frozen build effects.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (!KingdomConstruction.BeginProjection(ref Updated, out Failure))
			{
				return false;
			}
			GameObject scaffold;
			try
			{
				scaffold = GameObject.Create("r_KingdomScaffold");
			}
			catch (System.Exception ex)
			{
				Failure = "The improvement scaffold threw during creation: " + ex.Message;
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (scaffold == null)
			{
				Failure = "The improvement scaffold blueprint could not be created.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			if (!KingdomConstruction.Owns(System, zone, Updated)
				|| !EnsureExactImprovementPredecessor(System, zone, Work, Updated))
			{
				RemoveCreatedProjection(scaffold);
				Failure = "Improvement authority or predecessor changed during scaffold creation.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (authored && !KingdomArchitectureRuntime.TryFreeze(scaffold, architecture,
				out string freezeFailure))
			{
				RemoveCreatedProjection(scaffold);
				Failure = "The improvement scaffold could not freeze authored authority: "
					+ freezeFailure;
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			r_KingdomScaffold part = scaffold.GetPart<r_KingdomScaffold>();
			if (part == null)
			{
				bool removed = RemoveCreatedProjection(scaffold);
				Failure = "The improvement scaffold carries no raising capability.";
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			scaffold.SetStringProperty(BuildKeyProperty, Successor.Key);
			if (!KingdomConstruction.ApplyBuildTruth(scaffold, Updated))
			{
				RemoveCreatedProjection(scaffold);
				Failure = "The paid improvement has no exact frozen build effects.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			KingdomConstruction.Bind(scaffold, Updated);
			part.TargetBlueprint = Successor.Blueprint;
			part.TargetDisplayName = Successor.Name;
			part.StaffNeeded = Successor.Staff;
			part.ThresholdManning = KingdomRules.IsThresholdManning(Successor.Manning);
			long projectionTick = Updated.UpdatedTick;
			if (!part.TryInitializeDurableWork(Updated, projectionTick, out Failure))
			{
				bool removed = RemoveCreatedProjection(scaffold);
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (!KingdomConstruction.UpdateOutput(ref Updated, scaffold.ID))
			{
				bool removed = RemoveCreatedProjection(scaffold);
				Failure = "The improvement scaffold identity could not be published before AddObject.";
				if (!removed) KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			GameObject accepted;
			try
			{
				accepted = cell.AddObject(scaffold);
				KingdomSurvey.ObserveAddResultInActive(zone, scaffold, accepted);
			}
			catch (System.Exception ex)
			{
				bool removed = RemoveCreatedProjection(scaffold);
				Failure = "The improvement scaffold threw during AddObject: " + ex.Message;
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			GameObject globalScaffold;
			if (!ReferenceEquals(accepted, scaffold)
				|| !KingdomConstruction.Owns(System, zone, Updated)
				|| KingdomConstruction.FindExactId(zone, Updated.OutputId, out globalScaffold)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(globalScaffold, scaffold)
				|| !ExpectedImprovementScaffold(scaffold, cell, Successor, Updated,
					architecture, authored)
				|| !ReferenceEquals(scaffold.GetPart<r_KingdomScaffold>(), part)
				|| scaffold.GetIntProperty(r_KingdomScaffold.FinalPendingProperty) != 0
				|| !part.MatchesInitialDurableWork(Updated, projectionTick)
				|| !KingdomConstruction.HasReceipt(scaffold, Updated)
				|| !EnsureExactImprovementPredecessor(System, zone, Work, Updated)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				bool removed = RemoveCreatedProjection(scaffold);
				Failure = "The improvement scaffold could not be verified beside its predecessor.";
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			r_KingdomImprovement improvement = Work.RequirePart<r_KingdomImprovement>();
			improvement.SuccessorKey = Successor.Key;
			improvement.SuccessorBlueprint = Successor.Blueprint;
			improvement.Working = true;
			improvement.Scaffold = scaffold;
			improvement.WorkCompleteTick = Updated.DueTick;
			improvement.AnnouncedReason = 0;
			KingdomConstruction.Bind(Work, Updated);
			KingdomSurvey.ObserveChangedInActive(zone, Work);
			if (!improvement.Working || improvement.Scaffold != scaffold
				|| !ReferenceEquals(scaffold.GetPart<r_KingdomScaffold>(), part)
				|| !part.MatchesInitialDurableWork(Updated, projectionTick)
				|| !EnsureExactImprovementPredecessor(System, zone, Work, Updated))
			{
				Failure = "The improvement intent could not be verified on its predecessor.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			if (!KingdomConstruction.FinishProjection(ref Updated, true, true))
			{
				Failure = "The improvement scaffold stands, but Working did not persist.";
				return false;
			}
			return true;
		}

	}
}
