using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Observation helpers for <see cref="KingdomCampHeartNativeChecks"/>.
	/// <para>
	/// SCOPE OF THE OBSERVATION, STATED EXACTLY. The store's contents are read RAW: the
	/// container's own object list, each entry's exact identity, blueprint, exact holder, and raw
	/// stack count through the production <c>KingdomMaterials.RawCensusCountOf</c> - never an
	/// eventful count and never a material tally. The settlement-wide readings (which works are
	/// standing, how much water is stored) come from the production unbound-recovery observation:
	/// it builds the ordinary physical index WITHOUT legacy migration, citizenship publication,
	/// ledger work or economic simulation, is refused outright while a settlement pass holds a
	/// bound survey, and refuses a PARTIAL index rather than under-reporting the ground.
	/// </para>
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			internal int RiteX, RiteY;

			/// <summary>The authored water bill for the rung-2 climb, read from the production
			/// catalogue rather than repeated here as a number.</summary>
			internal int AuthoredWaterCost
			{
				get
				{
					KingdomRules.BuildEntry predecessor = null;
					KingdomRules.BuildEntry successor = null;
					bool known = KingdomData.TryGetBuilding(FirstRungKey, out predecessor)
						& KingdomData.TryGetBuilding(SecondRungKey, out successor);
					Require(known && predecessor != null && successor != null,
						"taf-camp-heart-catalogue-absent: the authored heart ladder is missing");
					return KingdomUpgradeRules.CostDrams(successor.CostDrams,
						predecessor.CostDrams, -1);
				}
			}

			/// <summary>A COMPLETE physical observation of the ground that publishes nothing.
			/// <c>TryTakeUnboundRecovery</c> is the production custody-only classification plus
			/// the two proofs a bare custody-only call does not make: that no settlement pass
			/// holds a bound survey, and that every root in the zone was actually classified. A
			/// partial index would silently under-report the works standing here.</summary>
			internal KingdomSurvey Census()
			{
				KingdomSurvey survey;
				Require(KingdomSurvey.TryTakeUnboundRecovery(Zone, out survey)
					&& survey != null && ReferenceEquals(survey.Ground, Zone),
					"taf-camp-heart-census-incomplete: the unbound custody observation refused or "
						+ "did not classify every root");
				return survey;
			}

			/// <summary>Every physical unit in the camp store, with its exact custody. Custody is
			/// read from the body's own Physics FIELDS: the container that holds it must be this
			/// exact store object, and its cell must be null, because a body cannot both be in a
			/// chest and stand on the ground. An entry that is not a valid body, carries no
			/// assigned identity, reports a nonpositive raw count, or contradicts itself FAILS
			/// with a <c>taf-</c> reason; nothing is skipped.</summary>
			internal List<KingdomCampHeartNativeCensus.Unit> ContentUnits(
				out List<GameObject> Bodies)
			{
				Require(GameObject.Validate(Store) && Store.Inventory != null,
					"taf-camp-store-lost-inventory: the camp store has no physical inventory");
				List<KingdomCampHeartNativeCensus.Unit> units =
					new List<KingdomCampHeartNativeCensus.Unit>();
				Bodies = new List<GameObject>();
				List<GameObject> objects = Store.Inventory.Objects;
				for (int i = 0; i < objects.Count; i++)
				{
					GameObject item = objects[i];
					RequireExactStoreCustody(item, "row " + i + " of the camp store");
					units.Add(new KingdomCampHeartNativeCensus.Unit(item.IDIfAssigned,
						item.Blueprint, HolderOf(item),
						KingdomMaterials.RawCensusCountOf(item)));
					Bodies.Add(item);
				}
				List<string> duplicates = KingdomCampHeartNativeCensus.Duplicates(units);
				Require(duplicates.Count == 0, "taf-camp-store-duplicate-identity: "
					+ KingdomCampHeartNativeCensus.Join(duplicates, 4));
				return units;
			}

			/// <summary>The field-only custody proof one body must pass to be counted: a valid
			/// body with an assigned identity, held by THIS store object, standing on no cell,
			/// and reporting a positive raw count.</summary>
			internal void RequireExactStoreCustody(GameObject Item, string Where)
			{
				Require(GameObject.Validate(Item),
					"taf-camp-store-invalid-entry: " + Where + " is not a valid body");
				Physics physics = Item.Physics;
				Require(physics != null,
					"taf-camp-store-no-physics: " + Where + " carries no physical custody");
				Require(ReferenceEquals(physics.InInventory, Store),
					"taf-camp-store-foreign-holder: " + Where + " is not held by this store");
				Require(physics.CurrentCell == null,
					"taf-camp-store-contradictory-custody: " + Where + " is in the store and on a "
						+ "cell at the same time");
				Require(!string.IsNullOrEmpty(Item.IDIfAssigned),
					"taf-camp-store-unassigned-identity: " + Where + " has no assigned identity");
				Require(KingdomMaterials.RawCensusCountOf(Item) > 0,
					"taf-camp-store-nonpositive-count: " + Where + " reports a raw count of "
						+ KingdomMaterials.RawCensusCountOf(Item));
			}

			/// <summary>The retained bodies themselves, compared BY REFERENCE. A replacement that
			/// reused the same identity string is a different object and is refused here even
			/// though every recorded field would match.</summary>
			internal void RequireSameBodies(List<GameObject> Retained,
				List<GameObject> Present, string Label)
			{
				for (int i = 0; i < Retained.Count; i++)
				{
					GameObject want = Retained[i];
					bool found = false;
					for (int j = 0; j < Present.Count; j++)
						if (ReferenceEquals(Present[j], want)) found = true;
					Require(found, "taf-camp-store-body-replaced: retained " + Label + " body "
						+ (want == null ? "(null)" : want.IDIfAssigned)
						+ " is no longer the same object in the camp store");
					RequireExactStoreCustody(want, "retained " + Label + " body " + i);
				}
			}

			/// <summary>The exact thing holding a body: the container that holds it, or the cell
			/// it stands on. Never "somewhere in the settlement".</summary>
			internal static string HolderOf(GameObject Item)
			{
				if (Item == null) return "(none)";
				GameObject container = Item.InInventory;
				if (container != null) return "inv:" + (container.IDIfAssigned ?? "(unassigned)");
				Cell cell = Item.CurrentCell;
				return cell == null ? "(none)"
					: "cell:(" + cell.X.ToString(CultureInfo.InvariantCulture) + ","
						+ cell.Y.ToString(CultureInfo.InvariantCulture) + ")";
			}

			/// <summary>Every listed unit must still be in the store as the SAME unit: same
			/// identity, same blueprint, same holder, same raw count. A matching total proves
			/// nothing about which objects it counted.</summary>
			internal void RequireHeld(List<KingdomCampHeartNativeCensus.Unit> Wanted,
				List<KingdomCampHeartNativeCensus.Unit> Present, string Label)
			{
				List<string> faults = KingdomCampHeartNativeCensus.Faults(Wanted, Present);
				Require(faults.Count == 0, faults.Count + " retained " + Label
					+ " unit(s) did not survive unchanged: "
					+ KingdomCampHeartNativeCensus.Join(faults, 4));
			}

			/// <summary>Every listed unit must be ABSENT FROM THE DEDICATED STORE. On its own
			/// this says nothing about where it went; the bill debit is proved separately from
			/// the production construction claim.</summary>
			internal void RequireAbsent(List<string> Wanted,
				List<KingdomCampHeartNativeCensus.Unit> Present, string Label)
			{
				List<string> left = KingdomCampHeartNativeCensus.Surviving(Wanted, Present);
				Require(left.Count == 0, left.Count + " " + Label
					+ " unit(s) the authored bill asks for are still in the camp store: "
					+ KingdomCampHeartNativeCensus.Join(left, 4));
			}

			/// <summary>The store is the same object, in the same cell, still dedicated.</summary>
			internal void RequireStoreIdentity()
			{
				Require(GameObject.Validate(Store) && Store.IDIfAssigned == StoreId,
					"taf-camp-store-identity-lost: the camp store is not the same object");
				Require(ReferenceEquals(Store.CurrentZone, Zone)
					&& ReferenceEquals(Store.CurrentCell, StoreCell),
					"taf-camp-store-moved: the camp store left its exact cell");
				Require(Store.Blueprint == StoreBlueprint
					&& Store.GetIntProperty("KingdomStockpile") == 1,
					"taf-camp-store-undedicated: the camp store stopped being the authored "
						+ "dedicated stockpile");
			}

			/// <summary>The one standing heart plot, observed rather than remembered.</summary>
			internal GameObject StandingHeart()
			{
				KingdomSurvey survey = Census();
				GameObject found = null;
				for (int i = 0; i < survey.Built.Count; i++)
				{
					GameObject item = survey.Built[i];
					if (!GameObject.Validate(item)
						|| item.GetIntProperty(KingdomPlots.HeartPlotProperty) != 1) continue;
					Require(found == null,
						"taf-camp-heart-ambiguous: more than one heart plot stands here");
					found = item;
				}
				Require(found != null, "taf-camp-heart-absent: no standing heart plot");
				return found;
			}

			internal string StandingRungKey()
			{
				return KingdomUpgrade.DesignKeyOf(StandingHeart());
			}
		}
	}
}
