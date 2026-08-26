using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomMaterialDebit
	{
		private void CaptureRemoved()
		{
			if (Plan == null || !MutationStarted) return;
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				KingdomMaterialDebitStep step = Plan.Steps[i];
				Entry entry = EntryFor(step);
				int removed = Removed[i];
				bool exact = true;
				if (!GameObject.Validate(entry.Item))
				{
					removed = entry.OriginalCount;
				}
				else if (StillSame(entry) && entry.Item.Count > 0 &&
					entry.Item.Count <= entry.OriginalCount)
				{
					int current = entry.Item.Count;
					removed = entry.OriginalCount - current;
				}
				else
				{
					exact = false;
				}
				if (exact && removed >= Removed[i])
				{
					Removed[i] = removed;
				}
				else if (!exact || removed < Removed[i])
				{
					ExactObservations[i] = false;
				}
			}
			if (!TopologyMatchesObserved()) TopologyUncertain = true;
		}

		private void MarkAllUncertain()
		{
			TopologyUncertain = true;
			for (int i = 0; i < ExactObservations.Count; i++) ExactObservations[i] = false;
		}

		private void ReadCurrent(out List<int> Current, out List<bool> Same)
		{
			Current = new List<int>();
			Same = new List<bool>();
			if (Plan == null) return;
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				Entry entry = EntryFor(Plan.Steps[i]);
				bool same = StillSame(entry);
				Same.Add(same);
				Current.Add(GameObject.Validate(entry.Item) ? entry.Item.Count : -1);
			}
		}

		private Entry EntryFor(KingdomMaterialDebitStep Step)
		{
			return (Step.Source >= 0 && Step.Source < Entries.Count) ? Entries[Step.Source] : null;
		}
	}
}
