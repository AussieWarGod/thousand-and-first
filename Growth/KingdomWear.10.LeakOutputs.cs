using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomWear
	{
		private static void ContinueLeakOutputs(KingdomSystem System, GameObject Work,
			r_KingdomWear Wear)
		{
			if (Wear == null || !GameObject.Validate(Work) || Wear.ParentObject != Work
				|| !ReferenceEquals(Work.GetPart<r_KingdomWear>(), Wear)
				|| !string.Equals(Wear.LeakOwnerId, Work.ID, StringComparison.Ordinal)
				|| Work.CurrentZone == null || Work.CurrentCell == null
				|| Work.CurrentCell.ParentZone != Work.CurrentZone
				|| !string.Equals(Wear.LeakZoneId, Work.CurrentZone.ZoneID,
					StringComparison.Ordinal)
				|| Work.CurrentCell.X != Wear.LeakCellX || Work.CurrentCell.Y != Wear.LeakCellY)
			{
				if (Wear != null)
				{
					Wear.LifecycleQuarantined = true;
					Wear.QuarantineReason =
						"A completed storage loss is no longer bound to its exact work, wear part, cell, and zone.";
					Wear.LeakPhase = (int)KingdomWearLeakPhase.Quarantined;
				}
				return;
			}
			KingdomWearLeakPhase phase = (KingdomWearLeakPhase)Wear.LeakPhase;
			if (Wear.LeakAnnounced && phase == KingdomWearLeakPhase.Mutated)
			{
				Wear.LeakLedgerState = (int)KingdomWearSinkDisposition.Skipped;
				Wear.LeakMessageState = (int)KingdomWearSinkDisposition.Skipped;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.Complete;
				phase = KingdomWearLeakPhase.Complete;
			}
			if (phase == KingdomWearLeakPhase.Mutated)
			{
				if (!KingdomChronicle.RecordOnce(System, Wear.LeakIncidentId + ":chronicle",
					Wear.LeakLine)) return;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.ChronicleDone;
				phase = KingdomWearLeakPhase.ChronicleDone;
			}
			if (phase == KingdomWearLeakPhase.ChronicleDone)
			{
				if (Wear.LeakLedgerState == (int)KingdomWearSinkDisposition.None)
					Wear.LeakLedgerState = (int)KingdomWearSinkDisposition.Pending;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.LedgerIntent;
				DeliverWearLedger(System, ref Wear.LeakLedgerState,
					"{{r|" + XRL.Language.Grammar.InitCap(Wear.LeakLine) + "}}");
				Wear.LeakPhase = (int)KingdomWearLeakPhase.LedgerDone;
				phase = KingdomWearLeakPhase.LedgerDone;
			}
			else if (phase == KingdomWearLeakPhase.LedgerIntent)
			{
				if (Wear.LeakLedgerState == (int)KingdomWearSinkDisposition.None)
					Wear.LeakLedgerState = (int)KingdomWearSinkDisposition.Attempting;
				Wear.LeakLedgerState = (int)KingdomWearRules.RecoverUninspectable(
					(KingdomWearSinkDisposition)Wear.LeakLedgerState);
				Wear.LeakPhase = (int)KingdomWearLeakPhase.LedgerDone;
				phase = KingdomWearLeakPhase.LedgerDone;
			}
			if (phase == KingdomWearLeakPhase.LedgerDone)
			{
				if (Wear.LeakMessageState == (int)KingdomWearSinkDisposition.None)
					Wear.LeakMessageState = (int)KingdomWearSinkDisposition.Pending;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.MessageIntent;
				DeliverWearMessage(ref Wear.LeakMessageState,
					"{{r|" + XRL.Language.Grammar.InitCap(Wear.LeakLine) + "}}");
				Wear.LeakPhase = (int)KingdomWearLeakPhase.MessageDone;
				phase = KingdomWearLeakPhase.MessageDone;
			}
			else if (phase == KingdomWearLeakPhase.MessageIntent)
			{
				if (Wear.LeakMessageState == (int)KingdomWearSinkDisposition.None)
					Wear.LeakMessageState = (int)KingdomWearSinkDisposition.Attempting;
				Wear.LeakMessageState = (int)KingdomWearRules.RecoverUninspectable(
					(KingdomWearSinkDisposition)Wear.LeakMessageState);
				Wear.LeakPhase = (int)KingdomWearLeakPhase.MessageDone;
				phase = KingdomWearLeakPhase.MessageDone;
			}
			if (phase == KingdomWearLeakPhase.MessageDone)
			{
				Wear.LeakAnnounced = true;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.Complete;
				phase = KingdomWearLeakPhase.Complete;
				KingdomLog.Log("wear: leak " + Work.Blueprint + " kind=" + Wear.LeakKind
					+ " lost=" + Wear.LeakActualLost + " incident=" + Wear.LeakIncidentId);
			}
			if (phase == KingdomWearLeakPhase.Complete) ClearLeakReceipt(Wear);
		}

		private static bool TryFoodPlan(GameObject Work, int Wanted, out string Ids,
			out string Originals, out string Allocations)
		{
			Ids = Originals = Allocations = null;
			if (!GameObject.Validate(Work) || Work.Inventory == null || Wanted <= 0) return false;
			List<string> ids = new List<string>();
			List<int> originals = new List<int>();
			List<int> allocations = new List<int>();
			List<GameObject> seen = new List<GameObject>();
			int remaining = Wanted;
			for (int i = 0; i < Work.Inventory.Objects.Count && remaining > 0; i++)
			{
				GameObject food = Work.Inventory.Objects[i];
				bool duplicate = false;
				for (int j = 0; j < seen.Count; j++)
				{
					if (ReferenceEquals(seen[j], food)) duplicate = true;
				}
				if (duplicate || !GameObject.Validate(food) || food.InInventory != Work
					|| (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient")))
				{
					continue;
				}
				if (string.IsNullOrEmpty(food.ID) || food.ID.IndexOf('|') >= 0 || food.Count <= 0)
				{
					return false;
				}
				int take = (food.Count < remaining) ? food.Count : remaining;
				if (food.ID.Length > KingdomWearRules.MaxObjectIdChars
					|| ids.Count >= KingdomWearRules.MaxRows) return false;
				seen.Add(food);
				ids.Add(food.ID);
				originals.Add(food.Count);
				allocations.Add(take);
				remaining -= take;
			}
			if (remaining != 0 || ids.Count == 0) return false;
			Ids = string.Join("|", ids.ToArray());
			Originals = JoinWearInts(originals);
			Allocations = JoinWearInts(allocations);
			return Ids.Length <= KingdomWearRules.MaxRowsChars
				&& Originals.Length <= KingdomWearRules.MaxRowsChars
				&& Allocations.Length <= KingdomWearRules.MaxRowsChars;
		}

		private static bool ObserveFoodPlan(GameObject Work, r_KingdomWear Wear,
			out int Current, out bool Exact, out int Proved)
		{
			Current = 0;
			Exact = false;
			Proved = 0;
			string[] ids;
			if (!GameObject.Validate(Work) || Work.Inventory == null
				|| !KingdomWearRules.TryObjectIdRows(Wear.LeakItemIds, out ids)) return false;
			int[] originals;
			int[] allocations;
			if (!TryWearInts(Wear.LeakItemOriginalCounts, out originals)
				|| !TryWearInts(Wear.LeakItemAllocations, out allocations)
				|| ids.Length == 0 || ids.Length != originals.Length
				|| ids.Length != allocations.Length) return false;
			bool allOriginal = true;
			bool allAfter = true;
			for (int i = 0; i < ids.Length; i++)
			{
				if (string.IsNullOrEmpty(ids[i]) || originals[i] <= 0 || allocations[i] <= 0
					|| allocations[i] > originals[i]) return false;
				for (int j = 0; j < i; j++)
				{
					if (string.Equals(ids[j], ids[i], StringComparison.Ordinal)) return false;
				}
				GameObject food = GameObject.FindByID(ids[i]);
				int rowCurrent;
				if (!GameObject.Validate(food))
				{
					rowCurrent = 0;
				}
				else
				{
					if (food.InInventory != Work || !Work.Inventory.Objects.Contains(food)
						|| (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient"))
						|| food.Count < 0) return false;
					rowCurrent = food.Count;
				}
				int intended = originals[i] - allocations[i];
				if (rowCurrent != originals[i]) allOriginal = false;
				if (rowCurrent != intended) allAfter = false;
				if (rowCurrent < intended || rowCurrent > originals[i]) return false;
				Proved += originals[i] - rowCurrent;
			}
			Current = KingdomSurvey.HeldIn(Work);
			Exact = (allOriginal && Current == Wear.LeakBefore)
				|| (allAfter && Current == Wear.LeakAfter && Proved == Wear.LeakWanted);
			return true;
		}

	}
}
