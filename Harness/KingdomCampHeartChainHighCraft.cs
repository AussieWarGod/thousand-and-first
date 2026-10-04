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
	/// SYNTHETIC, DISCLOSED. Nothing is hand-valued. The bit mint's whole decision procedure is the
	/// engine-free <see cref="KingdomCampHeartHighCraftRules.Fill"/>: it re-reads production's own
	/// stock (<c>KingdomMaterials.Stock</c>) before every body and mints only for a tier that
	/// reading still finds short; a candidate is ordinary portable salvage chosen in factory order
	/// (<see cref="KingdomCampHeartHighCraftRules.Refusal"/>); every created body is judged by
	/// production's and the engine's own readers before it is stored, and kept only when the
	/// stock reading rises by exactly its worth. A refused body is discarded and its blueprint
	/// skipped, never assumed; mints and skips are bounded and journalled.
	/// </para>
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		// Vanilla bodies production already classifies as exotics
		// (Growth/KingdomMaterials.01.Declarations.cs:125-131). Named, not derived, because the
		// authored bill names the KIND and the blueprint table is the production mapping.
		internal const string IngotBlueprint = "Bronze Ingot";
		internal const string GemBlueprint = "Gemstone";

		private sealed partial class Frame
		{
			private readonly List<GameObject> ChainHighCraft = new List<GameObject>();
			private KingdomCampHeartHighCraftRules.Ledger ChainHighCraftLedger;
			private string ChainHighCraftReport;

			/// <summary>Fills the supplemental store until production's own composite coverage
			/// predicates stop refusing. Called only for the fifth rung.</summary>
			private void SupplyChainHighCraft()
			{
				ChainHighCraft.Clear();
				ChainHighCraftLedger = new KingdomCampHeartHighCraftRules.Ledger();
				var exotics = KingdomMaterials.ExoticCostFor(ChainTo);
				var bits = KingdomMaterials.BitCostFor(ChainTo);
				Require(!exotics.IsEmpty() && !bits.IsEmpty(),
					"taf-camp-rung5-highcraft-absent: the arcology declares no bits or exotics");
				MintExotic(IngotBlueprint, KingdomExotic.Ingot, exotics.Get(KingdomExotic.Ingot));
				MintExotic(GemBlueprint, KingdomExotic.Gem, exotics.Get(KingdomExotic.Gem));
				string refused = KingdomCampHeartHighCraftRules.Fill(bits, ReadChainBits, ResolveChainBit,
					OfferChainBit, RetractChainBit, ChainHighCraftLedger);
				var stock = KingdomMaterials.Stock(Zone);
				// Production's own bit tally once the mint is done: the payment check compares the
				// bits the paid job reports lost against how far this reading falls.
				ChainBitsBefore = stock.Bits.Copy();
				ChainHighCraftReport = DescribeHighCraft(stock, bits, exotics, refused);
				bool covered = refused == null && KingdomMaterialRules.CoversExotics(stock.Exotics, exotics)
					&& KingdomMaterialRules.CoversBits(stock.Bits, bits);
				Require(KingdomScenarioJournal.Append("camp-heart-chain-exotics", covered,
					ChainHighCraftReport) == null, "high-craft supply journal unavailable");
				Require(refused == null, refused + "; " + ChainHighCraftReport);
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

			/// <summary>Production's own bit reading of this ground, or null when the read has no
			/// exact routed-input authority (it would then tally nothing and read as short). Each
			/// read takes a fresh survey, so a body stored since the last read is in custody; a
			/// bound pass would hand back its older survey, so one refuses the read by name.</summary>
			private KingdomBitTally ReadChainBits()
			{
				Require(!KingdomSurvey.HasBoundPass, "high-craft stock read found an outstanding survey");
				var stock = KingdomMaterials.Stock(Zone);
				return stock.InputLeaseAuthorityExact ? stock.Bits.Copy() : null;
			}

			/// <summary>The supply-time choice: the same factory walk as the setup preflight, but
			/// worth is asked of the engine's realised bit cost (TinkerItem.GetBitCostFor), which a
			/// world seed can rarely lower by one tier (BitType.ToRealBits); the walk then moves on.</summary>
			private string ResolveChainBit(int Tier, ICollection<string> Skipped)
			{
				return KingdomCampHeartHighCraftRules.Resolve(FactoryBlueprints(), Tier, Skipped,
					HighCraftVocabulary(), RealisedBitWorth)?.Key;
			}

			/// <summary>One body of a candidate, judged by production's and the engine's own readers
			/// in production's order of reading a stored body, and stored only when nothing refuses
			/// it. The factory is asked directly so a substituted or placed body is discarded rather
			/// than refusing the run.</summary>
			private string OfferChainBit(string Key, int Tier, out KingdomBitTally Unit)
			{
				Unit = null;
				GameObject body = GameObject.Create(Key);
				if (!GameObject.Validate(body)) return "inexact";
				Owned.Add(body);
				body.SetIntProperty("NoLoot", 1);
				var reading = new KingdomCampHeartHighCraftRules.BodyReading
				{
					Exact = body.Blueprint == Key && body.Count == 1 && body.CurrentCell == null
						&& body.InInventory == null,
					Takeable = body.IsTakeable(),
					Important = body.IsImportant(),
					Empty = KingdomOrdinaryCustody.TryProveEmpty(body, out _),
					Natural = body.IsNatural(),
					Creature = body.IsCreature,
					AlwaysStack = body.HasTag("AlwaysStack"),
					Material = KingdomMaterials.TryOrdinaryMaterialOf(body, out _),
					Exotic = KingdomMaterials.TryExoticOf(body, out _),
					Unit = KingdomMaterials.UnitBits(body)
				};
				string refusal = KingdomCampHeartHighCraftRules.BodyRefusal(reading, Tier);
				if (refusal != null)
				{
					DiscardHighCraft(body);
					return refusal;
				}
				StoreHighCraft(body);
				Unit = reading.Unit;
				return null;
			}

			/// <summary>Takes back the body just stored when production did not count it exactly.</summary>
			private void RetractChainBit()
			{
				Require(ChainHighCraft.Count > 0, "no stored high-craft body to retract");
				GameObject body = ChainHighCraft[ChainHighCraft.Count - 1];
				ChainHighCraft.RemoveAt(ChainHighCraft.Count - 1);
				DiscardHighCraft(body);
				Require(!ChainStore.Inventory.Objects.Contains(body),
					"retracted high-craft body is still in the supplemental store");
			}

			private void DiscardHighCraft(GameObject Body)
			{
				Owned.Remove(Body);
				Body.Obliterate(null, Silent: true);
				Require(!GameObject.Validate(Body), "skipped high-craft candidate was not discarded");
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
				KingdomBitTally Bits, KingdomExoticTally Exotics, string Refused)
			{
				var book = ChainHighCraftLedger;
				var text = new StringBuilder();
				text.Append("bill=").Append(KingdomScenarioRules.Bounded(ChainSupplyClaim))
					.Append("; wanted-bits=").Append(KingdomScenarioRules.Bounded(Bits.Describe()))
					.Append("; wanted-exotics=").Append(KingdomScenarioRules.Bounded(Exotics.Describe()))
					.Append("; held-bits=").Append(KingdomScenarioRules.Bounded(Stock.Bits.Describe()))
					.Append("; held-exotics=").Append(KingdomScenarioRules.Bounded(Stock.Exotics.Describe()))
					.Append("; minted=").Append(ChainHighCraft.Count)
					.Append("; bit-bodies=").Append(KingdomScenarioRules.Bounded(string.Join(",", book.Minted)))
					.Append("; skipped=").Append(KingdomScenarioRules.Bounded(string.Join(",", book.Notes)))
					.Append("; refused=").Append(KingdomScenarioRules.Bounded(Refused ?? "none"))
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
