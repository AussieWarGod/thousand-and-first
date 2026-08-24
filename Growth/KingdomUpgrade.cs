using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name. This part is
// only ever added in code, but it lives here anyway alongside r_KingdomScaffold and
// r_KingdomPlot: a part whose namespace depends on how it happened to be attached is a trap
// waiting for the first blueprint that names it.
namespace XRL.World.Parts
{
	/// <summary>
	/// The settlement's intent about one work it raised: what that work is to become, whether
	/// the founder has told the settlement to leave it alone, and &mdash; while an improvement
	/// is actually under way &mdash; the handover of everything the old work was carrying into
	/// the new one.
	/// <para>
	/// Attached lazily, and only to a work whose design actually names a successor, so an
	/// ordinary building never gains a part it has no use for. Absent means "no opinion", which
	/// is the state every building in every existing save is in.
	/// </para>
	/// <para>
	/// The work itself is <c>r_KingdomScaffold</c>'s, not this part's. This part does one thing
	/// the scaffold cannot: the scaffold creates the successor and destroys itself, and nobody
	/// but the predecessor knows what the predecessor was holding. So the predecessor keeps
	/// standing, and working, for the whole build, and steps aside only once its replacement is
	/// actually on the ground with the contents safely moved across.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomImprovement : IPart
	{
		/// <summary>Registry key of the design being built while <see cref="Working"/>. Null
		/// otherwise; the design this work grows into in general is read from the registry, not
		/// from here.</summary>
		public string SuccessorKey;

		/// <summary>Blueprint the scaffold was pointed at, so the finished object can be
		/// recognised in the cell without trusting the scaffold to have survived.</summary>
		public string SuccessorBlueprint;

		/// <summary>The founder's standing "leave this one as it is". Persists on the object,
		/// shows in its description, and is only ever set or cleared from the Charter.</summary>
		public bool Held;

		/// <summary>True between the scaffold going up and the handover completing.</summary>
		public bool Working;

		/// <summary>The scaffold raised for this improvement. Goes invalid the instant the
		/// scaffold completes &mdash; which is exactly the signal the handover waits on.</summary>
		public GameObject Scaffold;

		/// <summary>Tick the scaffold was due to finish, used only to decide when a work that
		/// never appeared has to be given up on rather than polled forever.</summary>
		public long WorkCompleteTick;

		/// <summary>
		/// The <c>KingdomUpgradeRules.UpgradeVerdict</c> last announced to the founder for this
		/// work, as an int. Zero means nothing has been announced &mdash; unambiguous because
		/// zero is <c>Ready</c>, which is never announced as a block. Announcing again is gated
		/// on the reason having actually CHANGED, so a settlement that has been short of water
		/// for a season says so once and then stops.
		/// </summary>
		public int AnnouncedReason;

		// The shipped IPart wire ends at AnnouncedReason. IComponent.Write/Read serializes every
		// instance field positionally, so durable handover state lives in namespaced GameObject
		// properties. Explicit accessors have no backing fields and therefore do not alter that wire.
		private const string HandoverPrefix = "r_TAF_ImprovementHandover:";
		private const string HandoverEscrowPrefix = "r_TAF_ImprovementItemEscrow:";
		private const int MaxHandoverText = 4096;
		private const int MaxHandoverComponents = 64;
		private const int MaxHandoverTopologyObjects = 4096;

		private int HandoverInt(string Name)
		{
			return ParentObject == null ? 0 : ParentObject.GetIntProperty(HandoverPrefix + Name);
		}

		private void HandoverInt(string Name, int Value)
		{
			ParentObject?.SetIntProperty(HandoverPrefix + Name, Value);
		}

		private string HandoverText(string Name)
		{
			return ParentObject?.GetStringProperty(HandoverPrefix + Name);
		}

		private void HandoverText(string Name, string Value)
		{
			ParentObject?.SetStringProperty(HandoverPrefix + Name, Value);
		}

		internal int HandoverPhase
		{
			get { return HandoverInt("LiquidPhase"); }
			set { HandoverInt("LiquidPhase", value); }
		}

		internal bool HandoverQuarantined
		{
			get { return HandoverInt("Quarantined") == 1; }
			set { HandoverInt("Quarantined", value ? 1 : 0); }
		}

		internal string HandoverFailure
		{
			get { return HandoverText("Failure"); }
			set { HandoverText("Failure", value); }
		}

		internal string HandoverSourceId
		{
			get { return HandoverText("SourceId"); }
			set { HandoverText("SourceId", value); }
		}

		internal string HandoverTargetId
		{
			get { return HandoverText("TargetId"); }
			set { HandoverText("TargetId", value); }
		}

		internal string HandoverConstructionReceipt
		{
			get { return HandoverText("ConstructionReceipt"); }
			set { HandoverText("ConstructionReceipt", value); }
		}

		internal int HandoverSourceVolumeBefore
		{
			get { return HandoverInt("SourceVolumeBefore"); }
			set { HandoverInt("SourceVolumeBefore", value); }
		}

		internal int HandoverSourceVolumeAfter
		{
			get { return HandoverInt("SourceVolumeAfter"); }
			set { HandoverInt("SourceVolumeAfter", value); }
		}

		internal int HandoverTargetVolumeBefore
		{
			get { return HandoverInt("TargetVolumeBefore"); }
			set { HandoverInt("TargetVolumeBefore", value); }
		}

		internal int HandoverTargetVolumeAfter
		{
			get { return HandoverInt("TargetVolumeAfter"); }
			set { HandoverInt("TargetVolumeAfter", value); }
		}

		internal int HandoverTargetCapacity
		{
			get { return HandoverInt("TargetCapacity"); }
			set { HandoverInt("TargetCapacity", value); }
		}

		internal string HandoverSourceComposition
		{
			get { return HandoverText("SourceComposition"); }
			set { HandoverText("SourceComposition", value); }
		}

		internal string HandoverTargetCompositionBefore
		{
			get { return HandoverText("TargetCompositionBefore"); }
			set { HandoverText("TargetCompositionBefore", value); }
		}

		internal string HandoverTargetCompositionAfter
		{
			get { return HandoverText("TargetCompositionAfter"); }
			set { HandoverText("TargetCompositionAfter", value); }
		}

		internal string HandoverItemId
		{
			get { return HandoverText("ItemId"); }
			set { HandoverText("ItemId", value); }
		}

		internal string HandoverItemBlueprint
		{
			get { return HandoverText("ItemBlueprint"); }
			set { HandoverText("ItemBlueprint", value); }
		}

		internal string HandoverItemDestinationId
		{
			get { return HandoverText("ItemDestinationId"); }
			set { HandoverText("ItemDestinationId", value); }
		}

		internal string HandoverItemEscrowKey
		{
			get { return HandoverText("ItemEscrowKey"); }
			set { HandoverText("ItemEscrowKey", value); }
		}

		internal int HandoverItemCount
		{
			get { return HandoverInt("ItemCount"); }
			set { HandoverInt("ItemCount", value); }
		}

		internal int HandoverItemPhase
		{
			get { return HandoverInt("ItemPhase"); }
			set { HandoverInt("ItemPhase", value); }
		}

		internal int HandoverItemDestinationKind
		{
			get { return HandoverInt("ItemDestinationKind"); }
			set { HandoverInt("ItemDestinationKind", value); }
		}

		internal int HandoverMovedItems
		{
			get { return HandoverInt("MovedItems"); }
			set { HandoverInt("MovedItems", value); }
		}

		internal int HandoverItemMovedBefore
		{
			get { return HandoverInt("ItemMovedBefore"); }
			set { HandoverInt("ItemMovedBefore", value); }
		}

		internal int HandoverItemMovedAfter
		{
			get { return HandoverInt("ItemMovedAfter"); }
			set { HandoverInt("ItemMovedAfter", value); }
		}

		internal bool HandoverInventoryDone
		{
			get { return HandoverInt("InventoryDone") == 1; }
			set { HandoverInt("InventoryDone", value ? 1 : 0); }
		}

		internal bool HandoverEffectsDone
		{
			get { return HandoverInt("EffectsDone") == 1; }
			set { HandoverInt("EffectsDone", value ? 1 : 0); }
		}

		internal bool HandoverFlagsValid()
		{
			return BinaryHandoverFlag("Quarantined")
				&& BinaryHandoverFlag("InventoryDone")
				&& BinaryHandoverFlag("EffectsDone");
		}

		private bool BinaryHandoverFlag(string Name)
		{
			int value = HandoverInt(Name);
			return value == 0 || value == 1;
		}

		/// <summary>
		/// Ticks past the scaffold's due time before an improvement whose successor never
		/// appeared is given up on. Generous: the scaffold only ticks in an active zone, so a
		/// founder who walks away mid-build must be able to come back to the same work.
		/// </summary>
		public const long AbandonGraceTicks = 2400L;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID;
		}

		internal static bool CarryLiquidDurable(GameObject SourceObject, GameObject TargetObject,
			r_KingdomImprovement Receipt, out int Moved)
		{
			Moved = 0;
			if (Receipt == null || !Receipt.HandoverFlagsValid())
				return FailHandover(Receipt, "Handover boolean flags are corrupt.");
			if (Receipt.HandoverQuarantined) return false;
			if (!ExactHandoverObjects(SourceObject, TargetObject, Receipt))
				return FailHandover(Receipt, "Handover endpoints do not match their exact IDs.");
			if (Receipt.HandoverPhase < 0 || Receipt.HandoverPhase > 3)
				return FailHandover(Receipt, "Liquid handover phase is corrupt.");
			if (!ExactLiquidReceiptShape(Receipt))
				return FailHandover(Receipt, "Liquid handover receipt is corrupt or unbounded.");
			LiquidVolume source = SourceObject.GetPart<LiquidVolume>();
			LiquidVolume target = TargetObject.GetPart<LiquidVolume>();
			if (source == null || source.Volume <= 0)
			{
				if (Receipt.HandoverPhase >= 1 && Receipt.HandoverSourceVolumeBefore > 0)
					return ResumeDrainedLiquid(SourceObject, TargetObject, Receipt, source, target,
						out Moved);
				Receipt.HandoverPhase = 3;
				return true;
			}
			if (target == null)
				return FailHandover(Receipt, "Liquid source has no exact successor vessel.");
			if (Receipt.HandoverPhase == 0)
			{
				int space = target.MaxVolume < 0 ? int.MaxValue : target.MaxVolume - target.Volume;
				string sourceComposition = EncodeLiquid(source);
				string targetComposition = EncodeLiquid(target);
				if (source.Volume <= 0 || target.Volume < 0 || space < source.Volume
					|| (long)target.Volume + source.Volume > int.MaxValue
					|| sourceComposition == null || targetComposition == null
					|| !TryFrozenLiquid(sourceComposition, source.Volume, out _)
					|| (target.Volume > 0
						&& !TryFrozenLiquid(targetComposition, target.Volume, out _)))
					return FailHandover(Receipt, "Successor liquid capacity changed before handover.");
				Receipt.HandoverSourceId = SourceObject.ID;
				Receipt.HandoverTargetId = TargetObject.ID;
				Receipt.HandoverSourceVolumeBefore = source.Volume;
				Receipt.HandoverSourceVolumeAfter = 0;
				Receipt.HandoverTargetVolumeBefore = target.Volume;
				Receipt.HandoverTargetVolumeAfter = -1;
				Receipt.HandoverTargetCapacity = target.MaxVolume;
				Receipt.HandoverSourceComposition = sourceComposition;
				Receipt.HandoverTargetCompositionBefore = targetComposition;
				Receipt.HandoverTargetCompositionAfter = null;
				Receipt.HandoverPhase = 1;
			}
			if (Receipt.HandoverPhase == 3)
			{
				if (!ExactLiquidEndpoint(SourceObject, source, Receipt.HandoverSourceVolumeAfter,
					EncodeEmptyLiquid()) || !ExactLiquidEndpoint(TargetObject, target,
					Receipt.HandoverTargetVolumeAfter, Receipt.HandoverTargetCompositionAfter)
					|| target.MaxVolume != Receipt.HandoverTargetCapacity)
					return FailHandover(Receipt, "Settled liquid receipt no longer matches both vessels.");
				Moved = Receipt.HandoverSourceVolumeBefore;
				return true;
			}
			if (Receipt.HandoverPhase != 1
				|| !ExactLiquidEndpoint(SourceObject, source, Receipt.HandoverSourceVolumeBefore,
					Receipt.HandoverSourceComposition)
				|| !ExactLiquidEndpoint(TargetObject, target, Receipt.HandoverTargetVolumeBefore,
					Receipt.HandoverTargetCompositionBefore)
				|| target.MaxVolume != Receipt.HandoverTargetCapacity)
				return FailHandover(Receipt, "Pending liquid receipt is ambiguous before drain.");

			int drained = KingdomLiquids.Drain(source, Receipt.HandoverSourceVolumeBefore);
			if (drained != Receipt.HandoverSourceVolumeBefore
				|| !ExactLiquidEndpoint(SourceObject, source, 0, EncodeEmptyLiquid())
				|| !ExactHandoverObjects(SourceObject, TargetObject, Receipt)
				|| !ReferenceEquals(target, TargetObject.GetPart<LiquidVolume>()))
				return FailHandover(Receipt, "Liquid drain did not leave the exact frozen aftermath.");
			Receipt.HandoverPhase = 2;
			return ResumeDrainedLiquid(SourceObject, TargetObject, Receipt, source, target,
				out Moved);
		}

		private static bool ResumeDrainedLiquid(GameObject SourceObject, GameObject TargetObject,
			r_KingdomImprovement Receipt, LiquidVolume Source, LiquidVolume Target, out int Moved)
		{
			Moved = 0;
			if (Receipt.HandoverPhase == 3)
			{
				if (ExactLiquidEndpoint(SourceObject, Source, Receipt.HandoverSourceVolumeAfter,
						EncodeEmptyLiquid()) && ExactLiquidEndpoint(TargetObject, Target,
						Receipt.HandoverTargetVolumeAfter, Receipt.HandoverTargetCompositionAfter)
					&& Target.MaxVolume == Receipt.HandoverTargetCapacity
					&& ExactHandoverObjects(SourceObject, TargetObject, Receipt))
				{
					Moved = Receipt.HandoverSourceVolumeBefore;
					return true;
				}
				return FailHandover(Receipt, "Completed liquid aftermath changed before recovery.");
			}
			if (Receipt.HandoverPhase != 2 || Source == null || Target == null
				|| !ExactLiquidEndpoint(SourceObject, Source, 0, EncodeEmptyLiquid())
				|| !ExactLiquidEndpoint(TargetObject, Target, Receipt.HandoverTargetVolumeBefore,
					Receipt.HandoverTargetCompositionBefore)
				|| Target.MaxVolume != Receipt.HandoverTargetCapacity
				|| !ExactHandoverObjects(SourceObject, TargetObject, Receipt))
				return FailHandover(Receipt, "Drained liquid receipt is ambiguous before fill.");
			LiquidVolume frozen;
			if (!TryFrozenLiquid(Receipt.HandoverSourceComposition,
				Receipt.HandoverSourceVolumeBefore, out frozen))
				return FailHandover(Receipt, "Frozen liquid composition cannot be reconstructed.");
			bool accepted = false;
			try { accepted = Target.MixWith(frozen, PouredFrom: SourceObject); }
			catch (System.Exception ex)
			{
				return CompensateLiquid(SourceObject, TargetObject, Receipt, Source, Target,
					frozen, "Liquid fill threw: " + ex.Message);
			}
			if (!ExactHandoverObjects(SourceObject, TargetObject, Receipt)
				|| !ReferenceEquals(Source, SourceObject.GetPart<LiquidVolume>())
				|| !ReferenceEquals(Target, TargetObject.GetPart<LiquidVolume>()))
				return FailHandover(Receipt, "A liquid endpoint changed during fill callback.");
			int expected = Receipt.HandoverTargetVolumeBefore
				+ Receipt.HandoverSourceVolumeBefore;
			if (accepted && Target.Volume == expected
				&& ExactLiquidEndpoint(SourceObject, Source, 0, EncodeEmptyLiquid()))
			{
				string after = EncodeLiquid(Target);
				if (after == null || !ExactLiquidEndpoint(TargetObject, Target, expected, after))
					return FailHandover(Receipt,
						"Liquid fill produced an invalid or unbounded after-composition.");
				Receipt.HandoverTargetVolumeAfter = Target.Volume;
				Receipt.HandoverTargetCompositionAfter = after;
				Receipt.HandoverPhase = 3;
				Moved = Receipt.HandoverSourceVolumeBefore;
				return true;
			}
			return CompensateLiquid(SourceObject, TargetObject, Receipt, Source, Target,
				frozen, "Liquid fill was vetoed or partial.");
		}

