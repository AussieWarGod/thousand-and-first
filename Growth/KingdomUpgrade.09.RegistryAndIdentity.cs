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

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
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

	}
}
