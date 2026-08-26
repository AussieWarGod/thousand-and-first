using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// A single-pass accounting of everything in a zone the kingdom cares about: dedicated
	/// water stores, open water, citizens, and the trade post. Take one per zone activation
	/// and pass it down; the alternative is a full-zone scan per question, and there are
	/// twenty questions.
	/// </summary>
	/// <remarks>A survey is a maintained transaction index. Physical commits must call
	/// <see cref="ObserveAdded"/>, <see cref="ObserveChanged"/>, or <see cref="ObserveRemoved"/>
	/// before a later pass step reads it. A bound pass reuses this exact instance; no helper may
	/// silently mix in a second whole-zone snapshot.</remarks>
	public partial class KingdomSurvey
	{
		private const int MaxIndexedObjects = 16384;

		private sealed class ReferenceComparer : IEqualityComparer<GameObject>
		{
			internal static readonly ReferenceComparer Instance = new ReferenceComparer();

			public bool Equals(GameObject X, GameObject Y)
			{
				return ReferenceEquals(X, Y);
			}

			public int GetHashCode(GameObject Item)
			{
				return RuntimeHelpers.GetHashCode(Item);
			}
		}

		private sealed class IndexedRow
		{
			internal GameObject Item;
			internal long Order;
			internal bool Citizen;
			internal bool Settler;
			internal bool TradePost;
			internal bool Built;
			internal bool Bed;
			internal bool Kitchen;
			internal bool Work;
			internal bool Defence;
			internal bool Larder;
			internal bool Pool;
			internal bool Store;
			internal bool Raider;
			internal bool Cairn;
			internal bool PlotWorks;
			internal bool Improvement;
			internal bool Notice;
			internal bool Shrine;
			internal bool Guest;
			internal bool NotableGuest;
			internal bool CausalPilgrim;
			internal bool Clearance;
			internal bool ConstructionRoot;
			internal bool PlotRoot;
			internal bool LayoutRoot;
			internal bool CropRow;
			internal bool NetworkPiece;
			internal bool LabJob;
			internal bool VisualRoot;
			internal bool PlotPart;
			internal bool ArchitectureComponent;
			internal bool GatehouseSatellite;
			internal bool DelveEndpoint;
			internal bool Furnishing;
			internal bool HeartRelic;
			internal bool MaterialStockpile;
			internal bool Transient;
			internal int ResidentId;
			internal int FoodStored;
			internal int FoodCapacity;
			internal int StoredWater;
			internal int OpenWater;
			internal int StorageSpace;
			internal int StorageCapacity;
			internal LiquidVolume Liquid;
			internal readonly List<GameObject> Loaded = new List<GameObject>();
		}

		[ThreadStatic]
		private static KingdomSurvey BoundSurvey;

		[ThreadStatic]
		private static int BoundDepth;

		private readonly Dictionary<GameObject, IndexedRow> Rows =
			new Dictionary<GameObject, IndexedRow>(ReferenceComparer.Instance);

		private readonly HashSet<GameObject> LoadedSet =
			new HashSet<GameObject>(ReferenceComparer.Instance);

		private long NextOrder;

		private int ClassificationPasses;

		private int ClassifiedRoots;

		private int ActiveReuses;

		private int ForeignClassifications;

		private int AddedMutations;

		private int ChangedMutations;

		private int RemovedMutations;

		private int TradePosts;

		private bool LoadedIndexComplete = true;

		/// <summary>One zone-root snapshot in Qud's deterministic cell/object order. New roots are
		/// appended only by <see cref="ObserveAdded"/>; callers must never mutate this list.</summary>
		public readonly List<GameObject> Objects = new List<GameObject>();

		/// <summary>Roots plus their recursively held objects, bounded once for exact receipt lookup.</summary>
		internal readonly List<GameObject> LoadedObjects = new List<GameObject>();

		/// <summary>Every exact civic body, including merchants and non-born enrolled citizens.</summary>
		public readonly List<GameObject> CitizenBodies = new List<GameObject>();

		public readonly List<GameObject> Raiders = new List<GameObject>();

		public readonly List<GameObject> Cairns = new List<GameObject>();

		public readonly List<GameObject> PlotWorks = new List<GameObject>();

		public readonly List<GameObject> Improvements = new List<GameObject>();

		public readonly List<GameObject> Notices = new List<GameObject>();

		public readonly List<GameObject> Shrines = new List<GameObject>();

		public readonly List<GameObject> Guests = new List<GameObject>();

		public readonly List<GameObject> NotableGuests = new List<GameObject>();

		public readonly List<GameObject> CausalPilgrims = new List<GameObject>();

		public readonly List<GameObject> Clearances = new List<GameObject>();

		/// <summary>Every raising root, plot root, layout root, crop row, declared liquid-line
		/// piece, active/persisted lab, visual-state candidate, resident-id body, and transient
		/// body classified during the one root walk. These are deliberately separate indexes:
		/// a semantic helper iterating <see cref="Objects"/> and reclassifying every root would
		/// still be a second whole-zone pass even though it avoided a second GetObjects call.</summary>
		public readonly List<GameObject> ConstructionRoots = new List<GameObject>();

		public readonly List<GameObject> PlotRoots = new List<GameObject>();

		public readonly List<GameObject> LayoutRoots = new List<GameObject>();

		public readonly List<GameObject> CropRows = new List<GameObject>();

		public readonly List<GameObject> NetworkPieces = new List<GameObject>();

		public readonly List<GameObject> LabJobs = new List<GameObject>();

		public readonly List<GameObject> VisualRoots = new List<GameObject>();

		/// <summary>Specialized physical-receipt indexes. Transaction validators consume these
		/// bounded subsets instead of walking every root after the survey has classified it.</summary>
		public readonly List<GameObject> PlotParts = new List<GameObject>();

		public readonly List<GameObject> ArchitectureComponents = new List<GameObject>();

		public readonly List<GameObject> GatehouseSatellites = new List<GameObject>();

		public readonly List<GameObject> DelveEndpoints = new List<GameObject>();

		public readonly List<GameObject> Furnishings = new List<GameObject>();

		public readonly List<GameObject> HeartRelics = new List<GameObject>();

		public readonly List<GameObject> MaterialStockpiles = new List<GameObject>();

		public readonly List<GameObject> ResidentBodies = new List<GameObject>();

		public readonly List<GameObject> Transients = new List<GameObject>();

		/// <summary>Synchronous pass binding. Nested helpers may re-enter only for the same survey.</summary>
		public sealed class PassScope : IDisposable
		{
			private readonly KingdomSurvey Survey;
			private bool Closed;

			internal PassScope(KingdomSurvey survey)
			{
				Survey = survey;
				if (BoundSurvey != null && !ReferenceEquals(BoundSurvey, survey))
					throw new InvalidOperationException("A second zone survey cannot enter an active settlement pass.");
				BoundSurvey = survey;
				BoundDepth++;
			}

			public void Dispose()
			{
				if (Closed) return;
				Closed = true;
				if (!ReferenceEquals(BoundSurvey, Survey) || BoundDepth <= 0)
					throw new InvalidOperationException("The active zone survey scope was replaced.");
				BoundDepth--;
				if (BoundDepth == 0)
				{
					BoundSurvey = null;
					Survey.EmitPassReceipt();
				}
			}
		}
	}
}