		private static bool CompensateLiquid(GameObject SourceObject, GameObject TargetObject,
			r_KingdomImprovement Receipt, LiquidVolume Source, LiquidVolume Target,
			LiquidVolume Frozen, string Failure)
		{
			// Exact compensation is possible only when target still equals its frozen before-image.
			if (!ExactLiquidEndpoint(TargetObject, Target, Receipt.HandoverTargetVolumeBefore,
				Receipt.HandoverTargetCompositionBefore))
				return FailHandover(Receipt, Failure + " Target changed, so compensation is unsafe.");
			try { Source.MixWith(Frozen, PouredFrom: TargetObject); }
			catch (System.Exception ex)
			{
				return FailHandover(Receipt, Failure + " Compensation threw: " + ex.Message);
			}
			if (!ExactLiquidEndpoint(SourceObject, Source, Receipt.HandoverSourceVolumeBefore,
					Receipt.HandoverSourceComposition)
				|| !ExactLiquidEndpoint(TargetObject, Target, Receipt.HandoverTargetVolumeBefore,
					Receipt.HandoverTargetCompositionBefore)
				|| Target.MaxVolume != Receipt.HandoverTargetCapacity
				|| !ExactHandoverObjects(SourceObject, TargetObject, Receipt))
				return FailHandover(Receipt, Failure + " Exact compensation could not be proved.");
			Receipt.HandoverPhase = 0;
			Receipt.HandoverFailure = Failure;
			return false;
		}

		internal static bool CarryInventoryDurable(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, out int Moved)
		{
			Moved = Receipt == null ? 0 : Receipt.HandoverMovedItems;
			if (Receipt == null || !Receipt.HandoverFlagsValid())
				return FailHandover(Receipt, "Handover boolean flags are corrupt.");
			if (Receipt.HandoverQuarantined) return false;
			if (Receipt.HandoverMovedItems < 0) return FailHandover(Receipt,
				"Inventory moved count is corrupt.");
			if (!ExactHandoverObjects(Source, Target, Receipt))
				return FailHandover(Receipt, "Inventory endpoints changed before transfer.");
			if (Receipt.HandoverInventoryDone)
			{
				if (Receipt.HandoverItemPhase != 0
					|| !string.IsNullOrEmpty(Receipt.HandoverItemEscrowKey)
					|| (Source.Inventory != null && Source.Inventory.Objects.Count != 0))
					return FailHandover(Receipt,
						"Settled inventory handover no longer has an empty exact source.");
				Moved = Receipt.HandoverMovedItems;
				return true;
			}
			if (Source.Inventory == null)
			{
				if (Receipt.HandoverItemPhase != 0
					|| !string.IsNullOrEmpty(Receipt.HandoverItemEscrowKey))
					return FailHandover(Receipt,
						"Inventory source part disappeared with an item pending.");
				Receipt.HandoverInventoryDone = true;
				return true;
			}
			if (Receipt.HandoverItemPhase == 0
				&& !string.IsNullOrEmpty(Receipt.HandoverItemEscrowKey))
				return FailHandover(Receipt,
					"An inventory escrow root exists without its pending phase.");
			if (Receipt.HandoverItemPhase != 0
				&& !ResumePendingItem(Source, Target, Where, Receipt)) return false;
			List<GameObject> held = new List<GameObject>(Source.Inventory.Objects);
			for (int i = 0; i < held.Count; i++)
			{
				GameObject item = held[i];
				if (!ExactItemOwner(item, Source, Receipt: null))
					return FailHandover(Receipt, "Inventory source changed while enumerated.");
				if (!BoundedIdentity(item.ID) || string.IsNullOrEmpty(item.Blueprint)
					|| item.Blueprint.Length > 256 || item.Count <= 0
					|| Receipt.HandoverMovedItems == int.MaxValue)
					return FailHandover(Receipt, "Inventory item identity or count is out of bounds.");
				string destination = Target?.Inventory != null ? Target.ID : CellKey(Where);
				if (!BoundedIdentity(destination))
					return FailHandover(Receipt, "Inventory destination cannot be frozen exactly.");
				Receipt.HandoverItemId = item.ID;
				Receipt.HandoverItemBlueprint = item.Blueprint;
				Receipt.HandoverItemCount = item.Count;
				Receipt.HandoverItemDestinationKind = Target?.Inventory != null ? 1 : 2;
				Receipt.HandoverItemDestinationId = destination;
				Receipt.HandoverItemMovedBefore = Receipt.HandoverMovedItems;
				Receipt.HandoverItemMovedAfter = Receipt.HandoverMovedItems + 1;
				Receipt.HandoverItemEscrowKey = EscrowKeyFor(Source, item,
					Receipt.HandoverItemMovedBefore);
				if (!RootEscrowItem(Source, Target, Where, Receipt, item)) return false;
				Receipt.HandoverItemPhase = 1;
				Inventory sourceInventory = Source.Inventory;
				bool removed;
				try { removed = sourceInventory.RemoveObjectFromInventory(item, null,
					Silent: true, NoStack: true); }
				catch (System.Exception ex)
				{
					if (!ReproveEscrowItem(Source, Target, Where, Receipt, item)) return false;
					if (!ReferenceEquals(sourceInventory, Source.Inventory)
						|| !ExactHandoverObjects(Source, Target, Receipt))
						return FailHandover(Receipt,
							"Inventory removal changed an endpoint before throwing: " + ex.Message);
					if (ExactItemOwner(item, Source, Receipt))
					{
						if (!RetirePendingItem(Receipt, item)) return false;
						Receipt.HandoverFailure = "Inventory removal threw before changing ownership: "
							+ ex.Message;
						return false;
					}
					if (ExactDestination(item, Target, Where, Receipt))
						return SettlePendingItem(Target, Where, Receipt, item);
					if (ExactLooseItem(item, Receipt))
					{
						Receipt.HandoverItemPhase = 2;
						return PlacePendingItem(Source, Target, Where, Receipt, item);
					}
					return FailHandover(Receipt,
						"Inventory removal lost, moved, replaced, or restacked its source before throwing: "
						+ ex.Message);
				}
				if (!ReproveEscrowItem(Source, Target, Where, Receipt, item)) return false;
				if (!ReferenceEquals(sourceInventory, Source.Inventory))
					return FailHandover(Receipt,
						"Inventory source part changed during removal callback.");
				if (!removed)
				{
					if (ExactItemOwner(item, Source, Receipt))
					{
						if (!RetirePendingItem(Receipt, item)) return false;
						return false;
					}
					if (ExactDestination(item, Target, Where, Receipt))
						return SettlePendingItem(Target, Where, Receipt, item);
					if (ExactLooseItem(item, Receipt))
						return RestoreItem(Source, Target, Where, Receipt, item,
							"Inventory removal refused after removing its exact source item.");
					return FailHandover(Receipt,
						"Inventory removal refused after changing exact ownership.");
				}
				if (ExactItemOwner(item, Source, Receipt))
					return FailHandover(Receipt,
						"Inventory removal reported success without changing ownership.");
				if (ExactDestination(item, Target, Where, Receipt))
					return SettlePendingItem(Target, Where, Receipt, item);
				if (!ExactLooseItem(item, Receipt))
					return FailHandover(Receipt, "Inventory removal lost, moved, replaced, or restacked its source.");
				Receipt.HandoverItemPhase = 2;
				if (!PlacePendingItem(Source, Target, Where, Receipt, item)) return false;
			}
			if ((Source.Inventory != null && Source.Inventory.Objects.Count != 0)
				|| Receipt.HandoverItemPhase != 0)
				return FailHandover(Receipt,
					"Inventory source changed after its frozen items were transferred.");
			Moved = Receipt.HandoverMovedItems;
			Receipt.HandoverInventoryDone = true;
			return true;
		}

		private static bool ResumePendingItem(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt)
		{
			if (Receipt.HandoverItemPhase < 1 || Receipt.HandoverItemPhase > 4
				|| !BoundedIdentity(Receipt.HandoverItemId)
				|| string.IsNullOrEmpty(Receipt.HandoverItemBlueprint)
				|| Receipt.HandoverItemBlueprint.Length > 256
				|| Receipt.HandoverItemCount <= 0
				|| !BoundedIdentity(Receipt.HandoverItemDestinationId)
				|| Receipt.HandoverItemDestinationKind < 1
				|| Receipt.HandoverItemDestinationKind > 2
				|| Receipt.HandoverItemMovedBefore < 0
				|| Receipt.HandoverItemMovedBefore == int.MaxValue
				|| Receipt.HandoverItemMovedAfter != Receipt.HandoverItemMovedBefore + 1
				|| (Receipt.HandoverMovedItems != Receipt.HandoverItemMovedBefore
					&& Receipt.HandoverMovedItems != Receipt.HandoverItemMovedAfter))
				return FailHandover(Receipt, "Pending inventory receipt is malformed.");
			GameObject item;
			if (!TryEscrowItem(Source, Target, Where, Receipt, out item)) return false;
			if (ExactDestination(item, Target, Where, Receipt))
				return SettlePendingItem(Target, Where, Receipt, item);
			if (Receipt.HandoverItemPhase >= 3)
				return FailHandover(Receipt,
					"Count-settlement phase lost its exact destination item.");
			if (ExactItemOwner(item, Source, Receipt))
			{
				// Exact source ownership proves prior attempt had no physical effect.
				if (!RetirePendingItem(Receipt, item)) return false;
				return false;
			}
			if (ExactEnteringCell(item, Source, Target, Where, Receipt))
			{
				item.Physics.CurrentCell = null;
				if (!ExactLooseItem(item, Receipt))
					return FailHandover(Receipt,
						"Cell-entry recovery could not restore the exact escrow item to loose state.");
			}
			if (!ExactLooseItem(item, Receipt))
				return FailHandover(Receipt, "Pending inventory item has an ambiguous owner.");
			Receipt.HandoverItemPhase = 2;
			return PlacePendingItem(Source, Target, Where, Receipt, item);
		}

		private static bool PlacePendingItem(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, GameObject Item)
		{
			Inventory destination = Receipt.HandoverItemDestinationKind == 1
				? Target?.Inventory : null;
			GameObject accepted = null;
			try
			{
				if (Receipt.HandoverItemDestinationKind == 1 && destination != null)
					accepted = destination.AddObject(Item, null, Silent: true, NoStack: true);
				else if (Receipt.HandoverItemDestinationKind == 2 && Where != null)
					accepted = Where.AddObject(Item, NoStack: true, Silent: true);
				else return RestoreItem(Source, Target, Where, Receipt, Item,
					"Inventory destination disappeared before AddObject.");
			}
			catch (System.Exception ex)
			{
				if (!ReproveEscrowItem(Source, Target, Where, Receipt, Item)) return false;
				if (ExactHandoverObjects(Source, Target, Receipt)
					&& (Receipt.HandoverItemDestinationKind != 1
						|| ReferenceEquals(destination, Target.Inventory))
					&& ExactDestination(Item, Target, Where, Receipt))
					return SettlePendingItem(Target, Where, Receipt, Item);
				return RestoreItem(Source, Target, Where, Receipt, Item,
					"Inventory AddObject threw: " + ex.Message);
			}
			if (!ReproveEscrowItem(Source, Target, Where, Receipt, Item)) return false;
			if (!ExactHandoverObjects(Source, Target, Receipt))
				return FailHandover(Receipt, "Inventory endpoint changed during AddObject callback.");
			if ((Receipt.HandoverItemDestinationKind == 1
					&& !ReferenceEquals(destination, Target.Inventory))
				|| !ReferenceEquals(accepted, Item))
				return FailHandover(Receipt,
					"Inventory AddObject replaced its exact destination or return identity.");
			if (ExactDestination(Item, Target, Where, Receipt))
				return SettlePendingItem(Target, Where, Receipt, Item);
			return RestoreItem(Source, Target, Where, Receipt, Item,
				"Inventory destination did not retain exact item ownership.");
		}

		private static bool RestoreItem(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, GameObject Item, string Failure)
		{
			if (ExactDestination(Item, Target, Where, Receipt))
				return SettlePendingItem(Target, Where, Receipt, Item);
			// Cell.AddObject runs EnvironmentalUpdate after assigning CurrentCell but before
			// Cell.Objects.Add. That exact escrow topology is recoverable: detach only the frozen
			// reference, prove it loose, then restore it to its exact source inventory.
			if (ExactEnteringCell(Item, Source, Target, Where, Receipt))
			{
				Item.Physics.CurrentCell = null;
				if (!ExactLooseItem(Item, Receipt))
					return FailHandover(Receipt,
						Failure + " Cell-entry recovery could not prove an exact loose item.");
			}
			if (!ExactLooseItem(Item, Receipt) && !ExactItemOwner(Item, Source, Receipt))
				return FailHandover(Receipt, Failure + " Exact recovery source is unavailable.");
			if (!ExactItemOwner(Item, Source, Receipt))
			{
				if (Source.Inventory == null)
					return FailHandover(Receipt,
						Failure + " Exact source inventory no longer exists.");
				GameObject restored;
				try { restored = Source.Inventory.AddObject(Item, null,
					Silent: true, NoStack: true); }
				catch (System.Exception ex)
				{
					ReproveEscrowItem(Source, Target, Where, Receipt, Item);
					return FailHandover(Receipt, Failure + " Recovery threw: " + ex.Message);
				}
				if (!ReproveEscrowItem(Source, Target, Where, Receipt, Item)) return false;
				if (!ReferenceEquals(restored, Item))
					return FailHandover(Receipt,
						Failure + " Recovery replaced the exact item identity.");
			}
			if (!ExactItemOwner(Item, Source, Receipt))
				return FailHandover(Receipt, Failure + " Exact source recovery failed.");
			if (!RetirePendingItem(Receipt, Item)) return false;
			Receipt.HandoverFailure = Failure;
			return false;
		}

		private static bool ExactHandoverObjects(GameObject Source, GameObject Target,
			r_KingdomImprovement Receipt)
		{
			if (!GameObject.Validate(Source) || !GameObject.Validate(Target) || Receipt == null
				|| Source.GetPart<r_KingdomImprovement>() != Receipt
				|| Source.CurrentCell == null || Target.CurrentCell != Source.CurrentCell) return false;
			if (Receipt.HandoverSourceId == null && Receipt.HandoverTargetId == null)
			{
				if (!BoundedIdentity(Source.ID) || !BoundedIdentity(Target.ID)) return false;
				Receipt.HandoverSourceId = Source.ID;
				Receipt.HandoverTargetId = Target.ID;
			}
			return BoundedIdentity(Receipt.HandoverSourceId)
				&& BoundedIdentity(Receipt.HandoverTargetId)
				&& Source.ID == Receipt.HandoverSourceId && Target.ID == Receipt.HandoverTargetId
				&& ExactHandoverAuthority(Source, Target, Receipt);
		}

