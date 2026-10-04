using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The engine side of the bit mint's choice: the factory read as declared blueprints, in the
	/// factory's own order, with production's own vocabulary, and the five-rung form's READ-ONLY
	/// preflight at chain setup. Nothing here creates a body.
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		/// <summary>The scrap tag production reads beside its own material tag
		/// (Growth/KingdomMaterials.03.StockClassification.cs, TryMaterialOf): vanilla writes it as
		/// a semantic tag, <c>&lt;stag Name="Scrap" /&gt;</c>, which the factory files as
		/// "Semantic" + name (decompile XRL/World/GameObjectFactory.cs, LoadBakedXML).</summary>
		internal const string HighCraftScrapTag = "SemanticScrap";

		/// <summary>Every factory blueprint in the factory's own order (its Blueprints dictionary
		/// is filled base files first, then mods, first-seen), read for declared data only.</summary>
		private static IEnumerable<KingdomCampHeartHighCraftRules.IBlueprint> FactoryBlueprints()
		{
			foreach (var pair in GameObjectFactory.Factory.Blueprints)
				yield return pair.Value == null ? null : new FactoryBlueprint(pair.Key, pair.Value);
		}

		private static KingdomCampHeartHighCraftRules.Vocabulary HighCraftVocabulary()
		{
			var exotics = new List<string>();
			foreach (string[] row in KingdomMaterials.ExoticBlueprints) exotics.AddRange(row);
			return new KingdomCampHeartHighCraftRules.Vocabulary(KingdomMaterials.MaterialTag,
				KingdomMaterials.ExoticTag, HighCraftScrapTag, KingdomMaterials.MaterialBlueprints, exotics);
		}

		/// <summary>The engine's realised bit cost for a candidate. Asked only of a candidate, so
		/// never of a part with no Bits, for which GetBitCostFor logs an error and invents "1".</summary>
		private static KingdomBitTally RealisedBitWorth(KingdomCampHeartHighCraftRules.IBlueprint Candidate)
		{
			string cost = XRL.World.Parts.TinkerItem.GetBitCostFor(Candidate.Key);
			if (string.IsNullOrEmpty(cost)) return null;
			var worth = new KingdomBitTally();
			for (int i = 0; i < cost.Length; i++)
				if (KingdomMaterialRules.TryBitTier(cost[i], out int tier)) worth.Add(tier, 1);
			return worth;
		}

		/// <summary>A factory blueprint as the engine-free rules read it.</summary>
		private sealed class FactoryBlueprint : KingdomCampHeartHighCraftRules.IBlueprint
		{
			private readonly GameObjectBlueprint Blueprint;

			internal FactoryBlueprint(string Key, GameObjectBlueprint Blueprint)
			{
				this.Key = Key;
				this.Blueprint = Blueprint;
			}

			public string Key { get; }
			public string Name => Blueprint.Name;
			public bool HasPart(string Part) => Blueprint.HasPart(Part);
			public bool? Flag(string Part, string Parameter) =>
				Blueprint.TryGetPartParameter<bool>(Part, Parameter, out bool value) ? value : (bool?)null;
			public string Text(string Part, string Parameter) =>
				Blueprint.GetPartParameter<string>(Part, Parameter);
			public bool Marked(string Name) => Blueprint.Tags.ContainsKey(Name)
				|| Blueprint.Props.ContainsKey(Name) || Blueprint.IntProps.ContainsKey(Name);
			public bool DescendsFrom(string Root) => Blueprint.DescendsFrom(Root);
		}

		private sealed partial class Frame
		{
			private string ChainHighCraftPreflight;

			/// <summary>READ-ONLY, at chain setup, for the five-rung form only: whether every tier the
			/// arcology's Bits name resolves, within the scan cap, to a candidate by its DECLARED bit
			/// tiers, and whether the two exotic blueprints exist. It creates nothing and asks the
			/// engine for no realised cost, so a factory that cannot supply the bill refuses here, in
			/// minutes, rather than after the 1->4 prefix. A rare realised-cost downgrade or a body a
			/// random modification fills can still skip a candidate at supply; the count beside each
			/// tier is the margin the walk has left.</summary>
			private void PreflightChainHighCraft()
			{
				string arcology = KingdomCampHeartChainRules.SuccessorKey(5);
				var bits = KingdomMaterials.BitCostFor(arcology);
				Require(!bits.IsEmpty() && !KingdomMaterials.ExoticCostFor(arcology).IsEmpty(),
					"taf-camp-rung5-highcraft-absent: the arcology declares no bits or exotics");
				var vocabulary = HighCraftVocabulary();
				var notes = new List<string>();
				bool resolved = true;
				for (int tier = 0; tier < KingdomMaterialRules.BitTierCount; tier++)
				{
					if (bits.Get(tier) <= 0) continue;
					var first = KingdomCampHeartHighCraftRules.Resolve(FactoryBlueprints(), tier, null,
						vocabulary, KingdomCampHeartHighCraftRules.DeclaredWorth);
					int count = KingdomCampHeartHighCraftRules.CountWorth(FactoryBlueprints(), tier,
						vocabulary, KingdomCampHeartHighCraftRules.DeclaredWorth);
					notes.Add("t" + tier + "=" + (first == null ? "none" : first.Key) + "(" + count + ")");
					resolved &= first != null;
				}
				bool exotic = GameObjectFactory.Factory.Blueprints.ContainsKey(IngotBlueprint)
					&& GameObjectFactory.Factory.Blueprints.ContainsKey(GemBlueprint);
				ChainHighCraftPreflight = "highcraft-preflight=" + string.Join(",", notes)
					+ "; exotic-blueprints=" + exotic;
				Require(resolved && exotic, "taf-camp-rung5-highcraft-infeasible: " + ChainHighCraftPreflight);
			}
		}
	}
}
