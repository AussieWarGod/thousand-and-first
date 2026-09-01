using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private static void CaptureInitialPlacement(ProviderObjectBatch Batch)
		{
			GameObject item = Batch.Item;
			Batch.InitialHolder = item?.InInventory;
			Cell cell = item?.CurrentCell ?? Batch.InitialHolder?.CurrentCell;
			Batch.InitialZone = cell?.ParentZone;
			Batch.InitialX = cell?.X ?? -1;
			Batch.InitialY = cell?.Y ?? -1;
			Batch.InitialCount = item?.Count ?? -1;
			Batch.InitiallyEquipped = item?.Equipped != null;
		}

		private bool ReproveCollectedProviderSnapshot(Zone Z, KingdomSurvey Survey,
			KingdomDesignationIndex Designations, List<ProviderCandidate> Candidates,
			out string Failure)
		{
			if (!ReproveProviderDescriptions(AdmittedProviderBatches, out Failure)) return false;
			if (!KingdomDesignationIndex.TryActiveZone(Z, Survey,
				out KingdomDesignationIndex current, out Failure)
				|| !Designations.SameSnapshot(current))
				return Fail(Failure ?? "designation authority changed during benefit snapshot",
					out Failure);
			if (!ReproveAdmittedBatches(AdmittedProviderBatches, out Failure)) return false;
			foreach (KeyValuePair<string, Aggregate> pair in ByIdentity)
			{
				Aggregate aggregate = pair.Value;
				Cell cell = aggregate.Root?.CurrentCell;
				if (!GameObject.Validate(aggregate.Root)
					|| KingdomConstruction.FindExactId(Z,
						aggregate.Reading.Designation.RootId, out GameObject exactRoot)
						!= KingdomPhysicalLookupState.Exact
					|| !ReferenceEquals(exactRoot, aggregate.Root) || cell == null
					|| !ReferenceEquals(cell.ParentZone, aggregate.InitialRootZone)
					|| cell.X != aggregate.InitialRootX || cell.Y != aggregate.InitialRootY)
					return Fail("designation root moved during physical benefit snapshot",
						out Failure);
			}
			return ReproveEvaluatedCandidates(Candidates, Z, Survey, current, out Failure);
		}

		private static bool ReproveAdmittedBatches(List<ProviderObjectBatch> Batches,
			out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Batches.Count; i++)
			{
				ProviderObjectBatch batch = Batches[i];
				if (!batch.Admitted) continue;
				if (!GameObject.Validate(batch.Item) || !SamePlacement(batch)
					|| ObjectAnchor(batch.Item) != batch.ObjectAnchor
					|| IdentityPrefix(batch.Item) != batch.IdentityPrefix)
					return Fail("admitted physical provider moved during its snapshot", out Failure);
				int count = CountProviderParts(batch.Item, out bool overflow);
				if (count != batch.ExplicitCount || overflow != batch.ProviderOverflow)
					return Fail("admitted physical provider roster changed during its snapshot",
						out Failure);
				if (!overflow && !SameExplicitReferences(batch))
					return Fail("admitted declarative provider detached during its snapshot",
						out Failure);
				if (batch.NativeAdmitted > 0 && !SameNativePrefix(batch))
					return Fail("admitted native provider changed during its snapshot", out Failure);
			}
			return true;
		}

		private static bool SamePlacement(ProviderObjectBatch Batch)
		{
			GameObject item = Batch.Item;
			GameObject holder = item?.InInventory;
			Cell cell = item?.CurrentCell ?? holder?.CurrentCell;
			return ReferenceEquals(holder, Batch.InitialHolder)
				&& ReferenceEquals(cell?.ParentZone, Batch.InitialZone)
				&& (cell?.X ?? -1) == Batch.InitialX && (cell?.Y ?? -1) == Batch.InitialY
				&& (item?.Count ?? -1) == Batch.InitialCount
				&& (item?.Equipped != null) == Batch.InitiallyEquipped;
		}

		private static bool SameExplicitReferences(ProviderObjectBatch Batch)
		{
			int observed = 0; bool[] used = new bool[Batch.Raw.Count];
			for (int p = 0; p < (Batch.Item.PartsList?.Count ?? 0); p++)
			{
				if (!(Batch.Item.PartsList[p] is IKingdomBenefitProvider provider)) continue;
				observed++;
				bool found = false;
				for (int i = 0; i < Batch.Raw.Count; i++)
					if (!used[i] && ReferenceEquals(Batch.Raw[i].Provider, provider))
					{
						used[i] = true; found = true; break;
					}
				if (!found) return false;
			}
			return observed == Batch.Raw.Count;
		}

		private static bool SameNativePrefix(ProviderObjectBatch Batch)
		{
			List<ProviderCandidate> current = new List<ProviderCandidate>();
			foreach (KingdomBenefitProviderDeclaration row in NativeProviders(
				Batch.Item, ExplicitRoof(Batch)))
				current.Add(Candidate(Batch, null, "native", row));
			if (current.Count != Batch.NativeCount) return false;
			current.Sort((a, b) => string.CompareOrdinal(a.StableKey, b.StableKey));
			List<ProviderCandidate> captured = new List<ProviderCandidate>();
			for (int i = 0; i < Batch.Candidates.Count; i++)
				if (Batch.Candidates[i].Native) captured.Add(Batch.Candidates[i]);
			captured.Sort((a, b) => string.CompareOrdinal(a.StableKey, b.StableKey));
			if (captured.Count != Batch.NativeAdmitted) return false;
			for (int i = 0; i < captured.Count; i++)
				if (captured[i].StableKey != current[i].StableKey) return false;
			return true;
		}
	}
}