		private static bool ExactHandoverAuthority(GameObject Source, GameObject Target,
			r_KingdomImprovement Receipt)
		{
			string frozen = Receipt?.HandoverConstructionReceipt;
			string sourceReceipt = Source?.GetStringProperty(KingdomConstruction.ReceiptProperty);
			string targetReceipt = Target?.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(frozen)) return false;
			if (!BoundedIdentity(frozen) || sourceReceipt != frozen || targetReceipt != frozen)
				return false;
			KingdomConstructionJob job;
			Zone zone = Source.CurrentZone;
			KingdomSystem system = The.Game == null
				? null : The.Game.RequireSystem<KingdomSystem>();
			GameObject exactSource;
			GameObject exactTarget;
			return zone != null && Target.CurrentZone == zone
				&& KingdomConstruction.TryFind(frozen, out job)
				&& job.Route == KingdomConstructionRoute.Improvement
				&& !KingdomConstructionRules.IsTerminal(job.Phase)
				&& Receipt.Working && !string.IsNullOrEmpty(Receipt.SuccessorBlueprint)
				&& Receipt.SuccessorBlueprint.Length <= 256
				&& !string.IsNullOrEmpty(Receipt.SuccessorKey)
				&& Receipt.SuccessorKey.Length <= KingdomConstructionRules.MaxTargetChars
				&& Source.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1
				&& Target.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1
				&& Target.Blueprint == Receipt.SuccessorBlueprint
				&& Target.GetStringProperty(KingdomUpgrade.BuildKeyProperty)
					== Receipt.SuccessorKey
				&& job.SubjectId == Source.ID && job.SourceId == Source.ID
				&& job.OutputId == Target.ID && job.TargetKey == Receipt.SuccessorKey
				&& Source.CurrentCell == zone.GetCell(job.X, job.Y)
				&& Target.CurrentCell == Source.CurrentCell
				&& KingdomConstruction.Owns(system, zone, job)
				&& KingdomConstruction.IsCurrent(job)
				&& KingdomConstruction.FindExactId(zone, Source.ID, out exactSource)
					== KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(exactSource, Source)
				&& KingdomConstruction.FindExactId(zone, Target.ID, out exactTarget)
					== KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(exactTarget, Target);
		}

		private static bool BoundedIdentity(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= 128;
		}

		private static string EscrowKeyFor(GameObject Source, GameObject Item, int MovedBefore)
		{
			return EscrowKeyFor(Source?.ID, Item?.ID, MovedBefore);
		}

		private static string EscrowKeyFor(string SourceId, string ItemId, int MovedBefore)
		{
			if (!BoundedIdentity(SourceId) || !BoundedIdentity(ItemId) || MovedBefore < 0)
				return null;
			byte[] bytes = Encoding.UTF8.GetBytes(SourceId + "\n" + ItemId + "\n"
				+ MovedBefore.ToString(CultureInfo.InvariantCulture));
			byte[] digest;
			using (SHA256 hash = SHA256.Create()) digest = hash.ComputeHash(bytes);
			StringBuilder key = new StringBuilder(HandoverEscrowPrefix, 96);
			for (int i = 0; i < digest.Length; i++)
				key.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
			return key.ToString();
		}

		private static bool BoundedEscrowKey(string Key)
		{
			return !string.IsNullOrEmpty(Key) && Key.Length <= 128
				&& Key.StartsWith(HandoverEscrowPrefix, StringComparison.Ordinal);
		}

		private static bool RootEscrowItem(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, GameObject Item)
		{
			string expected = EscrowKeyFor(Source, Item, Receipt.HandoverItemMovedBefore);
			if (The.Game == null || !BoundedEscrowKey(expected)
				|| Receipt.HandoverItemEscrowKey != expected)
				return FailHandover(Receipt, "The inventory escrow key could not be frozen exactly.");
			object collision;
			if (The.Game.ObjectGameState.TryGetValue(expected, out collision)
				&& !ReferenceEquals(collision, Item))
				return FailHandover(Receipt,
					"The inventory escrow key collides with another exact object.");
			The.Game.SetObjectGameState(expected, Item);
			if (!The.Game.ObjectGameState.TryGetValue(expected, out collision)
				|| !ReferenceEquals(collision, Item))
				return FailHandover(Receipt,
					"The exact inventory item did not remain rooted before removal.");
			GameObject rooted;
			if (!TryEscrowItem(Source, Target, Where, Receipt, out rooted)) return false;
			return (ReferenceEquals(rooted, Item)
				&& EscrowTopologyOf(Source, Target, Where, Receipt, Item)
					== KingdomHandoverItemTopology.Source) || FailHandover(Receipt,
						"The rooted inventory item did not remain at its exact source.");
		}

		private static bool TryEscrowItem(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, out GameObject Item)
		{
			Item = null;
			if (Receipt == null) return false;
			string key = Receipt?.HandoverItemEscrowKey;
			object rooted;
			if (The.Game == null || !BoundedEscrowKey(key)
				|| key != EscrowKeyFor(Source?.ID, Receipt?.HandoverItemId,
					Receipt.HandoverItemMovedBefore)
				|| !The.Game.ObjectGameState.TryGetValue(key, out rooted))
				return FailHandover(Receipt, "The exact inventory escrow root is absent or malformed.");
			Item = rooted as GameObject;
			if (!GameObject.Validate(Item) || Item.ID != Receipt.HandoverItemId
				|| Item.Blueprint != Receipt.HandoverItemBlueprint
				|| Item.Count != Receipt.HandoverItemCount
				|| EscrowTopologyOf(Source, Target, Where, Receipt, Item)
					== KingdomHandoverItemTopology.Invalid)
				return FailHandover(Receipt,
					"The rooted inventory item is missing, duplicated, replaced, or restacked.");
			return true;
		}

		private static bool ReproveEscrowItem(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, GameObject Expected)
		{
			GameObject rooted;
			if (!TryEscrowItem(Source, Target, Where, Receipt, out rooted)) return false;
			return ReferenceEquals(rooted, Expected) || FailHandover(Receipt,
				"The inventory callback replaced its exact rooted object reference.");
		}

		private static KingdomHandoverItemTopology EscrowTopologyOf(GameObject Source,
			GameObject Target,
			Cell Where, r_KingdomImprovement Receipt, GameObject Item)
		{
			if (!ExactHandoverObjects(Source, Target, Receipt) || Where == null
				|| Source.CurrentCell != Where || Target.CurrentCell != Where
				|| !ReferenceEquals(The.Game?.GetObjectGameState(
					Receipt.HandoverItemEscrowKey), Item)) return KingdomHandoverItemTopology.Invalid;
			int sourceRefs = ReferenceCount(Source.Inventory?.Objects, Item);
			int targetRefs = ReferenceCount(Target.Inventory?.Objects, Item);
			int cellRefs = ReferenceCount(Where.GetObjects(), Item);
			int idOccurrences;
			int exactOccurrences;
			if (!CountZoneIdentity(Where.ParentZone, Receipt.HandoverItemId, Item,
				out idOccurrences, out exactOccurrences) || Item.Physics == null)
				return KingdomHandoverItemTopology.Invalid;
			int inventoryOwner = Item.Physics.InInventory == null ? 0
				: ReferenceEquals(Item.Physics.InInventory, Source) ? 1
				: ReferenceEquals(Item.Physics.InInventory, Target) ? 2 : 3;
			int cellOwner = Item.CurrentCell == null ? 0
				: ReferenceEquals(Item.CurrentCell, Where) ? 1 : 2;
			return KingdomConstructionRules.HandoverItemTopology(sourceRefs, targetRefs,
				cellRefs, idOccurrences, exactOccurrences, inventoryOwner, cellOwner);
		}

		private static bool CountZoneIdentity(Zone Zone, string Id, GameObject Exact,
			out int Occurrences, out int ExactOccurrences)
		{
			Occurrences = 0;
			ExactOccurrences = 0;
			if (Zone == null || !BoundedIdentity(Id) || Exact == null) return false;
			List<GameObject> pending = new List<GameObject>(Zone.GetObjects());
			HashSet<GameObject> expanded = new HashSet<GameObject>();
			int visited = 0;
			while (pending.Count > 0)
			{
				if (++visited > MaxHandoverTopologyObjects) return false;
				int last = pending.Count - 1;
				GameObject item = pending[last];
				pending.RemoveAt(last);
				if (item == null) continue;
				if (item.ID == Id)
				{
					Occurrences++;
					if (ReferenceEquals(item, Exact)) ExactOccurrences++;
				}
				if (!expanded.Add(item)) return false;
				if (item.Inventory != null)
					for (int i = 0; i < item.Inventory.Objects.Count; i++)
						pending.Add(item.Inventory.Objects[i]);
			}
			return Occurrences <= 1 && ExactOccurrences <= 1;
		}

		private static bool ExactEnteringCell(GameObject Item, GameObject Source,
			GameObject Target, Cell Where, r_KingdomImprovement Receipt)
		{
			return EscrowTopologyOf(Source, Target, Where, Receipt, Item)
				== KingdomHandoverItemTopology.EnteringCell;
		}

		private static bool ExactLiquidEndpoint(GameObject Owner, LiquidVolume Part, int Volume,
			string Composition)
		{
			return GameObject.Validate(Owner) && Part != null && Part.ParentObject == Owner
				&& ReferenceEquals(Owner.GetPart<LiquidVolume>(), Part) && Part.Volume == Volume
				&& EncodeLiquid(Part) == Composition;
		}

		private static bool ExactItemOwner(GameObject Item, GameObject Owner,
			r_KingdomImprovement Receipt)
		{
			return GameObject.Validate(Item) && GameObject.Validate(Owner) && Owner.Inventory != null
				&& Item.Physics != null && Item.Physics.InInventory == Owner
				&& ReferenceCount(Owner.Inventory.Objects, Item) == 1
				&& (Receipt == null || (ExactEscrowReference(Receipt, Item)
					&& Item.ID == Receipt.HandoverItemId
					&& Item.Blueprint == Receipt.HandoverItemBlueprint
					&& Item.Count == Receipt.HandoverItemCount));
		}

		private static bool ExactLooseItem(GameObject Item, r_KingdomImprovement Receipt)
		{
			return GameObject.Validate(Item) && Item.Physics != null
				&& Item.Physics.InInventory == null && Item.CurrentCell == null
				&& ExactEscrowReference(Receipt, Item)
				&& Item.ID == Receipt.HandoverItemId && Item.Blueprint == Receipt.HandoverItemBlueprint
				&& Item.Count == Receipt.HandoverItemCount;
		}

		private static bool ExactDestination(GameObject Item, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt)
		{
			if (!GameObject.Validate(Item) || !ExactEscrowReference(Receipt, Item)
				|| Item.ID != Receipt.HandoverItemId
				|| Item.Blueprint != Receipt.HandoverItemBlueprint
				|| Item.Count != Receipt.HandoverItemCount) return false;
			if (Receipt.HandoverItemDestinationKind == 1)
				return GameObject.Validate(Target) && ExactItemOwner(Item, Target, Receipt)
					&& Target.ID == Receipt.HandoverItemDestinationId;
			return Receipt.HandoverItemDestinationKind == 2 && Where != null
				&& Item.Physics != null && Item.Physics.InInventory == null
				&& Item.CurrentCell == Where
				&& ReferenceCount(Where.GetObjects(), Item) == 1
				&& CellKey(Where) == Receipt.HandoverItemDestinationId;
		}

		private static bool ExactEscrowReference(r_KingdomImprovement Receipt, GameObject Item)
		{
			object rooted;
			return Receipt != null && GameObject.Validate(Item) && The.Game != null
				&& BoundedEscrowKey(Receipt.HandoverItemEscrowKey)
				&& The.Game.ObjectGameState.TryGetValue(Receipt.HandoverItemEscrowKey, out rooted)
				&& ReferenceEquals(rooted, Item);
		}

		private static int ReferenceCount(IList<GameObject> Objects, GameObject Item)
		{
			if (Objects == null || Item == null) return 0;
			int count = 0;
			for (int i = 0; i < Objects.Count; i++) if (ReferenceEquals(Objects[i], Item)) count++;
			return count;
		}

		private static string CellKey(Cell Where)
		{
			if (Where?.ParentZone == null || string.IsNullOrEmpty(Where.ParentZone.ZoneID)) return null;
			return Where.ParentZone.ZoneID + ":" + Where.X.ToString(CultureInfo.InvariantCulture)
				+ "," + Where.Y.ToString(CultureInfo.InvariantCulture);
		}

		private static bool SettlePendingItem(GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, GameObject Item)
		{
			if (!ExactDestination(Item, Target, Where, Receipt))
				return FailHandover(Receipt, "Inventory destination identity is not exact.");
			if (Receipt.HandoverItemPhase < 3) Receipt.HandoverItemPhase = 3;
			int current = Receipt.HandoverMovedItems;
			if (current == Receipt.HandoverItemMovedBefore)
				Receipt.HandoverMovedItems = Receipt.HandoverItemMovedAfter;
			else if (current != Receipt.HandoverItemMovedAfter)
				return FailHandover(Receipt,
					"Inventory moved count has a third value outside its frozen receipt.");
			if (Receipt.HandoverMovedItems != Receipt.HandoverItemMovedAfter) return false;
			Receipt.HandoverItemPhase = 4;
			return RetirePendingItem(Receipt, Item);
		}

		private static bool RetirePendingItem(r_KingdomImprovement Receipt, GameObject Item)
		{
			string key = Receipt?.HandoverItemEscrowKey;
			object rooted;
			if (The.Game == null || !BoundedEscrowKey(key)
				|| !The.Game.ObjectGameState.TryGetValue(key, out rooted)
				|| !ReferenceEquals(rooted, Item))
				return FailHandover(Receipt,
					"The exact inventory escrow root changed before receipt cleanup.");
			The.Game.ObjectGameState.Remove(key);
			if (The.Game.ObjectGameState.ContainsKey(key))
				return FailHandover(Receipt,
					"The exact inventory escrow root could not be retired after settlement.");
			ClearPendingItem(Receipt);
			return true;
		}

		private static void ClearPendingItem(r_KingdomImprovement Receipt)
		{
			// Phase zero is the commit marker. Stale identity properties are harmless if a save lands
			// between these property writes; no later callback consults them while phase is zero.
			Receipt.HandoverItemPhase = 0;
			Receipt.HandoverItemId = null;
			Receipt.HandoverItemBlueprint = null;
			Receipt.HandoverItemDestinationId = null;
			Receipt.HandoverItemEscrowKey = null;
			Receipt.HandoverItemCount = 0;
			Receipt.HandoverItemDestinationKind = 0;
			Receipt.HandoverItemMovedBefore = 0;
			Receipt.HandoverItemMovedAfter = 0;
		}

		internal static bool FailHandover(r_KingdomImprovement Receipt, string Failure)
		{
			if (Receipt != null)
			{
				Receipt.HandoverQuarantined = true;
				Receipt.HandoverFailure = Failure != null && Failure.Length > 2048
					? Failure.Substring(0, 2048) : Failure;
			}
			return false;
		}

		private static string EncodeEmptyLiquid()
		{
			return "v1";
		}

		private static bool ExactLiquidReceiptShape(r_KingdomImprovement Receipt)
		{
			if (Receipt.HandoverPhase == 0) return true;
			if (Receipt.HandoverSourceVolumeBefore == 0)
				return Receipt.HandoverPhase == 3
					&& Receipt.HandoverSourceVolumeAfter == 0;
			if (Receipt.HandoverSourceVolumeBefore < 0
				|| Receipt.HandoverSourceVolumeAfter != 0
				|| Receipt.HandoverTargetVolumeBefore < 0) return false;
			long expected = (long)Receipt.HandoverTargetVolumeBefore
				+ Receipt.HandoverSourceVolumeBefore;
			if (expected > int.MaxValue
				|| (Receipt.HandoverTargetCapacity != -1
					&& Receipt.HandoverTargetCapacity < expected)
				|| !TryFrozenLiquid(Receipt.HandoverSourceComposition,
					Receipt.HandoverSourceVolumeBefore, out _)
				|| (Receipt.HandoverTargetVolumeBefore == 0
					? Receipt.HandoverTargetCompositionBefore != EncodeEmptyLiquid()
					: !TryFrozenLiquid(Receipt.HandoverTargetCompositionBefore,
						Receipt.HandoverTargetVolumeBefore, out _))) return false;
			if (Receipt.HandoverPhase < 3)
				return Receipt.HandoverTargetVolumeAfter == -1
					&& Receipt.HandoverTargetCompositionAfter == null;
			return Receipt.HandoverTargetVolumeAfter == (int)expected
				&& TryFrozenLiquid(Receipt.HandoverTargetCompositionAfter,
					(int)expected, out _);
		}

		private static string EncodeLiquid(LiquidVolume Volume)
		{
			if (Volume == null || Volume.Volume <= 0) return EncodeEmptyLiquid();
			if (Volume.ComponentLiquids == null || Volume.ComponentLiquids.Count == 0
				|| Volume.ComponentLiquids.Count > MaxHandoverComponents) return null;
			List<string> keys = new List<string>(Volume.ComponentLiquids.Keys);
			keys.Sort(StringComparer.Ordinal);
			StringBuilder text = new StringBuilder("v1");
			int total = 0;
			for (int i = 0; i < keys.Count; i++)
			{
				int proportion = Volume.ComponentLiquids[keys[i]];
				if (string.IsNullOrEmpty(keys[i]) || keys[i].Length > 128
					|| proportion <= 0 || proportion > 1000) return null;
				total += proportion;
				text.Append(';').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(keys[i])))
					.Append(',').Append(proportion.ToString(
						CultureInfo.InvariantCulture));
				if (text.Length > MaxHandoverText) return null;
			}
			return total == 1000 ? text.ToString() : null;
		}

		private static bool TryFrozenLiquid(string Text, int Volume, out LiquidVolume Frozen)
		{
			Frozen = null;
			if (Volume <= 0 || string.IsNullOrEmpty(Text) || Text.Length > MaxHandoverText) return false;
			string[] terms = Text.Split(';');
			if (terms.Length < 2 || terms.Length - 1 > MaxHandoverComponents
				|| terms[0] != "v1") return false;
			Dictionary<string, int> components = new Dictionary<string, int>();
			int total = 0;
			for (int i = 1; i < terms.Length; i++)
			{
				string[] pair = terms[i].Split(',');
				int proportion;
				string key;
				try { key = Encoding.UTF8.GetString(Convert.FromBase64String(pair[0])); }
				catch { return false; }
				if (pair.Length != 2 || string.IsNullOrEmpty(key) || key.Length > 128
					|| components.ContainsKey(key)
					|| !int.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture,
						out proportion) || proportion <= 0 || proportion > 1000) return false;
				components.Add(key, proportion);
				total += proportion;
			}
			if (total != 1000) return false;
			Frozen = new LiquidVolume();
			Frozen.Volume = Volume;
			Frozen.ComponentLiquids = components;
			return EncodeLiquid(Frozen) == Text;
		}

		/// <summary>
		/// Puts the settlement's intent for this work on the work itself, so the founder can
		/// read it by looking at the thing rather than only in the Charter.
		/// </summary>
		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			if (HandoverQuarantined)
			{
				E.Postfix.Append("\n{{r|This improvement handover requires inspection: ")
					.Append(HandoverFailure ?? "its physical receipt is ambiguous")
					.Append(".}}");
			}
			else if (Working)
			{
				E.Postfix.Append("\n{{rules|The settlement is raising this into ")
					.Append(KingdomUpgrade.DisplayNameOf(SuccessorKey))
					.Append(".}}");
			}
			else if (Held)
			{
				E.Postfix.Append("\n{{rules|The settlement will leave this as it is.}}");
			}
			return base.HandleEvent(E);
		}

		/// <summary>
		/// Watches for the scaffold's own completion and moves everything across when it comes.
		/// Called once a turn while an improvement is under way, and does nothing at all until
		/// the scaffold is gone, which is cheap.
		/// </summary>
		/// <param name="TimeTick">Engine tick, for the abandonment grace period.</param>
		public void PollHandover(long TimeTick)
		{
			if (GameObject.Validate(ref Scaffold))
			{
				return;
			}
			Cell cell = ParentObject?.CurrentCell;
			GameObject successor;
			KingdomPhysicalLookupState successorState = FindSuccessor(cell, out successor);
			if (successorState == KingdomPhysicalLookupState.Ambiguous)
			{
				FailHandover(this, "The improvement successor ID is duplicated or malformed.");
				string duplicateReceipt = ParentObject.GetStringProperty(
					KingdomConstruction.ReceiptProperty);
				if (KingdomConstruction.TryFind(duplicateReceipt, out var duplicate))
					KingdomConstruction.Quarantine(ref duplicate, HandoverFailure);
				return;
			}
			if (successorState == KingdomPhysicalLookupState.Exact)
			{
				string receipt = ParentObject.GetStringProperty(KingdomConstruction.ReceiptProperty);
				KingdomConstructionJob job;
				if (!string.IsNullOrEmpty(receipt))
				{
					KingdomSystem system = The.Game == null
						? null : The.Game.RequireSystem<KingdomSystem>();
					if (!KingdomConstruction.TryFind(receipt, out job)
						|| !KingdomConstruction.Owns(system, ParentObject.CurrentZone, job)
						|| job.Route != KingdomConstructionRoute.Improvement
						|| KingdomConstructionRules.IsTerminal(job.Phase)) return;
					KingdomConstruction.Bind(successor, job);
				}
				KingdomUpgrade.HandOver(ParentObject, successor, SuccessorKey);
				return;
			}
			if (TimeTick < WorkCompleteTick + AbandonGraceTicks)
			{
				return;
			}
			// Paid work never evaporates. Publish the missing projection as retryable; the durable
			// construction step can raise the exact same successor without charging again.
			string id = ParentObject.GetStringProperty(KingdomConstruction.ReceiptProperty);
			KingdomConstructionJob outstanding;
			if (!string.IsNullOrEmpty(id) && KingdomConstruction.TryFind(id, out outstanding)
				&& outstanding.Phase != KingdomConstructionPhase.Outstanding)
			{
				KingdomConstruction.FinishProjection(ref outstanding, false, false,
					"The paid improvement scaffold is absent; projection remains outstanding.");
				MessageQueue.AddPlayerMessage("{{r|The improvement scaffold is gone, but its paid receipt remains queued.}}");
				KingdomLog.Log("improvement projection outstanding: " + ParentObject.Blueprint);
			}
		}

		/// <summary>
		/// The finished successor, once it is standing in the same cell. Matched on blueprint and
		/// on the settlement's own build mark, so an unrelated object dropped in the cell mid-
		/// build can never be mistaken for the new work.
		/// </summary>
		/// <param name="Where">Cell this work stands in. Null finds nothing.</param>
		public KingdomPhysicalLookupState FindSuccessor(Cell Where, out GameObject Successor)
		{
			Successor = null;
			if (Where == null || string.IsNullOrEmpty(SuccessorBlueprint))
			{
				return KingdomPhysicalLookupState.Absent;
			}
			string receipt = ParentObject.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (!string.IsNullOrEmpty(receipt))
			{
				if (!KingdomConstruction.TryFind(receipt, out var exactJob)
					|| string.IsNullOrEmpty(exactJob.OutputId))
					return KingdomPhysicalLookupState.Ambiguous;
				KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(
					Where.ParentZone, exactJob.OutputId, out var candidate);
				if (state != KingdomPhysicalLookupState.Exact) return state;
				if (candidate == ParentObject || candidate.CurrentCell != Where
					|| candidate.Blueprint != SuccessorBlueprint
					|| candidate.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1
					|| candidate.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != SuccessorKey
					|| candidate.GetStringProperty(KingdomConstruction.ReceiptProperty) != receipt)
					return KingdomPhysicalLookupState.Ambiguous;
				Successor = candidate;
				return KingdomPhysicalLookupState.Exact;
			}
			List<GameObject> objects = Where.GetObjects();
			int count = 0;
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject candidate = objects[i];
				if (candidate != ParentObject && candidate.Blueprint == SuccessorBlueprint
					&& candidate.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1
					&& candidate.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == SuccessorKey
					&& string.IsNullOrEmpty(candidate.GetStringProperty(
						KingdomConstruction.ReceiptProperty)))
				{
					count++;
					if (count == 1) Successor = candidate;
				}
			}
			if (count == 0) return KingdomPhysicalLookupState.Absent;
			if (count == 1)
			{
				GameObject global;
				if (KingdomConstruction.FindExactId(Where.ParentZone, Successor.ID,
					out global) == KingdomPhysicalLookupState.Exact
					&& ReferenceEquals(global, Successor)) return KingdomPhysicalLookupState.Exact;
			}
			Successor = null;
			return KingdomPhysicalLookupState.Ambiguous;
		}
	}
}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// Works that get better. A design may name what it grows into; when the settlement has
	/// earned it, the settlement raises the new work itself, out of what its stores can spare,
	/// through the same scaffold every commission uses &mdash; so an improvement is visibly work
	/// happening on the ground, not a number changing.
	/// <para>
	/// Improvements are AUTOMATIC, with a standing opt-out. That is a deliberate choice against
	/// the alternative of offering each one:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Clicking through a confirmation for every work at every stage is the
	/// foreman's job the mod refuses to hand the player. The founder sets intent; the settlement
	/// acts on it.</description></item>
	/// <item><description>An improvement only ever adds. Everything the old work held and
	/// everything the settlement had marked on it is carried across, so a founder who never
	/// opens the Charter loses nothing they had and simply comes home to a better
	/// settlement.</description></item>
	/// <item><description>It is never a surprise, because it is announced three times before it
	/// is a fact: once per game when the settlement first becomes able to do this at all, once
	/// when a particular work starts, and continuously by the scaffold standing in the
	/// cell.</description></item>
	/// <item><description>It can always be refused, permanently, without losing anything: a
	/// single work, or this whole ground, can be held as it is, and that choice persists and is
	/// visible on the object itself.</description></item>
	/// <item><description>It cannot cause a thirst. The cost never draws the stores below the
	/// reserve the settlement lives on, and it never spends a settler who is doing something
	/// else.</description></item>
	/// </list>
	/// <para>
	/// The arithmetic and every refusal sentence are in <see cref="KingdomUpgradeRules"/>.
	/// </para>
	/// </summary>
	public static class KingdomUpgrade
	{
		internal static void RetryConstruction(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.Improvement
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var successor))
			{
				return;
			}
			GameObject work;
			KingdomPhysicalLookupState workState = KingdomConstruction.FindExactId(
				Z, Job.SubjectId, out work);
			if (workState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"The improvement predecessor ID resolves to more than one loaded object.");
				return;
			}
			if (!EnsureExactImprovementPredecessor(System, Z, work, Job))
			{
				KingdomConstructionJob complete = Job;
				GameObject result;
				int results = r_KingdomScaffold.FindExactSuccessors(Z, Job,
					successor.Blueprint, null, out result);
				if (results > 1)
				{
					KingdomConstruction.Quarantine(ref complete,
						"More than one exact improvement successor carries this receipt.");
					return;
				}
				if (results != 1 || !r_KingdomScaffold.HasRemovalProof(result, Job.SubjectId))
				{
					KingdomConstruction.Quarantine(ref complete,
						"The improvement predecessor is not exact and no proved successor replaces it.");
					return;
				}
				if (KingdomConstruction.Complete(ref complete))
					r_KingdomScaffold.TellCompletion(System, result, complete);
				return;
			}
			r_KingdomImprovement improvement = work.GetPart<r_KingdomImprovement>();
			GameObject finished = null;
			KingdomPhysicalLookupState finishedState = improvement == null
				? KingdomPhysicalLookupState.Absent
				: improvement.FindSuccessor(work.CurrentCell, out finished);
			if (finishedState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob ambiguous = Job;
				KingdomConstruction.Quarantine(ref ambiguous,
					"The improvement successor ID is duplicated or malformed.");
				return;
			}
			if (finishedState == KingdomPhysicalLookupState.Exact)
			{
				KingdomConstruction.Bind(finished, Job);
				HandOver(work, finished, Job.TargetKey);
				return;
			}
			if (improvement != null && improvement.Working)
			{
				if (!ExpectedImprovementScaffold(improvement.Scaffold, work.CurrentCell, successor)
					|| !KingdomConstruction.HasReceipt(improvement.Scaffold, Job))
				{
					KingdomConstructionJob ambiguous = Job;
					KingdomConstruction.Quarantine(ref ambiguous,
						"The linked improvement scaffold is absent, moved, changed, or unreceipted.");
					return;
				}
				r_KingdomScaffold scaffoldPart = improvement.Scaffold.GetPart<r_KingdomScaffold>();
				if (scaffoldPart.RemainingTicks <= 0 && scaffoldPart.LastWorkedTick > 0)
					scaffoldPart.RetryDurable(System, Z, Job);
				else
				{
					KingdomConstructionJob working = Job;
					KingdomConstruction.FinishProjection(ref working, true, true);
				}
				return;
			}
			ProjectImprovement(System, work, successor, Job, out _, out _);
		}

		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.Improvement
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var successor)) return;
			if (Job.Phase == KingdomConstructionPhase.Complete)
			{
				GameObject completed;
				int completedCount = r_KingdomScaffold.FindExactSuccessors(Z, Job,
					successor.Blueprint, null, out completed);
				if (completedCount > 1)
				{
					KingdomConstructionJob duplicate = Job;
					KingdomConstruction.Quarantine(ref duplicate,
						"More than one terminal improvement successor carries this receipt.");
				}
				else if (completedCount == 1)
				{
					if (!r_KingdomScaffold.HasRemovalProof(completed, Job.SubjectId))
					{
						KingdomConstructionJob unproved = Job;
						KingdomConstruction.Quarantine(ref unproved,
							"The terminal improvement successor lacks predecessor-removal proof.");
					}
					else r_KingdomScaffold.TellCompletion(System, completed, Job);
				}
				return;
			}
			GameObject work;
			KingdomPhysicalLookupState workState = KingdomConstruction.FindExactId(
				Z, Job.SubjectId, out work);
			if (workState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"The improvement predecessor ID resolves to more than one loaded object.");
				return;
			}
			if (!EnsureExactImprovementPredecessor(System, Z, work, Job))
			{
				KingdomConstructionJob absent = Job;
				GameObject completed;
				int completedCount = r_KingdomScaffold.FindExactSuccessors(Z, Job,
					successor.Blueprint, null, out completed);
				if (completedCount == 1
					&& r_KingdomScaffold.HasRemovalProof(completed, Job.SubjectId))
				{
					if (KingdomConstruction.Complete(ref absent))
						r_KingdomScaffold.TellCompletion(System, completed, absent);
				}
				else
				{
					KingdomConstruction.Quarantine(ref absent, completedCount > 1
						? "More than one exact improvement successor carries this receipt."
						: "The improvement predecessor moved, changed, or disappeared without exact removal proof.");
				}
				return;
			}
			r_KingdomImprovement carriedIntent = GameObject.Validate(work)
				? work.GetPart<r_KingdomImprovement>() : null;
			Cell expectedCell = Z.GetCell(Job.X, Job.Y);
			GameObject exactScaffold;
			KingdomPhysicalLookupState scaffoldState = FindImprovementScaffold(
				expectedCell, successor, Job, out exactScaffold);
			if (scaffoldState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"The improvement scaffold is duplicated, moved, replaced, or malformed.");
				return;
			}
			if (carriedIntent != null && carriedIntent.Working
				&& GameObject.Validate(carriedIntent.Scaffold)
				&& (!ExpectedImprovementScaffold(carriedIntent.Scaffold, expectedCell, successor)
					|| !KingdomConstruction.HasReceipt(carriedIntent.Scaffold, Job)))
			{
				KingdomConstructionJob moved = Job;
				KingdomConstruction.Quarantine(ref moved,
					"The exact improvement scaffold moved, changed, or lost its receipt.");
				return;
			}
			GameObject scaffold = carriedIntent != null && carriedIntent.Working
					? (scaffoldState == KingdomPhysicalLookupState.Exact
						&& ReferenceEquals(exactScaffold, carriedIntent.Scaffold)
						? exactScaffold : null)
				: (scaffoldState == KingdomPhysicalLookupState.Exact ? exactScaffold : null);
			KingdomConstructionJob inspected = Job;
			if (GameObject.Validate(scaffold))
			{
				GameObject attemptedScaffold = scaffold;
				r_KingdomScaffold scaffoldPart = scaffold.GetPart<r_KingdomScaffold>();
				int finalPending = scaffold.GetIntProperty(r_KingdomScaffold.FinalPendingProperty);
				if (finalPending != 0 && finalPending != 1)
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The improvement scaffold final flag is not an exact boolean.");
					return;
				}
				if (Job.Phase == KingdomConstructionPhase.ProjectionPending
					&& finalPending == 0)
					KingdomConstruction.FinishProjection(ref inspected, true, true);
				else if (Job.Phase == KingdomConstructionPhase.Working
					|| Job.Phase == KingdomConstructionPhase.ProjectionPending)
					scaffoldPart.AdvanceDurable(System, Z, Job, The.Game.TimeTicks);
				else if (Job.Phase == KingdomConstructionPhase.Outstanding
					&& scaffoldPart.RemainingTicks <= 0 && scaffoldPart.LastWorkedTick > 0)
					scaffoldPart.RetryDurable(System, Z, Job);
				// Re-read after callbacks: the scaffold may now be gone and its exact successor present.
				scaffold = carriedIntent != null && carriedIntent.Working
					&& ExpectedImprovementScaffold(carriedIntent.Scaffold, expectedCell, successor)
					&& KingdomConstruction.HasReceipt(carriedIntent.Scaffold, Job)
						? carriedIntent.Scaffold : null;
				if (GameObject.Validate(attemptedScaffold))
				{
					if (!ExpectedImprovementScaffold(attemptedScaffold, expectedCell, successor)
						|| !KingdomConstruction.HasReceipt(attemptedScaffold, Job))
					{
						KingdomConstruction.Quarantine(ref inspected,
							"The improvement scaffold changed during its continuation callback.");
					}
					return;
				}
			}
			if (GameObject.Validate(work) && work.GetIntProperty(BuiltProperty) == 1
				&& work.GetStringProperty(BuildKeyProperty) == Job.TargetKey
				&& work.ID != Job.SubjectId)
			{
				if (!r_KingdomScaffold.HasRemovalProof(work, Job.SubjectId))
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The improvement successor lacks predecessor-removal proof.");
					return;
				}
				if (KingdomConstruction.Complete(ref inspected))
					r_KingdomScaffold.TellCompletion(System, work, inspected);
				return;
			}
			if (GameObject.Validate(work) && !string.IsNullOrEmpty(Job.SubjectId)
				&& work.ID != Job.SubjectId)
			{
				return;
			}
			if (GameObject.Validate(work))
			{
				r_KingdomImprovement improvement = work.GetPart<r_KingdomImprovement>();
				GameObject finished = null;
				KingdomPhysicalLookupState finishedState = improvement == null
					? KingdomPhysicalLookupState.Absent
					: improvement.FindSuccessor(work.CurrentCell, out finished);
				if (finishedState == KingdomPhysicalLookupState.Ambiguous)
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The improvement successor ID is duplicated or malformed.");
					return;
				}
				if (finishedState == KingdomPhysicalLookupState.Exact)
				{
					KingdomConstruction.Bind(finished, inspected);
					HandOver(work, finished, Job.TargetKey);
					return;
				}
			}
			else
			{
				GameObject result;
				KingdomPhysicalLookupState resultState = KingdomConstruction.FindReceipt(
					Z, Job, out result);
				if (resultState == KingdomPhysicalLookupState.Ambiguous)
				{
					KingdomConstruction.Quarantine(ref inspected,
						"More than one physical object carries the improvement receipt.");
					return;
				}
				if (GameObject.Validate(result) && result.GetIntProperty(BuiltProperty) == 1
					&& result.GetStringProperty(BuildKeyProperty) == Job.TargetKey
					&& r_KingdomScaffold.HasRemovalProof(result, Job.SubjectId))
				{
					if (KingdomConstruction.Complete(ref inspected))
						r_KingdomScaffold.TellCompletion(System, result, inspected);
				}
				return;
			}
			if (scaffold != null)
			{
				if (Job.Phase != KingdomConstructionPhase.Working)
				{
					KingdomConstruction.FinishProjection(ref inspected, true, true);
				}
				return;
			}
			KingdomConstruction.Quarantine(ref inspected,
				"The improvement projection has no safely identifiable scaffold or successor.");
		}

		private static bool HasActiveConstruction(GameObject Work)
		{
			return KingdomConstruction.ReceiptBlocksCurrent(Work);
		}

		public static bool Enabled => Options.GetOption("r_TAF_OptionImprovement") != "No";

		public const string BuiltProperty = "KingdomBuilt";

		public const string AdoptedProperty = "KingdomAdopted";

		/// <summary>
		/// The registry key a standing work was raised as, stamped by this file when one work
		/// becomes another so the next link of a chain resolves exactly rather than by guessing
		/// from the blueprint. Absent on everything built before improvements existed, which is
		/// why <see cref="DesignKeyOf"/> falls back to the blueprint.
		/// </summary>
		public const string BuildKeyProperty = "KingdomBuildKey";

		/// <summary>Game state remembering that the founder has been told, once, that the
		/// settlement betters its own works.</summary>
		public const string NoticedState = "r_TAF_ImprovementNoticed";

		/// <summary>Prefix of the per-zone game state carrying "leave this ground as it is".
		/// Keyed by zone rather than by settlement because a founder's wish to keep a camp crude
		/// is about a place, and because it then works without a new serialized field on any
		/// existing save.</summary>
		public const string GroundHeldState = "r_TAF_ImprovementHeld:";

		// Chains live beside the catalog rather than inside KingdomRules.BuildEntry so the registry
		// parser needs one line of wiring instead of a rewritten entry type. Filled by
		// KingdomData's single pass over the mergeable KingdomBuildings root, in that file's load
		// order and with the same last-wins override, so a third-party file can add a chain to our
		// design, replace ours, or clear it by re-declaring the entry without an UpgradesTo.
		private static readonly Dictionary<string, KingdomUpgradeRules.UpgradeChain> _chains = new Dictionary<string, KingdomUpgradeRules.UpgradeChain>();

		/// <summary>Every upgrade chain the loaded <c>KingdomBuildings</c> files declare, keyed by
		/// the design that grows.</summary>
		public static Dictionary<string, KingdomUpgradeRules.UpgradeChain> Chains
		{
			get
			{
				KingdomData.EnsureBuildings();
				return _chains;
			}
		}

		/// <summary>
		/// Forgets every registered chain. Called by the registry loader before it re-reads the
		/// XML streams, so a reload never leaves a chain behind for a design that no longer
		/// declares one.
		/// </summary>
		public static void ClearChains()
		{
			_chains.Clear();
		}

		/// <summary>
		/// Registers one entry's upgrade attributes as the registry parses it. Call once per
		/// <c>&lt;building&gt;</c> element that parsed successfully, with the raw attribute
		/// strings; all five may be null, which registers "this design never changes".
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Blank keys are ignored.</param>
		/// <param name="UpgradesTo">Raw <c>UpgradesTo</c> attribute.</param>
		/// <param name="UpgradeCost">Raw <c>UpgradeCost</c> attribute.</param>
		/// <param name="UpgradeTicks">Raw <c>UpgradeTicks</c> attribute.</param>
		/// <param name="UpgradeCrew">Raw <c>UpgradeCrew</c> attribute.</param>
		/// <param name="UpgradeMinStage">Raw <c>UpgradeMinStage</c> attribute.</param>
		public static void RegisterChain(string Key, string UpgradesTo, string UpgradeCost, string UpgradeTicks, string UpgradeCrew, string UpgradeMinStage)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			if (!KingdomUpgradeRules.TryParseUpgradeAttributes(Key, UpgradesTo, UpgradeCost, UpgradeTicks, UpgradeCrew, UpgradeMinStage, out KingdomUpgradeRules.UpgradeChain chain, out string error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + error);
				// A malformed chain leaves the design unable to change rather than half-chained,
				// and clears whatever an earlier file registered under this key: the entry that
				// carried it has just been replaced.
				chain = new KingdomUpgradeRules.UpgradeChain();
			}
			_chains[Key] = chain;
		}

		/// <summary>Drops the parsed chains so the next read re-reads the XML. For the dev
		/// reload wish; ordinary play never needs it.</summary>
		public static void Reload()
		{
			KingdomData.Reload();
		}

		/// <summary>The chain a design declares, if any.</summary>
		/// <param name="Key">Registry key of the standing design.</param>
		/// <param name="Chain">The chain, or null.</param>
		/// <returns>True only when a usable chain was declared.</returns>
		public static bool TryGetChain(string Key, out KingdomUpgradeRules.UpgradeChain Chain)
		{
			Chain = null;
			if (string.IsNullOrEmpty(Key))
			{
				return false;
			}
			KingdomData.EnsureBuildings();
			return _chains.TryGetValue(Key, out Chain) && Chain != null && Chain.Defined;
		}

		/// <summary>
		/// What a standing work counts as in the registry. Prefers the key the settlement
		/// stamped when it raised or improved the work, then the key it was adopted under, and
		/// only then reads the blueprint back &mdash; which is what lets works raised before
		/// improvements existed take part without a migration.
		/// </summary>
		/// <param name="Work">The standing object.</param>
		/// <returns>A registry key, or null when no design matches.</returns>
		public static string DesignKeyOf(GameObject Work)
		{
			if (Work == null)
			{
				return null;
			}
			string stamped = Work.GetStringProperty(BuildKeyProperty);
			if (!string.IsNullOrEmpty(stamped))
			{
				return stamped;
			}
			string adopted = Work.GetStringProperty(KingdomAdopt.AdoptedKeyProperty);
			if (!string.IsNullOrEmpty(adopted))
			{
				return adopted;
			}
			List<KingdomRules.BuildEntry> entries = KingdomData.Buildings;
			List<string> keys = new List<string>();
			List<bool> chained = new List<bool>();
			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i].Blueprint == Work.Blueprint)
				{
					keys.Add(entries[i].Key);
					chained.Add(TryGetChain(entries[i].Key, out _));
				}
			}
			int chosen = KingdomUpgradeRules.ChooseDesignIndex(chained.ToArray());
			return (chosen < 0) ? null : keys[chosen];
		}

		/// <summary>What a design is called, for a sentence. Falls back to the key so a
		/// half-loaded registry still produces readable prose.</summary>
		public static string DisplayNameOf(string Key)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return "something better";
			}
			if (KingdomData.TryGetBuilding(Key, out KingdomRules.BuildEntry entry))
			{
				return entry.Name;
			}
			return Key;
		}

		/// <summary>Whether the founder has told the settlement to leave this whole ground as it
		/// is.</summary>
		/// <param name="Z">Zone to ask about. Null is never held.</param>
		public static bool IsGroundHeld(Zone Z)
		{
			if (Z == null || The.Game == null)
			{
				return false;
			}
			return The.Game.GetIntGameState(GroundHeldState + Z.ZoneID) == 1;
		}

		/// <summary>Sets or clears "leave this ground as it is". Nothing standing is changed
		/// either way; only what the settlement will do next.</summary>
		/// <param name="Z">Zone to hold or release.</param>
		/// <param name="Hold">True to hold.</param>
		public static void SetGroundHeld(Zone Z, bool Hold)
		{
			if (Z != null && The.Game != null)
			{
				The.Game.SetIntGameState(GroundHeldState + Z.ZoneID, Hold ? 1 : 0);
			}
		}

		/// <summary>Everything the settlement knows about one work's improvement, without
		/// changing anything.</summary>
		public struct Assessment
		{
			/// <summary>False when there was nothing to assess at all.</summary>
			public bool Valid;

			public KingdomUpgradeRules.UpgradeVerdict Verdict;

			/// <summary>Registry key of the standing design, or null.</summary>
			public string Key;

			/// <summary>Registry key of the design it grows into, or null.</summary>
			public string SuccessorKey;

			/// <summary>The successor's registry entry, or null when it did not resolve.</summary>
			public KingdomRules.BuildEntry Successor;

			public int CostDrams;

			public int Reserve;

			public int Shortfall;

			public int CrewNeeded;

			public GrowthStage StageNeeded;

			public long BuildTicks;

			/// <summary>Sustained output this work contributes, in drams a day &mdash; the
			/// <c>water</c> it carries. Zero for a work the settlement does not drink from.
			/// </summary>
			public int SupportPerDay;

			/// <summary>Drams the settlement goes without while this work is rebuilt, from
			/// <c>KingdomUpgradeRules.OutputLost</c>.</summary>
			public int OutputLost;

			/// <summary>Drams the stores would still hold above the reserve once the improvement
			/// is paid for and the outage borne. Negative is the dip a forced improvement takes.
			/// </summary>
			public int Margin;

			/// <summary>What the absorption law was told about this work, kept so the Charter can
			/// disclose the dip without measuring it a second time.</summary>
			public KingdomUpgradeRules.AbsorptionDemand Demand;

			/// <summary>The sentence the founder is owed, or null when the verdict correctly
			/// says nothing.</summary>
			public string Reason;
		}

		/// <summary>
		/// Reads one standing work against the settlement and reports what its improvement is
		/// waiting on, changing nothing. Safe to call for a listing.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the work stands in.</param>
		/// <param name="Work">The standing work.</param>
		/// <param name="Survey">This pass's survey, for the stores.</param>
		/// <param name="FreeHands">Settlers not already spoken for.</param>
		/// <param name="OtherWorkUnderway">Whether another improvement is already going on this
		/// ground.</param>
		public static Assessment Assess(KingdomSystem System, Zone Z, GameObject Work, KingdomSurvey Survey, int FreeHands, bool OtherWorkUnderway)
		{
			Assessment assessment = default;
			if (System == null || !System.Founded || Z == null || Survey == null)
			{
				return assessment;
			}
			if (Work == null || !GameObject.Validate(Work))
			{
				return assessment;
			}
			assessment.Valid = true;
			assessment.Key = DesignKeyOf(Work);
			if (!TryGetChain(assessment.Key, out KingdomUpgradeRules.UpgradeChain chain))
			{
				assessment.Verdict = KingdomUpgradeRules.UpgradeVerdict.NoSuccessor;
				return assessment;
			}
			assessment.SuccessorKey = chain.SuccessorKey;
			bool known = KingdomData.TryGetBuilding(chain.SuccessorKey, out KingdomRules.BuildEntry successor)
				&& GameObjectFactory.Factory.GetBlueprintIfExists(successor.Blueprint) != null;
			assessment.Successor = known ? successor : null;
			KingdomRules.BuildEntry predecessor;
			int predecessorCost = KingdomData.TryGetBuilding(assessment.Key, out predecessor) ? predecessor.CostDrams : 0;
			assessment.CostDrams = KingdomUpgradeRules.CostDrams(known ? successor.CostDrams : 0, predecessorCost, chain.CostDramsOverride);
			assessment.BuildTicks = KingdomUpgradeRules.BuildTicks(known ? successor.BuildTicks : 0L, chain.BuildTicksOverride);
			assessment.CrewNeeded = KingdomUpgradeRules.CrewRequired(known ? successor.Staff : 0, chain.CrewOverride);
			assessment.StageNeeded = KingdomUpgradeRules.StageRequired(known ? successor.MinStage : GrowthStage.Camp, chain.HasMinStageOverride, chain.MinStageOverride);
			assessment.Reserve = KingdomUpgradeRules.ReserveDrams(System.Population, System.Stage);
			assessment.Shortfall = KingdomUpgradeRules.Shortfall(Survey.StoredWater, assessment.CostDrams, assessment.Reserve);
			// The absorption law (brief, Addendum 3). Measured here, judged in the rules half.
			assessment.Demand = MeasureAbsorption(System, Z, Work, predecessor, assessment.SuccessorKey, assessment.BuildTicks);
			assessment.SupportPerDay = assessment.Demand.SupportPerDay;
			assessment.OutputLost = KingdomUpgradeRules.OutputLost(assessment.Demand.SupportPerDay, assessment.BuildTicks);
			assessment.Margin = KingdomUpgradeRules.AbsorptionMargin(Survey.StoredWater, assessment.CostDrams, assessment.Reserve, assessment.OutputLost);
			r_KingdomImprovement improvement = Work.GetPart<r_KingdomImprovement>();
			assessment.Verdict = KingdomUpgradeRules.Assess(
				HasSuccessor: true,
				SuccessorKnown: known,
				StyleAllowed: !known || KingdomRules.StyleAllows(successor.Styles, System.Style),
				OurWork: Work.GetIntProperty(BuiltProperty) == 1 && Work.GetIntProperty(AdoptedProperty) != 1,
				AlreadyWorking: (improvement != null && improvement.Working)
					|| HasActiveConstruction(Work),
				HeldOnThisGround: IsGroundHeld(Z),
				HeldByFounder: improvement != null && improvement.Held,
				Stage: System.Stage,
				StageNeeded: assessment.StageNeeded,
				FreeHands: FreeHands,
				CrewNeeded: assessment.CrewNeeded,
				ContentsFit: ContentsWouldFit(Work, known ? successor.Blueprint : null),
				StoredWater: Survey.StoredWater,
				Cost: assessment.CostDrams,
				Reserve: assessment.Reserve,
				OtherWorkUnderway: OtherWorkUnderway,
				Absorption: assessment.Demand);
			assessment.Reason = KingdomUpgradeRules.ReasonLine(assessment.Verdict, KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName), known ? successor.Name : null, assessment.StageNeeded, assessment.CrewNeeded, assessment.Shortfall);
			// An improvement climbs within the ground it was staked on. When the next tier wants more
			// of the plot than the founder staked, or the ground it would grow onto is where a
			// household's yard trade stands, the founder is told by name and chooses.
			if (KingdomUpgradeRules.IsReady(assessment.Verdict)
				&& KingdomPlots.GrowRefused(Work, assessment.SuccessorKey, out string groundRefusal))
			{
				assessment.Verdict = KingdomUpgradeRules.UpgradeVerdict.NoGroundToGrow;
				assessment.Reason = groundRefusal;
			}
			return assessment;
		}

		/// <summary>
		/// Whether everything the predecessor is carrying would have somewhere to go. Read off
		/// the successor's BLUEPRINT rather than a created object, because this is asked before
		/// anything is built and the answer must be able to refuse.
		/// </summary>
		/// <param name="Work">The standing work.</param>
		/// <param name="SuccessorBlueprint">Blueprint of what it would become. Null fits
		/// nothing that is being carried.</param>
		public static bool ContentsWouldFit(GameObject Work, string SuccessorBlueprint)
		{
			if (Work == null)
			{
				return false;
			}
			int storedLiquid = 0;
			LiquidVolume volume = Work.GetPart<LiquidVolume>();
			if (volume != null && volume.Volume > 0)
			{
				storedLiquid = volume.Volume;
			}
			int heldItems = (Work.Inventory != null) ? Work.Inventory.Objects.Count : 0;
			GameObjectBlueprint blueprint = string.IsNullOrEmpty(SuccessorBlueprint) ? null : GameObjectFactory.Factory.GetBlueprintIfExists(SuccessorBlueprint);
			if (blueprint == null)
			{
				return storedLiquid <= 0 && heldItems <= 0;
			}
			int capacity = 0;
			if (blueprint.HasPart("LiquidVolume"))
			{
				capacity = blueprint.HasPartParameter("LiquidVolume", "MaxVolume")
					? blueprint.GetPartParameter("LiquidVolume", "MaxVolume", KingdomUpgradeRules.UnknownCapacity)
					: KingdomUpgradeRules.UnknownCapacity;
			}
			return KingdomUpgradeRules.ContentsWouldFit(storedLiquid, capacity, heldItems, blueprint.HasPart("Inventory"));
		}

		/// <summary>
		/// Measures everything the absorption law (brief, Addendum 3) judges, off real ground.
		/// Nothing here reads the clock, the age of the work, or how long anything has stood: the
		/// figures are what the settlement holds right now and what the designs declare. The only
		/// duration involved is the improvement's own build time, which sizes the outage and never
		/// causes the trigger.
		/// </summary>
		/// <param name="System">The kingdom, for its population.</param>
		/// <param name="Z">Zone the work stands in, walked once for the lodging elsewhere.</param>
		/// <param name="Work">The standing work.</param>
		/// <param name="Predecessor">Its registry entry, or null when it did not resolve.</param>
		/// <param name="SuccessorKey">Registry key of the design it would become.</param>
		/// <param name="BuildTicks">The improvement's build time, from
		/// <c>KingdomUpgradeRules.BuildTicks</c>.</param>
		public static KingdomUpgradeRules.AbsorptionDemand MeasureAbsorption(KingdomSystem System, Zone Z, GameObject Work, KingdomRules.BuildEntry Predecessor, string SuccessorKey, long BuildTicks)
		{
			KingdomUpgradeRules.AbsorptionDemand demand = KingdomUpgradeRules.AbsorptionDemand.None;
			demand.BuildTicks = BuildTicks;
			if (Predecessor == null)
			{
				return demand;
			}
			List<KindAmount> carries;
			if (!KingdomCatalogueRules.TryParseTally(Predecessor.Carries, out carries, out _))
			{
				// A malformed Carries is already reported by the catalogue validator. Everything it
				// managed to parse still counts, which is what TryParseTally hands back.
			}
			demand.IsHousing = string.Equals(Predecessor.Category, HousingCategory, StringComparison.OrdinalIgnoreCase);
			demand.Residents = KingdomCatalogueRules.AmountOf(carries, KingdomCatalogueRules.SupportRoof);
			demand.LuxuryCarried = KingdomCatalogueRules.AmountOf(carries, LuxurySupport);
			demand.SupportPerDay = KingdomCatalogueRules.AmountOf(carries, KingdomCatalogueRules.SupportWater);
			demand.CurrentShelter = ShelterOf(Predecessor.Key);
			int lodgingElsewhere = 0;
			int bestShelter = 0;
			string bestKey = null;
			if (Z != null)
			{
				foreach (GameObject item in Z.GetObjects())
				{
					if (item == Work || item.GetIntProperty(BuiltProperty) != 1)
					{
						continue;
					}
					string key = DesignKeyOf(item);
					KingdomRules.BuildEntry entry;
					if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
					{
						continue;
					}
					List<KindAmount> theirs;
					KingdomCatalogueRules.TryParseTally(entry.Carries, out theirs, out _);
					int roof = KingdomCatalogueRules.AmountOf(theirs, KingdomCatalogueRules.SupportRoof);
					if (roof <= 0)
					{
						continue;
					}
					lodgingElsewhere += roof;
					int shelter = ShelterOf(key);
					if (shelter > bestShelter || bestKey == null)
					{
						bestShelter = (shelter > bestShelter) ? shelter : bestShelter;
						bestKey = key;
					}
				}
			}
			int spare = lodgingElsewhere - ((System == null) ? 0 : System.Population);
			demand.SpareLodging = (spare > 0) ? spare : 0;
			demand.OfferedShelter = bestShelter;
			// Addendum 4: the best roof on offer must also be somewhere these people would actually
			// live. One citizen with nowhere to charge holds the rebuild exactly as a missing roof
			// does -- and holds it only: nobody is moved, and the refusal is named by the verdict.
			demand.QuartersRefused = demand.IsHousing
				&& KingdomUpgradeRules.QuartersRefused(KingdomQol.OfferOf(bestKey, Z), ResidentProfilesIn(Z), out _);
			demand.MaterialsInHand = Z == null || KingdomMaterials.CanPayUpgrade(Z, SuccessorKey, out _);
			demand.CraftMet = CraftReaches(System, Z, SuccessorKey);
			return demand;
		}

		/// <summary>
		/// The quality-of-life profiles of the citizens standing on this ground, for the Needs
		/// check Addendum 4 re-bases displacement tolerance onto. Read fresh every time, because
		/// nothing in that vocabulary is stored anywhere.
		/// </summary>
		/// <returns>Never null; empty for a null zone or a zone with nobody in it, which refuses
		/// nothing.</returns>
		private static List<QolProfile> ResidentProfilesIn(Zone Z)
		{
			List<QolProfile> profiles = new List<QolProfile>();
			if (Z == null)
			{
				return profiles;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomCitizen") == 1)
				{
					profiles.Add(KingdomQol.ProfileOf(item));
				}
			}
			return profiles;
		}

		/// <summary>The catalogue category housing is filed under, which the absorption law judges
		/// by displacement rather than by the output margin.</summary>
		public const string HousingCategory = "housing";

		/// <summary>The lifting support the luxury lane is denominated in. A design that lifts it
		/// houses somebody with a standard; one that does not houses settlers.</summary>
		public const string LuxurySupport = "luxury";

		/// <summary>
		/// Shelter rank of a design's own tier. A design that is not a plot has no roof state of
		/// its own and is read as a walled room, because a single-cell work the settlement raised
		/// stands as an object with its own walls rather than as open ground.
		/// </summary>
		/// <param name="Key">Registry key of the design.</param>
		public static int ShelterOf(string Key)
		{
			KingdomPlotRules.PlotSpec spec;
			if (string.IsNullOrEmpty(Key) || !KingdomPlots.TryGetSpec(Key, out spec) || spec == null)
			{
				return KingdomUpgradeRules.RoomShelter;
			}
			return KingdomPlotRules.ShelterRank(spec.Roof);
		}

		/// <summary>
		/// Whether the settlement's craft and learning reach a design. The district and territory
		/// gates are deliberately NOT applied: the predecessor is already standing on this ground,
		/// so re-asking where it may stand would refuse improvements the founder sited legitimately
		/// and could no longer do anything about.
		/// </summary>
		public static bool CraftReaches(KingdomSystem System, Zone Z, string Key)
		{
			KingdomRules.BuildEntry entry;
			if (System == null || string.IsNullOrEmpty(Key) || !KingdomData.TryGetBuilding(Key, out entry))
			{
				return true;
			}
			ZoningJudgement judgement = KingdomZoning.Judge(System, Z?.ZoneID, entry);
			return judgement.Verdict != ZoningVerdict.RefusedUnlearned
				&& judgement.Verdict != ZoningVerdict.RefusedTechLevel;
		}

		/// <summary>
		/// The settlement's improvement pass: completes any handover that finished while the
		/// founder was away, then starts at most one improvement and says at most one thing.
		/// Called from the settlement's zone-activated pass after growth, because growth is what
		/// decides which settlers are already spoken for.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Z">Zone the founder is standing in.</param>
		/// <param name="Survey">This pass's survey.</param>
		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			// HandOver asks for one more pass so a founder who stands and watches sees the next
			// work start. That call must not re-enter this one: the settlement betters one work
			// per visit, and a pass that started an improvement inside its own handover would
			// start as many as there were works to hand over.
			if (_resolving)
			{
				return;
			}
			_resolving = true;
			try
			{
				Resolve(System, Z, Survey);
			}
			finally
			{
				_resolving = false;
			}
		}

		// True while OnZoneActivated is inside its own pass. Not serialized and not state: it
		// describes the call stack, not the settlement.
		private static bool _resolving;

		private static void Resolve(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			int freeHands = System.Population - System.AssignedCrew;
			if (freeHands < 0)
			{
				freeHands = 0;
			}
			// Finished improvements are handed over here as well as on the work's own turn tick.
			// The tick is the responsive path; this is the one that cannot be missed, because the
			// settlement pass runs whenever the founder is standing here and a handover that never
			// happens leaves the old work standing beside its replacement forever.
			List<GameObject> pending = new List<GameObject>();
			foreach (GameObject item in Z.GetObjects())
			{
				r_KingdomImprovement working = item.GetPart<r_KingdomImprovement>();
				if (working != null && working.Working)
				{
					pending.Add(item);
				}
			}
			for (int i = 0; i < pending.Count; i++)
			{
				GameObject item = pending[i];
				if (!GameObject.Validate(ref item))
				{
					continue;
				}
				r_KingdomImprovement working = item.GetPart<r_KingdomImprovement>();
				if (working != null && working.Working)
				{
					working.PollHandover(The.Game.TimeTicks);
				}
			}
			List<GameObject> works = new List<GameObject>();
			bool otherWorkUnderway = false;
			foreach (GameObject item in Z.GetObjects())
			{
				r_KingdomImprovement improvement = item.GetPart<r_KingdomImprovement>();
				if ((improvement != null && improvement.Working) || HasActiveConstruction(item))
				{
					otherWorkUnderway = true;
				}
				if (item.GetIntProperty(BuiltProperty) == 1)
				{
					works.Add(item);
				}
			}
			GameObject readyWork = null;
			Assessment readyAssessment = default;
			GameObject speaksFirst = null;
			Assessment speaksFirstAssessment = default;
			bool anyImprovable = false;
			for (int i = 0; i < works.Count; i++)
			{
				Assessment assessment = Assess(System, Z, works[i], Survey, freeHands, otherWorkUnderway);
				if (!assessment.Valid || assessment.Verdict == KingdomUpgradeRules.UpgradeVerdict.NoSuccessor)
				{
					continue;
				}
				anyImprovable = true;
				if (KingdomUpgradeRules.IsReady(assessment.Verdict) && readyWork == null)
				{
					readyWork = works[i];
					readyAssessment = assessment;
				}
				else if (KingdomUpgradeRules.IsBlocked(assessment.Verdict) && speaksFirst == null
					&& works[i].RequirePart<r_KingdomImprovement>().AnnouncedReason != (int)assessment.Verdict)
				{
					speaksFirst = works[i];
					speaksFirstAssessment = assessment;
				}
			}
			if (anyImprovable)
			{
				GiveFirstNotice(System);
			}
			if (readyWork != null)
			{
				Begin(System, Z, readyWork, readyAssessment, Survey);
				return;
			}
			if (speaksFirst != null && speaksFirstAssessment.Reason != null)
			{
				speaksFirst.RequirePart<r_KingdomImprovement>().AnnouncedReason = (int)speaksFirstAssessment.Verdict;
				MessageQueue.AddPlayerMessage("{{K|" + speaksFirstAssessment.Reason + "}}");
				System.Ledger.Note("{{K|" + speaksFirstAssessment.Reason + "}}");
			}
		}

		/// <summary>
		/// Tells the founder once per game that the settlement betters its own works, and where
		/// to stop it. Modal on purpose and exactly once: this is the only moment in the mod
		/// where the settlement will change something the founder placed the order for, and
		/// nobody should ever discover that by finding it already done.
		/// </summary>
		/// <param name="System">The kingdom, for its name.</param>
		public static void GiveFirstNotice(KingdomSystem System)
		{
			if (The.Game == null || The.Game.GetIntGameState(NoticedState) == 1)
			{
				return;
			}
			The.Game.SetIntGameState(NoticedState, 1);
			Popup.Show(KingdomUpgradeRules.FirstNoticeLine(System.SeatName));
		}

		/// <summary>
		/// Raises the scaffolding for one improvement, in the predecessor's own cell. The
		/// predecessor keeps standing and keeps working for the whole build; nothing it holds is
		/// touched until its replacement is actually on the ground.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Z">Zone the work stands in.</param>
		/// <param name="Work">The work being improved.</param>
		/// <param name="A">Its assessment, which must be <c>Ready</c>.</param>
		/// <param name="Survey">This pass's survey, which the cost is drawn through so its
		/// counters stay true for everything that runs after.</param>
		/// <returns>True once scaffolding is actually standing and the water is actually
		/// spent.</returns>
		public static bool Begin(KingdomSystem System, Zone Z, GameObject Work, Assessment A, KingdomSurvey Survey)
		{
			if (!A.Valid || !KingdomUpgradeRules.IsReady(A.Verdict) || A.Successor == null)
			{
				return false;
			}
			Cell cell = Work?.CurrentCell;
			if (cell == null || HasActiveConstruction(Work)
				|| KingdomConstruction.HasActiveSubject(System, Z,
					KingdomConstructionRoute.Improvement, Work))
			{
				return false;
			}
			KingdomWaterDebit water = Survey.ReserveExactWater(A.CostDrams);
			KingdomMaterialDebit materials = KingdomMaterials.ReserveUpgradePayment(Z,
				A.SuccessorKey);
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
				KingdomMaterials.UpgradeCostFor(A.SuccessorKey));
			long now = The.Game.TimeTicks;
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, Z,
				KingdomConstructionRoute.Improvement, cell, Work, A.SuccessorKey, A.Key,
				A.CostDrams, claim, now, now + A.BuildTicks);
			KingdomConstructionStartResult funding = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (funding == KingdomConstructionStartResult.Refused)
			{
				KingdomLog.Log("improvement refused cleanly: "
					+ (fundingFailure ?? A.SuccessorKey));
				return false;
			}
			KingdomConstruction.Bind(Work, job);
			if (funding == KingdomConstructionStartResult.Outstanding)
			{
				System.Ledger.Note("{{r|The improvement receipt remains outstanding. The old work stands while its exact claim retries.}}");
				return true;
			}
			if (!ProjectImprovement(System, Work, A.Successor, job, out job,
				out string projectionFailure))
			{
				System.Ledger.Note("{{r|The paid improvement could not yet raise its scaffold. Its receipt remains queued.}}");
				KingdomLog.Log("construction: improvement projection waits: " + projectionFailure);
				return true;
			}
			string standing = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string line = KingdomUpgradeRules.BegunLine(standing, A.Successor.Name, A.CostDrams);
			MessageQueue.AddPlayerMessage("{{G|" + line + "}}");
			System.Ledger.Note("{{G|" + line + "}}");
			KingdomChronicle.Record(System, "the " + standing + " at " + System.KingdomDisplayName + " was set to be raised into " + KingdomUpgradeRules.Article(A.Successor.Name));
			KingdomLog.Log("improvement begun: " + A.Key + " -> " + A.SuccessorKey + " cost=" + A.CostDrams + " ticks=" + A.BuildTicks + " at " + cell.X + "," + cell.Y);
			return true;
		}

		private static bool ProjectImprovement(KingdomSystem System, GameObject Work,
			KingdomRules.BuildEntry Successor, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			Cell cell = Work?.CurrentCell;
			Zone zone = cell?.ParentZone;
			if (Successor == null || !EnsureExactImprovementPredecessor(System, zone, Work, Job))
			{
				Failure = "The paid predecessor no longer matches its exact recorded identity.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			r_KingdomImprovement existing = Work.GetPart<r_KingdomImprovement>();
			GameObject exactScaffold;
			KingdomPhysicalLookupState scaffoldState = FindImprovementScaffold(
				cell, Successor, Job, out exactScaffold);
			if (scaffoldState == KingdomPhysicalLookupState.Ambiguous)
			{
				Failure = "The improvement scaffold is duplicated, moved, replaced, or malformed.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			GameObject paidScaffold = existing != null && existing.Working
				? (scaffoldState == KingdomPhysicalLookupState.Exact
					&& ReferenceEquals(exactScaffold, existing.Scaffold)
						? exactScaffold : null)
				: (scaffoldState == KingdomPhysicalLookupState.Exact ? exactScaffold : null);
			if (paidScaffold != null)
			{
				r_KingdomImprovement recovered = Work.RequirePart<r_KingdomImprovement>();
				recovered.SuccessorKey = Successor.Key;
				recovered.SuccessorBlueprint = Successor.Blueprint;
				recovered.Working = true;
				recovered.Scaffold = paidScaffold;
				recovered.WorkCompleteTick = Job.DueTick;
				KingdomConstruction.Bind(Work, Job);
				if (!KingdomConstruction.FinishProjection(ref Updated, true, true))
				{
					Failure = "The paid improvement scaffold stands, but Working did not persist.";
					return false;
				}
				return true;
			}
			if (existing != null && existing.Working
				&& scaffoldState != KingdomPhysicalLookupState.Exact)
			{
				Failure = "The linked improvement scaffold lacks exact frozen output identity proof.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (!KingdomConstruction.BeginProjection(ref Updated, out Failure))
			{
				return false;
			}
			GameObject scaffold;
			try
			{
				scaffold = GameObject.Create("r_KingdomScaffold");
			}
			catch (System.Exception ex)
			{
				Failure = "The improvement scaffold threw during creation: " + ex.Message;
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (scaffold == null)
			{
				Failure = "The improvement scaffold blueprint could not be created.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			if (!KingdomConstruction.Owns(System, zone, Updated)
				|| !EnsureExactImprovementPredecessor(System, zone, Work, Updated))
			{
				RemoveCreatedProjection(scaffold);
				Failure = "Improvement authority or predecessor changed during scaffold creation.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (!KingdomConstruction.UpdateOutput(ref Updated, scaffold.ID))
			{
				bool removed = RemoveCreatedProjection(scaffold);
				Failure = "The improvement scaffold identity could not be published before AddObject.";
				if (!removed) KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			r_KingdomScaffold part = scaffold.GetPart<r_KingdomScaffold>();
			if (part == null)
			{
				bool removed = RemoveCreatedProjection(scaffold);
				Failure = "The improvement scaffold carries no raising capability.";
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			scaffold.SetStringProperty(BuildKeyProperty, Successor.Key);
			KingdomConstruction.Bind(scaffold, Updated);
			part.TargetBlueprint = Successor.Blueprint;
			part.TargetDisplayName = Successor.Name;
			part.CompleteTick = Updated.DueTick;
			part.StaffNeeded = Successor.Staff;
			part.ThresholdManning = KingdomRules.IsThresholdManning(Successor.Manning);
			if (Successor.Defence > 0)
			{
				bool hasTinkering = The.Player != null && The.Player.HasSkill("Tinkering");
				bool hasAdvancedTinkering = The.Player != null && The.Player.HasSkill("Tinkering_Tinker1");
				scaffold.SetIntProperty("KingdomDefencePending", KingdomRules.WallDefence(
					Successor.Defence, System.FoundingTerrainBlueprint,
					System.FoundingRegionName, hasTinkering, hasAdvancedTinkering));
			}
			GameObject accepted;
			try
			{
				accepted = cell.AddObject(scaffold);
			}
			catch (System.Exception ex)
			{
				bool removed = RemoveCreatedProjection(scaffold);
				Failure = "The improvement scaffold threw during AddObject: " + ex.Message;
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			GameObject globalScaffold;
			if (!ReferenceEquals(accepted, scaffold)
				|| !KingdomConstruction.Owns(System, zone, Updated)
				|| KingdomConstruction.FindExactId(zone, Updated.OutputId, out globalScaffold)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(globalScaffold, scaffold)
				|| !ExpectedImprovementScaffold(scaffold, cell, Successor)
				|| !KingdomConstruction.HasReceipt(scaffold, Updated)
				|| !EnsureExactImprovementPredecessor(System, zone, Work, Updated)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				bool removed = RemoveCreatedProjection(scaffold);
				Failure = "The improvement scaffold could not be verified beside its predecessor.";
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			r_KingdomImprovement improvement = Work.RequirePart<r_KingdomImprovement>();
			improvement.SuccessorKey = Successor.Key;
			improvement.SuccessorBlueprint = Successor.Blueprint;
			improvement.Working = true;
			improvement.Scaffold = scaffold;
			improvement.WorkCompleteTick = Updated.DueTick;
			improvement.AnnouncedReason = 0;
			KingdomConstruction.Bind(Work, Updated);
			if (!improvement.Working || improvement.Scaffold != scaffold
				|| !EnsureExactImprovementPredecessor(System, zone, Work, Updated))
			{
				Failure = "The improvement intent could not be verified on its predecessor.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			if (!KingdomConstruction.FinishProjection(ref Updated, true, true))
			{
				Failure = "The improvement scaffold stands, but Working did not persist.";
				return false;
			}
			return true;
		}

		private static bool ExpectedImprovementScaffold(GameObject Scaffold, Cell Cell,
			KingdomRules.BuildEntry Successor)
		{
			r_KingdomScaffold part = GameObject.Validate(Scaffold)
				? Scaffold.GetPart<r_KingdomScaffold>() : null;
			return part != null && Scaffold.CurrentCell == Cell && Successor != null
				&& Scaffold.GetStringProperty(BuildKeyProperty) == Successor.Key
				&& part.TargetBlueprint == Successor.Blueprint;
		}

		private static KingdomPhysicalLookupState FindImprovementScaffold(Cell Cell,
			KingdomRules.BuildEntry Successor, KingdomConstructionJob Job,
			out GameObject Scaffold)
		{
			Scaffold = null;
			if (Cell == null || Successor == null || Job == null
				|| string.IsNullOrEmpty(Job.OutputId)) return KingdomPhysicalLookupState.Absent;
			GameObject found = null;
			int count = 0;
			foreach (GameObject item in Cell.GetObjects())
			{
				if (!KingdomConstruction.HasReceipt(item, Job)) continue;
				count++;
				if (count > 1 || item.ID != Job.OutputId
					|| !ExpectedImprovementScaffold(item, Cell, Successor))
					return KingdomPhysicalLookupState.Ambiguous;
				found = item;
			}
			GameObject global;
			KingdomPhysicalLookupState globalState = KingdomConstruction.FindExactId(
				Cell.ParentZone, Job.OutputId, out global);
			if (count == 0)
				return globalState == KingdomPhysicalLookupState.Absent
					? KingdomPhysicalLookupState.Absent : KingdomPhysicalLookupState.Ambiguous;
			if (globalState != KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(global, found)) return KingdomPhysicalLookupState.Ambiguous;
			Scaffold = found;
			return KingdomPhysicalLookupState.Exact;
		}

		private static bool IsImprovementPredecessorIdentity(KingdomSystem System, Zone Z,
			GameObject Work, KingdomConstructionJob Job)
		{
			Cell cell = Z == null || Job == null ? null : Z.GetCell(Job.X, Job.Y);
			return GameObject.Validate(Work) && cell != null
				&& KingdomConstruction.Owns(System, Z, Job)
				&& Work.ID == Job.SubjectId && Work.CurrentZone == Z && Work.CurrentCell == cell
				&& Work.GetIntProperty(BuiltProperty) == 1
				&& (string.IsNullOrEmpty(Job.Payload)
					|| Work.GetStringProperty(BuildKeyProperty) == Job.Payload);
		}

		private static bool EnsureExactImprovementPredecessor(KingdomSystem System, Zone Z,
			GameObject Work, KingdomConstructionJob Job)
		{
			if (!IsImprovementPredecessorIdentity(System, Z, Work, Job)
				|| !KingdomConstruction.IsCurrent(Job)) return false;
			GameObject global;
			if (KingdomConstruction.FindExactId(Z, Job.SubjectId, out global)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(global, Work)) return false;
			string receipt = Work.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(receipt)) KingdomConstruction.Bind(Work, Job);
			return KingdomConstruction.HasReceipt(Work, Job);
		}

		private static bool RemoveCreatedProjection(GameObject Object)
		{
			try
			{
				return !GameObject.Validate(Object)
					|| (Object.Obliterate(null, Silent: true) && !GameObject.Validate(Object));
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Begins an improvement the settlement offered and would not start on its own: a working
		/// building the city leans on (<c>HeldOffer</c>). The dip must already have been disclosed
		/// to the founder and consented to &mdash; <see cref="OpenHeldOffer"/> is the only caller,
		/// and it shows <c>KingdomUpgradeRules.DipLine</c> before it asks.
		/// <para>
		/// The verdict is copied to <c>Ready</c> before <see cref="Begin"/> is called, because the
		/// founder's word is exactly what makes it ready: every other condition the law checks has
		/// already passed, and the offer is the ONE verdict a founder may overrule. Nothing else
		/// is relaxed &mdash; <see cref="Begin"/> still refuses if the water or the material is
		/// not actually there when it reaches for it.
		/// </para>
		/// </summary>
		/// <returns>True once scaffolding is standing and the water is spent.</returns>
		public static bool Force(KingdomSystem System, Zone Z, GameObject Work, Assessment A, KingdomSurvey Survey)
		{
			if (!A.Valid || !KingdomUpgradeRules.IsOffer(A.Verdict) || A.Successor == null)
			{
				return false;
			}
			Assessment consented = A;
			consented.Verdict = KingdomUpgradeRules.UpgradeVerdict.Ready;
			if (!Begin(System, Z, Work, consented, Survey))
			{
				return false;
			}
			string standing = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string forced = KingdomUpgradeRules.ForcedLine(standing, A.Successor.Name, A.Margin);
			MessageQueue.AddPlayerMessage("{{W|" + forced + "}}");
			System.Ledger.Note("{{W|" + forced + "}}");
			KingdomChronicle.Record(System, "the " + standing + " at " + System.KingdomDisplayName + " was set to be raised on the founder's word, and the settlement went into its reserve to do it");
			KingdomLog.Log("improvement forced: " + A.Key + " -> " + A.SuccessorKey + " outage=" + A.OutputLost + " margin=" + A.Margin);
			return true;
		}

		/// <summary>
		/// Puts one held offer to the founder with the dip disclosed BEFORE consent, and forces it
		/// only if they say so. Answers whether the work was started, so the caller knows the
		/// listing behind it is stale.
		/// </summary>
		public static bool OpenHeldOffer(KingdomSystem System, Zone Z, GameObject Work, Assessment A, KingdomSurvey Survey)
		{
			if (!KingdomUpgradeRules.IsOffer(A.Verdict))
			{
				return false;
			}
			string standing = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string successor = (A.Successor != null) ? A.Successor.Name : DisplayNameOf(A.SuccessorKey);
			int picked = Popup.PickOption(
				Title: standing,
				Intro: KingdomUpgradeRules.DipLine(standing, successor, A.SupportPerDay, A.BuildTicks, A.Margin),
				Options: new string[2] { "Raise it anyway, and go into the reserve", "Leave it as it is for now" },
				AllowEscape: true);
			if (picked != 0)
			{
				return false;
			}
			return Force(System, Z, Work, A, Survey);
		}

		/// <summary>
		/// Moves everything from the old work into the new one and takes the old work down.
		/// <para>
		/// Carries the contents first &mdash; liquid by its actual mixture, then every held
		/// object &mdash; and only then the settlement's own marks. A dedication is the founder's
		/// decision about a thing, and losing one because the thing improved would be the worst
		/// bug this system could have, so <c>KingdomLarder</c> and <c>KingdomStores</c> are
		/// carried explicitly rather than left to the scaffold's own blueprint-keyed guess.
		/// </para>
		/// <para>
		/// Anything that still will not fit is poured or dropped in the cell rather than
		/// destroyed. That path should be unreachable &mdash;
		/// <see cref="ContentsWouldFit(GameObject, string)"/> refuses the improvement before it
		/// starts &mdash; but water nobody can see is water this mod has quietly invented a
		/// second place to keep.
		/// </para>
		/// </summary>
		/// <param name="Predecessor">The old work. Destroyed once emptied.</param>
		/// <param name="Successor">The new work, already standing.</param>
		/// <param name="SuccessorKey">Registry key to stamp on the new work.</param>
		public static void HandOver(GameObject Predecessor, GameObject Successor, string SuccessorKey)
		{
			if (!GameObject.Validate(Predecessor) || !GameObject.Validate(Successor))
			{
				return;
			}
			Cell cell = Predecessor.CurrentCell;
			string predecessorId = Predecessor.ID;
			if (cell == null || Successor.CurrentCell != cell
				|| Successor.GetIntProperty(BuiltProperty) != 1)
			{
				return;
			}
			r_KingdomImprovement intent = Predecessor.GetPart<r_KingdomImprovement>();
			if (intent != null && !string.IsNullOrEmpty(intent.SuccessorBlueprint)
				&& Successor.Blueprint != intent.SuccessorBlueprint)
			{
				return;
			}
			if (intent == null || !intent.HandoverFlagsValid()) return;
			if (intent.HandoverSourceId == null && intent.HandoverTargetId == null)
			{
				if (string.IsNullOrEmpty(Predecessor.ID) || Predecessor.ID.Length > 128
					|| string.IsNullOrEmpty(Successor.ID) || Successor.ID.Length > 128) return;
				intent.HandoverSourceId = Predecessor.ID;
				intent.HandoverTargetId = Successor.ID;
			}
			else if (intent.HandoverSourceId != Predecessor.ID
				|| intent.HandoverTargetId != Successor.ID) return;
			string receipt = Predecessor.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(receipt))
			{
				r_KingdomImprovement.FailHandover(intent,
					"Legacy improvement handover lacks a current exact construction receipt.");
				return;
			}
			if (string.IsNullOrEmpty(intent.HandoverConstructionReceipt))
			{
				if (!string.IsNullOrEmpty(receipt))
				{
					if (receipt.Length > 128) return;
					intent.HandoverConstructionReceipt = receipt;
				}
			}
			else if (intent.HandoverConstructionReceipt != receipt) return;
			KingdomConstructionJob job = null;
			KingdomSystem ownerSystem = null;
			if (!string.IsNullOrEmpty(receipt))
			{
				ownerSystem = The.Game == null
					? null : The.Game.RequireSystem<KingdomSystem>();
				if (!KingdomConstruction.TryFind(receipt, out job)
					|| !KingdomConstruction.Owns(ownerSystem, Predecessor.CurrentZone, job)
					|| job.Route != KingdomConstructionRoute.Improvement
					|| KingdomConstructionRules.IsTerminal(job.Phase)
					|| (job.Phase != KingdomConstructionPhase.Working
						&& job.Phase != KingdomConstructionPhase.ProjectionPending
						&& job.Phase != KingdomConstructionPhase.Outstanding)
					|| job.SubjectId != Predecessor.ID
					|| SuccessorKey != job.TargetKey || intent == null || !intent.Working
					|| intent.Scaffold == null
					|| Successor.GetStringProperty(r_KingdomScaffold.RemovalProofProperty)
						!= intent.Scaffold.ID
					|| !EnsureExactImprovementPredecessor(ownerSystem, Predecessor.CurrentZone,
						Predecessor, job)
					|| !r_KingdomScaffold.IsExactSuccessor(Successor, Predecessor.CurrentZone,
						cell, job, intent.SuccessorBlueprint)) return;
				if (!KingdomConstruction.BeginProjection(ref job, out _)) return;
				if (job.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending)
				{
					r_KingdomImprovement.FailHandover(intent,
						"Improvement removal was interrupted before callback-success proof.");
					KingdomConstruction.Quarantine(ref job,
						intent.HandoverFailure);
					return;
				}
				KingdomConstruction.Bind(Successor, job);
			}
			if (!ExactHandoverEndpointsAfterCallback(Predecessor, Successor, cell,
				SuccessorKey, intent, job))
			{
				r_KingdomImprovement.FailHandover(intent,
					"The exact handover endpoints are absent, duplicated, or unauthorized.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return;
			}
			int carriedLiquid;
			if (!r_KingdomImprovement.CarryLiquidDurable(Predecessor, Successor, intent,
				out carriedLiquid))
			{
				if (job != null)
				{
					if (intent.HandoverQuarantined)
						KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					else KingdomConstruction.FinishProjection(ref job, false, false,
						"The exact liquid handover was restored and remains retryable.");
				}
				return;
			}
			if (!ExactHandoverEndpointsAfterCallback(Predecessor, Successor, cell,
				SuccessorKey, intent, job))
			{
				r_KingdomImprovement.FailHandover(intent,
					"A construction endpoint changed during liquid handover.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return;
			}
			int carriedItems;
			if (!r_KingdomImprovement.CarryInventoryDurable(Predecessor, Successor, cell, intent,
				out carriedItems))
			{
				if (job != null)
				{
					if (intent.HandoverQuarantined)
						KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					else KingdomConstruction.FinishProjection(ref job, false, false,
						"The exact item handover was restored and remains retryable.");
				}
				return;
			}
			if (!ExactHandoverEndpointsAfterCallback(Predecessor, Successor, cell,
				SuccessorKey, intent, job))
			{
				r_KingdomImprovement.FailHandover(intent,
					"A construction endpoint changed during item handover.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return;
			}
			if (!intent.HandoverEffectsDone)
			{
				// Grow first: it reads and restamps the predecessor's plot. CarryMarks is the final,
				// callback-free publication that the successor owns the predecessor's founder marks.
					try
					{
						if (!KingdomPlots.GrowInPlace(Predecessor, Successor, SuccessorKey))
							throw new InvalidOperationException(
								"The frozen plot-growth receipt did not settle exactly.");
					}
				catch (System.Exception ex)
				{
					r_KingdomImprovement.FailHandover(intent,
						"Plot growth threw during handover: " + ex.Message);
					if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return;
				}
				if (!ExactHandoverEndpointsAfterCallback(Predecessor, Successor, cell,
					SuccessorKey, intent, job))
				{
					r_KingdomImprovement.FailHandover(intent,
						"An improvement endpoint changed during plot growth.");
					if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return;
				}
				CarryMarks(Predecessor, Successor, SuccessorKey);
				if (!ExactCarriedMarks(Predecessor, Successor, SuccessorKey))
				{
					r_KingdomImprovement.FailHandover(intent,
						"Founder marks did not settle exactly on the successor.");
					if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return;
				}
				intent.HandoverEffectsDone = true;
			}
			else if (!KingdomPlots.GrowInPlace(Predecessor, Successor, SuccessorKey)
				|| !ExactCarriedMarks(Predecessor, Successor, SuccessorKey))
			{
				r_KingdomImprovement.FailHandover(intent,
					"Settled founder marks changed before predecessor removal.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return;
			}
			string predecessorName = KingdomDesign.ReferenceFor(Predecessor, Predecessor.ShortDisplayName);
			LiquidVolume remaining = Predecessor.GetPart<LiquidVolume>();
			if (remaining != null && remaining.Volume > 0 && cell != null)
			{
				r_KingdomImprovement.FailHandover(intent,
					"Liquid reappeared after the exact handover receipt settled.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return;
			}
			GameObject exactPredecessor;
			GameObject exactSuccessor;
			if (!GameObject.Validate(Predecessor) || Predecessor.CurrentCell != cell
				|| Successor.CurrentCell != cell || Successor.GetIntProperty(BuiltProperty) != 1
				|| Successor.GetStringProperty(BuildKeyProperty) != SuccessorKey
				|| (job != null && (!KingdomConstruction.HasReceipt(Predecessor, job)
					|| !r_KingdomScaffold.IsExactSuccessor(Successor,
						Predecessor.CurrentZone, cell, job, intent.SuccessorBlueprint)
						|| !KingdomConstruction.Owns(ownerSystem, Predecessor.CurrentZone, job)
						|| KingdomConstruction.FindExactId(Predecessor.CurrentZone,
							predecessorId, out exactPredecessor) != KingdomPhysicalLookupState.Exact
						|| !ReferenceEquals(exactPredecessor, Predecessor)
						|| KingdomConstruction.FindExactId(Predecessor.CurrentZone,
							Successor.ID, out exactSuccessor) != KingdomPhysicalLookupState.Exact
						|| !ReferenceEquals(exactSuccessor, Successor)
						|| Successor.GetStringProperty(r_KingdomScaffold.RemovalProofProperty)
						!= intent.Scaffold.ID
					|| !KingdomConstruction.IsCurrent(job))))
			{
				r_KingdomImprovement.FailHandover(intent,
					"The improved successor could not be verified before handover.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return;
			}
			if (job != null && !KingdomConstruction.UpdatePhysical(ref job,
				KingdomPhysicalPhase.FinalRemovalPending, carriedItems, carriedLiquid, 0,
				predecessorId, Successor.ID, "improvement-handover:v1"))
			{
				r_KingdomImprovement.FailHandover(intent,
					"The final predecessor-removal intent could not be published exactly.");
				if (job != null && KingdomConstruction.IsCurrent(job))
					KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return;
			}
			bool removed;
			try
			{
				removed = Predecessor.Destroy(null, Silent: true);
			}
			catch (System.Exception ex)
			{
				r_KingdomImprovement.FailHandover(intent,
					"Improvement predecessor removal threw: " + ex.Message);
				if (job != null)
					KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return;
			}
			KingdomPhysicalLookupState predecessorState = job == null
				? (GameObject.Validate(Predecessor) ? KingdomPhysicalLookupState.Exact
					: KingdomPhysicalLookupState.Absent)
				: KingdomConstruction.FindExactId(Successor.CurrentZone, predecessorId, out _);
			if (!removed || GameObject.Validate(Predecessor)
				|| predecessorState != KingdomPhysicalLookupState.Absent
				|| Successor.CurrentCell != cell)
			{
				r_KingdomImprovement.FailHandover(intent,
					"Improvement removal was vetoed, moved, or partially changed an endpoint.");
				if (job != null)
					KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return;
			}
			if (job != null)
			{
					GameObject exactAfter;
					if (!KingdomConstruction.Owns(ownerSystem, Successor.CurrentZone, job)
						|| KingdomConstruction.FindExactId(Successor.CurrentZone,
							Successor.ID, out exactAfter) != KingdomPhysicalLookupState.Exact
						|| !ReferenceEquals(exactAfter, Successor)
						|| !r_KingdomScaffold.IsExactSuccessor(Successor, Successor.CurrentZone,
						cell, job, intent.SuccessorBlueprint)
						|| !KingdomConstruction.IsCurrent(job))
				{
					r_KingdomImprovement.FailHandover(intent,
						"The improvement successor changed during predecessor removal.");
					KingdomConstruction.Quarantine(ref job,
						intent.HandoverFailure);
					return;
				}
				Successor.SetStringProperty(r_KingdomScaffold.RemovalProofProperty, predecessorId);
				if (!r_KingdomScaffold.HasRemovalProof(Successor, predecessorId))
				{
					r_KingdomImprovement.FailHandover(intent,
						"The improvement successor did not retain predecessor-removal proof.");
					KingdomConstruction.Quarantine(ref job,
						intent.HandoverFailure);
					return;
				}
				if (!KingdomConstruction.UpdatePhysical(ref job,
					KingdomPhysicalPhase.FinalRemoved, carriedItems, carriedLiquid, 0,
					predecessorId, Successor.ID, "improvement-handover:v1"))
				{
					r_KingdomImprovement.FailHandover(intent,
						"Exact predecessor absence could not be committed to its receipt.");
					if (KingdomConstruction.IsCurrent(job))
						KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return;
				}
				if (!KingdomConstruction.Complete(ref job))
				{
					r_KingdomImprovement.FailHandover(intent,
						"The physically closed improvement receipt could not complete.");
					if (KingdomConstruction.IsCurrent(job))
						KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return;
				}
				r_KingdomScaffold.TellCompletion(ownerSystem, Successor, job);
			}
			if (carriedLiquid > 0 || carriedItems > 0)
			{
				MessageQueue.AddPlayerMessage("{{G|Everything the " + predecessorName + " held was moved into " + KingdomDesign.ReferenceFor(Successor, Successor.ShortDisplayName) + ".}}");
			}
			KingdomLog.Log("improvement handover: " + predecessorName + " -> " + Successor.Blueprint + " liquid=" + carriedLiquid + " items=" + carriedItems);
			// A settlement that is standing there watching should be able to watch the next one
			// start, rather than having to walk out and back in. Bounded by the reserve and by
			// free hands exactly as the pass itself is, and it still only ever starts one - and it
			// is a no-op when the handover was itself driven by that pass, which is what keeps
			// "one work per visit" true.
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			Zone zone = Successor.CurrentZone;
			if (system != null && zone != null)
			{
				KingdomSystem.Guard("improvement follow-on", delegate
				{
					OnZoneActivated(system, zone, KingdomSurvey.Take(zone, system));
				});
			}
		}

		private static bool ExactHandoverEndpointsAfterCallback(GameObject Predecessor,
			GameObject Successor, Cell Cell, string SuccessorKey, r_KingdomImprovement Intent,
			KingdomConstructionJob Job)
		{
			KingdomSystem system = The.Game == null
				? null : The.Game.RequireSystem<KingdomSystem>();
			Zone zone = GameObject.Validate(Predecessor) ? Predecessor.CurrentZone : null;
			GameObject exactPredecessor;
			GameObject exactSuccessor;
			return GameObject.Validate(Predecessor) && GameObject.Validate(Successor)
				&& Intent != null && Predecessor.GetPart<r_KingdomImprovement>() == Intent
				&& Predecessor.ID == Intent.HandoverSourceId
				&& Successor.ID == Intent.HandoverTargetId
				&& Predecessor.CurrentCell == Cell && Successor.CurrentCell == Cell
				&& Successor.GetIntProperty(BuiltProperty) == 1
				&& Successor.GetStringProperty(BuildKeyProperty) == SuccessorKey
				&& KingdomConstruction.FindExactId(zone, Predecessor.ID,
					out exactPredecessor) == KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(exactPredecessor, Predecessor)
				&& KingdomConstruction.FindExactId(zone, Successor.ID,
					out exactSuccessor) == KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(exactSuccessor, Successor)
				&& (Job == null || (KingdomConstruction.Owns(system, zone, Job)
					&& KingdomConstruction.HasReceipt(Predecessor, Job)
					&& r_KingdomScaffold.IsExactSuccessor(Successor,
						Predecessor.CurrentZone, Cell, Job, Intent.SuccessorBlueprint)
					&& KingdomConstruction.IsCurrent(Job)));
		}

		private static bool ExactCarriedMarks(GameObject Predecessor, GameObject Successor,
			string SuccessorKey)
		{
			if (!GameObject.Validate(Predecessor) || !GameObject.Validate(Successor)
				|| Successor.GetIntProperty(BuiltProperty) != 1
				|| Successor.GetStringProperty(BuildKeyProperty) != SuccessorKey) return false;
			if (Predecessor.GetIntProperty(KingdomAdopt.LarderProperty) == 1
				&& (Successor.Inventory == null
					|| Successor.GetIntProperty(KingdomAdopt.LarderProperty) != 1)) return false;
			if (Predecessor.GetIntProperty(KingdomAdopt.StoresProperty) == 1
				&& (Successor.GetPart<LiquidVolume>() == null
					|| Successor.GetIntProperty(KingdomAdopt.StoresProperty) != 1)) return false;
			if (Predecessor.GetIntProperty(KingdomSalvage.CertifiedProperty) == 1
				&& Successor.GetIntProperty(KingdomSalvage.CertifiedProperty) != 1) return false;
			string given = Predecessor.GetStringProperty(KingdomDesign.GivenNameProperty);
			if (!string.IsNullOrEmpty(given)
				&& Successor.GetStringProperty(KingdomDesign.GivenNameProperty) != given) return false;
			if (Predecessor.GetIntProperty(AdoptedProperty) == 1
				&& Successor.GetIntProperty(AdoptedProperty) != 1) return false;
			if (KingdomPlots.TryReadRect(Predecessor, out _))
			{
				if (!KingdomPlots.TryReadRect(Successor, out _)) return false;
				string plot = Predecessor.GetStringProperty(KingdomPlots.PlotIdProperty);
				if (!string.IsNullOrEmpty(plot)
					&& Successor.GetStringProperty(KingdomPlots.PlotIdProperty) != plot) return false;
			}
			return Predecessor.GetIntProperty(KingdomPlots.YieldingProperty) != 1
				|| Successor.GetIntProperty(KingdomPlots.YieldingProperty) == 1;
		}

		/// <summary>
		/// Pours the predecessor's liquid into the successor, mixture and all. Every component is
		/// moved at its real dram count &mdash; <c>ComponentLiquids</c> holds parts per thousand,
		/// not drams &mdash; and both ends are measured rather than assumed, because
		/// <c>AddDrams</c> clamps silently and <c>UseDrams</c> reports something else entirely.
		/// </summary>
		/// <returns>Drams actually moved.</returns>
		public static int CarryLiquid(GameObject Predecessor, GameObject Successor)
		{
			r_KingdomImprovement receipt = Predecessor?.GetPart<r_KingdomImprovement>();
			int moved;
			return r_KingdomImprovement.CarryLiquidDurable(Predecessor, Successor, receipt,
				out moved) ? moved : 0;
		}

		/// <summary>
		/// Moves every object out of the predecessor. Anything the successor will not take is put
		/// down in the cell rather than destroyed.
		/// </summary>
		/// <returns>Objects actually moved or set down.</returns>
		public static int CarryInventory(GameObject Predecessor, GameObject Successor, Cell Where)
		{
			r_KingdomImprovement receipt = Predecessor?.GetPart<r_KingdomImprovement>();
			int moved;
			return r_KingdomImprovement.CarryInventoryDurable(Predecessor, Successor, Where,
				receipt, out moved) ? moved : 0;
		}

		/// <summary>
		/// Carries the settlement's own marks onto the improved work. The scaffold has already
		/// set what the new DESIGN says it is &mdash; built, staffed, defended, and dedicated if
		/// it holds liquid. This carries what the FOUNDER decided about the old one, which the
		/// scaffold cannot know: a larder dedication (which the scaffold keys off one blueprint
		/// and would silently drop), a stores dedication, an adoption, and a machine's grid
		/// certification.
		/// </summary>
		public static void CarryMarks(GameObject Predecessor, GameObject Successor, string SuccessorKey)
		{
			if (Predecessor == null || Successor == null)
			{
				return;
			}
			Successor.SetIntProperty(BuiltProperty, 1);
			if (!string.IsNullOrEmpty(SuccessorKey))
			{
				Successor.SetStringProperty(BuildKeyProperty, SuccessorKey);
			}
			if (Predecessor.GetIntProperty(KingdomAdopt.LarderProperty) == 1 && Successor.Inventory != null)
			{
				Successor.SetIntProperty(KingdomAdopt.LarderProperty, 1);
			}
			if (Predecessor.GetIntProperty(KingdomAdopt.StoresProperty) == 1 && Successor.GetPart<LiquidVolume>() != null)
			{
				Successor.SetIntProperty(KingdomAdopt.StoresProperty, 1);
			}
			if (Predecessor.GetIntProperty(KingdomSalvage.CertifiedProperty) == 1)
			{
				Successor.SetIntProperty(KingdomSalvage.CertifiedProperty, 1);
			}
			// A name the founder gave is the most personal decision anything in this mod records.
			// Losing one because the thing it was given to got better would be the same bug as
			// losing a dedication, so it is carried the same way and for the same reason.
			string given = Predecessor.GetStringProperty(KingdomDesign.GivenNameProperty);
			if (!string.IsNullOrEmpty(given))
			{
				Successor.SetStringProperty(KingdomDesign.GivenNameProperty, given);
			}
			// An adopted work is never improved (UpgradeVerdict.NotOurWork), so this is
			// unreachable today. It is carried anyway because the cost of being wrong is a
			// founder's own building quietly losing the settlement's recognition of it.
			if (Predecessor.GetIntProperty(AdoptedProperty) == 1)
			{
				Successor.SetIntProperty(AdoptedProperty, 1);
				string adoptedKey = Predecessor.GetStringProperty(KingdomAdopt.AdoptedKeyProperty);
				if (!string.IsNullOrEmpty(adoptedKey))
				{
					Successor.SetStringProperty(KingdomAdopt.AdoptedKeyProperty, adoptedKey);
				}
				string adoptedMark = Predecessor.GetStringProperty(KingdomAdopt.AdoptedMarkProperty);
				if (!string.IsNullOrEmpty(adoptedMark))
				{
					Successor.SetStringProperty(KingdomAdopt.AdoptedMarkProperty, adoptedMark);
				}
			}
		}

		/// <summary>
		/// The Charter's improvements screen: everything on this ground that can grow, what it
		/// grows into, and &mdash; for anything that is not growing &mdash; why not, in one
		/// sentence each. Picking a work holds it or releases it; the last entry does the same
		/// for the whole ground.
		/// <para>
		/// Nothing here starts, cancels, or hurries a work. The founder's only decision in this
		/// screen is what to leave alone, which is the one decision the settlement cannot make
		/// for them.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		public static void ShowImprovements(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			if (!Enabled)
			{
				Popup.Show("The settlement is not bettering its own works. (That module is switched off in the options.)");
				return;
			}
			Zone zone = The.Player?.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Your works are looked over on the kingdom's own ground.");
				return;
			}
			while (true)
			{
				KingdomSurvey survey = KingdomSurvey.Take(zone, System);
				int freeHands = System.Population - System.AssignedCrew;
				if (freeHands < 0)
				{
					freeHands = 0;
				}
				List<GameObject> works = new List<GameObject>();
				List<Assessment> assessments = new List<Assessment>();
				List<string> lines = new List<string>();
				bool otherWorkUnderway = false;
				foreach (GameObject item in zone.GetObjects())
				{
					r_KingdomImprovement improvement = item.GetPart<r_KingdomImprovement>();
					if ((improvement != null && improvement.Working) || HasActiveConstruction(item))
					{
						otherWorkUnderway = true;
					}
				}
				foreach (GameObject item in zone.GetObjects())
				{
					if (item.GetIntProperty(BuiltProperty) != 1)
					{
						continue;
					}
					Assessment assessment = Assess(System, zone, item, survey, freeHands, otherWorkUnderway);
					if (!assessment.Valid || assessment.Verdict == KingdomUpgradeRules.UpgradeVerdict.NoSuccessor)
					{
						continue;
					}
					works.Add(item);
					assessments.Add(assessment);
					lines.Add(EntryLine(item, assessment));
				}
				bool groundHeld = IsGroundHeld(zone);
				lines.Add(groundHeld
					? "{{W|Let this ground improve itself again}}"
					: "{{K|Leave this ground as it is}}");
				if (works.Count == 0)
				{
					Popup.Show("Nothing standing here is built to grow into anything else yet.");
					return;
				}
				int picked = Popup.PickOption(Title: "The works of " + System.SeatName, Intro: "Pick a work to leave as it is, or to let grow again.", Options: lines, AllowEscape: true);
				if (picked < 0)
				{
					return;
				}
				if (picked >= works.Count)
				{
					SetGroundHeld(zone, !groundHeld);
					KingdomGovernanceScope.Commit("set ground improvements");
					return;
				}
				// A held offer is the one verdict the founder may overrule, so picking it asks
				// rather than toggling: the dip is disclosed and consented to before anything moves.
				// Everything else in this screen still only ever decides what to leave alone.
				Assessment picking = assessments[picked];
				if (KingdomUpgradeRules.IsOffer(picking.Verdict))
				{
					if (OpenHeldOffer(System, zone, works[picked], picking, survey))
					{
						KingdomGovernanceScope.Commit("force improvement");
					}
					// Leaving, escaping, or failing the force attempt is a cancellation. A held
					// offer must never fall through into the ordinary held-state toggle.
					return;
				}
				r_KingdomImprovement held = works[picked].RequirePart<r_KingdomImprovement>();
				held.Held = !held.Held;
				held.AnnouncedReason = 0;
				KingdomGovernanceScope.Commit("set work improvement");
				return;
			}
		}

		/// <summary>One work's line in the Charter listing: what it is, what it would become, and
		/// its state or the reason it has none.</summary>
		public static string EntryLine(GameObject Work, Assessment A)
		{
			string name = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string successor = (A.Successor != null) ? A.Successor.Name : DisplayNameOf(A.SuccessorKey);
			switch (A.Verdict)
			{
			case KingdomUpgradeRules.UpgradeVerdict.AlreadyWorking:
				return "{{G|" + name + "}} - being raised into " + successor;
			case KingdomUpgradeRules.UpgradeVerdict.Ready:
				return "{{G|" + name + "}} - ready to be raised into " + successor + " for {{C|" + A.CostDrams + " drams}}";
			case KingdomUpgradeRules.UpgradeVerdict.HeldOffer:
				return "{{W|" + name + "}} - ready to improve, and held: the city leans on it. Pick it to raise it anyway.";
			case KingdomUpgradeRules.UpgradeVerdict.NotOurWork:
				return "{{K|" + name + "}} - yours, not the settlement's. It is left exactly as you made it.";
			case KingdomUpgradeRules.UpgradeVerdict.StyleForbids:
				return "{{K|" + name + "}} - " + successor + " is not built in a city of this kind";
			default:
				return "{{K|" + name + "}} - " + (A.Reason ?? ("would become " + successor));
			}
		}
	}
}
