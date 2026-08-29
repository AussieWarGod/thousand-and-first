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
		/// <summary>
		/// Counts buildings this zone already has standing or scaffolded, by the exact rule
		/// <c>KingdomCommission.Commission</c> uses for its own cap check: walls are exempt, a
		/// scaffold in progress already counts. Kept in step with that rule by hand, since a plan
		/// and a founder-issued commission must compete for one shared allowance rather than each
		/// getting their own.
		/// </summary>
		private static int CountBuilt(KingdomSurvey Survey)
		{
			return Survey == null ? 0 : KingdomPlots.CountBuilt(Survey.Objects);
		}

		/// <summary>
		/// Turns a realised plan into a scaffold, in place: the same <c>r_KingdomScaffold</c>
		/// pipeline <c>KingdomCommission.Commission</c> hands a founder-issued commission to, just
		/// sited at the marker's own cell instead of a freshly chosen one, because the founder
		/// already chose it when they staked the plan.
		/// </summary>
		private static bool Realize(KingdomSystem System, GameObject MarkerObject,
			KingdomRules.BuildEntry Entry, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated)
		{
			Updated = Job;
			Cell cell = GameObject.Validate(MarkerObject) ? MarkerObject.CurrentCell : null;
			Zone zone = cell?.ParentZone;
			if (Job != null && Job.Route == KingdomConstructionRoute.PlotPlan)
			{
				r_KingdomPlanMarker plotMarker = GameObject.Validate(MarkerObject)
					? MarkerObject.GetPart<r_KingdomPlanMarker>() : null;
				if (!KingdomConstructionRules.TryReadBuildTruth(Job,
						out bool hasPlot, out bool frontier, out _)
					|| !hasPlot || frontier || Entry == null || cell == null
					|| !KingdomConstruction.Owns(System, zone, Job)
					|| MarkerObject.IDIfAssigned != (Job.SourceId ?? Job.SubjectId) || plotMarker == null
					|| plotMarker.DesignKey != Entry.Key || !KingdomConstruction.IsCurrent(Job))
				{
					KingdomConstruction.Quarantine(ref Updated,
						"The paid plot plan no longer matches its exact marker and design.");
					return false;
				}
				if (!KingdomConstruction.BeginProjection(ref Updated, out _)) return false;
				KingdomConstruction.Bind(MarkerObject, Updated);
				if (!KingdomConstruction.HasReceipt(MarkerObject, Updated)
					|| MarkerObject.CurrentCell != cell || plotMarker.DesignKey != Entry.Key
					|| !KingdomConstruction.IsCurrent(Updated))
				{
					KingdomConstruction.Quarantine(ref Updated,
						"The plot marker changed across its durable projection boundary.");
					return false;
				}
				KingdomConstructionJob plotUpdated;
				bool plotStaked = KingdomPlots.StakeFromPlan(System, MarkerObject, Entry,
					Updated, out plotUpdated);
				Updated = plotUpdated;
				return plotStaked;
			}
			Cell expected = zone == null || Job == null ? null : zone.GetCell(Job.X, Job.Y);
			if (Entry == null || Job == null || Job.Route != KingdomConstructionRoute.PlanScaffold
				|| cell == null || cell != expected || !KingdomConstruction.Owns(System, zone, Job)
				|| !IsExactPlanMarker(MarkerObject, zone, expected, Job, Entry, false)
				|| !KingdomConstruction.IsCurrent(Job))
			{
				KingdomConstruction.Quarantine(ref Updated,
					"The paid plan marker no longer matches its exact recorded ground and design.");
				return false;
			}
			if (!KingdomConstructionRules.TryReadBuildTruth(Job, out _, out _, out _))
			{
				KingdomConstruction.Quarantine(ref Updated,
					"The unprojected legacy plan predates frozen build effects.");
				return false;
			}
			if (!KingdomConstruction.BeginProjection(ref Updated, out _))
			{
				return false;
			}
			KingdomConstruction.Bind(MarkerObject, Updated);
			if (!IsExactPlanMarker(MarkerObject, zone, expected, Updated, Entry, true)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				KingdomConstruction.Quarantine(ref Updated,
					"The plan marker changed across its durable projection boundary.");
				return false;
			}
			GameObject scaffold;
			try
			{
				scaffold = GameObject.Create("r_KingdomScaffold");
			}
			catch (System.Exception ex)
			{
				KingdomConstruction.Quarantine(ref Updated,
					"The plan's scaffold threw during creation: " + ex.Message);
				return false;
			}
			if (scaffold == null)
			{
				KingdomConstruction.FinishProjection(ref Updated, false, false,
					"The plan's scaffold blueprint could not be created.");
				return false;
			}
			if (!KingdomConstruction.Owns(System, zone, Updated)
				|| !KingdomConstruction.IsCurrent(Updated)
				|| !IsExactPlanMarker(MarkerObject, zone, expected, Updated, Entry, true))
			{
				RemoveCreated(scaffold, zone);
				KingdomConstruction.Quarantine(ref Updated,
					"Plan authority or predecessor changed during scaffold creation.");
				return false;
			}
			// Read off the marker before it is taken down: the look the founder chose when they
			// staked the plan rides on the marker exactly as it rides on a scaffold.
			scaffold.SetStringProperty(KingdomUpgrade.BuildKeyProperty, Entry.Key);
			if (!KingdomConstruction.ApplyBuildTruth(scaffold, Updated))
			{
				RemoveCreated(scaffold, zone);
				KingdomConstruction.Quarantine(ref Updated,
					"The paid plan has no exact frozen build effects.");
				return false;
			}
			KingdomDesign.StageSkin(scaffold, Entry, MarkerObject.GetStringProperty(KingdomDesign.PlannedSkinProperty));
			KingdomCeremony.TransferPlanQuote(MarkerObject, scaffold);
			KingdomConstruction.Bind(scaffold, Updated);
			r_KingdomScaffold part = scaffold.GetPart<r_KingdomScaffold>();
			long projectionTick = Updated.UpdatedTick;
			if (part == null)
			{
				bool removed = RemoveCreated(scaffold, zone);
				if (removed)
					KingdomConstruction.FinishProjection(ref Updated, false, false,
						"The plan's scaffold carries no raising capability.");
				else
					KingdomConstruction.Quarantine(ref Updated,
						"The invalid plan scaffold could not be removed exactly.");
				return false;
			}
			else
			{
				part.TargetBlueprint = Entry.Blueprint;
				part.TargetDisplayName = Entry.Name;
				part.StaffNeeded = Entry.Staff;
				part.ThresholdManning = KingdomRules.IsThresholdManning(Entry.Manning);
				if (!part.TryInitializeDurableWork(Updated, projectionTick, out string workFailure))
				{
					bool removed = RemoveCreated(scaffold, zone);
					if (removed)
						KingdomConstruction.FinishProjection(ref Updated, false, false, workFailure);
					else KingdomConstruction.Quarantine(ref Updated,
						workFailure + " Exact cleanup also failed.");
					return false;
				}
				if (!KingdomConstruction.UpdateOutput(ref Updated, scaffold.ID))
				{
					bool removed = RemoveCreated(scaffold, zone);
					KingdomConstruction.Quarantine(ref Updated, removed
						? "The plan scaffold identity conflicted before AddObject."
						: "The plan scaffold identity conflicted and exact cleanup failed.");
					return false;
				}
			}
			GameObject accepted;
			try
			{
				accepted = cell.AddObject(scaffold);
				KingdomSurvey.ObserveAddResultInActive(zone, scaffold, accepted);
			}
			catch (System.Exception ex)
			{
				bool removed = RemoveCreated(scaffold, zone);
				KingdomConstruction.Quarantine(ref Updated, (removed
					? "The plan scaffold threw after its identity was published: "
					: "The plan scaffold threw and could not be removed exactly: ") + ex.Message);
				return false;
			}
			GameObject exactScaffold;
			if (!ReferenceEquals(accepted, scaffold)
				|| !KingdomConstruction.Owns(System, zone, Updated)
				|| KingdomConstruction.FindExactId(zone, Updated.OutputId, out exactScaffold)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactScaffold, scaffold)
				|| scaffold.CurrentCell != cell || scaffold.CurrentZone != zone
				|| scaffold.GetPart<r_KingdomScaffold>()?.TargetBlueprint != Entry.Blueprint
				|| !ReferenceEquals(scaffold.GetPart<r_KingdomScaffold>(), part)
				|| scaffold.GetIntProperty(r_KingdomScaffold.FinalPendingProperty) != 0
				|| !part.MatchesInitialDurableWork(Updated, projectionTick)
				|| scaffold.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Entry.Key
				|| !KingdomConstruction.HasReceipt(scaffold, Updated)
				|| !KingdomConstruction.IsCurrent(Updated)
				|| !IsExactPlanMarker(MarkerObject, zone, expected, Updated, Entry, true))
			{
				bool removed = RemoveCreated(scaffold, zone);
				KingdomConstruction.Quarantine(ref Updated, removed
					? "The published plan scaffold changed during AddObject."
					: "The published plan scaffold changed and could not be removed exactly.");
				return false;
			}
			string markerId = MarkerObject.IDIfAssigned;
			bool markerRemoved;
			try
			{
				markerRemoved = MarkerObject.Destroy(null, Silent: true);
			}
			catch (System.Exception ex)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(zone, MarkerObject);
				KingdomConstruction.Quarantine(ref Updated,
					"Plan-marker removal threw after scaffold placement: " + ex.Message);
				return false;
			}
			if (markerRemoved && !GameObject.Validate(MarkerObject))
				KingdomSurvey.ObserveRemovedFromActive(zone, MarkerObject);
			if (KingdomConstructionRules.ExactRemovalAction(true, markerRemoved,
				GameObject.Validate(MarkerObject), KingdomConstruction.FindExactId(
					zone, markerId, out _) != KingdomPhysicalLookupState.Absent, true)
				!= KingdomExactRemovalAction.ProvedAbsent)
			{
				KingdomConstruction.Quarantine(ref Updated,
					"Plan-marker removal was vetoed, moved, replaced, or only partially changed the predecessor.");
				return false;
			}
			if (!TryProveMarkerRemoval(System, zone, scaffold, expected, Entry, markerId,
				ref Updated, out string removalFailure)
				|| !ReferenceEquals(scaffold.GetPart<r_KingdomScaffold>(), part)
				|| !part.MatchesInitialDurableWork(Updated, projectionTick))
			{
				KingdomConstruction.Quarantine(ref Updated,
					removalFailure ?? "The planned scaffold changed during marker removal.");
				return false;
			}
			if (!KingdomConstruction.UpdateSubject(ref Updated, scaffold.IDIfAssigned))
			{
				return false;
			}
			if (!KingdomConstruction.FinishProjection(ref Updated, true, true)) return false;
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			KingdomChronicle.Record(System, XRL.Language.Grammar.A(Entry.Name) + " began to rise at " + realm + ", true to the plan staked there");
			System.Ledger.Note("{{G|The plan staked at " + realm + " is under way: the " + Entry.Name + " rises.}}");
			MessageQueue.AddPlayerMessage("{{G|The plan staked at " + realm + " is under way. The " + Entry.Name + " rises.}}");
			return true;
		}

	}
}
