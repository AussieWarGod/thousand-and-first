using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomCommission
	{
		private static bool ProjectScaffold(KingdomSystem System, Zone Z,
			KingdomRules.BuildEntry Entry, string SkinKey, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			Cell cell = Z?.GetCell(Job.X, Job.Y);
			bool gatehouse = KingdomGatehouseRules.IsGatehouse(Entry?.Key);
			KingdomGatehousePlan gatePlan = null;
			if (gatehouse && !KingdomGatehouseRules.TryDecode(Job.Payload, out gatePlan))
			{
				Failure = "The paid gatehouse receipt has no exact frozen footprint.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (CountExpectedScaffolds(Z, Job, Entry) > 1)
			{
				Failure = "More than one commissioned scaffold carries the exact receipt.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			GameObject existing = FindExpectedScaffold(Z, Job, Entry);
			if (gatehouse && existing != null && !KingdomGatehouse.ScaffoldMatches(existing, gatePlan))
			{
				Failure = "The gatehouse scaffold no longer carries its exact footprint reservation.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (gatehouse && !KingdomGatehouse.TryAudit(Z, gatePlan, null, existing, out Failure))
			{
				return false;
			}
			if (IsExpectedScaffold(existing, cell, Entry, Job))
			{
				if (Updated.SubjectId != existing.ID
					&& !KingdomConstruction.UpdateSubject(ref Updated, existing.ID))
				{
					Failure = "The scaffold identity could not be published.";
					return false;
				}
				if (!KingdomConstruction.FinishProjection(ref Updated, true, true))
				{
					Failure = "The scaffold stands, but its Working state did not persist.";
					return false;
				}
				return true;
			}
			GameObject unexpected;
			KingdomPhysicalLookupState receiptState = KingdomConstruction.FindReceipt(
				Z, Job, out unexpected);
			if (receiptState != KingdomPhysicalLookupState.Absent)
			{
				Failure = receiptState == KingdomPhysicalLookupState.Ambiguous
					? "More than one physical object carries the construction receipt."
					: "The construction receipt is attached to an unexpected projection.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (!KingdomConstructionRules.TryReadBuildTruth(Job, out _, out _, out _))
			{
				Failure = "The unprojected legacy commission predates frozen build effects.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (cell == null || !KingdomConstruction.BeginProjection(ref Updated, out Failure))
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
				Failure = "The scaffold blueprint threw during creation: " + ex.Message;
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
				if (scaffold == null)
			{
				Failure = "The scaffold blueprint could not be created.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
				}
				if (!KingdomConstruction.UpdateOutput(ref Updated, scaffold.ID))
				{
					bool removed = RemoveCreated(scaffold, Z);
					Failure = "The scaffold identity could not be published before AddObject.";
					if (!removed) KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				if (!KingdomConstruction.Owns(System, Z, Updated)
					|| !KingdomConstruction.IsCurrent(Updated))
				{
					RemoveCreated(scaffold, Z);
					Failure = "Commission authority changed during scaffold creation.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
			scaffold.SetStringProperty(KingdomUpgrade.BuildKeyProperty, Entry.Key);
			if (!KingdomConstruction.ApplyBuildTruth(scaffold, Updated))
			{
				RemoveCreated(scaffold, Z);
				Failure = "The paid commission has no exact frozen build effects.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			KingdomDesign.StageSkin(scaffold, Entry, gatehouse ? null : SkinKey);
			KingdomConstruction.Bind(scaffold, Updated);
			if (gatehouse && !KingdomGatehouse.TryStageScaffold(scaffold, gatePlan))
			{
				bool removed = RemoveCreated(scaffold, Z);
				Failure = "The gatehouse scaffold could not retain its full footprint reservation.";
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			r_KingdomScaffold part = scaffold.GetPart<r_KingdomScaffold>();
			if (part == null)
			{
				bool removed = RemoveCreated(scaffold, Z);
				Failure = "The created scaffold carries no raising capability.";
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			part.TargetBlueprint = Entry.Blueprint;
			part.TargetDisplayName = Entry.Name;
			part.CompleteTick = Updated.DueTick;
			part.StaffNeeded = Entry.Staff;
			part.ThresholdManning = KingdomRules.IsThresholdManning(Entry.Manning);
			GameObject accepted;
			try
			{
				accepted = cell.AddObject(scaffold);
				KingdomSurvey.ObserveAddResultInActive(Z, scaffold, accepted);
			}
			catch (System.Exception ex)
			{
				bool removed = RemoveCreated(scaffold, Z);
				Failure = "The scaffold threw while entering its commissioned cell: " + ex.Message;
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			GameObject exactScaffold;
			if (!ReferenceEquals(accepted, scaffold)
				|| !KingdomConstruction.Owns(System, Z, Updated)
				|| KingdomConstruction.FindExactId(Z, Updated.OutputId, out exactScaffold)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactScaffold, scaffold)
				|| !IsExpectedScaffold(scaffold, cell, Entry, Updated)
				|| !KingdomConstruction.HasReceipt(scaffold, Updated)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				bool removed = RemoveCreated(scaffold, Z);
				Failure = "The scaffold could not be verified in its commissioned cell.";
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (!KingdomConstruction.UpdateSubject(ref Updated, scaffold.ID))
			{
				Failure = "The commissioned scaffold identity could not be published.";
				return false;
			}
			if (!KingdomConstruction.FinishProjection(ref Updated, true, true))
			{
				Failure = "The commissioned scaffold stands, but its Working state did not persist.";
				return false;
			}
			return true;
		}

		private static GameObject FindExpectedScaffold(Zone Z, KingdomConstructionJob Job,
			KingdomRules.BuildEntry Entry)
		{
			Cell cell = Z?.GetCell(Job.X, Job.Y);
			if (cell == null) return null;
			GameObject found = null;
			GameObject exact = null;
			int count = 0;
			foreach (GameObject item in cell.GetObjects())
			{
				if (IsExpectedScaffold(item, cell, Entry, Job)
					&& KingdomConstruction.HasReceipt(item, Job))
				{
					count++;
					if (item.ID == Job.OutputId || item.ID == Job.SubjectId) exact = item;
					else if (found == null) found = item;
				}
			}
			GameObject global;
			return count == 1 && exact != null
				&& KingdomConstruction.FindExactId(Z, exact.ID, out global)
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

		private static int CountExpectedScaffolds(Zone Z, KingdomConstructionJob Job,
			KingdomRules.BuildEntry Entry)
		{
			if (Z == null || Job == null || Entry == null) return 0;
			Cell cell = Z?.GetCell(Job.X, Job.Y);
			if (cell == null) return 0;
			int count = 0;
			foreach (GameObject item in cell.GetObjects())
					if (IsExpectedScaffold(item, cell, Entry, Job)
						&& KingdomConstruction.HasReceipt(item, Job))
					{
						if (item.ID != Job.OutputId && item.ID != Job.SubjectId) return 2;
						count++;
					}
			return count;
		}

		private static bool IsExpectedScaffold(GameObject Scaffold, Cell Cell,
			KingdomRules.BuildEntry Entry, KingdomConstructionJob Job)
		{
			if (!GameObject.Validate(Scaffold) || Scaffold.CurrentCell != Cell || Entry == null
				|| Scaffold.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Entry.Key)
			{
				return false;
			}
			r_KingdomScaffold part = Scaffold.GetPart<r_KingdomScaffold>();
			return part != null && part.TargetBlueprint == Entry.Blueprint
				&& (KingdomConstruction.BuildTruthMatches(Scaffold, Job)
					|| KingdomConstruction.LegacyProjectedBuildTruthMatches(
						Scaffold, Job, false));
		}

	}
}
