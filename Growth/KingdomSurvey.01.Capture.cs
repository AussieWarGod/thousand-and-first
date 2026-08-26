using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSurvey
	{
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
	}
}
