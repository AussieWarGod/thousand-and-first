using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		/// <summary>Finds the one live paid improvement job represented by both physical
		/// endpoints. This deliberately ignores handover-local flags: it exists to quarantine
		/// their corruption, while refusing to act on a merely matching or foreign receipt.</summary>
		private static bool TryResolveExactHandoverJob(GameObject Predecessor,
			GameObject Successor, string SuccessorKey, out KingdomConstructionJob Job)
		{
			Job = null;
			if (!GameObject.Validate(Predecessor) || !GameObject.Validate(Successor)
				|| string.IsNullOrEmpty(SuccessorKey)) return false;
			Zone zone = Predecessor.CurrentZone;
			Cell cell = Predecessor.CurrentCell;
			if (zone == null || cell == null || Successor.CurrentZone != zone
				|| Successor.CurrentCell != cell
				|| Predecessor.HasIntProperty(KingdomConstruction.ReceiptProperty)
				|| Successor.HasIntProperty(KingdomConstruction.ReceiptProperty)) return false;
			string sourceReceipt = Predecessor.GetStringProperty(
				KingdomConstruction.ReceiptProperty);
			string targetReceipt = Successor.GetStringProperty(
				KingdomConstruction.ReceiptProperty);
			KingdomConstructionJob job;
			KingdomSystem system = The.Game == null
				? null : The.Game.RequireSystem<KingdomSystem>();
			if (string.IsNullOrEmpty(sourceReceipt) || sourceReceipt != targetReceipt
				|| !KingdomConstruction.TryFind(sourceReceipt, out job)
				|| job.Route != KingdomConstructionRoute.Improvement
				|| KingdomConstructionRules.IsTerminal(job.Phase)
				|| (job.Phase != KingdomConstructionPhase.Working
					&& job.Phase != KingdomConstructionPhase.ProjectionPending
					&& job.Phase != KingdomConstructionPhase.Outstanding)
				|| job.SubjectId != Predecessor.IDIfAssigned
				|| job.SourceId != Predecessor.IDIfAssigned
				|| job.OutputId != Successor.IDIfAssigned
				|| job.TargetKey != SuccessorKey || job.X != cell.X || job.Y != cell.Y
				|| Successor.GetIntProperty(BuiltProperty) != 1
				|| Successor.GetStringProperty(BuildKeyProperty) != SuccessorKey
				|| !KingdomConstruction.Owns(system, zone, job)
				|| !KingdomConstruction.IsCurrent(job)
				|| !IsImprovementPredecessorIdentity(system, zone, Predecessor, job)
				|| !r_KingdomScaffold.IsExactPendingImprovementSuccessor(Successor)
				|| !r_KingdomScaffold.IsExactSuccessor(Successor, zone, cell, job,
					Successor.Blueprint, Predecessor)) return false;
			GameObject exactPredecessor;
			GameObject exactSuccessor;
			if (KingdomConstruction.FindExactId(zone, Predecessor.IDIfAssigned,
					out exactPredecessor) != KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactPredecessor, Predecessor)
				|| KingdomConstruction.FindExactId(zone, Successor.IDIfAssigned,
					out exactSuccessor) != KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactSuccessor, Successor)) return false;
			Job = job;
			return true;
		}

		private static void FailExactHandover(GameObject Predecessor, GameObject Successor,
			string SuccessorKey, string Failure)
		{
			KingdomConstructionJob job;
			bool exact = TryResolveExactHandoverJob(Predecessor, Successor,
				SuccessorKey, out job);
			r_KingdomImprovement intent = GameObject.Validate(Predecessor)
				? Predecessor.GetPart<r_KingdomImprovement>() : null;
			r_KingdomImprovement.FailHandover(intent, Failure);
			if (exact) KingdomConstruction.Quarantine(ref job, Failure);
		}
	}
}
