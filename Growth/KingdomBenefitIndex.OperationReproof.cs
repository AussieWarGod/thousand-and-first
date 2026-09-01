using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private bool ReproveAfterCustomOperation(ProviderCandidate Candidate,
			KingdomDesignationMatch Original, Aggregate Aggregate, Zone Z,
			KingdomDesignationIndex Designations, out string Failure)
		{
			Failure = null;
			GameObject item = Candidate.Item;
			GameObject root = Aggregate.Root;
			if (!GameObject.Validate(item) || item.IsBroken()
				|| !GameObject.Validate(root) || root.IsBroken()
				|| !ProviderStillAttached(item, Candidate.Provider))
				return ReproofFail("custom provider or designation root changed", out Failure);
			if (KingdomConstruction.FindExactId(Z,
				Original.Designation.RootId, out GameObject exactRoot)
				!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(root, exactRoot)
				|| !SameRootPlacement(Aggregate))
				return ReproofFail("custom callback changed the exact designation root", out Failure);
			string affinity = item.HasTag("r_KingdomProviderBuildKey")
				? item.GetTag("r_KingdomProviderBuildKey", null) ?? "" : null;
			if (!KingdomBenefitOperationRules.ProviderMatchesDesign(affinity,
				Original.Designation.BuildingKey))
				return ReproofFail("custom callback changed provider design affinity", out Failure);
			if (!TryProviderCell(item, Candidate.Declaration.Scope, out Cell cell,
				out GameObject holder, out bool inContainer)
				|| cell.X != Original.X || cell.Y != Original.Y)
				return ReproofFail("custom callback moved provider custody", out Failure);
			List<KingdomDesignationMatch> matches = Designations.MatchingExact(cell.X, cell.Y,
				Candidate.Declaration.Scope, inContainer, Candidate.Declaration.NetworkKey);
			if (!TryAssign(item, matches, Designations, out KingdomDesignationMatch current,
				out _, out _) || !SameMatch(Original, current))
				return ReproofFail("custom callback changed designation assignment", out Failure);
			if (Candidate.Declaration.Scope == KingdomBenefitScope.Container
				&& !(ReferenceEquals(item, root)
					|| inContainer && ReferenceEquals(holder, root)))
				return ReproofFail("custom callback changed container custody", out Failure);
			Aggregate.AccessRead = false; Aggregate.Reachable = null;
			Aggregate.ShellRead = false;
			if (!Accessible(Aggregate, current, Z))
				return ReproofFail("custom callback removed provider access", out Failure);
			if (NeedsCover(Candidate.Declaration.Scope) && !ShellValid(Aggregate, current, Z))
				return ReproofFail("custom callback removed physical cover", out Failure);
			return true;
		}

		private static bool ProviderStillAttached(GameObject Item,
			IKingdomBenefitProvider Provider)
		{
			for (int i = 0; i < (Item.PartsList?.Count ?? 0); i++)
				if (ReferenceEquals(Item.PartsList[i], Provider)) return true;
			return false;
		}

		private static bool SameRootPlacement(Aggregate Aggregate)
		{
			Cell cell = Aggregate.Root?.CurrentCell;
			return cell != null && ReferenceEquals(cell.ParentZone, Aggregate.InitialRootZone)
				&& cell.X == Aggregate.InitialRootX && cell.Y == Aggregate.InitialRootY;
		}

		private static bool SameMatch(KingdomDesignationMatch A, KingdomDesignationMatch B)
		{
			return KingdomDesignationIndex.SameExactDesignation(A.Designation, B.Designation)
				&& A.X == B.X && A.Y == B.Y
				&& A.Use == B.Use && A.Cover == B.Cover && A.NetworkKey == B.NetworkKey;
		}

		private static bool ReproofFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
