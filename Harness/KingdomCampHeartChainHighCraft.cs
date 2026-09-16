using System.Collections.Generic;
using System.Text;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The half of the arcology's bill that no rung below it has: its own <c>Bits</c> and
	/// <c>Exotics</c>. Production reserves them beside the predecessor's <c>UpgradeMaterials</c>
	/// for this one successor (Growth/KingdomUpgrade.14.Begin.cs:155-172), while
	/// <c>KingdomMaterials.CanPayUpgrade</c> - the preflight every rung below uses - reads the
	/// plain tally alone (Growth/KingdomMaterials.06.InfrastructureAndDelivery.cs:141-156). A
	/// fixture that stocked only the plain tally would preflight green and be refused at funding.
	/// <para>
	/// SYNTHETIC, DISCLOSED. Nothing is hand-valued: every minted body is classified by
	/// PRODUCTION'S OWN readers - <c>KingdomMaterials.UnitBits</c> and
	/// <c>KingdomMaterials.TryExoticOf</c> (Growth/KingdomMaterials.03.StockClassification.cs:96-121,
	/// :157-185) - and the fixture keeps minting only while the production coverage predicates
	/// still refuse. A blueprint that turns out to be worth nothing is skipped, not assumed.
	/// </para>
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		// Vanilla bodies production already classifies as exotics
		// (Growth/KingdomMaterials.01.Declarations.cs:125-131). Named, not derived, because the
		// authored bill names the KIND and the blueprint table is the production mapping.
		internal const string IngotBlueprint = "Bronze Ingot";
		internal const string GemBlueprint = "Gemstone";
		private const int HighCraftScanCap = 4096;
		private const int HighCraftMintCap = 64;

		private sealed partial class Frame
		{
			private readonly List<GameObject> ChainHighCraft = new List<GameObject>();
			private string ChainHighCraftReport;

			/// <summary>Fills the supplemental store until production's own composite coverage
			/// predicates stop refusing. Called only for the fifth rung.</summary>
			private void SupplyChainHighCraft()
			{
				ChainHighCraft.Clear();
				var exotics = KingdomMaterials.ExoticCostFor(ChainTo);
				var bits = KingdomMaterials.BitCostFor(ChainTo);
				Require(!exotics.IsEmpty() && !bits.IsEmpty(),
					"taf-camp-rung5-highcraft-absent: the arcology declares no bits or exotics");
				MintExotic(IngotBlueprint, KingdomExotic.Ingot, exotics.Get(KingdomExotic.Ingot));
				MintExotic(GemBlueprint, KingdomExotic.Gem, exotics.Get(KingdomExotic.Gem));
				MintBits(bits);
				var stock = KingdomMaterials.Stock(Zone);
				ChainHighCraftReport = DescribeHighCraft(stock, bits, exotics);
				Require(KingdomScenarioJournal.Append("camp-heart-chain-exotics", true,
					ChainHighCraftReport) == null, "high-craft supply journal unavailable");
				Require(KingdomMaterialRules.CoversExotics(stock.Exotics, exotics),
					"taf-camp-rung5-exotics-short: " + ChainHighCraftReport);
				Require(KingdomMaterialRules.CoversBits(stock.Bits, bits),
					"taf-camp-rung5-bits-short: " + ChainHighCraftReport);
			}

			private void MintExotic(string Blueprint, KingdomExotic Kind, int Units)
			{
				for (int i = 0; i < Units; i++)
				{
					var unit = Create(Blueprint);
					Require(KingdomMaterials.TryExoticOf(unit, out var read) && read == Kind,
						"taf-camp-rung5-exotic-misclassified: " + Blueprint + " is not " + Kind);
					StoreHighCraft(unit);
				}
			}

			/// <summary>One body per still-wanted bit, chosen by asking production what each
			/// candidate blueprint is worth rather than by asserting a table of our own. The scan
			/// is bounded and stops as soon as the tier is covered.</summary>
			private void MintBits(KingdomBitTally Wanted)
			{
				for (int tier = 0; tier < KingdomMaterialRules.BitTierCount; tier++)
				{
					int owed = Wanted.Get(tier);
					while (owed > 0)
					{
						Require(ChainHighCraft.Count < HighCraftMintCap,
							"taf-camp-rung5-bits-unbounded: the bit mint exceeded its cap");
						string blueprint = BlueprintWorthTier(tier);
						Require(!string.IsNullOrEmpty(blueprint),
							"taf-camp-rung5-bit-tier-unsourced: no scanned blueprint is worth a "
								+ "tier-" + tier + " bit");
						var unit = Create(blueprint);
						var worth = KingdomMaterials.UnitBits(unit);
						Require(worth.Get(tier) > 0,
							"taf-camp-rung5-bit-misclassified: " + blueprint + " lost its tier-"
								+ tier + " worth once created");
						StoreHighCraft(unit);
						owed -= worth.Get(tier);
					}
				}
			}

			/// <summary>The first blueprint the engine's own factory offers that production reads
			/// as worth a bit of this tier. Bounded, read-only, and nothing is created until a
			/// candidate is found.</summary>
			private string BlueprintWorthTier(int Tier)
			{
				int scanned = 0;
				foreach (var pair in GameObjectFactory.Factory.Blueprints)
				{
					if (++scanned > HighCraftScanCap) break;
					var blueprint = pair.Value;
					if (blueprint == null || blueprint.IsBaseBlueprint()
						|| !blueprint.HasPart("TinkerItem")) continue;
					string cost = XRL.World.Parts.TinkerItem.GetBitCostFor(pair.Key);
					if (string.IsNullOrEmpty(cost)) continue;
					var worth = new KingdomBitTally();
					for (int i = 0; i < cost.Length; i++)
						if (KingdomMaterialRules.TryBitTier(cost[i], out int read)) worth.Add(read, 1);
					if (worth.Get(Tier) > 0) return pair.Key;
				}
				return null;
			}

			private void StoreHighCraft(GameObject Unit)
			{
				string id = Unit.ID;
				Require(!string.IsNullOrEmpty(id) && Unit.IDIfAssigned == id
					&& ReferenceEquals(ChainStore.Inventory.AddObject(Unit, null, Silent: true,
						NoStack: true), Unit)
					&& ReferenceEquals(Unit.InInventory, ChainStore) && Unit.CurrentCell == null,
					"synthetic high-craft unit did not retain exact custody");
				ChainHighCraft.Add(Unit);
			}

			private string DescribeHighCraft(KingdomMaterials.MaterialStock Stock,
				KingdomBitTally Bits, KingdomExoticTally Exotics)
			{
				var text = new StringBuilder();
				text.Append("bill=").Append(KingdomScenarioRules.Bounded(ChainSupplyClaim))
					.Append("; wanted-bits=").Append(KingdomScenarioRules.Bounded(Bits.Describe()))
					.Append("; wanted-exotics=").Append(KingdomScenarioRules.Bounded(Exotics.Describe()))
					.Append("; held-bits=").Append(KingdomScenarioRules.Bounded(Stock.Bits.Describe()))
					.Append("; held-exotics=").Append(KingdomScenarioRules.Bounded(Stock.Exotics.Describe()))
					.Append("; minted=").Append(ChainHighCraft.Count)
					.Append("; stores=").Append(Stock.Stockpiles.Count)
					.Append("; covers-bits=").Append(KingdomMaterialRules.CoversBits(Stock.Bits, Bits))
					.Append("; covers-exotics=").Append(
						KingdomMaterialRules.CoversExotics(Stock.Exotics, Exotics))
					.Append("; synthetic-high-craft=true");
				return text.ToString();
			}
		}
	}
}
