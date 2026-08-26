using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private const string AbilityEffectPrefix = "ability-v2";

		private static string AbilityEffect(CharterAbilityObservation Observation)
		{
			return Observation == null ? null : AbilityEffectPrefix + "|" +
				(Observation.FullHash ?? "-") + "|" + (Observation.StableHash ?? "-") + "|" +
				(Observation.TargetTemplateHash ?? "-") + "|" + (Observation.State ?? "-");
		}

		private static string AbilityIntent(string StableHash, string TargetTemplateHash,
			string TargetState)
		{
			return AbilityEffectPrefix + "|-|" + (StableHash ?? "-") + "|" +
				(TargetTemplateHash ?? "-") + "|" + (TargetState ?? "-");
		}

		private static bool TryParseAbilityEffect(string Value, out string FullHash,
			out string StableHash, out string TargetTemplateHash, out string State)
		{
			FullHash = null; StableHash = null; TargetTemplateHash = null; State = null;
			if (Value == null || Value.Length > 512) return false;
			string[] fields = Value.Split('|');
			if (fields.Length != 5 || fields[0] != AbilityEffectPrefix) return false;
			FullHash = fields[1] == "-" ? null : fields[1];
			StableHash = fields[2] == "-" ? null : fields[2];
			TargetTemplateHash = fields[3] == "-" ? null : fields[3];
			State = fields[4];
			return (FullHash == null || FullHash == "player-absent" ||
				ValidProofHash(FullHash)) &&
				(StableHash == "player-absent" || ValidProofHash(StableHash)) &&
				(TargetTemplateHash == null || ValidProofHash(TargetTemplateHash)) &&
				(State == "player-absent" || State == "valid" || State == "removed" ||
				 State == "recoverable" || State == "invalid");
		}

		private sealed class CharterReferenceSnapshot
		{
			public GameObject Player;
			public PartRack Parts;
			public ActivatedAbilities Abilities;
			public Dictionary<Guid, ActivatedAbilityEntry> Map;
			public List<CommandCooldown> Cooldowns;
			public KingdomCharterPart Part;
			public string StableHash;
			public List<IPart> OtherParts = new List<IPart>();
			public List<GameObject> OtherPartOwners = new List<GameObject>();
			public List<Guid> OtherIds = new List<Guid>();
			public List<ActivatedAbilityEntry> OtherEntries =
				new List<ActivatedAbilityEntry>();
			public List<ActivatedAbilities> OtherOwners = new List<ActivatedAbilities>();
			public List<CommandCooldown> OtherEntryCooldowns = new List<CommandCooldown>();
			public List<ConsoleLib.Console.Renderable> OtherTileDefaults =
				new List<ConsoleLib.Console.Renderable>();
			public List<ConsoleLib.Console.Renderable> OtherTileToggleOns =
				new List<ConsoleLib.Console.Renderable>();
			public List<ConsoleLib.Console.Renderable> OtherTileDisabled =
				new List<ConsoleLib.Console.Renderable>();
			public List<ConsoleLib.Console.Renderable> OtherTileCoolingDown =
				new List<ConsoleLib.Console.Renderable>();
			public List<CommandCooldown> CooldownRows = new List<CommandCooldown>();
		}

		private static bool TryCaptureCharterReferences(out CharterReferenceSnapshot Snapshot)
		{
			Snapshot = new CharterReferenceSnapshot { Player = The.Player };
			if (The.Player == null) return true;
			Snapshot.Parts = The.Player.PartsList;
			Snapshot.Abilities = The.Player.ActivatedAbilities;
			Snapshot.Map = Snapshot.Abilities?.AbilityByGuid;
			Snapshot.Cooldowns = Snapshot.Abilities?.Cooldowns;
			if (Snapshot.Parts == null || Snapshot.Parts.Count > 4096 || Snapshot.Map == null ||
				Snapshot.Map.Count > 4096 || Snapshot.Cooldowns == null ||
				Snapshot.Cooldowns.Count > 4096 ||
				!TryHashCharterInvariant(out Snapshot.StableHash,
					out string ignoredTarget, out bool ignoredOwner)) return false;
			for (int i = 0; i < Snapshot.Parts.Count; i++)
			{
				IPart part = Snapshot.Parts[i];
				if (part != null && part.GetType().Name == "KingdomCharterPart")
				{
					if (Snapshot.Part != null || !(part is KingdomCharterPart typed)) return false;
					Snapshot.Part = typed;
				}
				else
				{
					Snapshot.OtherParts.Add(part);
					Snapshot.OtherPartOwners.Add(part?.ParentObject);
				}
			}
			foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in Snapshot.Map)
				if (row.Value == null || row.Value.Command != KingdomCharterPart.COMMAND)
				{
					Snapshot.OtherIds.Add(row.Key); Snapshot.OtherEntries.Add(row.Value);
					Snapshot.OtherOwners.Add(row.Value?.Abilities);
					Snapshot.OtherEntryCooldowns.Add(row.Value?.CommandCooldown);
					Snapshot.OtherTileDefaults.Add(row.Value?.UITileDefault);
					Snapshot.OtherTileToggleOns.Add(row.Value?.UITileToggleOn);
					Snapshot.OtherTileDisabled.Add(row.Value?.UITileDisabled);
					Snapshot.OtherTileCoolingDown.Add(row.Value?.UITileCoolingDown);
				}
			for (int i = 0; i < Snapshot.Cooldowns.Count; i++)
				Snapshot.CooldownRows.Add(Snapshot.Cooldowns[i]);
			return true;
		}

		private static bool CharterReferencesStillMatch(CharterReferenceSnapshot Snapshot,
			bool AllowPartCreation)
		{
			if (Snapshot == null || !ReferenceEquals(The.Player, Snapshot.Player)) return false;
			if (Snapshot.Player == null) return true;
			if (!ReferenceEquals(The.Player.PartsList, Snapshot.Parts) ||
				!ReferenceEquals(The.Player.ActivatedAbilities, Snapshot.Abilities) ||
				!ReferenceEquals(Snapshot.Abilities?.AbilityByGuid, Snapshot.Map) ||
				!ReferenceEquals(Snapshot.Abilities?.Cooldowns, Snapshot.Cooldowns)) return false;
			if (!TryHashCharterInvariant(out string stableHash, out string ignoredTarget,
				out bool ignoredOwner) || stableHash != Snapshot.StableHash) return false;
			KingdomCharterPart currentPart = null;
			int otherPartIndex = 0;
			for (int i = 0; i < The.Player.PartsList.Count; i++)
			{
				IPart part = The.Player.PartsList[i];
				if (part != null && part.GetType().Name == "KingdomCharterPart")
				{
					if (currentPart != null || !(part is KingdomCharterPart typed)) return false;
					currentPart = typed;
				}
				else
				{
					if (otherPartIndex >= Snapshot.OtherParts.Count ||
						!ReferenceEquals(part, Snapshot.OtherParts[otherPartIndex]) ||
						!ReferenceEquals(part?.ParentObject,
							Snapshot.OtherPartOwners[otherPartIndex])) return false;
					otherPartIndex++;
				}
			}
			if (otherPartIndex != Snapshot.OtherParts.Count) return false;
			if (Snapshot.Part != null ? !ReferenceEquals(Snapshot.Part, currentPart) :
				(!AllowPartCreation && currentPart != null)) return false;
			int otherCount = 0;
			foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in Snapshot.Map)
				if (row.Value == null || row.Value.Command != KingdomCharterPart.COMMAND)
				{
					if (otherCount >= Snapshot.OtherEntries.Count ||
						row.Key != Snapshot.OtherIds[otherCount] ||
						!ReferenceEquals(row.Value, Snapshot.OtherEntries[otherCount]) ||
						!ReferenceEquals(row.Value?.Abilities, Snapshot.OtherOwners[otherCount]) ||
						!ReferenceEquals(row.Value?.CommandCooldown,
							Snapshot.OtherEntryCooldowns[otherCount]) ||
						!ReferenceEquals(row.Value?.UITileDefault,
							Snapshot.OtherTileDefaults[otherCount]) ||
						!ReferenceEquals(row.Value?.UITileToggleOn,
							Snapshot.OtherTileToggleOns[otherCount]) ||
						!ReferenceEquals(row.Value?.UITileDisabled,
							Snapshot.OtherTileDisabled[otherCount]) ||
						!ReferenceEquals(row.Value?.UITileCoolingDown,
							Snapshot.OtherTileCoolingDown[otherCount])) return false;
					otherCount++;
				}
			if (otherCount != Snapshot.OtherEntries.Count ||
				Snapshot.Cooldowns.Count != Snapshot.CooldownRows.Count) return false;
			for (int i = 0; i < Snapshot.Cooldowns.Count; i++)
				if (!ReferenceEquals(Snapshot.Cooldowns[i], Snapshot.CooldownRows[i])) return false;
			return true;
		}

	}
}
