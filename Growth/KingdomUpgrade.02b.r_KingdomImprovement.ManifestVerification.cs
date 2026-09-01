using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomImprovement
	{
		private sealed class HandoverManifestState
		{
			internal int Count;
			internal int DestinationKind;
			internal string DestinationId;
			internal string SourceId;
			internal string TargetId;
			internal string ConstructionReceipt;
			internal string[] ItemIds;
			internal string[] Blueprints;
			internal int[] Counts;
			internal string[] Roots;
		}

		internal static bool VerifyHandoverContentCustody(GameObject Source,
			GameObject Target, Cell Where, r_KingdomImprovement Receipt, bool RequireSettled,
			out string Failure)
		{
			Failure = null;
			if (!ExactHandoverObjects(Source, Target, Receipt) || Where == null
				|| Source.CurrentCell != Where || Target.CurrentCell != Where)
				return ManifestFailure(out Failure, "Content-custody endpoints are not exact.");
			HandoverManifestState state;
			if (!TryReadManifest(Target, out state, out Failure)) return false;
			if (state.SourceId != Source.IDIfAssigned || state.TargetId != Target.IDIfAssigned
				|| state.ConstructionReceipt != Receipt.HandoverConstructionReceipt)
				return ManifestFailure(out Failure, "Content-custody authority changed.");
			if (state.DestinationKind == 1 && (Target.Inventory == null
					|| state.DestinationId != Target.IDIfAssigned)
				|| state.DestinationKind == 2 && state.DestinationId != CellKey(Where))
				return ManifestFailure(out Failure, "Content-custody destination changed.");
			if (Receipt.HandoverMovedItems < 0 || Receipt.HandoverMovedItems > state.Count
				|| Receipt.HandoverItemPhase < 0 || Receipt.HandoverItemPhase > 4)
				return ManifestFailure(out Failure, "Content-custody progress is outside its manifest.");
			int pendingIndex = Receipt.HandoverItemPhase == 0
				? -1 : Receipt.HandoverItemMovedBefore;
			int sourceReferences = 0;
			for (int i = 0; i < state.Count; i++)
			{
				object rooted;
				GameObject item;
				GameObject global;
				if (The.Game == null || !The.Game.ObjectGameState.TryGetValue(state.Roots[i],
						out rooted) || (item = rooted as GameObject) == null
					|| !ExactManifestItem(state, i, item)
					|| KingdomConstruction.FindGlobalLiveId(state.ItemIds[i], out global)
						!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(global, item))
					return ManifestFailure(out Failure,
						"A frozen inventory item lost exact global custody.");
				KingdomHandoverManifestSlot expected = KingdomUpgradeContentRules.ExpectedSlot(i,
					state.Count, Receipt.HandoverMovedItems, pendingIndex,
					Receipt.HandoverItemPhase);
				if (expected == KingdomHandoverManifestSlot.Source)
				{
					if (!ExactItemOwner(item, Source, null)) return ManifestFailure(out Failure,
						"An unmoved manifest item left its exact source.");
					sourceReferences++;
				}
				else if (expected == KingdomHandoverManifestSlot.Destination)
				{
					if (!ExactManifestDestination(item, Target, Where, state))
						return ManifestFailure(out Failure,
							"A moved manifest item left its exact destination.");
				}
				else if (expected == KingdomHandoverManifestSlot.Pending)
				{
					GameObject pending;
					if (Receipt.HandoverItemId != state.ItemIds[i]
						|| Receipt.HandoverItemBlueprint != state.Blueprints[i]
						|| Receipt.HandoverItemCount != state.Counts[i]
						|| !TryEscrowItem(Source, Target, Where, Receipt, out pending)
						|| !ReferenceEquals(pending, item)) return ManifestFailure(out Failure,
							"The pending item no longer matches its full manifest entry.");
					if (ExactItemOwner(item, Source, Receipt)) sourceReferences++;
				}
				else return ManifestFailure(out Failure,
					"Inventory progress cannot be reconciled to its manifest.");
			}
			int actualSource = Source.Inventory == null ? 0 : Source.Inventory.Objects.Count;
			if (actualSource != sourceReferences)
				return ManifestFailure(out Failure,
					"The source inventory contains an item outside frozen custody.");
			if (RequireSettled && (!Receipt.HandoverInventoryDone
				|| Receipt.HandoverMovedItems != state.Count || Receipt.HandoverItemPhase != 0))
				return ManifestFailure(out Failure, "Inventory custody is not fully settled.");
			if ((RequireSettled || Receipt.HandoverPhase == 3)
				&& !VerifyLiquidCustody(Target, state.ConstructionReceipt, out _, out Failure))
				return false;
			return true;
		}

		internal static bool VerifySettledHandoverContentCustody(GameObject Successor,
			string ConstructionReceipt, out int Items, out int Liquid, out string Failure)
		{
			Items = 0;
			Liquid = 0;
			Failure = null;
			HandoverManifestState state;
			if (!GameObject.Validate(Successor)
				|| !TryReadManifest(Successor, out state, out Failure)) return false;
			if (state.TargetId != Successor.IDIfAssigned
				|| state.ConstructionReceipt != ConstructionReceipt)
				return ManifestFailure(out Failure, "Settled content authority changed.");
			Cell where = Successor.CurrentCell;
			if (where == null || state.DestinationKind == 1 && (Successor.Inventory == null
					|| state.DestinationId != Successor.IDIfAssigned)
				|| state.DestinationKind == 2 && state.DestinationId != CellKey(where))
				return ManifestFailure(out Failure, "Settled content destination changed.");
			for (int i = 0; i < state.Count; i++)
			{
				object rooted;
				GameObject item;
				GameObject global;
				if (The.Game == null || !The.Game.ObjectGameState.TryGetValue(state.Roots[i],
						out rooted) || (item = rooted as GameObject) == null
					|| !ExactManifestItem(state, i, item)
					|| KingdomConstruction.FindGlobalLiveId(state.ItemIds[i], out global)
						!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(global, item)
					|| !ExactManifestDestination(item, Successor, where, state))
					return ManifestFailure(out Failure,
						"Settled inventory custody is absent, moved, or replaced.");
			}
			if (!VerifyLiquidCustody(Successor, ConstructionReceipt, out Liquid, out Failure))
				return false;
			Items = state.Count;
			return true;
		}

		private static bool ReproveManifestAfterCallback(GameObject Source, GameObject Target,
			Cell Where, r_KingdomImprovement Receipt)
		{
			string failure;
			return VerifyHandoverContentCustody(Source, Target, Where, Receipt, false,
				out failure) || FailHandover(Receipt, failure);
		}

		private static bool TryReadManifest(GameObject Owner, out HandoverManifestState State,
			out string Failure)
		{
			State = null;
			Failure = null;
			if (!RequiredManifestInt(Owner, "Schema", out int schema) || schema != 1
				|| !RequiredManifestInt(Owner, "Count", out int count)
				|| !KingdomUpgradeContentRules.ManifestCardinalityValid(count)
				|| !RequiredManifestInt(Owner, "DestinationKind", out int kind)
				|| kind < 1 || kind > 2
				|| !RequiredManifestText(Owner, "SourceId", out string sourceId)
				|| !RequiredManifestText(Owner, "TargetId", out string targetId)
				|| !RequiredManifestText(Owner, "ConstructionReceipt", out string receipt)
				|| !RequiredManifestText(Owner, "DestinationId", out string destinationId)
				|| !RequiredManifestText(Owner, "Digest", out string digest)
				|| !BoundedIdentity(sourceId) || !BoundedIdentity(targetId)
				|| !BoundedIdentity(receipt) || !BoundedIdentity(destinationId)
				|| targetId != Owner?.IDIfAssigned || string.IsNullOrEmpty(digest)
				|| digest.Length > 128)
				return ManifestFailure(out Failure, "Inventory manifest header is malformed.");
			State = new HandoverManifestState { Count = count, DestinationKind = kind,
				DestinationId = destinationId, SourceId = sourceId, TargetId = targetId,
				ConstructionReceipt = receipt, ItemIds = new string[count],
				Blueprints = new string[count], Counts = new int[count], Roots = new string[count] };
			StringBuilder canonical = BeginManifestDigestValues(sourceId, targetId, receipt,
				count, kind, destinationId);
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < count; i++)
			{
				if (!RequiredEntryText(Owner, i, "Id", out State.ItemIds[i])
					|| !RequiredEntryText(Owner, i, "Blueprint", out State.Blueprints[i])
					|| !RequiredEntryInt(Owner, i, "Count", out State.Counts[i])
					|| !RequiredEntryText(Owner, i, "Root", out State.Roots[i])
					|| !BoundedIdentity(State.ItemIds[i]) || !ids.Add(State.ItemIds[i])
					|| string.IsNullOrEmpty(State.Blueprints[i])
					|| State.Blueprints[i].Length > 256 || State.Counts[i] <= 0
					|| State.Roots[i] != ManifestEscrowKey(receipt, sourceId, targetId,
						i, State.ItemIds[i]))
					return ManifestFailure(out Failure, "Inventory manifest entry is malformed.");
				AppendManifestTerm(canonical, State.ItemIds[i]);
				AppendManifestTerm(canonical, State.Blueprints[i]);
				AppendManifestTerm(canonical, State.Counts[i].ToString(
					CultureInfo.InvariantCulture));
				AppendManifestTerm(canonical, State.Roots[i]);
			}
			return FinishManifestDigest(canonical) == digest || ManifestFailure(out Failure,
				"Inventory manifest digest changed.");
		}

		private static StringBuilder BeginManifestDigestValues(string SourceId, string TargetId,
			string Receipt, int Count, int Kind, string DestinationId)
		{
			StringBuilder value = new StringBuilder();
			AppendManifestTerm(value, SourceId);
			AppendManifestTerm(value, TargetId);
			AppendManifestTerm(value, Receipt);
			AppendManifestTerm(value, Count.ToString(CultureInfo.InvariantCulture));
			AppendManifestTerm(value, Kind.ToString(CultureInfo.InvariantCulture));
			AppendManifestTerm(value, DestinationId);
			return value;
		}

		private static bool ExactManifestItem(HandoverManifestState State, int Index,
			GameObject Item)
		{
			return GameObject.Validate(Item) && Item.IDIfAssigned == State.ItemIds[Index]
				&& Item.Blueprint == State.Blueprints[Index] && Item.Count == State.Counts[Index];
		}

		private static bool ExactManifestDestination(GameObject Item, GameObject Target,
			Cell Where, HandoverManifestState State)
		{
			if (State.DestinationKind == 1) return ExactItemOwner(Item, Target, null);
			return Item.Physics != null && Item.Physics.InInventory == null
				&& Item.CurrentCell == Where && ReferenceCount(Where.GetObjects(), Item) == 1;
		}

		private static bool RequiredManifestText(GameObject Owner, string Name, out string Value)
		{
			return RequiredText(Owner, ManifestKey(Name), out Value);
		}

		private static bool RequiredManifestInt(GameObject Owner, string Name, out int Value)
		{
			return RequiredInt(Owner, ManifestKey(Name), out Value);
		}

		private static bool RequiredEntryText(GameObject Owner, int Index, string Name,
			out string Value)
		{
			return RequiredText(Owner, ManifestEntryKey(Index, Name), out Value);
		}

		private static bool RequiredEntryInt(GameObject Owner, int Index, string Name, out int Value)
		{
			return RequiredInt(Owner, ManifestEntryKey(Index, Name), out Value);
		}

		private static bool RequiredText(GameObject Owner, string Key, out string Value)
		{
			Value = Owner?.GetStringProperty(Key);
			return Owner != null && Owner.HasStringProperty(Key) && !Owner.HasIntProperty(Key);
		}

		private static bool RequiredInt(GameObject Owner, string Key, out int Value)
		{
			Value = Owner == null ? 0 : Owner.GetIntProperty(Key);
			return Owner != null && Owner.HasIntProperty(Key) && !Owner.HasStringProperty(Key);
		}

		private static bool ManifestFailure(out string Failure, string Message)
		{
			Failure = Message;
			return false;
		}
	}
}
