using System;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		/// <summary>
		/// Everything the improvement handover proves about a physically closed successor before
		/// it settles anything: this job, this ground, this blueprint, a predecessor that stayed
		/// gone, an unduplicated removal proof, and carried contents that still agree with the
		/// receipt they were counted into.
		/// <para>
		/// Extracted whole, with its legacy zero-content carve-out INSIDE it, so the rung's
		/// post-callback re-ask is the identical question rather than a weaker parallel one. The
		/// first evaluation is exactly where it always was.
		/// </para>
		/// </summary>
		private static bool ExactImprovementHandoverProof(KingdomSystem System, Zone Z,
			GameObject Successor, KingdomConstructionJob Job, out string Failure)
		{
			Failure = null;
			GameObject exact;
			KingdomRules.BuildEntry entry;
			if (System == null || Z == null || Job == null || !GameObject.Validate(Successor)
				|| Job.Route != KingdomConstructionRoute.Improvement
				|| !KingdomConstruction.Owns(System, Z, Job) || !KingdomConstruction.IsCurrent(Job)
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out entry)
				|| Successor.IDIfAssigned != Job.OutputId || Successor.CurrentCell != Z.GetCell(Job.X, Job.Y)
				|| KingdomConstruction.FindGlobalPredecessorAuthority(Job, Successor, out _)
					!= KingdomPhysicalLookupState.Absent
				|| KingdomConstruction.FindExactId(Z, Job.OutputId, out exact)
					!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(exact, Successor)
				|| !r_KingdomScaffold.IsExactSuccessor(Successor, Z,
					Z.GetCell(Job.X, Job.Y), Job, entry.Blueprint)
				|| Successor.HasIntProperty(r_KingdomScaffold.RemovalProofProperty)
				|| !ExactRecoverableRemovalReceipt(Job)
				|| !r_KingdomScaffold.HasRemovalProof(Successor,
					Job.SubjectId))
			{
				Failure = "Final improvement-removal evidence is absent, duplicated, or changed.";
				return false;
			}
			bool legacyZeroContent = !ExactRemovalReceipt(Job)
				&& ExactRecoverableRemovalReceipt(Job);
			if (!legacyZeroContent
				&& (!r_KingdomImprovement.VerifySettledHandoverContentCustody(Successor,
					Job.Id, out int settledItems, out int settledLiquid, out Failure)
					|| settledItems != Job.PhysicalIndex
					|| settledLiquid != Job.PhysicalAmount))
			{
				Failure = Failure ?? "Settled handover contents disagree with the removal receipt.";
				return false;
			}
			return true;
		}
	}
}
