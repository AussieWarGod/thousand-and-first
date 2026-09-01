using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private static bool TryCollectProviders(IList<GameObject> Loaded,
			KingdomBenefitIndex Result, out List<ProviderCandidate> Candidates,
			out string Failure)
		{
			Candidates = null; Failure = null;
			if (Loaded == null) return Fail("physical provider roster is absent", out Failure);
			try
			{
				long explicitCount = 0L; long nativeOnlyCount = 0L;
				for (int i = 0; i < Loaded.Count; i++)
				{
					GameObject item = Loaded[i];
					if (!GameObject.Validate(item)) continue;
					int count = CountProviderParts(item, out _);
					explicitCount += count;
					if (count != 0) continue;
					nativeOnlyCount += NativeProviderCount(item, false);
				}

				List<ProviderObjectBatch> batches = new List<ProviderObjectBatch>();
				long reproofExplicit = 0L; long reproofNativeOnly = 0L;
				for (int i = 0; i < Loaded.Count; i++)
				{
					GameObject item = Loaded[i];
					if (!GameObject.Validate(item)) continue;
					int count = CountProviderParts(item, out bool overflow);
					int possibleNative = NativeProviderCount(item, false);
					reproofExplicit += count;
					if (count == 0)
						reproofNativeOnly += possibleNative;
					if (count == 0 && possibleNative == 0) continue;
					ProviderObjectBatch batch = new ProviderObjectBatch { Item = item,
						ObjectAnchor = ObjectAnchor(item), IdentityPrefix = IdentityPrefix(item),
						ExactIdentity = ExactAssignedId(item) != null,
						ExplicitCount = count, ProviderOverflow = overflow };
					CaptureInitialPlacement(batch);
					batches.Add(batch);
				}
				if (reproofExplicit != explicitCount || reproofNativeOnly != nativeOnlyCount)
					return Fail("physical provider evidence changed after bounded preflight",
						out Failure);
				batches.Sort((a, b) =>
				{
					if (a.ExactIdentity != b.ExactIdentity) return a.ExactIdentity ? -1 : 1;
					int anchor = string.CompareOrdinal(a.ObjectAnchor, b.ObjectAnchor);
					return anchor != 0 ? anchor
						: string.CompareOrdinal(a.IdentityPrefix, b.IdentityPrefix);
				});
				int admitted = 0; bool limited = false;
				for (int start = 0; start < batches.Count;)
				{
					int end = start + 1;
					while (end < batches.Count && SameAdmissionAnchor(
						batches[start], batches[end])) end++;
					int explicitRows = 0;
					for (int i = start; i < end; i++)
						explicitRows += KingdomBenefitAdmissionRules.ExplicitRows(
							batches[i].ExplicitCount, batches[i].ProviderOverflow);
					if (!KingdomBenefitAdmissionRules.TryAdmitWholeGroup(
						ref admitted, explicitRows))
					{
						limited = true; start = end; continue;
					}
					for (int i = start; i < end; i++)
					{
						ProviderObjectBatch batch = batches[i];
						if (!ReproveAndPopulate(batch, out Failure)) return false;
						batch.Admitted = true;
						if (batch.ProviderOverflow)
							Result.RecordLoose(batch.IdentityPrefix + "#provider-overflow",
								batch.ObjectAnchor + "|provider-overflow",
								KingdomBenefitFault.MalformedProvider,
								"object exceeded its bounded provider rows; all declarative rows refused");
						else Describe(batch, Result);
					}
					int nativeRows = 0;
					for (int i = start; i < end; i++)
					{
						ProviderObjectBatch batch = batches[i];
						batch.NativeCount = NativeProviderCount(batch.Item, ExplicitRoof(batch));
						nativeRows += batch.NativeCount;
					}
					if (KingdomBenefitAdmissionRules.TryAdmitWholeGroup(
						ref admitted, nativeRows))
					{
						for (int i = start; i < end; i++)
							if (!AddNative(batches[i], batches[i].NativeCount,
								out Failure)) return false;
					}
					else if (end == start + 1)
					{
						int prefix = KingdomBenefitAdmissionRules.NativePrefix(
							admitted, batches[start].NativeCount);
						if (!AddNative(batches[start], prefix, out Failure)) return false;
						admitted += prefix; limited = true;
					}
					else limited = true;
					start = end;
				}
				if (!ReproveAdmittedBatches(batches, out Failure)) return false;
				Result.AdmittedProviderBatches.Clear();
				for (int i = 0; i < batches.Count; i++)
					if (batches[i].Admitted) Result.AdmittedProviderBatches.Add(batches[i]);
				if (limited)
					Result.RecordLoose("<provider-admission>#overflow",
						"zone|provider-admission-overflow",
						KingdomBenefitFault.ObservationLimit,
						"additional physical providers were refused by stable bounded admission");
				Candidates = new List<ProviderCandidate>();
				for (int i = 0; i < batches.Count; i++)
				{
					ProviderObjectBatch batch = batches[i];
					Dictionary<string, int> keys = CountKeys(batch.Candidates);
					for (int c = 0; c < batch.Candidates.Count; c++)
					{
						ProviderCandidate candidate = batch.Candidates[c];
						if (keys[candidate.Declaration.Key] != 1)
						{
							Result.RecordLoose(candidate.IdentityBase, candidate.StableKey,
								KingdomBenefitFault.DuplicateIdentity,
								"object repeats provider key " + candidate.Declaration.Key,
								candidate.Declaration.Key); continue;
						}
						Candidates.Add(candidate);
					}
				}
				return true;
			}
			catch (Exception exception)
			{
				Candidates = null;
				return Fail("physical provider collection threw " + exception.GetType().Name,
					out Failure);
			}
		}

		private static bool SameAdmissionAnchor(ProviderObjectBatch A, ProviderObjectBatch B)
		{
			return A.ObjectAnchor == B.ObjectAnchor && A.IdentityPrefix == B.IdentityPrefix;
		}

		private static bool ReproveAndPopulate(ProviderObjectBatch Batch, out string Failure)
		{
			Failure = null;
			int count = CountProviderParts(Batch.Item, out bool overflow);
			if (count != Batch.ExplicitCount || overflow != Batch.ProviderOverflow)
				return Fail("physical provider evidence changed after bounded preflight",
					out Failure);
			if (overflow) return true;
			for (int p = 0; p < (Batch.Item.PartsList?.Count ?? 0); p++)
				if (Batch.Item.PartsList[p] is IKingdomBenefitProvider provider)
					Batch.Raw.Add(new RawProviderCandidate { Provider = provider,
						TypeName = ProviderType(provider) });
			return Batch.Raw.Count == count || Fail(
				"physical provider evidence changed after bounded preflight", out Failure);
		}

		private static int CountProviderParts(GameObject Item, out bool Overflow)
		{
			int count = 0; Overflow = false;
			for (int p = 0; p < (Item.PartsList?.Count ?? 0); p++)
				if (Item.PartsList[p] is IKingdomBenefitProvider)
				{
					count++;
					if (count > KingdomBenefitEmbodimentRules.MaxProviderPartsPerObject)
					{
						Overflow = true; return count;
					}
				}
			return count;
		}

		private static void Describe(ProviderObjectBatch Batch, KingdomBenefitIndex Result)
		{
			Batch.Raw.Sort((a, b) => string.CompareOrdinal(a.TypeName, b.TypeName));
			for (int i = 0; i < Batch.Raw.Count; i++)
			{
				RawProviderCandidate raw = Batch.Raw[i];
				raw.CanonicalDescription = ObserveDescription(raw.Provider,
					out KingdomBenefitProviderDeclaration declared, out string fault);
				if (declared == null)
				{
					string anchor = Batch.ObjectAnchor + "|provider-type|" + raw.TypeName;
					Result.RecordLoose(Batch.IdentityPrefix + "#provider-type:" + raw.TypeName,
						anchor, KingdomBenefitFault.MalformedProvider,
						fault ?? "provider refused its declaration"); continue;
				}
				Batch.Candidates.Add(Candidate(Batch, raw.Provider, raw.TypeName, declared));
			}
		}

		private static bool AddNative(ProviderObjectBatch Batch, int Prefix, out string Failure)
		{
			Failure = null;
			List<ProviderCandidate> nativeRows = new List<ProviderCandidate>();
			foreach (KingdomBenefitProviderDeclaration native in NativeProviders(Batch.Item,
				ExplicitRoof(Batch)))
				nativeRows.Add(Candidate(Batch, null, "native", native));
			if (nativeRows.Count != Batch.NativeCount || Prefix < 0 || Prefix > nativeRows.Count)
				return Fail(
				"native provider evidence changed after bounded preflight", out Failure);
			nativeRows.Sort((a, b) => string.CompareOrdinal(a.StableKey, b.StableKey));
			for (int i = 0; i < Prefix; i++) Batch.Candidates.Add(nativeRows[i]);
			Batch.NativeAdmitted = Prefix;
			return true;
		}

		private static bool ExplicitRoof(ProviderObjectBatch Batch)
		{
			Dictionary<string, int> keys = CountKeys(Batch.Candidates);
			for (int i = 0; i < Batch.Candidates.Count; i++)
				if (!Batch.Candidates[i].Native
					&& keys[Batch.Candidates[i].Declaration.Key] == 1
					&& Carries(Batch.Candidates[i].Declaration, "roof")) return true;
			return false;
		}

		private static ProviderCandidate Candidate(ProviderObjectBatch Batch,
			IKingdomBenefitProvider Provider, string TypeName,
			KingdomBenefitProviderDeclaration Declaration)
		{
			string stable = CandidateStableKey(Batch.ObjectAnchor, TypeName, Declaration);
			return new ProviderCandidate { Batch = Batch, Item = Batch.Item, Provider = Provider,
				Native = Provider == null, Declaration = Declaration,
				TypeName = TypeName, StableKey = stable,
				IdentityBase = Batch.IdentityPrefix + "#provider:" + Declaration.Key
					+ ":" + TypeName };
		}

		private static Dictionary<string, int> CountKeys(List<ProviderCandidate> Candidates)
		{
			Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < Candidates.Count; i++)
			{
				string key = Candidates[i].Declaration.Key;
				result[key] = result.TryGetValue(key, out int count) ? count + 1 : 1;
			}
			return result;
		}

		private static bool Carries(KingdomBenefitProviderDeclaration Declaration, string Kind)
		{
			for (int i = 0; i < Declaration.Carries.Count; i++)
				if (Declaration.Carries[i].Kind == Kind) return true;
			return false;
		}
	}
}
