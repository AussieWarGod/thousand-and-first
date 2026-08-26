using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomMaterialDebit
	{
		private List<KingdomMaterialDebitSource> SnapshotSources()
		{
			List<KingdomMaterialDebitSource> sources = new List<KingdomMaterialDebitSource>();
			List<GameObject> seenContainers = new List<GameObject>();
			List<GameObject> seenItems = new List<GameObject>();
			List<GameObject> ordered = new List<GameObject>();
			GameObject requiredContainer = GameObject.Validate(RequiredItem)
				? RequiredItem.InInventory : null;
			if (RequiredItem != null && (!GameObject.Validate(RequiredItem)
				|| RequiredItem.Count != 1 || string.IsNullOrEmpty(RequiredItemId)
				|| RequiredItem.ID != RequiredItemId || !GameObject.Validate(requiredContainer)
				|| requiredContainer.Inventory == null
				|| !ContainsReference(requiredContainer.Inventory.Objects, RequiredItem)
				|| !ContainsReference(Stock.Stockpiles, requiredContainer)))
				throw new InvalidOperationException(
					"The exact required item is not a single owned stockpile object.");
			if (requiredContainer != null) ordered.Add(requiredContainer);
			for (int i = 0; i < Stock.Stockpiles.Count; i++)
				if (!ReferenceEquals(Stock.Stockpiles[i], requiredContainer))
					ordered.Add(Stock.Stockpiles[i]);
			for (int i = 0; i < ordered.Count; i++)
			{
				GameObject container = ordered[i];
				if (!ValidContainer(container) || ContainsReference(seenContainers, container))
				{
					continue;
				}
				seenContainers.Add(container);
				ContainerWitness witness = new ContainerWitness
				{
					Container = container,
					Inventory = container.Inventory,
					ObjectList = container.Inventory.Objects,
					Zone = container.CurrentZone
				};
				List<GameObject> held = new List<GameObject>(witness.ObjectList);
				for (int j = 0; j < held.Count; j++)
				{
					GameObject item = held[j];
					witness.Held.Add(new HeldWitness
					{
						Item = item,
						Blueprint = item == null ? null : item.Blueprint,
						Count = item == null ? -1 : item.Count
					});
				}
				Containers.Add(witness);
				if (ReferenceEquals(container, requiredContainer))
				{
					AddSource(container, witness, RequiredItem, seenItems, sources, true);
				}
				for (int j = 0; j < held.Count; j++)
				{
					GameObject item = held[j];
					if (ReferenceEquals(item, RequiredItem)) continue;
					AddSource(container, witness, item, seenItems, sources, false);
				}
			}
			if (RequiredItem != null && RequiredSource < 0)
				throw new InvalidOperationException(
					"The exact required stockpile item is not a material source.");
			return sources;
		}

		private void AddSource(GameObject Container, ContainerWitness Witness, GameObject Item,
			List<GameObject> SeenItems, List<KingdomMaterialDebitSource> Sources, bool Required)
		{
			if (!ValidHeld(Container, Item) || ContainsReference(SeenItems, Item)) return;
			if (!ClassifySource(Item, out KingdomMaterialDebitSourceKind kind,
				out int kindIndex, out KingdomBitTally unitBits)) return;
			SeenItems.Add(Item);
			int source = Entries.Count;
			Entries.Add(new Entry
			{
				Container = Container, Item = Item, Witness = Witness, Blueprint = Item.Blueprint,
				OriginalCount = Item.Count, Kind = kind, KindIndex = kindIndex,
				UnitBits = unitBits.Copy()
			});
			Sources.Add(new KingdomMaterialDebitSource(source, kind, kindIndex,
				Item.Count, unitBits));
			if (Required) RequiredSource = source;
		}

		private bool RequiredSourceWasConsumed()
		{
			if (RequiredItem == null) return true;
			if (RequiredSource < 0 || Plan == null || RequiredItem.Count != 1
				|| RequiredItem.ID != RequiredItemId) return false;
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				KingdomMaterialDebitStep step = Plan.Steps[i];
				if (step.Source == RequiredSource)
					return step.Original == 1 && step.Taken == 1 && step.NeedsFinalization;
			}
			return false;
		}
	}
}
