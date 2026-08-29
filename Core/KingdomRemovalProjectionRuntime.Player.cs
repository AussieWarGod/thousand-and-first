using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomRemovalProjectionRuntime
	{
		private const int MaxTerminalPlayerObjects = 20000;
		private const string PlayerCompletionId = "taf:removal-complete:player:v1";

		internal static bool TryInspectPlayer(out List<string> Rows, out string Failure)
		{
			return TryInspectPlayerCore(true, out Rows, out Failure);
		}

		internal static bool PlayerProjectionAbsent(out string Failure)
		{
			return TryInspectPlayerCore(false, out List<string> rows, out Failure)
				&& rows.Count == 0;
		}

		private static bool TryInspectPlayerCore(bool RequireCharter,
			out List<string> Rows, out string Failure)
		{
			Rows = new List<string>(); Failure = null;
			GameObject player = The.Player;
			if (player == null) return Fail("the player body is absent", out Failure);
			int charterCount = 0;
			for (int i = 0; i < (player.PartsList?.Count ?? 0); i++)
			{
				IPart part = player.PartsList[i];
				string name = part?.GetType().Name;
				if (!KingdomRemovalCoverage.IsCustomPart(name)) continue;
				if (name != "KingdomCharterPart")
					return Fail("a non-Charter custom part remains on the player: " + name,
						out Failure);
				charterCount++; Rows.Add("part\u001f" + name);
			}
			Dictionary<Guid, ActivatedAbilityEntry> abilities =
				player.ActivatedAbilities?.AbilityByGuid;
			int commandCount = 0;
			if (abilities != null) foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in abilities)
					if (Contains(KingdomRemovalCoverage.AbilityCommands, row.Value?.Command))
					{
						if (row.Value.Command != KingdomCharterPart.COMMAND)
							return Fail("a non-Charter custom command remains on the player: "
								+ row.Value.Command, out Failure);
						commandCount++;
						Rows.Add("ability\u001f" + row.Key.ToString("D") + "\u001f" + row.Value.Command);
					}
			if (RequireCharter ? charterCount != 1 || commandCount != 1
				: charterCount != 0 || commandCount != 0)
				return Fail(RequireCharter
					? "terminal player projection requires one exact Charter part and command"
					: "terminal player projection still has a Charter part or command", out Failure);
			if (!TryInspectPlayerObjectGraph(player, RequireCharter, out Failure)) return false;
			if (RequireCharter)
			{
				if (!TryInspectPlayerCutCandidate(player,
					out List<KingdomCharterPart> charters, out List<Guid> ids, out Failure))
					return false;
				if (charters.Count != 1 || ids.Count != 1)
					return Fail("the terminal Charter cut is incomplete or substituted", out Failure);
				if (charters[0].ActivatedAbilityID == Guid.Empty
					|| charters[0].ActivatedAbilityID != ids[0])
					return Fail("the Charter part and command do not share exact identity", out Failure);
			}
			Rows.Sort(StringComparer.Ordinal);
			return true;
		}

		internal static bool TryAuthenticatePlayerCutProgress(KingdomRemovalRecord Receipt,
			out bool Absent, out string Failure)
		{
			Absent = false; Failure = null;
			if (Receipt == null || Receipt.Kind != KingdomRemovalProjectionKind.Ability
				|| Receipt.Id != PlayerCompletionId
				|| Receipt.Disposition != KingdomRemovalDisposition.TerminalIntent
				|| Receipt.Amount != 2L
				|| !KingdomRealmRetirementRules.Digest(Receipt.BeforeDigest))
				return Fail("the frozen player terminal receipt is absent or malformed", out Failure);
			if (TryInspectPlayerCore(false, out List<string> terminal, out string _)
				&& terminal.Count == 0)
			{
				Absent = true;
				return KingdomRealmRemovalRetryRules.AuthenticatedPlayerTerminalProgress(
					Receipt.Amount, false, false, false)
					|| Fail("native player absence is outside the frozen terminal bound", out Failure);
			}
			GameObject player = The.Player;
			if (!TryInspectPlayerCutCandidate(player, out List<KingdomCharterPart> charters,
				out List<Guid> ids, out Failure)) return false;
			bool partPresent = charters.Count == 1;
			bool abilityPresent = ids.Count == 1;
			if (!partPresent)
				return Fail("a Charter command remains without its frozen part authority", out Failure);
			Guid frozenId = charters[0].ActivatedAbilityID;
			if (frozenId == Guid.Empty || (abilityPresent && ids[0] != frozenId))
				return Fail("the remaining Charter cut has foreign or changed identity", out Failure);
			List<string> fullPair = new List<string>
			{
				"part\u001fKingdomCharterPart",
				"ability\u001f" + frozenId.ToString("D") + "\u001f" + KingdomCharterPart.COMMAND
			};
			fullPair.Sort(StringComparer.Ordinal);
			bool digestMatches = Receipt.BeforeDigest == KingdomRetirementDigestRules.Evidence(
				"removal-preview-player", fullPair);
			return KingdomRealmRemovalRetryRules.AuthenticatedPlayerTerminalProgress(
				Receipt.Amount, partPresent, abilityPresent, digestMatches)
				|| Fail("the remaining Charter cut is not an authenticated frozen suffix", out Failure);
		}

		internal static bool TryRemovePlayerProjection(KingdomRemovalRecord Receipt,
			out int Removed, out string Failure)
		{
			Removed = 0; Failure = null;
			if (!TryAuthenticatePlayerCutProgress(Receipt, out bool absent, out Failure))
				return false;
			if (absent) return true;
			GameObject player = The.Player;
			if (!TryInspectPlayerCutCandidate(player, out List<KingdomCharterPart> charters,
				out List<Guid> ids, out Failure)) return false;
			Dictionary<Guid, ActivatedAbilityEntry> abilities =
				player.ActivatedAbilities?.AbilityByGuid;
			string callback = null;
			try
			{
				for (int i = 0; i < ids.Count; i++)
				{
					if (!player.ActivatedAbilities.RemoveAbility(ids[i]))
						throw new InvalidOperationException("frozen Charter ability disappeared");
					Removed++;
				}
				for (int i = charters.Count - 1; i >= 0; i--)
				{
					KingdomCharterPart charter = charters[i];
					player.RemovePart(charter);
					if (charter.ParentObject != null)
						throw new InvalidOperationException("Charter part remained attached");
					Removed++;
				}
			}
			catch (Exception ex) { callback = ex.Message; }
			if (TryAuthenticatePlayerCutProgress(Receipt, out absent, out Failure) && absent)
				return true;
			return Fail((callback == null ? "player Charter projection remains"
				: "terminal player callback failed before absence: " + callback)
				+ (string.IsNullOrEmpty(Failure) ? "" : "; " + Failure), out Failure);
		}

		private static bool TryInspectPlayerCutCandidate(GameObject Player,
			out List<KingdomCharterPart> Charters, out List<Guid> AbilityIds,
			out string Failure)
		{
			Charters = new List<KingdomCharterPart>(); AbilityIds = new List<Guid>();
			Failure = null;
			if (Player == null || !TryInspectPlayerObjectGraph(Player, true, out Failure))
				return false;
			for (int i = 0; i < (Player.PartsList?.Count ?? 0); i++)
				if (Player.PartsList[i] is KingdomCharterPart charter) Charters.Add(charter);
			Dictionary<Guid, ActivatedAbilityEntry> abilities =
				Player.ActivatedAbilities?.AbilityByGuid;
			if (abilities != null) foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in abilities)
				if (Contains(KingdomRemovalCoverage.AbilityCommands, row.Value?.Command))
				{
					if (row.Value.Command != KingdomCharterPart.COMMAND)
						return Fail("a foreign custom command entered the Charter cut", out Failure);
					if (row.Key == Guid.Empty || row.Value.ID != row.Key
						|| !ReferenceEquals(row.Value.Abilities, Player.ActivatedAbilities))
						return Fail("the Charter command key, entry, and owner topology diverged",
							out Failure);
					AbilityIds.Add(row.Key);
				}
			if (Charters.Count > 1 || AbilityIds.Count > 1
				|| Charters.Count + AbilityIds.Count == 0)
				return Fail("terminal player cut is absent, duplicated, or substituted", out Failure);
			return true;
		}

		private static bool TryInspectPlayerObjectGraph(GameObject Player, bool PermitCharter,
			out string Failure)
		{
			Failure = null;
			Queue<GameObject> pending = new Queue<GameObject>();
			HashSet<GameObject> seen = new HashSet<GameObject>();
			pending.Enqueue(Player);
			while (pending.Count > 0)
			{
				GameObject item = pending.Dequeue();
				if (item == null || !GameObject.Validate(item))
					return Fail("the loaded player object graph contains an invalid object", out Failure);
				if (!seen.Add(item)) continue;
				if (seen.Count > MaxTerminalPlayerObjects)
					return Fail("the loaded player object graph exceeds its terminal scan cap",
						out Failure);
				if (!ReferenceEquals(item, Player)
					&& KingdomRemovalCoverage.IsOwnedBlueprint(item.Blueprint))
					return Fail("a TAF blueprint remains in loaded player custody: "
						+ item.Blueprint, out Failure);
				for (int i = 0; i < (item.PartsList?.Count ?? 0); i++)
				{
					IPart part = item.PartsList[i];
					string name = part?.GetType().Name;
					if (PermitCharter && ReferenceEquals(item, Player)
						&& name == "KingdomCharterPart") continue;
					if (name == "r_KingdomFounderKnowledge"
						|| name == "r_KingdomLabEffectLedger"
						|| name?.IndexOf("Registry", StringComparison.Ordinal) >= 0)
						return Fail("value-bearing player carrier requires native conversion before removal: "
							+ name, out Failure);
					if (KingdomRemovalCoverage.IsCustomPart(name)
						|| (!string.IsNullOrEmpty(name) && name.StartsWith("r_Kingdom",
							StringComparison.Ordinal)))
						return Fail("a custom part remains in loaded player custody: " + name,
							out Failure);
				}
				if (HasOwnedPlayerProperty(item))
					return Fail("a TAF object property remains in loaded player custody",
						out Failure);
				if (!TryInspectCampfire(item, out List<string> campfire, out Failure)) return false;
				if (campfire.Count > 0)
					return Fail("a value-bearing realm dish remains in player custody; place its campfire on tracked ground for native conversion",
						out Failure);
				List<GameObject> children = item.GetInventoryAndEquipment();
				for (int i = 0; i < children.Count; i++) pending.Enqueue(children[i]);
			}
			return true;
		}

		private static bool HasOwnedPlayerProperty(GameObject Item)
		{
			if (Item?.Property != null) foreach (string key in Item.Property.Keys)
				if (KingdomRemovalCoverage.IsOwnedObjectProperty(key)) return true;
			if (Item?.IntProperty != null) foreach (string key in Item.IntProperty.Keys)
				if (KingdomRemovalCoverage.IsOwnedObjectProperty(key)) return true;
			return false;
		}

		private static bool Contains(string[] Values, string Value)
		{
			for (int i = 0; i < Values.Length; i++) if (Values[i] == Value) return true;
			return false;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
