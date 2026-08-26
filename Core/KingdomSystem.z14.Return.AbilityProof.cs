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
		private sealed class CharterAbilityObservation
		{
			public string FullHash;
			public string StableHash;
			public string TargetTemplateHash;
			public string State;
			public bool Recoverable;
		}

		private static bool TryObserveCharterAbility(out CharterAbilityObservation Observation)
		{
			Observation = null;
			string full = InspectCharterAbility(out bool valid, out bool recoverable);
			if (full == null) return false;
			if (The.Player == null)
			{
				Observation = new CharterAbilityObservation
				{
					FullHash = full, StableHash = "player-absent",
					TargetTemplateHash = null, State = "player-absent", Recoverable = true
				};
				return true;
			}
			if (!TryHashCharterInvariant(out string stable, out string targetTemplate,
				out bool exactTargetOwner)) return false;
			valid = valid && exactTargetOwner && targetTemplate != null;
			string state = valid ? "valid" : CharterAbilityRemoved() ? "removed" :
				recoverable ? "recoverable" : "invalid";
			Observation = new CharterAbilityObservation
			{
				FullHash = full, StableHash = stable, TargetTemplateHash = targetTemplate,
				State = state, Recoverable = recoverable
			};
			return true;
		}

		private static bool TryHashCharterInvariant(out string StableHash,
			out string TargetTemplateHash, out bool ExactTargetOwner)
		{
			StableHash = null; TargetTemplateHash = null; ExactTargetOwner = false;
			GameObject player = The.Player;
			if (player == null) { StableHash = "player-absent"; ExactTargetOwner = true; return true; }
			try
			{
				ActivatedAbilities activated = player.ActivatedAbilities;
				Dictionary<Guid, ActivatedAbilityEntry> map = activated?.AbilityByGuid;
				List<CommandCooldown> cooldowns = activated?.Cooldowns;
				if (player.PartsList == null || player.PartsList.Count > 4096 || map == null ||
					map.Count > 4096 || cooldowns == null || cooldowns.Count > 4096) return false;
				using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
				using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream,
					new System.Text.UTF8Encoding(false, true), true))
				{
					writer.Write(0x54414932); // TAI2
					List<object> referenceTopology = new List<object>();
					WriteReferenceTopologyProof(writer, player, referenceTopology);
					WriteReferenceTopologyProof(writer, player.PartsList, referenceTopology);
					WriteProofString(writer, player.IDIfAssigned);
					int otherParts = 0;
					for (int i = 0; i < player.PartsList.Count; i++)
						if (player.PartsList[i] == null ||
							player.PartsList[i].GetType().Name != "KingdomCharterPart") otherParts++;
					writer.Write(otherParts);
					for (int i = 0; i < player.PartsList.Count; i++)
					{
						IPart part = player.PartsList[i];
						if (part != null && part.GetType().Name == "KingdomCharterPart") continue;
						WriteReferenceTopologyProof(writer, part, referenceTopology);
						WriteReferenceTopologyProof(writer, part?.ParentObject, referenceTopology);
						WriteProofString(writer, part?.GetType().FullName);
					}
					WriteReferenceTopologyProof(writer, activated, referenceTopology);
					WriteReferenceTopologyProof(writer, map, referenceTopology);
					WriteReferenceTopologyProof(writer, cooldowns, referenceTopology);
					writer.Write(activated.Silent);
					int otherEntries = 0;
					foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in map)
						if (row.Value == null || row.Value.Command != KingdomCharterPart.COMMAND)
							otherEntries++;
					writer.Write(otherEntries);
					ActivatedAbilityEntry target = null;
					Guid targetId = Guid.Empty;
					int targetCount = 0;
					foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in map)
					{
						if (row.Value != null && row.Value.Command == KingdomCharterPart.COMMAND)
						{
							targetCount++;
							if (targetCount == 1) { target = row.Value; targetId = row.Key; }
							else { target = null; targetId = Guid.Empty; }
							continue;
						}
						writer.Write(row.Key.ToByteArray());
						WriteReferenceTopologyProof(writer, row.Value, referenceTopology);
						WriteReferenceTopologyProof(writer, row.Value?.Abilities,
							referenceTopology);
						WriteReferenceTopologyProof(writer, row.Value?.CommandCooldown,
							referenceTopology);
						WriteReferenceTopologyProof(writer, row.Value?.UITileDefault,
							referenceTopology);
						WriteReferenceTopologyProof(writer, row.Value?.UITileToggleOn,
							referenceTopology);
						WriteReferenceTopologyProof(writer, row.Value?.UITileDisabled,
							referenceTopology);
						WriteReferenceTopologyProof(writer, row.Value?.UITileCoolingDown,
							referenceTopology);
						writer.Write(row.Value == null ? (byte)0 :
							ReferenceEquals(row.Value.Abilities, activated) ? (byte)1 : (byte)2);
						WriteActivatedAbilityProof(writer, row.Value);
					}
					writer.Write(cooldowns.Count);
					for (int i = 0; i < cooldowns.Count; i++)
					{
						CommandCooldown cooldown = cooldowns[i];
						WriteReferenceTopologyProof(writer, cooldown, referenceTopology);
						writer.Write(cooldown == null ? (byte)0 : (byte)1);
						if (cooldown != null)
						{
							WriteProofString(writer, cooldown.Command);
							writer.Write(cooldown.Segments); writer.Write(cooldown.Token);
						}
					}
					if (!FinishProofHash(stream, writer, out StableHash)) return false;
					if (targetCount == 1 && target != null)
					{
						ExactTargetOwner = targetId != Guid.Empty && target.ID == targetId &&
							ReferenceEquals(target.Abilities, activated);
						using (System.IO.MemoryStream targetStream = new System.IO.MemoryStream())
						using (System.IO.BinaryWriter targetWriter = new System.IO.BinaryWriter(targetStream,
							new System.Text.UTF8Encoding(false, true), true))
						{
							targetWriter.Write(0x54415432); // TAT2
							WriteReferenceTopologyProof(targetWriter, target, referenceTopology);
							WriteReferenceTopologyProof(targetWriter, target.Abilities,
								referenceTopology);
							WriteReferenceTopologyProof(targetWriter, target.CommandCooldown,
								referenceTopology);
							WriteReferenceTopologyProof(targetWriter, target.UITileDefault,
								referenceTopology);
							WriteReferenceTopologyProof(targetWriter, target.UITileToggleOn,
								referenceTopology);
							WriteReferenceTopologyProof(targetWriter, target.UITileDisabled,
								referenceTopology);
							WriteReferenceTopologyProof(targetWriter, target.UITileCoolingDown,
								referenceTopology);
							WriteActivatedAbilityTemplateProof(targetWriter, target);
							if (!FinishProofHash(targetStream, targetWriter,
								out TargetTemplateHash)) return false;
						}
					}
					else ExactTargetOwner = targetCount == 0;
					return true;
				}
			}
			catch { return false; }
		}

	}
}
