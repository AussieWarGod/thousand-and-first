using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// A live, non-serializable receipt bound to the exact objects in dedicated stockpiles.
	/// Reservation only reads. A caller must persist its own durable job and
	/// <see cref="KingdomMaterialDebitResult.Requested"/> claim before calling <see cref="Commit"/>.
	/// <para>
	/// Qud's destroy path is not transactional: Stacker turns an ordinary destroy of a nonterminal
	/// stack into a measured one-unit decrement and veto, while a permitted whole-object obliteration
	/// runs teardown and graveyards the identity. This receipt tells those outcomes apart. It never
	/// recreates a graveyard object and never calls a partial loss an all-or-nothing refusal.
	/// </para>
	/// </summary>
	public sealed class KingdomMaterialDebit
	{
		private sealed class HeldWitness
		{
			internal GameObject Item;
			internal string Blueprint;
			internal int Count;
		}

		private sealed class ContainerWitness
		{
			internal GameObject Container;
			internal Inventory Inventory;
			internal List<GameObject> ObjectList;
			internal Zone Zone;
			internal readonly List<HeldWitness> Held = new List<HeldWitness>();
		}

		private sealed class Entry
		{
			internal GameObject Container;
			internal GameObject Item;
			internal ContainerWitness Witness;
			internal string Blueprint;
			internal int OriginalCount;
			internal KingdomMaterialDebitSourceKind Kind;
			internal int KindIndex;
			internal KingdomBitTally UnitBits;
		}

		private readonly KingdomMaterials.MaterialStock Stock;
		private readonly List<Entry> Entries = new List<Entry>();
		private readonly List<ContainerWitness> Containers = new List<ContainerWitness>();
		private readonly List<int> Removed = new List<int>();
		private readonly List<bool> ExactObservations = new List<bool>();
		private KingdomMaterialDebitPlan Plan;
		private Zone ReservedZone;
		private readonly GameObject RequiredItem;
		private readonly string RequiredItemId;
		private int RequiredSource = -1;
		private bool TopologyUncertain;
		private bool Operating;
		private bool StockAdjusted;
		private bool MutationStarted;
		private KingdomMaterialDebitCost AdjustedLoss;

		public KingdomMaterialDebitResult Reservation { get; private set; }

		public KingdomMaterialDebitResult Result { get; private set; }

		public bool CanCompensate
		{
			get
			{
				if (TopologyUncertain || !AllObservationsExact()) return false;
				List<int> current;
				List<bool> same;
				ReadCurrent(out current, out same);
				return KingdomMaterialDebitRules.CanCompensate(Plan, Removed, current, same);
			}
		}

		private KingdomMaterialDebit(KingdomMaterials.MaterialStock Stock,
			KingdomMaterialDebitCost Cost, GameObject RequiredItem = null)
		{
			this.Stock = Stock;
			this.RequiredItem = RequiredItem;
			this.RequiredItemId = GameObject.Validate(RequiredItem) ? RequiredItem.ID : null;
			KingdomMaterialDebitCost requested = (Cost == null)
				? new KingdomMaterialDebitCost()
				: Cost.Copy();
			Reservation = KingdomMaterialDebitRules.EmptyResult(
				KingdomMaterialDebitOutcome.InvalidReservation,
				KingdomMaterialDebitFault.InvalidCost, requested, "The material claim is absent.");
			Result = Reservation;
		}

		internal static KingdomMaterialDebit Reserve(KingdomMaterials.MaterialStock Stock,
			KingdomMaterialDebitCost Cost)
		{
			return Reserve(Stock, Cost, null);
		}

		/// <summary>
		/// Reserves the same composite claim while requiring one exact, identity-stable stockpile
		/// object to answer it. The exact reference is planned first and must be fully consumed; an
		/// equivalent object of the same material can never substitute for it.
		/// </summary>
		internal static KingdomMaterialDebit Reserve(KingdomMaterials.MaterialStock Stock,
			KingdomMaterialDebitCost Cost, GameObject RequiredItem)
		{
			KingdomMaterialDebit debit = new KingdomMaterialDebit(Stock, Cost, RequiredItem);
			if (Cost == null)
			{
				return debit;
			}
			if (Stock == null)
			{
				debit.FailReservation(KingdomMaterialDebitFault.InvalidStock,
					"The stockpile reading is absent.");
				return debit;
			}
			try
			{
				debit.ReservedZone = Stock.Zone;
				List<KingdomMaterialDebitSource> sources = debit.SnapshotSources();
				KingdomMaterialDebitFault fault;
				if (!KingdomMaterialDebitRules.TryPlan(Cost, sources, out debit.Plan, out fault))
				{
					debit.FailReservation(fault, ReservationFailure(fault));
					return debit;
				}
				if (RequiredItem != null && !debit.RequiredSourceWasConsumed())
				{
					debit.FailReservation(KingdomMaterialDebitFault.InvalidSources,
						"The exact required stockpile item does not answer this material claim.");
					return debit;
				}
				for (int i = 0; i < debit.Plan.Steps.Count; i++)
				{
					debit.Removed.Add(0);
					debit.ExactObservations.Add(true);
				}
				debit.Reservation = KingdomMaterialDebitRules.EmptyResult(
					KingdomMaterialDebitOutcome.Reserved, KingdomMaterialDebitFault.None,
					Cost, null);
				debit.Result = debit.Reservation;
			}
			catch (Exception ex)
			{
				debit.FailReservation(KingdomMaterialDebitFault.Exception, Describe(ex));
			}
			return debit;
		}

		/// <summary>
		/// Attempts the planned debit once. Exact success, clean refusal, recoverable partial and
		/// irreversible partial are separate results. A second call is idempotent and never mutates.
		/// </summary>
		public KingdomMaterialDebitResult Commit()
		{
			if (Result.Outcome != KingdomMaterialDebitOutcome.Reserved)
			{
				return Result;
			}
			if (Operating)
			{
				return Transient(KingdomMaterialDebitFault.Busy, "The material receipt is already operating.");
			}
			Operating = true;
			try
			{
				if (!AllStillReserved())
				{
					MarkAllUncertain();
					return FinishFailure(KingdomMaterialDebitFault.SourceChanged,
						"A reserved stockpile source changed before the debit began.");
				}

				// Nonterminal stack work first. Stacker deliberately returns false after decrementing
				// one; the before/after count, not the boolean, is authoritative.
				for (int i = 0; i < Plan.Steps.Count; i++)
				{
					KingdomMaterialDebitStep step = Plan.Steps[i];
					if (step.NeedsFinalization)
					{
						continue;
					}
					Entry entry = EntryFor(step);
					for (int unit = 0; unit < step.Taken; unit++)
					{
						int expectedBefore = step.Original - unit;
						if (!ObservedStateMatches() || !StillSame(entry) ||
							entry.Item.Count != expectedBefore || Removed[i] != unit)
						{
							MarkAllUncertain();
							CaptureRemoved();
							return FinishFailure(KingdomMaterialDebitFault.SourceChanged,
								"A stack changed between measured decrements.");
						}
						try
						{
							MutationStarted = true;
							entry.Item.Destroy(null, Silent: true);
						}
						catch (Exception ex)
						{
							CaptureRemoved();
							return FinishFailure(KingdomMaterialDebitFault.Exception, Describe(ex));
						}
						CaptureRemoved();
						if (TopologyUncertain || !ExactObservations[i] ||
							Removed[i] != unit + 1 || !ObservedStateMatches() ||
							!StillSame(entry) || entry.Item.Count != expectedBefore - 1)
						{
							return FinishFailure(KingdomMaterialDebitFault.OperationRefused,
								"A stack did not yield exactly one measured unit.");
						}
					}
				}

				// Whole sources are necessarily irreversible after teardown. Each is last and calls
				// Obliterate exactly once; its one BeforeDestroy callback is authoritative.
				for (int i = 0; i < Plan.Steps.Count; i++)
				{
					KingdomMaterialDebitStep step = Plan.Steps[i];
					if (!step.NeedsFinalization)
					{
						continue;
					}
					Entry entry = EntryFor(step);
					if (!ObservedStateMatches() || !StillSame(entry) ||
						entry.Item.Count != step.Original || Removed[i] != 0)
					{
						MarkAllUncertain();
						CaptureRemoved();
						return FinishFailure(KingdomMaterialDebitFault.SourceChanged,
							"A terminal source changed before finalization.");
					}
					bool returned = false;
					try
					{
						MutationStarted = true;
						returned = entry.Item.Obliterate(null, Silent: true);
					}
					catch (Exception ex)
					{
						CaptureRemoved();
						return FinishFailure(KingdomMaterialDebitFault.Exception, Describe(ex));
					}
					CaptureRemoved();
					if (!TopologyUncertain && ExactObservations[i] &&
						Removed[i] == step.Original && !GameObject.Validate(entry.Item) &&
						ObservedStateMatches())
					{
						continue;
					}
					if (!TopologyUncertain && ExactObservations[i] && Removed[i] == 0 &&
						StillSame(entry) && entry.Item.Count == step.Original)
					{
						return FinishFailure(returned
							? KingdomMaterialDebitFault.OperationMismatch
							: KingdomMaterialDebitFault.OperationRefused,
							"A terminal source did not reach its promised final state.");
					}
					return FinishFailure(KingdomMaterialDebitFault.OperationMismatch,
						"A terminal callback left source ownership or count uncertain.");
				}

				CaptureRemoved();
				if (!AllAtPlannedResult())
				{
					return FinishFailure(KingdomMaterialDebitFault.OperationMismatch,
						"The physical post-debit state does not match the receipt.");
				}
				Result = Classify(KingdomMaterialDebitFault.None, null);
				AdjustStockFor(Result.Lost);
				return Result;
			}
			catch (Exception ex)
			{
				CaptureRemoved();
				return FinishFailure(KingdomMaterialDebitFault.Exception, Describe(ex));
			}
			finally
			{
				Operating = false;
			}
		}

		/// <summary>
		/// Restores only counts on the same surviving objects, and only when every count is exactly
		/// the measured post-debit value. A finalized object is never resurrected or replaced.
		/// </summary>
		public KingdomMaterialDebitResult Compensate()
		{
			if (Result.Outcome == KingdomMaterialDebitOutcome.CompensatedExact
				|| Result.Outcome == KingdomMaterialDebitOutcome.Cancelled)
			{
				return Result;
			}
			if (Result.Outcome != KingdomMaterialDebitOutcome.RecoverablePartial
				&& Result.Outcome != KingdomMaterialDebitOutcome.ExactCommit)
			{
				return Transient(KingdomMaterialDebitFault.WrongPhase,
					"This receipt is not in a compensable phase.");
			}
			if (Operating)
			{
				return Transient(KingdomMaterialDebitFault.Busy,
					"The material receipt is already operating.");
			}
			Operating = true;
			try
			{
				if (!CanCompensate)
				{
					return Transient(KingdomMaterialDebitFault.CompensationUnsafe,
						"The exact original object/count proof no longer holds.");
				}
				KingdomMaterialDebitCost lossBefore = Result.Lost.Copy();
				for (int i = 0; i < Plan.Steps.Count; i++)
				{
					if (Removed[i] <= 0)
					{
						continue;
					}
					if (!TryRestoreCountAndFlush(i))
					{
						Result = Classify(KingdomMaterialDebitFault.CompensationFailed,
							"A restoration callback changed exact source ownership or count.");
						ReconcileStockFor(Result.Lost);
						return Result;
					}
				}
				if (!AllStillReserved())
				{
					Result = Classify(KingdomMaterialDebitFault.CompensationFailed,
						"One or more original stack counts could not be restored exactly.");
					ReconcileStockFor(Result.Lost);
					return Result;
				}
				if (StockAdjusted)
				{
					RestoreStockAdjustment(lossBefore);
				}
				Result = new KingdomMaterialDebitResult(
					KingdomMaterialDebitOutcome.CompensatedExact,
					KingdomMaterialDebitFault.None, Plan.Requested,
					new KingdomMaterialDebitCost(), Plan.Requested,
					new KingdomMaterialDebitCost(), 0, null);
				return Result;
			}
			catch (Exception ex)
			{
				CaptureRemoved();
				Result = Classify(KingdomMaterialDebitFault.CompensationFailed, Describe(ex));
				ReconcileStockFor(Result.Lost);
				return Result;
			}
			finally
			{
				Operating = false;
			}
		}

		/// <summary>Cancels a read-only reservation. No physical source is touched.</summary>
		public KingdomMaterialDebitResult Cancel()
		{
			if (Result.Outcome == KingdomMaterialDebitOutcome.Cancelled)
			{
				return Result;
			}
			if (Result.Outcome != KingdomMaterialDebitOutcome.Reserved)
			{
				return Transient(KingdomMaterialDebitFault.WrongPhase,
					"Only an unused material reservation can be cancelled.");
			}
			Result = new KingdomMaterialDebitResult(KingdomMaterialDebitOutcome.Cancelled,
				KingdomMaterialDebitFault.None, Plan.Requested, new KingdomMaterialDebitCost(),
				Plan.Requested, new KingdomMaterialDebitCost(), 0, null);
			return Result;
		}

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

		private KingdomMaterialDebitResult FinishFailure(KingdomMaterialDebitFault Fault,
			string Failure)
		{
			CaptureRemoved();
			Result = Classify(Fault, Failure);
			// Physical stack-count compensation is safe only while every exact identity and
			// expected post-count still proves itself. Try it immediately; never improvise replacement.
			if (Result.Outcome == KingdomMaterialDebitOutcome.RecoverablePartial)
			{
				KingdomMaterialDebitResult compensated = CompensateDuringCommit();
				if (compensated != null)
				{
					return compensated;
				}
			}
			AdjustStockFor(Result.Lost);
			return Result;
		}

		private KingdomMaterialDebitResult CompensateDuringCommit()
		{
			if (TopologyUncertain || !AllObservationsExact()) return null;
			List<int> current;
			List<bool> same;
			ReadCurrent(out current, out same);
			if (!KingdomMaterialDebitRules.CanCompensate(Plan, Removed, current, same))
			{
				return null;
			}
			try
			{
				for (int i = 0; i < Plan.Steps.Count; i++)
				{
					if (Removed[i] > 0)
					{
						if (!TryRestoreCountAndFlush(i)) return null;
					}
				}
				if (!AllStillReserved())
				{
					return null;
				}
				Result = new KingdomMaterialDebitResult(
					KingdomMaterialDebitOutcome.CleanRefusal, Result.Fault,
					Plan.Requested, new KingdomMaterialDebitCost(), Plan.Requested,
					new KingdomMaterialDebitCost(), 0,
					Result.Failure + " Every measured stack count was restored exactly.");
				return Result;
			}
			catch
			{
				CaptureRemoved();
				Result = Classify(KingdomMaterialDebitFault.CompensationFailed,
					"Automatic exact-count compensation failed.");
				return null;
			}
		}

		private KingdomMaterialDebitResult Classify(KingdomMaterialDebitFault Fault,
			string Failure)
		{
			List<bool> same = new List<bool>();
			List<bool> exact = new List<bool>();
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				same.Add(StillSame(EntryFor(Plan.Steps[i])));
				exact.Add(!TopologyUncertain && ExactObservations[i]);
			}
			return KingdomMaterialDebitRules.Classify(Plan, Removed, same, exact, Fault, Failure);
		}

		/// <summary>
		/// Count restoration is the only safe inverse for a surviving stack. Qud's forward
		/// Stacker.Destroy path flushes the owning inventory's cached weight; the inverse must do the
		/// same or compensation can leave a physically restored stockpile carrying less cached mass.
		/// </summary>
		private bool TryRestoreCountAndFlush(int Index)
		{
			if (Index < 0 || Index >= Plan.Steps.Count || !ObservedStateMatches()) return false;
			KingdomMaterialDebitStep step = Plan.Steps[Index];
			Entry entry = EntryFor(step);
			int removed = Removed[Index];
			if (removed <= 0) return true;
			if (removed >= step.Original || !StillSame(entry) ||
				entry.Item.Count != step.Original - removed)
			{
				return false;
			}
			try
			{
				entry.Item.Count = step.Original;
				entry.Witness.Inventory.FlushWeightCache();
				entry.Item.FlushContextWeightCaches();
			}
			catch
			{
				// Post-state proof below remains authoritative even when notification threw.
			}
			if (!StillSame(entry) || entry.Item.Count != step.Original)
			{
				ExactObservations[Index] = false;
				TopologyUncertain = true;
				return false;
			}
			Removed[Index] = 0;
			if (!TopologyMatchesObserved())
			{
				ExactObservations[Index] = false;
				TopologyUncertain = true;
				return false;
			}
			return true;
		}

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

		private void AdjustStockFor(KingdomMaterialDebitCost Loss)
		{
			if (StockAdjusted || Loss == null || Loss.IsEmpty) return;
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				Stock.Tally.Add((KingdomMaterial)i, -Loss.Materials.Get((KingdomMaterial)i));
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				Stock.Bits.Add(i, -Loss.Bits.Get(i));
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				Stock.Exotics.Add((KingdomExotic)i, -Loss.Exotics.Get((KingdomExotic)i));
			}
			AdjustedLoss = Loss.Copy();
			StockAdjusted = true;
		}

		private void RestoreStockAdjustment(KingdomMaterialDebitCost Loss)
		{
			KingdomMaterialDebitCost restore = AdjustedLoss ?? Loss;
			if (!StockAdjusted || restore == null) return;
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				Stock.Tally.Add((KingdomMaterial)i, restore.Materials.Get((KingdomMaterial)i));
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				Stock.Bits.Add(i, restore.Bits.Get(i));
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				Stock.Exotics.Add((KingdomExotic)i, restore.Exotics.Get((KingdomExotic)i));
			}
			AdjustedLoss = null;
			StockAdjusted = false;
		}

		private void ReconcileStockFor(KingdomMaterialDebitCost Loss)
		{
			if (StockAdjusted)
			{
				RestoreStockAdjustment(AdjustedLoss);
			}
			AdjustStockFor(Loss);
		}

		private void FailReservation(KingdomMaterialDebitFault Fault, string Failure)
		{
			KingdomMaterialDebitCost requested = Reservation.Requested;
			Plan = null;
			Entries.Clear();
			Containers.Clear();
			Removed.Clear();
			ExactObservations.Clear();
			Reservation = KingdomMaterialDebitRules.EmptyResult(
				KingdomMaterialDebitOutcome.InvalidReservation, Fault, requested, Failure);
			Result = Reservation;
		}

		private KingdomMaterialDebitResult Transient(KingdomMaterialDebitFault Fault,
			string Failure)
		{
			return new KingdomMaterialDebitResult(Result.Outcome, Fault, Result.Requested,
				Result.Spent, Result.Outstanding, Result.Lost, Result.FinalizedSources, Failure,
				Result.MeasurementExact);
		}

		private static bool ContainsReference(IList<GameObject> Items, GameObject Candidate)
		{
			for (int i = 0; Items != null && i < Items.Count; i++)
			{
				if (ReferenceEquals(Items[i], Candidate)) return true;
			}
			return false;
		}

		private static bool SameBits(KingdomBitTally A, KingdomBitTally B)
		{
			if (A == null || B == null) return A == B;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				if (A.Get(i) != B.Get(i)) return false;
			}
			return true;
		}

		private static string ReservationFailure(KingdomMaterialDebitFault Fault)
		{
			switch (Fault)
			{
			case KingdomMaterialDebitFault.InsufficientMaterials:
				return "The exact material sources do not cover the claim.";
			case KingdomMaterialDebitFault.InsufficientBits:
				return "The exact bit-stock sources do not cover the claim.";
			case KingdomMaterialDebitFault.InsufficientExotics:
				return "The exact exotic sources do not cover the claim.";
			default:
				return "The exact material claim could not be reserved.";
			}
		}

		private static string Describe(Exception Exception)
		{
			return (Exception == null)
				? "An unknown engine exception interrupted the material receipt."
				: Exception.GetType().Name + ": " + Exception.Message;
		}
	}
}
