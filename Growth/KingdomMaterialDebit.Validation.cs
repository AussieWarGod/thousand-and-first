using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomMaterialDebit
	{
		private bool AllStillReserved()
		{
			if (Plan == null)
			{
				return false;
			}
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				Entry entry = EntryFor(Plan.Steps[i]);
				if (!StillSame(entry) || entry.Item.Count != entry.OriginalCount)
				{
					return false;
				}
			}
			return TopologyMatchesObserved();
		}

		private bool AllAtPlannedResult()
		{
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				if (!ExactObservations[i]) continue;
				KingdomMaterialDebitStep step = Plan.Steps[i];
				Entry entry = EntryFor(step);
				if (step.NeedsFinalization)
				{
					if (GameObject.Validate(entry.Item)) return false;
				}
				else if (!StillSame(entry) || entry.Item.Count != step.Remaining)
				{
					return false;
				}
			}
			return !TopologyUncertain && TopologyMatchesObserved();
		}

		private bool StillSame(Entry Entry)
		{
			if (Entry == null || !ContainerStillExact(Entry.Witness)
				|| !ValidHeld(Entry.Container, Entry.Item)
				|| !string.Equals(Entry.Blueprint, Entry.Item.Blueprint, StringComparison.Ordinal))
			{
				return false;
			}
			if (ReferenceEquals(Entry.Item, RequiredItem)
				&& (Entry.Item.ID != RequiredItemId || Entry.Item.Count != 1)) return false;
			KingdomMaterialDebitSourceKind kind;
			int kindIndex;
			KingdomBitTally bits;
			return ClassifySource(Entry.Item, out kind, out kindIndex, out bits)
				&& kind == Entry.Kind && kindIndex == Entry.KindIndex
				&& SameBits(bits, Entry.UnitBits);
		}

		private bool ValidContainer(GameObject Container)
		{
			return GameObject.Validate(Container) && KingdomMaterials.IsStockpile(Container)
				&& Container.Inventory != null
				&& ReferenceEquals(Stock.Zone, ReservedZone)
				&& (ReservedZone == null || ReferenceEquals(Container.CurrentZone, ReservedZone));
		}

		private bool ValidHeld(GameObject Container, GameObject Item)
		{
			return ValidContainer(Container) && GameObject.Validate(Item) && Item.Count > 0
				&& ReferenceEquals(Item.InInventory, Container)
				&& ContainsReference(Container.Inventory.Objects, Item);
		}

		private bool ContainerStillExact(ContainerWitness Witness)
		{
			return Witness != null && ValidContainer(Witness.Container) &&
				ReferenceEquals(Witness.Container.CurrentZone, Witness.Zone) &&
				ReferenceEquals(Witness.Container.Inventory, Witness.Inventory) &&
				Witness.Inventory != null &&
				ReferenceEquals(Witness.Inventory.Objects, Witness.ObjectList) &&
				Witness.ObjectList != null;
		}

		private bool ObservedStateMatches()
		{
			return !TopologyUncertain && AllObservationsExact() && TopologyMatchesObserved();
		}

		private bool AllObservationsExact()
		{
			if (Plan == null || ExactObservations.Count != Plan.Steps.Count) return false;
			for (int i = 0; i < ExactObservations.Count; i++)
			{
				if (!ExactObservations[i]) return false;
			}
			return true;
		}

		private bool TopologyMatchesObserved()
		{
			if (Plan == null || Removed.Count != Plan.Steps.Count) return false;
			for (int i = 0; i < Containers.Count; i++)
			{
				ContainerWitness container = Containers[i];
				if (!ContainerStillExact(container)) return false;
				int expectedCount = container.Held.Count;
				for (int j = 0; j < container.Held.Count; j++)
				{
					HeldWitness held = container.Held[j];
					int stepIndex = StepIndexFor(held.Item);
					if (stepIndex >= 0)
					{
						KingdomMaterialDebitStep step = Plan.Steps[stepIndex];
						Entry entry = EntryFor(step);
						int removed = Removed[stepIndex];
						if (removed < 0 || removed > step.Original) return false;
						if (removed == step.Original)
						{
							expectedCount--;
							if (GameObject.Validate(held.Item) ||
								ContainsReference(container.ObjectList, held.Item)) return false;
						}
						else if (!StillSame(entry) || entry.Item.Count != step.Original - removed)
						{
							return false;
						}
					}
					else if (!GameObject.Validate(held.Item) || held.Item.Count != held.Count ||
						!string.Equals(held.Item.Blueprint, held.Blueprint, StringComparison.Ordinal) ||
						!ReferenceEquals(held.Item.InInventory, container.Container) ||
						!ContainsReference(container.ObjectList, held.Item))
					{
						return false;
					}
				}
				if (container.ObjectList.Count != expectedCount) return false;
				for (int j = 0; j < container.ObjectList.Count; j++)
				{
					GameObject current = container.ObjectList[j];
					bool expected = false;
					for (int k = 0; k < container.Held.Count; k++)
					{
						HeldWitness held = container.Held[k];
						if (!ReferenceEquals(held.Item, current)) continue;
						int stepIndex = StepIndexFor(current);
						expected = stepIndex < 0 || Removed[stepIndex] < Plan.Steps[stepIndex].Original;
						break;
					}
					if (!expected) return false;
				}
			}
			return true;
		}

		private int StepIndexFor(GameObject Item)
		{
			if (Plan == null) return -1;
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				Entry entry = EntryFor(Plan.Steps[i]);
				if (entry != null && ReferenceEquals(entry.Item, Item)) return i;
			}
			return -1;
		}

		private static bool ClassifySource(GameObject Item,
			out KingdomMaterialDebitSourceKind Kind, out int KindIndex,
			out KingdomBitTally UnitBits)
		{
			Kind = KingdomMaterialDebitSourceKind.None;
			KindIndex = -1;
			UnitBits = new KingdomBitTally();
			KingdomMaterial material;
			if (KingdomMaterials.TryMaterialOf(Item, out material))
			{
				Kind = KingdomMaterialDebitSourceKind.Material;
				KindIndex = (int)material;
				return true;
			}
			KingdomExotic exotic;
			if (KingdomMaterials.TryExoticOf(Item, out exotic))
			{
				Kind = KingdomMaterialDebitSourceKind.Exotic;
				KindIndex = (int)exotic;
				return true;
			}
			UnitBits = KingdomMaterials.UnitBits(Item);
			if (!UnitBits.IsEmpty())
			{
				Kind = KingdomMaterialDebitSourceKind.BitStock;
				KindIndex = 0;
				return true;
			}
			return false;
		}
	}
}
