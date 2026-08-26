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
	public partial class r_KingdomImprovement : IPart
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
	}
}
