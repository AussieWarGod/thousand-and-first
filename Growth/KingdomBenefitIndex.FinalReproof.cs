using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private bool ReproveEvaluatedCandidates(List<ProviderCandidate> Candidates, Zone Z,
			KingdomSurvey Survey, KingdomDesignationIndex Designations, out string Failure)
		{
			Failure = null;
			foreach (KeyValuePair<string, Aggregate> pair in ByIdentity)
			{
				pair.Value.AccessRead = false; pair.Value.Reachable = null;
				pair.Value.ShellRead = false;
			}
			for (int i = 0; i < Candidates.Count; i++)
				if (Candidates[i].Matched && !ReproveCandidate(Candidates[i], Z, Survey,
					Designations, out Failure)) return false;
			return true;
		}

		private bool ReproveCandidate(ProviderCandidate Candidate, Zone Z, KingdomSurvey Survey,
			KingdomDesignationIndex Designations, out string Failure)
		{
			Failure = null; GameObject item = Candidate.Item;
			Aggregate aggregate = Candidate.AssignedAggregate; GameObject root = aggregate?.Root;
			if (!GameObject.Validate(item) || !GameObject.Validate(root)
				|| Candidate.Provider != null
					&& !ProviderStillAttached(item, Candidate.Provider))
				return ReproofFail("evaluated provider or root changed", out Failure);
			if (KingdomConstruction.FindExactId(Z, Candidate.Match.Designation.RootId,
				out GameObject exactRoot) != KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(root, exactRoot) || !SameRootPlacement(aggregate))
				return ReproofFail("evaluated provider lost its exact designation root", out Failure);
			string affinity = item.HasTag("r_KingdomProviderBuildKey")
				? item.GetTag("r_KingdomProviderBuildKey", null) ?? "" : null;
			if (!KingdomBenefitOperationRules.ProviderMatchesDesign(affinity,
				Candidate.Match.Designation.BuildingKey))
				return ReproofFail("evaluated provider changed design affinity", out Failure);
			if (!TryProviderCell(item, Candidate.Declaration.Scope, out Cell cell,
				out GameObject holder, out bool inContainer)
				|| cell.X != Candidate.Match.X || cell.Y != Candidate.Match.Y)
				return ReproofFail("evaluated provider changed custody", out Failure);
			List<KingdomDesignationMatch> matches = Designations.MatchingExact(cell.X, cell.Y,
				Candidate.Declaration.Scope, inContainer, Candidate.Declaration.NetworkKey);
			if (!TryAssign(item, matches, Designations, out KingdomDesignationMatch current,
				out _, out _) || !SameMatch(Candidate.Match, current))
				return ReproofFail("evaluated provider changed designation assignment", out Failure);
			bool custody = Candidate.Declaration.Scope != KingdomBenefitScope.Container
				|| ReferenceEquals(item, root) || inContainer && ReferenceEquals(holder, root);
			if (custody != Candidate.CustodyValid)
				return ReproofFail("evaluated provider changed container custody", out Failure);
			if (Candidate.AccessObserved
				&& Accessible(aggregate, current, Z) != Candidate.AccessValid)
				return ReproofFail("evaluated provider access changed", out Failure);
			if (Candidate.ShellObserved
				&& ShellValid(aggregate, current, Z) != Candidate.ShellValid)
				return ReproofFail("evaluated provider physical cover changed", out Failure);
			if (Candidate.OperationObserved)
			{
				if (item.IsBroken() != Candidate.ItemBroken
					|| root.IsBroken() != Candidate.RootBroken
					|| PhysicalConditionPercent(item) != Candidate.ItemConditionPercent
					|| PhysicalConditionPercent(root) != Candidate.RootConditionPercent)
					return ReproofFail("evaluated provider physical condition changed", out Failure);
				if (Candidate.Declaration.Operation != KingdomBenefitOperation.Custom
					&& OperationPercent(item, root, Candidate.Provider,
						Candidate.Declaration.Operation, Survey,
						Candidate.Match.Designation.BuildingKey, out _, out _)
						!= Candidate.OperationPercent)
					return ReproofFail("evaluated provider operation changed", out Failure);
			}
			return true;
		}
	}
}
