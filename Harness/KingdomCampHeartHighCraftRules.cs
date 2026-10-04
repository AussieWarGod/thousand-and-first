using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Engine-free decisions of the arcology's synthetic bit mint, held once so the native chain
	/// and the licensed walk of the installed corpus (DevTests/KingdomCampHeartHighCraftCorpusTests.cs)
	/// make the SAME choice, and both test projects execute the loop against fakes.
	/// <para>
	/// A blueprint is a candidate only when one fresh body of it is ordinary portable salvage that
	/// production could hold, count and spend as bits (<see cref="Refusal"/>). The body actually
	/// created is then judged by production's and the engine's own readers
	/// (<see cref="BodyRefusal"/>) and kept only when production's own stock tally rises by exactly
	/// its worth (<see cref="CountedExactly"/>). <see cref="Fill"/> mints only while that tally
	/// still leaves a tier short, so a body that already covers a later tier ends the mint early.
	/// </para>
	/// </summary>
	internal static class KingdomCampHeartHighCraftRules
	{
		internal const int ScanCap = 4096;
		internal const int MintCap = 64;
		internal const int SkipCap = 32;

		/// <summary>What the mint reads of one factory blueprint: declared data only, never a body.</summary>
		internal interface IBlueprint
		{
			/// <summary>The factory key a body is created by.</summary>
			string Key { get; }
			/// <summary>The blueprint's own name; differs from the key only for a compatibility alias.</summary>
			string Name { get; }
			bool HasPart(string Part);
			/// <summary>A declared boolean part parameter, or null when it is not declared.</summary>
			bool? Flag(string Part, string Parameter);
			/// <summary>A declared text part parameter, or null when it is not declared.</summary>
			string Text(string Part, string Parameter);
			/// <summary>Whether a live tag, string property or int property of this name is declared.</summary>
			bool Marked(string Name);
			/// <summary>Whether the inheritance chain passes through this blueprint.</summary>
			bool DescendsFrom(string Root);
		}

		/// <summary>Production's own material and exotic vocabulary
		/// (Growth/KingdomMaterials.01.Declarations.cs, read by TryMaterialOf and TryExoticOf in
		/// Growth/KingdomMaterials.03.StockClassification.cs), handed in by the caller because those
		/// tables live on an engine-touching class.</summary>
		internal sealed class Vocabulary
		{
			internal readonly string MaterialTag, ExoticTag, ScrapTag;
			internal readonly HashSet<string> Materials = new HashSet<string>(StringComparer.Ordinal);
			internal readonly HashSet<string> Exotics = new HashSet<string>(StringComparer.Ordinal);

			internal Vocabulary(string MaterialTag, string ExoticTag, string ScrapTag,
				IEnumerable<string> Materials, IEnumerable<string> Exotics)
			{
				this.MaterialTag = MaterialTag;
				this.ExoticTag = ExoticTag;
				this.ScrapTag = ScrapTag;
				foreach (string name in Materials ?? new string[0]) this.Materials.Add(name);
				foreach (string name in Exotics ?? new string[0]) this.Exotics.Add(name);
			}
		}

		/// <summary>What production's and the engine's own readers say of one created body.</summary>
		internal sealed class BodyReading
		{
			/// <summary>The factory returned one fresh, unplaced body of exactly the asked blueprint.</summary>
			internal bool Exact;
			internal bool Takeable, Important, Empty, Natural, Creature, AlwaysStack, Material, Exotic;
			/// <summary>KingdomMaterials.UnitBits of the body.</summary>
			internal KingdomBitTally Unit;
		}

		/// <summary>The mint's record: kept bodies as tier:key, skipped keys, and why each was skipped.</summary>
		internal sealed class Ledger
		{
			internal readonly List<string> Minted = new List<string>();
			internal readonly List<string> Skipped = new List<string>();
			internal readonly List<string> Notes = new List<string>();
		}

		/// <summary>Creates, judges and stores one body for a tier. Answers null with the stored
		/// body's unit worth, or the reason the body was discarded unstored.</summary>
		internal delegate string Offer(string Key, int Tier, out KingdomBitTally Unit);

		/// <summary>
		/// Why no fresh body of this blueprint can be bit stock, or null for a candidate. Each
		/// refusal names an engine or production fact: a base blueprint is abstract
		/// (GameObjectBlueprint.IsBaseBlueprint); UnitBits reads only a TinkerItem that can be
		/// disassembled, and TinkerItem.GetBitCostFor logs an error and invents "1" for a part with no
		/// Bits (decompile XRL/World/Parts/TinkerItem.cs:133-158); a substitute or alias creates or
		/// prices another blueprint; Inventory.AddObject refuses an untakeable body
		/// (XRL/World/Parts/Inventory.cs:256-267); only Body, EnergyCellSocket, Inventory and
		/// MagazineAmmoLoader report custody contents, and production never counts or spends a body
		/// that holds another (KingdomConstructionInputLeaseAuthority.CanUseMaterial ->
		/// KingdomOrdinaryCustody.TryProveEmpty); important, quest and always-stack bodies are refused
		/// by production's own stock and routed-input readers; production claims materials and
		/// exotics before bits; a vessel destroyed rather than obliterated pours its liquid into its
		/// holder's cell (XRL/World/Parts/LiquidVolume.cs:3808-3824), which here is the heart's own
		/// ground. Furniture, creatures and natural equipment are not salvage a founder carries in,
		/// so the mint keeps to bodies descended from the engine's own Item root.
		/// </summary>
		internal static string Refusal(IBlueprint B, Vocabulary V)
		{
			if (B == null || V == null) return "unreadable";
			if (B.Marked("BaseObject")) return "abstract";
			if (!B.HasPart("TinkerItem")) return "no-tinker";
			if (B.Flag("TinkerItem", "CanDisassemble") == false) return "no-disassembly";
			if (string.IsNullOrEmpty(B.Text("TinkerItem", "Bits"))) return "no-bits";
			if (!string.Equals(B.Key, B.Name, StringComparison.Ordinal)
				|| !string.IsNullOrEmpty(B.Text("TinkerItem", "SubstituteBlueprint"))
				|| B.Marked("CreateSubstituteBlueprint")) return "substitute";
			if (B.Flag("Physics", "Takeable") == false) return "untakeable";
			if (!B.DescendsFrom("Item")) return "not-item";
			if (B.Marked("Creature") || B.HasPart("Brain") || B.HasPart("Body")) return "creature";
			if (B.Marked("Natural") || B.Marked("NaturalGear")) return "natural";
			if (B.HasPart("Inventory") || B.HasPart("EnergyCellSocket")
				|| B.HasPart("MagazineAmmoLoader")) return "holder";
			if (B.HasPart("LiquidVolume")) return "vessel";
			if (B.Marked("Important") || B.Marked("QuestItem")) return "important";
			if (B.Marked("AlwaysStack")) return "always-stack";
			if (B.Marked(V.MaterialTag) || B.Marked(V.ScrapTag) || V.Materials.Contains(B.Name))
				return "material";
			if (B.Marked(V.ExoticTag) || V.Exotics.Contains(B.Name)) return "exotic";
			return null;
		}

		/// <summary>The first candidate in factory order, within the scan cap, worth a bit of this
		/// tier. Worth is asked only of a candidate, never of a refused blueprint.</summary>
		internal static IBlueprint Resolve(IEnumerable<IBlueprint> Factory, int Tier,
			ICollection<string> Skipped, Vocabulary V, Func<IBlueprint, KingdomBitTally> Worth)
		{
			if (Factory == null || Worth == null) return null;
			int scanned = 0;
			foreach (IBlueprint candidate in Factory)
			{
				if (++scanned > ScanCap) break;
				if (candidate == null || (Skipped != null && Skipped.Contains(candidate.Key))
					|| Refusal(candidate, V) != null) continue;
				KingdomBitTally worth = Worth(candidate);
				if (worth != null && worth.Get(Tier) > 0) return candidate;
			}
			return null;
		}

		/// <summary>How many candidates within the scan cap are worth a bit of this tier.</summary>
		internal static int CountWorth(IEnumerable<IBlueprint> Factory, int Tier, Vocabulary V,
			Func<IBlueprint, KingdomBitTally> Worth)
		{
			if (Factory == null || Worth == null) return 0;
			int scanned = 0, found = 0;
			foreach (IBlueprint candidate in Factory)
			{
				if (++scanned > ScanCap) break;
				if (candidate == null || Refusal(candidate, V) != null) continue;
				KingdomBitTally worth = Worth(candidate);
				if (worth != null && worth.Get(Tier) > 0) found++;
			}
			return found;
		}

		/// <summary>A blueprint's DECLARED bit tiers (the template GetBitCostFor realises), with no
		/// engine call: what the read-only preflight and the licensed corpus walk resolve by.</summary>
		internal static KingdomBitTally DeclaredWorth(IBlueprint B)
		{
			return B != null && KingdomMaterialRules.TryParseBitCost(B.Text("TinkerItem", "Bits"),
				out KingdomBitTally worth, out string _) ? worth : null;
		}

		/// <summary>The lowest tier production's own coverage predicate still finds short, or -1
		/// exactly when KingdomMaterialRules.CoversBits holds.</summary>
		internal static int FirstShortTier(KingdomBitTally Held, KingdomBitTally Wanted)
		{
			KingdomBitTally missing = KingdomMaterialRules.MissingBits(Held, Wanted);
			for (int tier = 0; tier < KingdomMaterialRules.BitTierCount; tier++)
				if (missing.Get(tier) > 0) return tier;
			return -1;
		}

		/// <summary>Why a created body must be discarded, in production's own order of reading a
		/// stored body (CanUseMaterial, then material, exotic, bits), or null to store it.</summary>
		internal static string BodyRefusal(BodyReading R, int Tier)
		{
			if (R == null || !R.Exact) return "inexact";
			if (!R.Takeable) return "untakeable";
			if (R.Important) return "important";
			if (!R.Empty) return "holds";
			if (R.Natural) return "natural";
			if (R.Creature) return "creature";
			if (R.AlwaysStack) return "always-stack";
			if (R.Material) return "material";
			if (R.Exotic) return "exotic";
			if (R.Unit == null || R.Unit.Get(Tier) <= 0) return "worthless";
			return null;
		}

		/// <summary>Whether production's stock tally rose by exactly one body's unit worth in every
		/// tier: the stored body is counted, once, and nothing else moved.</summary>
		internal static bool CountedExactly(KingdomBitTally Before, KingdomBitTally After,
			KingdomBitTally Unit)
		{
			if (Before == null || After == null || Unit == null || Unit.IsEmpty()) return false;
			for (int tier = 0; tier < KingdomMaterialRules.BitTierCount; tier++)
				if ((long)After.Get(tier) - Before.Get(tier) != Unit.Get(tier)) return false;
			return true;
		}

		/// <summary>
		/// Mints until production's own stock reading covers the wanted bits. Returns null once
		/// covered, else the refusal. Every pass re-reads the stock, so a tier an earlier body
		/// already covers is never minted for; a discarded body skips its blueprint; a stored body
		/// production does not count exactly is retracted and its blueprint skipped. Mints and skips
		/// are both capped, so the loop always ends.
		/// </summary>
		internal static string Fill(KingdomBitTally Wanted, Func<KingdomBitTally> ReadHeld,
			Func<int, ICollection<string>, string> Resolve, Offer Mint, Action Retract, Ledger Book)
		{
			if (Wanted == null || ReadHeld == null || Resolve == null || Mint == null
				|| Retract == null || Book == null) return "taf-camp-rung5-bits-unwired";
			for (;;)
			{
				KingdomBitTally held = ReadHeld();
				if (held == null)
					return "taf-camp-rung5-bits-unreadable: production's stock read had no exact authority";
				int tier = FirstShortTier(held, Wanted);
				if (tier < 0) return null;
				if (Book.Minted.Count >= MintCap)
					return "taf-camp-rung5-bits-unbounded: the bit mint exceeded its cap";
				string key = Resolve(tier, Book.Skipped);
				if (string.IsNullOrEmpty(key))
					return "taf-camp-rung5-bit-tier-unsourced: no scanned blueprint is worth a tier-"
						+ tier + " bit";
				string reason = Mint(key, tier, out KingdomBitTally unit);
				if (reason == null)
				{
					if (CountedExactly(held, ReadHeld(), unit))
					{
						Book.Minted.Add(tier + ":" + key);
						continue;
					}
					Retract();
					reason = "uncounted";
				}
				if (Book.Skipped.Count >= SkipCap)
					return "taf-camp-rung5-bits-unsourced: too many candidate bodies were not bit stock";
				Book.Skipped.Add(key);
				Book.Notes.Add(key + "(" + reason + ")");
			}
		}
	}
}
