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
	public class KingdomSurvey
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
		/// <summary>The exact zone this snapshot was taken from. Runtime-only; surveys are never
		/// serialized. Construction presence needs the ground even when it contains no finished
		/// work yet, which cannot be recovered honestly from a population counter.</summary>
		public Zone Ground;

		public int StoredWater;

		public int OpenWater;

		public int StorageSpace;

		public int StorageCapacity;

		public int Citizens;

		public bool HasTradePost => TradePosts > 0;

		/// <summary>
		/// Kingdom-wide defence bonus from garrison districts, folded in by
		/// <see cref="Take(Zone, KingdomSystem)"/>. Zero on a survey taken with the plain
		/// <see cref="Take(Zone)"/> overload, which knows nothing outside its own zone.
		/// </summary>
		public int DistrictDefenceBonus;

		/// <summary>
		/// Servings of food seen in the settlement's dedicated containers this pass: items
		/// carrying vanilla <c>Food</c> or <c>PreparedCookingIngredient</c>. Consuming food
		/// through <see cref="ConsumeFood"/> keeps this correct; a food item placed after the
		/// survey was taken does not retroactively appear here.
		/// </summary>
		public int FoodStored;

		/// <summary>
		/// Servings the settlement's dedicated larders could hold between them, from each
		/// container's own declared capacity (<see cref="KingdomRules.LarderCapacityTag"/>,
		/// defaulting to <see cref="KingdomRules.DefaultLarderCapacity"/>). The food side of
		/// <see cref="StorageCapacity"/>, and physical in exactly the same way: a vessel says how
		/// much it holds, and the catalogue never does.
		/// </summary>
		public int FoodCapacity;

		/// <summary>
		/// Room left in the larders. DERIVED rather than counted, unlike its water counterpart
		/// <see cref="StorageSpace"/>, so that a caller which puts food in by another road
		/// &mdash; the kitchen garden's own harvest, which spawns crops straight into
		/// <see cref="Larders"/> and adjusts <see cref="FoodStored"/> &mdash; cannot leave this
		/// figure stale behind it.
		/// </summary>
		public int FoodSpace => (FoodCapacity > FoodStored) ? (FoodCapacity - FoodStored) : 0;

		/// <summary>Coarse abundance read on <see cref="FoodStored"/>. See
		/// <see cref="KingdomRules.ClassifyPantry"/>.</summary>
		public KingdomRules.PantryTier FoodAbundance;

		/// <summary>
		/// Containers marked as larders this pass. <see cref="ConsumeFood"/> walks these, in the
		/// order found, so a shared meal only ever draws from what the founder actually
		/// dedicated.
		/// </summary>
		public readonly List<GameObject> Larders = new List<GameObject>();

		public readonly List<LiquidVolume> Stores = new List<LiquidVolume>();

		public readonly List<LiquidVolume> Pools = new List<LiquidVolume>();

		public readonly List<GameObject> Settlers = new List<GameObject>();

		/// <summary>Beds the settlement built. Population cannot exceed these.</summary>
		public int Beds;

		/// <summary>
		/// Finished works here that carry vanilla's <c>Campfire</c> part &mdash; the communal
		/// fire, and the oven above it. A settlement with none of these cannot cook, however full
		/// its larders are, which is the gate on the favoured meal
		/// (<c>KingdomRules.JudgeMeal</c>).
		/// </summary>
		public int Kitchens;

		/// <summary>Works the settlement built that require crew, in placement order.</summary>
		public readonly List<GameObject> Works = new List<GameObject>();

		/// <summary>
		/// Everything the settlement built and finished, crewed or not, in placement order.
		/// <para>
		/// A superset of <see cref="Works"/> and of <see cref="Defences"/>: a cistern asks for
		/// nobody and a palisade defends without a crew, and both stand. This is the list
		/// <c>KingdomSubsidence</c> sums <c>Carries</c> over, so a settlement is measured by
		/// everything holding it up rather than only by the parts of it that want hands.
		/// </para>
		/// <para>
		/// Gated on <c>KingdomBuilt</c> like the rest, which is what makes "a scaffold carries
		/// nothing until it is raised" true here as well &mdash; a half-built cistern feeds
		/// nobody, and a settlement cannot outrun its own level by staking plans.
		/// </para>
		/// </summary>
		public readonly List<GameObject> Built = new List<GameObject>();

		/// <summary>Defensive works built here, crewed or not.</summary>
		public readonly List<GameObject> Defences = new List<GameObject>();

		/// <summary>Walks the zone once and classifies every object of interest.</summary>
		/// <param name="Z">Zone to survey. Null yields an empty survey.</param>
		public static KingdomSurvey Take(Zone Z)
		{
			KingdomSurvey bound = ActiveFor(Z);
			if (bound != null)
			{
				bound.ActiveReuses++;
				return bound;
			}
			// A remote snapshot during a bound settlement transaction is not invisible: its
			// classification belongs to the active pass receipt even though the returned index
			// remains scoped to the remote zone. Native acceptance requires this to stay zero.
			if (BoundSurvey != null && Z != null
				&& !ReferenceEquals(BoundSurvey.Ground, Z))
				BoundSurvey.ForeignClassifications++;
			KingdomSurvey survey = new KingdomSurvey();
			survey.Ground = Z;
			KingdomSystem citizenshipSystem = The.Game?.GetSystem<KingdomSystem>();
			if (Z == null)
			{
				return survey;
			}
			int releasedLegacyFurnishings = 0;
			survey.ClassificationPasses++;
			List<GameObject> roots = Z.GetObjects();
			survey.ClassifiedRoots = roots.Count;
			for (int i = 0; i < roots.Count; i++)
			{
				GameObject item = roots[i];
				// Old population-furnished plots marked every liquid prop as a separate civic store.
				// That multiplied one legal plot into up to sixty-four accounting rows. Current
				// authored components never carry this authority; migrate old non-root plot pieces by
				// releasing the mark only. Vessel and water remain physically untouched, while any
				// standing signed debt remains on the city row for real civic roots to settle.
				if (item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1
					&& item.GetIntProperty("KingdomBuilt") != 1
					&& item.GetIntProperty("KingdomStores") == 1)
				{
					item.SetIntProperty("KingdomStores", 0);
					releasedLegacyFurnishings++;
				}
				if (item.GetIntProperty("KingdomCitizen") == 1
					&& item.GetPart<r_KingdomCitizenship>() == null
					&& citizenshipSystem != null)
				{
					// Old saves carried only a global marker. Bind their explicit legacy-unknown
					// receipt during this same one-pass scan; never count the marker by itself.
					string legacyFailure;
					KingdomCitizenship.ObserveLegacy(citizenshipSystem, item, out legacyFailure);
				}
				survey.AddRoot(item, citizenshipSystem);
			}
			if (releasedLegacyFurnishings > 0)
			{
				string line = releasedLegacyFurnishings
					+ " old plot furnishing" + (releasedLegacyFurnishings == 1 ? " is" : "s are")
					+ " personal capacity now. Nothing in them was moved or lost.";
				KingdomLog.Log("stores: " + line);
				if (Z.IsActive()) MessageQueue.AddPlayerMessage("{{W|" + line + "}}");
			}
			survey.FoodAbundance = KingdomRules.ClassifyPantry(survey.FoodStored);
			return survey;
		}

		/// <summary>Returns the already-bound roots during a semantic pass. Outside one, this takes
		/// one ordinary classified survey rather than exposing a second unclassified scan.</summary>
		public static IEnumerable<GameObject> ObjectsFor(Zone Z)
		{
			KingdomSurvey survey = ActiveFor(Z);
			if (survey != null)
			{
				survey.ActiveReuses++;
				return survey.Objects;
			}
			return Take(Z).Objects;
		}

		public static KingdomSurvey ActiveFor(Zone Z)
		{
			return BoundSurvey != null && Z != null && ReferenceEquals(BoundSurvey.Ground, Z)
				? BoundSurvey : null;
		}

		public PassScope BindPass()
		{
			if (Ground == null) throw new InvalidOperationException("A groundless survey cannot bind a pass.");
			return new PassScope(this);
		}

		private void AddRoot(GameObject Item, KingdomSystem System)
		{
			if (!GameObject.Validate(Item) || Rows.ContainsKey(Item)) return;
			IndexedRow row = Capture(Item, System, NextOrder++);
			Rows.Add(Item, row);
			Objects.Add(Item);
			Publish(row, true);
			IndexLoadedBranch(row);
		}

		private IndexedRow Capture(GameObject Item, KingdomSystem System, long Order)
		{
			IndexedRow row = new IndexedRow { Item = Item, Order = Order };
			row.Citizen = BelongsToRealm(System, Item);
			row.TradePost = row.Citizen && Item.GetIntProperty("VillageMerchant") == 1;
			row.Settler = row.Citizen && !row.TradePost
				&& Item.GetIntProperty("KingdomBorn") == 1 && !Item.IsPlayer() && !Item.IsPlayerLed();
			// A led, dead, or allegiance-diverged body still witnesses the resident id it carries.
			// Citizenship controls the civic lists, never whether the one ground scan can find that
			// exact body for the roster's Present/Led/Killed/Missing judgement.
			row.ResidentId = Simulation.City.KingdomResidents.IdOf(Item);
			row.Built = Item.GetIntProperty("KingdomBuilt") == 1;
			row.Bed = row.Built && Item.HasPart("Bed");
			row.Kitchen = row.Built && Item.HasPart("Campfire");
			row.Work = row.Built && Item.GetIntProperty("KingdomStaffNeeded") > 0
				&& (KingdomCrops.FieldOf(Item) == null || KingdomCrops.IsSown(Item));
			row.Defence = row.Built && Item.GetIntProperty("KingdomDefence") > 0;
			row.Larder = Item.GetIntProperty("KingdomLarder") == 1 && Item.Inventory != null;
			if (row.Larder)
			{
				row.FoodCapacity = CapacityOf(Item);
				row.FoodStored = HeldIn(Item);
			}
			row.Liquid = Item.GetPart<LiquidVolume>();
			if (row.Liquid != null && row.Liquid.Volume >= 0)
			{
				bool fresh = KingdomLiquids.HasFreshWater(row.Liquid);
				row.Pool = row.Liquid.MaxVolume < 0 && fresh;
				if (row.Pool) row.OpenWater = row.Liquid.Volume;
				row.Store = row.Liquid.MaxVolume >= 0
					&& Item.GetIntProperty("KingdomStores") == 1;
				if (row.Store)
				{
					row.StorageCapacity = row.Liquid.MaxVolume;
					if (fresh) row.StoredWater = row.Liquid.Volume;
					if (row.Liquid.Volume < row.Liquid.MaxVolume
						&& KingdomLiquids.CanReceiveFreshWater(row.Liquid))
						row.StorageSpace = row.Liquid.MaxVolume - row.Liquid.Volume;
				}
			}
			row.Raider = Item.GetIntProperty("KingdomRaider") == 1;
			row.Cairn = row.Built && string.Equals(Item.Blueprint, "r_KingdomCairn",
				StringComparison.Ordinal);
			row.PlotWorks = Item.GetPart<r_KingdomPlotWorks>() != null;
			row.Improvement = Item.GetPart<r_KingdomImprovement>() != null;
			row.Notice = Item.GetPart<r_KingdomNotice>() != null;
			row.Shrine = row.Built && Item.HasPart("Shrine");
			row.Guest = Item.GetIntProperty("KingdomGuest") == 1;
			row.NotableGuest = Item.GetIntProperty("KingdomNotableGuest") == 1;
			row.CausalPilgrim = Item.GetIntProperty(KingdomLocus.CausalPilgrimProperty) == 1;
			row.Clearance = Item.GetPart<r_KingdomClearance>() != null;
			row.ConstructionRoot = Item.GetPart<r_KingdomPlotWorks>() != null
				|| Item.GetPart<r_KingdomScaffold>() != null;
			row.PlotRoot = KingdomPlots.TryReadRect(Item, out _);
			row.LayoutRoot = KingdomLayout.TryReadMark(Item, out _);
			row.CropRow = Item.GetIntProperty(KingdomCrops.RowProperty) == 1
				&& !string.IsNullOrEmpty(Item.GetStringProperty(KingdomCrops.RowFieldProperty));
			row.NetworkPiece = (row.Built || Item.GetIntProperty("KingdomGrid") == 1)
				&& (Item.GetPart<r_KingdomLiquidConduit>() != null
					|| Item.GetPart<r_KingdomLiquidTap>() != null
					|| Item.GetPart<r_KingdomLiquidCrossover>() != null);
			row.LabJob = Item.GetPart<r_KingdomLabJob>() != null;
			row.VisualRoot = row.Built || row.ConstructionRoot
				|| Item.GetIntProperty(KingdomPlots.HeartPlotProperty) == 1
				|| Item.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1;
			row.PlotPart = Item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1;
			row.ArchitectureComponent = Item.GetIntProperty(
				KingdomArchitectureStamper.ComponentSchemaProperty)
				== KingdomArchitectureStamper.ComponentSchema;
			row.GatehouseSatellite = Item.GetIntProperty(
				KingdomGatehouse.SatelliteProperty) == 1;
			row.DelveEndpoint = Item.GetIntProperty(
				KingdomDelveLink.EndpointSchemaProperty) == KingdomDelveLink.EndpointSchema;
			row.Furnishing = !string.IsNullOrEmpty(Item.GetStringProperty(
				KingdomPlots.FurnishReceiptProperty));
			row.HeartRelic = Item.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1;
			row.MaterialStockpile = KingdomMaterials.IsStockpile(Item) && Item.Inventory != null;
			row.Transient = Item.GetIntProperty(Simulation.City.KingdomResidents.JobIdProperty) > 0;
			return row;
		}

		private static bool BelongsToRealm(KingdomSystem citizenshipSystem, GameObject item)
		{
			return KingdomCitizenship.BelongsTo(citizenshipSystem, item);
		}

		private void Publish(IndexedRow Row, bool Add)
		{
			int sign = Add ? 1 : -1;
			Citizens += sign * (Row.Citizen ? 1 : 0);
			TradePosts += sign * (Row.TradePost ? 1 : 0);
			Beds += sign * (Row.Bed ? 1 : 0);
			Kitchens += sign * (Row.Kitchen ? 1 : 0);
			FoodStored += sign * Row.FoodStored;
			FoodCapacity += sign * Row.FoodCapacity;
			StoredWater += sign * Row.StoredWater;
			OpenWater += sign * Row.OpenWater;
			StorageSpace += sign * Row.StorageSpace;
			StorageCapacity += sign * Row.StorageCapacity;
			Publish(CitizenBodies, Row, Row.Citizen, Add);
			Publish(Settlers, Row, Row.Settler, Add);
			Publish(Built, Row, Row.Built, Add);
			Publish(Works, Row, Row.Work, Add);
			Publish(Defences, Row, Row.Defence, Add);
			Publish(Larders, Row, Row.Larder, Add);
			Publish(Raiders, Row, Row.Raider, Add);
			Publish(Cairns, Row, Row.Cairn, Add);
			Publish(PlotWorks, Row, Row.PlotWorks, Add);
			Publish(Improvements, Row, Row.Improvement, Add);
			Publish(Notices, Row, Row.Notice, Add);
			Publish(Shrines, Row, Row.Shrine, Add);
			Publish(Guests, Row, Row.Guest, Add);
			Publish(NotableGuests, Row, Row.NotableGuest, Add);
			Publish(CausalPilgrims, Row, Row.CausalPilgrim, Add);
			Publish(Clearances, Row, Row.Clearance, Add);
			Publish(ConstructionRoots, Row, Row.ConstructionRoot, Add);
			Publish(PlotRoots, Row, Row.PlotRoot, Add);
			Publish(LayoutRoots, Row, Row.LayoutRoot, Add);
			Publish(CropRows, Row, Row.CropRow, Add);
			Publish(NetworkPieces, Row, Row.NetworkPiece, Add);
			Publish(LabJobs, Row, Row.LabJob, Add);
			Publish(VisualRoots, Row, Row.VisualRoot, Add);
			Publish(PlotParts, Row, Row.PlotPart, Add);
			Publish(ArchitectureComponents, Row, Row.ArchitectureComponent, Add);
			Publish(GatehouseSatellites, Row, Row.GatehouseSatellite, Add);
			Publish(DelveEndpoints, Row, Row.DelveEndpoint, Add);
			Publish(Furnishings, Row, Row.Furnishing, Add);
			Publish(HeartRelics, Row, Row.HeartRelic, Add);
			Publish(MaterialStockpiles, Row, Row.MaterialStockpile, Add);
			Publish(ResidentBodies, Row, Row.ResidentId > 0, Add);
			Publish(Transients, Row, Row.Transient, Add);
			Publish(Stores, Row, Row.Store, Add);
			Publish(Pools, Row, Row.Pool, Add);
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
		}

		private void Publish(List<GameObject> List, IndexedRow Row, bool Member, bool Add)
		{
			if (!Member) return;
			if (!Add)
			{
				List.Remove(Row.Item);
				return;
			}
			int low = 0;
			int high = List.Count;
			while (low < high)
			{
				int middle = low + ((high - low) / 2);
				IndexedRow existing;
				if (!Rows.TryGetValue(List[middle], out existing)
					|| KingdomSurveyIndexRules.ComesBeforeOrEqual(existing.Order, Row.Order)) low = middle + 1;
				else high = middle;
			}
			List.Insert(low, Row.Item);
		}

		private void Publish(List<LiquidVolume> List, IndexedRow Row, bool Member, bool Add)
		{
			if (!Member || Row.Liquid == null) return;
			if (!Add)
			{
				List.Remove(Row.Liquid);
				return;
			}
			int low = 0;
			int high = List.Count;
			while (low < high)
			{
				int middle = low + ((high - low) / 2);
				IndexedRow existing;
				GameObject owner = List[middle]?.ParentObject;
				if (owner == null || !Rows.TryGetValue(owner, out existing)
					|| KingdomSurveyIndexRules.ComesBeforeOrEqual(existing.Order, Row.Order)) low = middle + 1;
				else high = middle;
			}
			List.Insert(low, Row.Liquid);
		}

		private void IndexLoadedBranch(IndexedRow Row)
		{
			List<GameObject> pending = new List<GameObject> { Row.Item };
			for (int cursor = 0; cursor < pending.Count; cursor++)
			{
				GameObject item = pending[cursor];
				if (!GameObject.Validate(item) || !LoadedSet.Add(item))
				{
					LoadedIndexComplete = false;
					continue;
				}
				if (LoadedObjects.Count >= MaxIndexedObjects)
				{
					LoadedIndexComplete = false;
					LoadedSet.Remove(item);
					continue;
				}
				LoadedObjects.Add(item);
				Row.Loaded.Add(item);
				Inventory inventory = item.Inventory;
				if (inventory == null || inventory.Objects == null) continue;
				for (int i = 0; i < inventory.Objects.Count; i++) pending.Add(inventory.Objects[i]);
			}
		}

		private void RemoveLoadedBranch(IndexedRow Row)
		{
			for (int i = 0; i < Row.Loaded.Count; i++)
			{
				LoadedSet.Remove(Row.Loaded[i]);
				LoadedObjects.Remove(Row.Loaded[i]);
			}
			Row.Loaded.Clear();
		}

		/// <summary>Publishes one new root into every index. Unknown off-ground objects are refused.</summary>
		public bool ObserveAdded(GameObject Item)
		{
			bool known = Item != null && Rows.ContainsKey(Item);
			bool valid = GameObject.Validate(Item);
			bool here = valid && Ground != null && ReferenceEquals(Item.CurrentZone, Ground)
				&& Item.CurrentCell != null && ReferenceEquals(Item.CurrentCell.ParentZone, Ground);
			KingdomSurveyIndexRules.Mutation action = KingdomSurveyIndexRules.Classify(known, valid, here);
			if (action == KingdomSurveyIndexRules.Mutation.Refresh) return ObserveChanged(Item);
			if (action != KingdomSurveyIndexRules.Mutation.Add) return false;
			AddRoot(Item, The.Game?.GetSystem<KingdomSystem>());
			bool added = Rows.ContainsKey(Item);
			if (added) AddedMutations++;
			return added;
		}

		/// <summary>Reclassifies one exact known root after a physical/property commit.</summary>
		public bool ObserveChanged(GameObject Item)
		{
			IndexedRow old = null;
			bool known = Item != null && Rows.TryGetValue(Item, out old);
			bool valid = GameObject.Validate(Item);
			bool here = valid && Ground != null && ReferenceEquals(Item.CurrentZone, Ground)
				&& Item.CurrentCell != null && ReferenceEquals(Item.CurrentCell.ParentZone, Ground);
			KingdomSurveyIndexRules.Mutation action = KingdomSurveyIndexRules.Classify(known, valid, here);
			if (action == KingdomSurveyIndexRules.Mutation.Remove) return ObserveRemoved(Item);
			if (action == KingdomSurveyIndexRules.Mutation.Add) return ObserveAdded(Item);
			if (action != KingdomSurveyIndexRules.Mutation.Refresh) return false;
			Publish(old, false);
			RemoveLoadedBranch(old);
			IndexedRow fresh = Capture(Item, The.Game?.GetSystem<KingdomSystem>(), old.Order);
			Rows[Item] = fresh;
			Publish(fresh, true);
			IndexLoadedBranch(fresh);
			ChangedMutations++;
			return true;
		}

		/// <summary>Re-proves the actual topology after an engine callback threw. Qud callbacks may
		/// apply their physical effect before raising: a known survivor refreshes, a known absence
		/// removes, and an unknown object that actually landed on this ground is added.</summary>
		public bool ObserveCurrentTopology(GameObject Item)
		{
			return ObserveChanged(Item);
		}

		/// <summary>Removes one known root after its exact destruction/move commits.</summary>
		public bool ObserveRemoved(GameObject Item)
		{
			IndexedRow row;
			if (Item == null || !Rows.TryGetValue(Item, out row)) return false;
			Publish(row, false);
			RemoveLoadedBranch(row);
			Rows.Remove(Item);
			Objects.Remove(Item);
			RemovedMutations++;
			return true;
		}

		/// <summary>Updates a receipt-bound object's cached contribution after the caller already
		/// published the exact aggregate delta. Category changes are refused as mixed evidence.</summary>
		internal bool SynchronizeReceiptObject(GameObject Item)
		{
			IndexedRow old;
			if (Item == null || !Rows.TryGetValue(Item, out old) || !GameObject.Validate(Item)) return false;
			IndexedRow fresh = Capture(Item, The.Game?.GetSystem<KingdomSystem>(), old.Order);
			if (!SameShape(old, fresh)) return false;
			RemoveLoadedBranch(old);
			Rows[Item] = fresh;
			IndexLoadedBranch(fresh);
			return true;
		}

		private static bool SameShape(IndexedRow A, IndexedRow B)
		{
			return A.Citizen == B.Citizen && A.Settler == B.Settler
				&& A.TradePost == B.TradePost && A.Built == B.Built && A.Bed == B.Bed
				&& A.Kitchen == B.Kitchen && A.Work == B.Work && A.Defence == B.Defence
				&& A.Larder == B.Larder && A.Pool == B.Pool && A.Store == B.Store
				&& A.Raider == B.Raider && A.Cairn == B.Cairn && A.PlotWorks == B.PlotWorks
				&& A.Improvement == B.Improvement && A.Notice == B.Notice
				&& A.Shrine == B.Shrine && A.Guest == B.Guest
				&& A.NotableGuest == B.NotableGuest && A.CausalPilgrim == B.CausalPilgrim
				&& A.Clearance == B.Clearance && A.ConstructionRoot == B.ConstructionRoot
				&& A.PlotRoot == B.PlotRoot && A.LayoutRoot == B.LayoutRoot
				&& A.CropRow == B.CropRow && A.NetworkPiece == B.NetworkPiece
				&& A.LabJob == B.LabJob && A.VisualRoot == B.VisualRoot
				&& A.PlotPart == B.PlotPart
				&& A.ArchitectureComponent == B.ArchitectureComponent
				&& A.GatehouseSatellite == B.GatehouseSatellite
				&& A.DelveEndpoint == B.DelveEndpoint
				&& A.Furnishing == B.Furnishing && A.HeartRelic == B.HeartRelic
				&& A.MaterialStockpile == B.MaterialStockpile
				&& A.Transient == B.Transient && A.ResidentId == B.ResidentId
				&& ReferenceEquals(A.Liquid, B.Liquid);
		}

		internal bool TryLoaded(out IList<GameObject> Loaded)
		{
			Loaded = LoadedObjects;
			return LoadedIndexComplete;
		}

		private void EmitPassReceipt()
		{
			if (!KingdomLog.Enabled) return;
			KingdomLog.Log("survey: zone=" + (Ground?.ZoneID ?? "<none>")
				+ " classifications=" + ClassificationPasses
				+ " foreign=" + ForeignClassifications
				+ " roots=" + ClassifiedRoots + " indexed=" + Objects.Count
				+ " reuses=" + ActiveReuses + " added=" + AddedMutations
				+ " changed=" + ChangedMutations + " removed=" + RemovedMutations);
		}

		public GameObject FindCitizen(int ResidentId)
		{
			if (ResidentId <= 0) return null;
			GameObject found = null;
			for (int i = 0; i < CitizenBodies.Count; i++)
			{
				GameObject item = CitizenBodies[i];
				IndexedRow row;
				if (!Rows.TryGetValue(item, out row) || row.ResidentId != ResidentId) continue;
				if (found != null) return null;
				found = item;
			}
			return found;
		}

		/// <summary>Exact resident-body witness from the maintained id index. Duplicate id bodies
		/// fail closed as Missing; publishing a transition from ambiguous physical evidence would
		/// conceal the very duplication the binding invariant exists to expose.</summary>
		internal bool TryWitnessResident(int ResidentId,
			out Simulation.City.KingdomBodyWitness Witness)
		{
			Witness = Simulation.City.KingdomBodyWitness.Missing;
			GameObject found = null;
			for (int i = 0; i < ResidentBodies.Count; i++)
			{
				GameObject item = ResidentBodies[i];
				if (!GameObject.Validate(item)
					|| Simulation.City.KingdomResidents.IdOf(item) != ResidentId) continue;
				if (found != null) return false;
				found = item;
			}
			if (found == null) return true;
			if (found.IsPlayerLed() || found.IsPlayer())
				Witness = Simulation.City.KingdomBodyWitness.Led;
			else Witness = found.IsAlive ? Simulation.City.KingdomBodyWitness.Present
				: Simulation.City.KingdomBodyWitness.Killed;
			return true;
		}

		/// <summary>One exact live body for a persisted engine id, restricted to the binding kind's
		/// already-classified subset. Null means absent or ambiguous.</summary>
		internal GameObject FindBoundBody(string ObjectId,
			Simulation.City.KingdomBindingKind Kind)
		{
			if (string.IsNullOrEmpty(ObjectId)) return null;
			List<GameObject> candidates = Kind == Simulation.City.KingdomBindingKind.Resident
				? ResidentBodies : Transients;
			GameObject found = null;
			for (int i = 0; i < candidates.Count; i++)
			{
				GameObject item = candidates[i];
				if (!GameObject.Validate(item)
					|| !string.Equals(item.IDIfAssigned, ObjectId, StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = item;
			}
			return found;
		}

		internal GameObject FindTransient(int JobId)
		{
			if (JobId <= 0) return null;
			GameObject found = null;
			for (int i = 0; i < Transients.Count; i++)
			{
				GameObject item = Transients[i];
				if (!GameObject.Validate(item)
					|| item.GetIntProperty(Simulation.City.KingdomResidents.JobIdProperty) != JobId)
					continue;
				if (found != null) return null;
				found = item;
			}
			return found;
		}

		public static bool ObserveAddedToActive(Zone Z, GameObject Item)
		{
			KingdomSurvey survey = ActiveFor(Z);
			return survey != null && survey.ObserveAdded(Item);
		}

		public static bool ObserveChangedInActive(Zone Z, GameObject Item)
		{
			KingdomSurvey survey = ActiveFor(Z);
			return survey != null && survey.ObserveChanged(Item);
		}

		/// <summary>Callback-failure seam: publish what physically exists, not what the callback
		/// returned or threw, into the one bound survey.</summary>
		public static bool ObserveCurrentTopologyInActive(Zone Z, GameObject Item)
		{
			KingdomSurvey survey = ActiveFor(Z);
			return survey != null && survey.ObserveCurrentTopology(Item);
		}

		/// <summary>AddObject may stack into or replace the attempted object. Re-prove both
		/// identities so a landed replacement refreshes instead of remaining stale.</summary>
		public static void ObserveAddResultInActive(Zone Z, GameObject Attempted,
			GameObject Accepted)
		{
			KingdomSurvey survey = ActiveFor(Z);
			if (survey == null) return;
			survey.ObserveCurrentTopology(Attempted);
			if (!ReferenceEquals(Accepted, Attempted))
				survey.ObserveCurrentTopology(Accepted);
		}

		public static bool ObserveRemovedFromActive(Zone Z, GameObject Item)
		{
			KingdomSurvey survey = ActiveFor(Z);
			return survey != null && survey.ObserveRemoved(Item);
		}

		/// <summary>
		/// As <see cref="Take(Zone)"/>, but also folds in the settlement-wide defence bonus its
		/// districts earn. A garrison trains the whole watch, not just the tower standing on it,
		/// so the bonus is read from every claimed zone's district, not only this one.
		/// </summary>
		/// <param name="Z">Zone to survey. Null yields an empty survey.</param>
		/// <param name="System">Kingdom whose claimed-zone districts contribute the bonus.</param>
		public static KingdomSurvey Take(Zone Z, KingdomSystem System)
		{
			KingdomSurvey survey = Take(Z);
			if (System != null)
			{
				survey.DistrictDefenceBonus = KingdomRules.DistrictsDefenceBonus(System.ZoneDistricts.Values);
			}
			return survey;
		}

		/// <summary>
		/// The settlement's defence: the sum of its defensive works, counting only those with
		/// the crew to man them, plus any kingdom-wide bonus from garrison districts. A
		/// watchtower with nobody in it defends nothing; a garrison district defends everywhere.
		/// </summary>
		public int Defence()
		{
			int total = 0;
			for (int i = 0; i < Defences.Count; i++)
			{
				GameObject work = Defences[i];
				int need = work.GetIntProperty("KingdomStaffNeeded");
				int effectiveness = (need > 0) ? work.GetIntProperty("KingdomEffectiveness") : 100;
				effectiveness = KingdomCrews.ApplyAffinity(work, effectiveness);
				total += work.GetIntProperty("KingdomDefence") * effectiveness / 100;
			}
			return total + DistrictDefenceBonus;
		}

		/// <summary>Draws water from the dedicated stores, updating the survey's counters.</summary>
		/// <param name="Drams">Amount requested.</param>
		/// <returns>Amount actually drawn, which may be less than requested.</returns>
		public int Consume(int Drams)
		{
			int remaining = Drams;
			for (int i = 0; i < Stores.Count && remaining > 0; i++)
			{
				LiquidVolume store = Stores[i];
				if (!KingdomLiquids.HasFreshWater(store))
				{
					continue;
				}
				int removed = KingdomLiquids.Drain(store, remaining);
				if (removed > 0)
				{
					remaining -= removed;
					StoredWater -= removed;
					StorageSpace += removed;
					SynchronizeReceiptObject(store.ParentObject);
				}
			}
			return Drams - remaining;
		}

		/// <summary>
		/// Reserves an all-or-nothing water debit against the exact dedicated vessels in this
		/// snapshot. Reservation does not remove water. The returned receipt must be
		/// <see cref="KingdomWaterDebit.Commit"/>ted after the caller's other preconditions pass,
		/// and may be <see cref="KingdomWaterDebit.Rollback"/>ed into those same vessels if the
		/// enclosing operation later fails. Use <see cref="Consume"/> instead where a deliberately
		/// partial simulation loss is the rule.
		/// </summary>
		/// <param name="Drams">Exact amount required. Non-positive amounts reserve a total no-op.</param>
		public KingdomWaterDebit ReserveExactWater(int Drams)
		{
			return KingdomWaterDebit.Reserve(this, Drams);
		}

		/// <summary>Try-pattern facade for callers that must not proceed without an exact receipt.</summary>
		public bool TryReserveExactWater(int Drams, out KingdomWaterDebit Debit)
		{
			Debit = ReserveExactWater(Drams);
			return Debit.State == KingdomWaterDebitState.Reserved;
		}

		/// <summary>
		/// Spends food from the dedicated larders, updating <see cref="FoodStored"/> and
		/// <see cref="FoodAbundance"/> to match. Draws whole food items, and partial stacks, from
		/// <see cref="Larders"/> in the order found, until the amount is met or the larders run
		/// out &mdash; never more than what is actually there, and never from anything the founder
		/// has not dedicated.
		/// </summary>
		/// <param name="Amount">Food units requested.</param>
		/// <returns>Amount actually spent, which may be less than requested.</returns>
		public int ConsumeFood(int Amount)
		{
			int _;
			return ConsumeFood(Amount, null, out _);
		}

		/// <summary>
		/// The meal-shaped draw (Addendum 11(b)): spends the day's food out of the dedicated
		/// larders, <b>reaching for the settlement's own dish first</b>.
		/// <para>
		/// <b>The order, stated once and deterministic.</b> Pass one takes
		/// <paramref name="Preferred"/> &mdash; the preserved staple the settlement's favourite
		/// dish is made of, which is also what its mill produces &mdash; walking
		/// <see cref="Larders"/> in the order found and each larder's inventory in the order
		/// held. Pass two takes everything else that is food, in the same order. Nothing is
		/// random, so the same larders drained in the same sequence give the same answer on every
		/// reload, which is what Addendum 12(d) asks of any draw that lands on real containers.
		/// </para>
		/// <para>
		/// Why the staple goes first and not last: a settlement eats what it cooks. The staple is
		/// the thing the fields grew and the mill bound to keep, and a granary that hoards its own
		/// dish while the settlement chews raw tubers is not a settlement anybody would write
		/// down. It also makes the favoured meal reachable in exactly the case it should be
		/// &mdash; when the chain from field to mill to table is actually running.
		/// </para>
		/// </summary>
		/// <param name="Amount">Food units requested.</param>
		/// <param name="Preferred">The dish's staple blueprint, or null to draw in plain order.</param>
		/// <param name="FromPreferred">Set to how much of the draw came off that staple, which is
		/// what <c>KingdomRules.JudgeMeal</c> reads to decide whether the settlement ate its own
		/// dish or merely ate.</param>
		/// <returns>Amount actually spent, which may be less than requested.</returns>
		public int ConsumeFood(int Amount, string Preferred, out int FromPreferred)
		{
			FromPreferred = 0;
			int remaining = Amount;
			if (!string.IsNullOrEmpty(Preferred))
			{
				FromPreferred = Draw(ref remaining, Preferred);
			}
			Draw(ref remaining, null);
			int spent = Amount - remaining;
			FoodStored -= spent;
			if (FoodStored < 0)
			{
				FoodStored = 0;
			}
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			SynchronizeLarders();
			return spent;
		}

		/// <summary>
		/// One pass of the draw. Counters are left to the caller so a two-pass draw adjusts
		/// <see cref="FoodStored"/> exactly once.
		/// </summary>
		/// <param name="Remaining">Servings still wanted; decremented in place.</param>
		/// <param name="Blueprint">Restrict to this blueprint, or null for anything edible.</param>
		/// <returns>Servings this pass took.</returns>
		private int Draw(ref int Remaining, string Blueprint)
		{
			int took = 0;
			for (int i = 0; i < Larders.Count && Remaining > 0; i++)
			{
				GameObject container = Larders[i];
				if (container == null || container.Inventory == null)
				{
					continue;
				}
				// Snapshot first: destroying a food item below removes it from this same
				// Inventory list, and mutating a collection mid-foreach throws.
				List<GameObject> held = new List<GameObject>(container.Inventory.Objects);
				for (int j = 0; j < held.Count && Remaining > 0; j++)
				{
					GameObject food = held[j];
					if (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient"))
					{
						continue;
					}
					if (Blueprint != null && food.Blueprint != Blueprint)
					{
						continue;
					}
					// Destroy() on a stack of more than one decrements it by exactly one and
					// leaves the object in place (see Stacker.HandleEvent(BeforeDestroyObjectEvent));
					// only the last unit actually removes it. Validate stops the loop the moment
					// that happens, rather than trusting a return value for it.
					while (Remaining > 0 && GameObject.Validate(food))
					{
						food.Destroy(null, Silent: true);
						Remaining--;
						took++;
					}
				}
			}
			return took;
		}

		/// <summary>
		/// Takes raw crops of one named blueprint out of the larders, for industry rather than
		/// for a mouth (Addendum 11(b): food "used by industry to produce things"). The grinding
		/// mill's input half; <see cref="StoreFood"/> puts the preserved staple back.
		/// <para>
		/// Named rather than general on purpose: a mill grinds the settlement's own harvest, and
		/// a draw that took anything edible would grind the staple it had just made back into
		/// itself. Same order and the same determinism as <see cref="ConsumeFood"/>.
		/// </para>
		/// </summary>
		/// <param name="Blueprint">The crop to grind. Null or empty takes nothing.</param>
		/// <param name="Amount">Crops wanted.</param>
		/// <returns>Crops actually taken, which may be fewer than asked for.</returns>
		public int ConsumeCrop(string Blueprint, int Amount)
		{
			if (string.IsNullOrEmpty(Blueprint) || Amount <= 0)
			{
				return 0;
			}
			int remaining = Amount;
			int took = Draw(ref remaining, Blueprint);
			FoodStored -= took;
			if (FoodStored < 0)
			{
				FoodStored = 0;
			}
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			SynchronizeLarders();
			return took;
		}


		/// <summary>
		/// Servings one container holds right now: vanilla <c>Food</c> or
		/// <c>PreparedCookingIngredient</c> items, counted by stack rather than by object so a
		/// stack of twenty apples reads as twenty.
		/// </summary>
		/// <param name="Container">Any object. Null, or one with no inventory, holds nothing.</param>
		public static int HeldIn(GameObject Container)
		{
			if (Container == null || Container.Inventory == null)
			{
				return 0;
			}
			int held = 0;
			foreach (GameObject item in Container.Inventory.Objects)
			{
				if (item.HasPart("Food") || item.HasPart("PreparedCookingIngredient"))
				{
					held += item.Count;
				}
			}
			return held;
		}

		/// <summary>Current edible units of one exact blueprint across this survey's dedicated
		/// larders. Used only for inspection; the ration draw remains authoritative.</summary>
		public int CountFood(string Blueprint)
		{
			if (string.IsNullOrEmpty(Blueprint))
			{
				return 0;
			}
			int held = 0;
			for (int i = 0; i < Larders.Count; i++)
			{
				GameObject container = Larders[i];
				if (container == null || container.Inventory == null)
				{
					continue;
				}
				foreach (GameObject item in container.Inventory.Objects)
				{
					if (item.Blueprint != Blueprint || (!item.HasPart("Food")
						&& !item.HasPart("PreparedCookingIngredient")) || item.Count <= 0)
					{
						continue;
					}
					if (held > int.MaxValue - item.Count)
					{
						return int.MaxValue;
					}
					held += item.Count;
				}
			}
			return held;
		}

		/// <summary>
		/// Servings one container was built to hold, off its blueprint's own
		/// <see cref="KingdomRules.LarderCapacityTag"/> &mdash; the food side of a vessel's
		/// <c>MaxVolume</c>. A container that declares nothing gets
		/// <see cref="KingdomRules.DefaultLarderCapacity"/>, which is the chest a founder walked
		/// up to and dedicated by hand.
		/// </summary>
		public static int CapacityOf(GameObject Container)
		{
			if (Container == null)
			{
				return 0;
			}
			int declared;
			// GetTag reads the blueprint's own dictionary, so a modded pantry declares its size
			// in XML exactly the way a modded cistern declares MaxVolume.
			if (!int.TryParse(Container.GetTag(KingdomRules.LarderCapacityTag, ""), out declared))
			{
				declared = 0;
			}
			return KingdomRules.LarderCapacity(declared);
		}

		/// <summary>
		/// Takes a finished work into the settlement's food stores and keeps this survey's
		/// counters in step, so a granary raised before this pass is a pantry from the moment the
		/// pass notices it rather than from the pass after.
		/// <para>
		/// STANDARDS 7 &mdash; "commissioned storage auto-flags" &mdash; is the whole warrant.
		/// Nothing the founder placed is swept up: the caller is expected to have checked
		/// <c>KingdomBuilt</c> and <see cref="KingdomRules.IsCivicLarderBlueprint"/>, which
		/// between them mean the settlement paid for this and the catalogue calls it a pantry.
		/// </para>
		/// </summary>
		/// <param name="Work">The finished container. Null, one with no inventory, or one already
		/// dedicated is left alone.</param>
		/// <returns>True when this call is what dedicated it.</returns>
		public bool AdoptLarder(GameObject Work)
		{
			if (Work == null || Work.Inventory == null || Work.GetIntProperty("KingdomLarder") == 1)
			{
				return false;
			}
			Work.SetIntProperty("KingdomLarder", 1);
			if (Rows.ContainsKey(Work))
			{
				ObserveChanged(Work);
			}
			else
			{
				Larders.Add(Work);
				FoodCapacity += CapacityOf(Work);
				FoodStored += HeldIn(Work);
				FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			}
			return true;
		}

		/// <summary>
		/// Puts food into the dedicated larders, updating the survey's counters. The food mirror
		/// of <see cref="Store"/>, and bounded the same way: each container takes what it has room
		/// for and no more, and whatever did not fit is handed back to the caller to be honest
		/// about.
		/// <para>
		/// Room is measured off each container as it is reached rather than read from a figure
		/// cached at <see cref="Take(Zone)"/> time, because the harvest path spawns crops into
		/// these same containers by another road within the same pass.
		/// </para>
		/// </summary>
		/// <param name="Amount">Servings offered.</param>
		/// <param name="Blueprint">What the food physically is &mdash; the settlement's own crop,
		/// from <c>KingdomData.CropForStyle</c>, so a fungal city's granary fills
		/// with mushrooms. An unknown blueprint stores nothing rather than minting a null.</param>
		/// <returns>Servings actually stored; the remainder had nowhere to go.</returns>
		public int StoreFood(int Amount, string Blueprint)
		{
			if (Amount <= 0 || string.IsNullOrEmpty(Blueprint))
			{
				return 0;
			}
			int remaining = Amount;
			for (int i = 0; i < Larders.Count && remaining > 0; i++)
			{
				GameObject container = Larders[i];
				if (container == null || container.Inventory == null)
				{
					continue;
				}
				int room = CapacityOf(container) - HeldIn(container);
				if (room <= 0)
				{
					continue;
				}
				int put = (room < remaining) ? room : remaining;
				for (int j = 0; j < put; j++)
				{
					GameObject food = GameObject.Create(Blueprint);
					if (food == null)
					{
						// The blueprint does not resolve. Nothing further is stored and nothing is
						// lost: the caller gets the rest of its offer back and can say so.
						return Amount - remaining;
					}
					// A crop this settlement's own style names but that is not actually food would
					// otherwise be an unbounded spawn: HeldIn would never count it, so the room
					// would never fill and every pass would put more of it in the chest forever.
					// Refuse the whole errand instead, and take the one object back out with us.
					if (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient"))
					{
						food.Obliterate();
						return Amount - remaining;
					}
					container.Inventory.AddObject(food, Silent: true);
					remaining--;
				}
			}
			int stored = Amount - remaining;
			FoodStored += stored;
			FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
			SynchronizeLarders();
			return stored;
		}

		/// <summary>
		/// Puts food into one exact dedicated larder and returns the measured inventory delta.
		/// Catch-up prices one container touch as one medium unit, so its runtime may not call the
		/// broad <see cref="StoreFood"/> loop and then pretend only one larder was touched.
		/// A callback exception is judged by the physical before/after count; deferred food is not
		/// harvest loss and this method deliberately does not write the settlement ledger.
		/// </summary>
		public int StoreFoodIn(GameObject Container, int Amount, string Blueprint)
		{
			if (!GameObject.Validate(Container) || Container.Inventory == null
				|| !Larders.Contains(Container) || Amount <= 0 || string.IsNullOrEmpty(Blueprint))
			{
				return 0;
			}
			int before = HeldIn(Container);
			int room = CapacityOf(Container) - before;
			int wanted = (Amount < room) ? Amount : room;
			if (wanted <= 0) return 0;
			int accepted = 0;
			for (int i = 0; i < wanted; i++)
			{
				int heldBefore = HeldIn(Container);
				GameObject food = null;
				try
				{
					food = GameObject.Create(Blueprint);
					if (!GameObject.Validate(food)
						|| (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient")))
					{
						if (GameObject.Validate(food)) food.Obliterate();
						break;
					}
					Container.Inventory.AddObject(food, Silent: true);
				}
				catch
				{
					// Measured inventory delta below decides whether callback completed.
				}
				int heldAfter = HeldIn(Container);
				if (heldAfter != heldBefore + 1)
				{
					if (GameObject.Validate(food) && food.InInventory != Container) food.Obliterate();
					break;
				}
				accepted++;
			}
			if (accepted > 0)
			{
				FoodStored += accepted;
				FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
				SynchronizeReceiptObject(Container);
			}
			return accepted;
		}

		/// <summary>
		/// Food lost out of one damaged larder, keeping the survey's counters correct the same way
		/// <see cref="ConsumeFood"/> does. The food mirror of <see cref="LeakFrom"/>, and loss
		/// rather than transfer for the same reason: this is a harvest gone bad in a holed
		/// granary, not a harvest moved somewhere the founder can walk up to (Addendum 10(b)).
		/// </summary>
		/// <param name="Container">The damaged pantry. Must be one of <see cref="Larders"/>.</param>
		/// <param name="Amount">Servings the spoilage is owed.</param>
		/// <returns>Servings actually lost, measured from the container rather than assumed.</returns>
		public int SpoilFrom(GameObject Container, int Amount)
		{
			int lost;
			TrySpoilFromExact(Container, Amount, out lost);
			return lost;
		}

		private sealed class SpoilFrame
		{
			internal GameObject Container;
			internal string ContainerId;
			internal Zone Zone;
			internal string ZoneId;
			internal Cell Cell;
			internal Inventory Inventory;
			internal List<GameObject> List;
			internal GameObject[] Items;
			internal string[] ItemIds;
			internal int[] Counts;
			internal bool[] Edible;
			internal List<GameObject> LarderList;
			internal GameObject[] LarderRows;
			internal int FoodStored;
			internal int FoodCapacity;
			internal KingdomRules.PantryTier FoodAbundance;
		}

		/// <summary>
		/// Invokes each destructive food callback once. Every unit is counted only after the exact
		/// same container, Inventory part/list, item ordering, identities, ownership, and counts prove
		/// the one expected transition. A veto with no delta never counts.
		/// </summary>
		public bool TrySpoilFromExact(GameObject Container, int Amount, out int Lost)
		{
			Lost = 0;
			SpoilFrame frame;
			if (!TryCaptureSpoilFrame(Container, Amount, out frame)) return false;
			int[] expected = (int[])frame.Counts.Clone();
			int remaining = Amount;
			for (int i = 0; i < frame.Items.Length && remaining > 0; i++)
			{
				if (!frame.Edible[i]) continue;
				GameObject food = frame.Items[i];
				while (remaining > 0 && expected[i] > 0)
				{
					if (!SpoilTopologyExact(frame, expected)) return false;
					int before = expected[i];
					try
					{
						food.Destroy(null, Silent: true);
					}
					catch
					{
						// The exact post-callback topology below, never the exception or return value,
						// decides whether one physical unit was lost.
					}
					expected[i] = before - 1;
					if (!SpoilTopologyExact(frame, expected))
					{
						expected[i] = before;
						if (SpoilTopologyExact(frame, expected))
						{
							if (!PublishSpoilCounters(frame, Lost)) Lost = 0;
						}
						else Lost = 0;
						return false;
					}
					Lost++;
					remaining--;
				}
			}
			if (!PublishSpoilCounters(frame, Lost))
			{
				Lost = 0;
				return false;
			}
			return Lost == Amount;
		}

		private bool PublishSpoilCounters(SpoilFrame Frame, int Lost)
		{
			if (Frame == null || Lost < 0 || Lost > Frame.FoodStored
				|| FoodStored != Frame.FoodStored || FoodCapacity != Frame.FoodCapacity
				|| FoodAbundance != Frame.FoodAbundance) return false;
			if (Lost > 0)
			{
					FoodStored = Frame.FoodStored - Lost;
					FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
					SynchronizeReceiptObject(Frame.Container);
			}
			return true;
		}

		private bool TryCaptureSpoilFrame(GameObject Container, int Amount,
			out SpoilFrame Frame)
		{
			Frame = null;
			Inventory inventory = GameObject.Validate(Container) ? Container.Inventory : null;
			if (inventory == null || inventory.Objects == null || inventory.ParentObject != Container
				|| Container.CurrentZone == null || Container.CurrentCell == null
				|| Container.CurrentCell.ParentZone != Container.CurrentZone || Amount <= 0
				|| FoodStored < Amount || !Larders.Contains(Container)) return false;
			GameObject[] items = inventory.Objects.ToArray();
			int[] counts = new int[items.Length];
			string[] ids = new string[items.Length];
			bool[] edible = new bool[items.Length];
			int available = 0;
			for (int i = 0; i < items.Length; i++)
			{
				GameObject item = items[i];
				if (!GameObject.Validate(item) || item.Physics == null || item.InInventory != Container
					|| item.CurrentCell != null || item.Count <= 0 || string.IsNullOrEmpty(item.ID)) return false;
				for (int j = 0; j < i; j++) if (ReferenceEquals(items[j], item)) return false;
				counts[i] = item.Count;
				ids[i] = item.ID;
				edible[i] = item.HasPart("Food") || item.HasPart("PreparedCookingIngredient");
				if (edible[i])
				{
					long next = (long)available + item.Count;
					available = (next > int.MaxValue) ? int.MaxValue : (int)next;
				}
			}
			if (available < Amount) return false;
			Frame = new SpoilFrame
			{
				Container = Container,
				ContainerId = Container.ID,
				Zone = Container.CurrentZone,
				ZoneId = Container.CurrentZone.ZoneID,
				Cell = Container.CurrentCell,
				Inventory = inventory,
				List = inventory.Objects,
				Items = items,
				ItemIds = ids,
				Counts = counts,
				Edible = edible,
				LarderList = Larders,
				LarderRows = Larders.ToArray(),
				FoodStored = FoodStored,
				FoodCapacity = FoodCapacity,
				FoodAbundance = FoodAbundance
			};
			return true;
		}

		private bool SpoilTopologyExact(SpoilFrame Frame, int[] Expected)
		{
			if (Frame == null || Expected == null || Expected.Length != Frame.Items.Length
				|| !GameObject.Validate(Frame.Container) || Frame.Container.ID != Frame.ContainerId
				|| Frame.Container.CurrentZone != Frame.Zone || Frame.Zone.ZoneID != Frame.ZoneId
				|| Frame.Container.CurrentCell != Frame.Cell
				|| Frame.Cell == null || Frame.Cell.ParentZone != Frame.Zone
				|| !ReferenceEquals(Frame.Container.Inventory, Frame.Inventory)
				|| Frame.Inventory.ParentObject != Frame.Container
				|| !ReferenceEquals(Frame.Inventory.Objects, Frame.List)
				|| !ReferenceEquals(Larders, Frame.LarderList)
				|| Larders.Count != Frame.LarderRows.Length
				|| FoodStored != Frame.FoodStored || FoodCapacity != Frame.FoodCapacity
				|| FoodAbundance != Frame.FoodAbundance) return false;
			for (int i = 0; i < Frame.LarderRows.Length; i++)
				if (!ReferenceEquals(Larders[i], Frame.LarderRows[i])) return false;
			int live = 0;
			for (int i = 0; i < Frame.Items.Length; i++) if (Expected[i] > 0) live++;
			if (Frame.List.Count != live) return false;
			int row = 0;
			for (int i = 0; i < Frame.Items.Length; i++)
			{
				GameObject item = Frame.Items[i];
				if (Expected[i] <= 0)
				{
					if (Frame.List.Contains(item) || GameObject.Validate(item)) return false;
					continue;
				}
				if (!ReferenceEquals(Frame.List[row++], item) || !GameObject.Validate(item)
					|| item.ID != Frame.ItemIds[i] || item.Count != Expected[i]
					|| item.InInventory != Frame.Container || item.CurrentCell != null
					|| (item.HasPart("Food") || item.HasPart("PreparedCookingIngredient")) != Frame.Edible[i])
					return false;
			}
			return true;
		}

		/// <summary>
		/// Water lost out of one damaged store, keeping the survey's counters correct the same way
		/// <see cref="Consume"/> does. Loss and not transfer: this water runs into the ground and
		/// is gone (Addendum 10(b)), which is why it does not go through
		/// <c>KingdomLiquids.PourOnGround</c> the way a manifest's surplus does &mdash; that
		/// surplus is water a founder can still walk up to, and this is not.
		/// </summary>
		/// <param name="Store">The damaged vessel. Must be one of <see cref="Stores"/>.</param>
		/// <param name="Drams">Amount the leak is owed.</param>
		/// <returns>Drams actually lost, measured from the vessel rather than assumed.</returns>
		public int LeakFrom(LiquidVolume Store, int Drams)
		{
			int lost;
			return TryLeakFromExact(Store, Drams, out lost) ? lost : 0;
		}

		/// <summary>Exact callback-safe leak from one dedicated pure-water vessel.</summary>
		public bool TryLeakFromExact(LiquidVolume Store, int Drams, out int Lost)
		{
			Lost = 0;
			GameObject owner = (Store == null) ? null : Store.ParentObject;
			Zone zone = GameObject.Validate(owner) ? owner.CurrentZone : null;
			Cell cell = GameObject.Validate(owner) ? owner.CurrentCell : null;
			string ownerId = GameObject.Validate(owner) ? owner.ID : null;
			string zoneId = (zone == null) ? null : zone.ZoneID;
			Dictionary<string, int> dictionary = (Store == null) ? null : Store.ComponentLiquids;
			Dictionary<string, int> components = (dictionary == null)
				? null : new Dictionary<string, int>(dictionary);
			LiquidVolume[] rows = Stores.ToArray();
			int occurrences = 0;
			for (int i = 0; i < rows.Length; i++) if (ReferenceEquals(rows[i], Store)) occurrences++;
			if (Store == null || Drams <= 0 || occurrences != 1 || !GameObject.Validate(owner)
				|| zone == null || cell == null || cell.ParentZone != zone
				|| owner.GetIntProperty("KingdomStores") != 1 || Store.ParentObject != owner
				|| !ReferenceEquals(owner.GetPart<LiquidVolume>(), Store)
				|| dictionary == null || components == null || Store.MaxVolume < 0
				|| Store.Volume < Drams || !KingdomLiquids.HasFreshWater(Store)
				|| StoredWater < Drams || StorageSpace < 0) return false;
			int before = Store.Volume;
			int max = Store.MaxVolume;
			int oldStored = StoredWater;
			int oldSpace = StorageSpace;
			try
			{
				KingdomLiquids.Drain(Store, Drams);
			}
			catch
			{
				// Exact post-state below is authoritative even when a refresh callback throws.
			}
			if (!GameObject.Validate(owner) || owner.ID != ownerId || owner.CurrentZone != zone
				|| zone.ZoneID != zoneId || owner.CurrentCell != cell || cell.ParentZone != zone
				|| owner.GetIntProperty("KingdomStores") != 1
				|| Store.ParentObject != owner || !ReferenceEquals(owner.GetPart<LiquidVolume>(), Store)
				|| Store.MaxVolume != max || Store.Volume != before - Drams
				|| !ReferenceEquals(Store.ComponentLiquids, dictionary)
				|| !LeakComponentsExact(Store.ComponentLiquids, components, Store.Volume == 0)
				|| Stores.Count != rows.Length || StoredWater != oldStored || StorageSpace != oldSpace)
				return false;
			for (int i = 0; i < rows.Length; i++) if (!ReferenceEquals(Stores[i], rows[i])) return false;
			int newSpace;
			try { newSpace = checked(oldSpace + Drams); }
			catch (OverflowException) { return false; }
			StoredWater = oldStored - Drams;
			StorageSpace = newSpace;
			SynchronizeReceiptObject(owner);
			Lost = Drams;
			return true;
		}

		private static bool LeakComponentsExact(Dictionary<string, int> Current,
			Dictionary<string, int> Before, bool Empty)
		{
			if (Current == null || Before == null) return false;
			if (Empty) return Current.Count == 0;
			if (Current.Count != Before.Count) return false;
			foreach (KeyValuePair<string, int> pair in Before)
			{
				int value;
				if (!Current.TryGetValue(pair.Key, out value) || value != pair.Value) return false;
			}
			return true;
		}

		/// <summary>Pours water into the dedicated stores, updating the survey's counters.</summary>
		/// <param name="Drams">Amount offered.</param>
		/// <returns>Amount actually stored; the remainder had nowhere to go.</returns>
		public int Store(int Drams)
		{
			int remaining = Drams;
			for (int i = 0; i < Stores.Count && remaining > 0; i++)
			{
				LiquidVolume store = Stores[i];
				if (store.Volume >= store.MaxVolume || !KingdomLiquids.CanReceiveFreshWater(store))
				{
					continue;
				}
				int drams = store.MaxVolume - store.Volume;
				if (drams > remaining)
				{
					drams = remaining;
				}
				int added = KingdomLiquids.Fill(store, "water", drams);
				if (added > 0)
				{
					remaining -= added;
					StoredWater += added;
					StorageSpace -= added;
					SynchronizeReceiptObject(store.ParentObject);
				}
			}
			return Drams - remaining;
		}

		/// <summary>
		/// Pours into one exact dedicated vessel. The physical volume delta, including a callback
		/// that completed and then threw, is the only amount published to survey counters.
		/// </summary>
		public int StoreIn(LiquidVolume Store, int Drams)
		{
			if (Store == null || Drams <= 0 || !Stores.Contains(Store)
				|| Store.MaxVolume < 0 || Store.Volume < 0 || Store.Volume >= Store.MaxVolume
				|| !KingdomLiquids.CanReceiveFreshWater(Store)) return 0;
			int before = Store.Volume;
			int wanted = Store.MaxVolume - before;
			if (wanted > Drams) wanted = Drams;
			try
			{
				KingdomLiquids.Fill(Store, "water", wanted);
			}
			catch
			{
				// Measured volume delta below decides whether callback completed.
			}
			int added = Store.Volume - before;
			if (added <= 0 || added > wanted) return 0;
			StoredWater += added;
			StorageSpace -= added;
			SynchronizeReceiptObject(Store.ParentObject);
			return added;
		}

		/// <summary>Drains open water sources, updating the survey's counters.</summary>
		/// <param name="Drams">Amount requested.</param>
		/// <returns>Amount actually drawn.</returns>
		public int DrawFromPools(int Drams)
		{
			int remaining = Drams;
			for (int i = 0; i < Pools.Count && remaining > 0; i++)
			{
				LiquidVolume pool = Pools[i];
				if (pool.Volume <= 0)
				{
					continue;
				}
				int removed = KingdomLiquids.Drain(pool, remaining);
				if (removed > 0)
				{
					remaining -= removed;
					OpenWater -= removed;
					SynchronizeReceiptObject(pool.ParentObject);
				}
			}
			return Drams - remaining;
		}

		private void SynchronizeLarders()
		{
			for (int i = 0; i < Larders.Count; i++) SynchronizeReceiptObject(Larders[i]);
		}
	}
}
