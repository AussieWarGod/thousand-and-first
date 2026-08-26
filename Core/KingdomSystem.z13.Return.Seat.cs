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
		private bool DispatchReturnSeat(Zone Site, KingdomRealmArchive Archive,
			out string Refusal)
		{
			Refusal = "";
			string before = SeatEffect(City?.SettlementId, Away?.City?.SettlementId);
			bool shouldSwap = Site != null && Archive.Away != null &&
				Archive.Away.ClaimedZones != null && Archive.Away.ClaimedZones.Contains(Site.ZoneID) &&
				(Archive.Seat.ClaimedZones == null || !Archive.Seat.ClaimedZones.Contains(Site.ZoneID));
			string after = shouldSwap
				? SeatEffect(Archive.Away.City?.SettlementId, Archive.Seat.City?.SettlementId)
				: SeatEffect(Archive.Seat.City?.SettlementId, Archive.Away?.City?.SettlementId);
			KingdomRealmCallbackReceipt receipt = Archive.ReturnSeat;
			if (receipt.Phase != KingdomRealmCallbackPhase.None)
			{
				before = receipt.BeforeEffect; after = receipt.AfterEffect;
				shouldSwap = !string.Equals(before, after, StringComparison.Ordinal);
			}
			string current = SeatEffect(City?.SettlementId, Away?.City?.SettlementId);
			if (receipt.Phase == KingdomRealmCallbackPhase.Settled)
				return current == after && SettledCallbackStillMatches(Archive, receipt,
					current, out Refusal);
			if (!PrepareReturnCallback(Archive, receipt, KingdomRealmCallbackScope.Seat,
				before, after,
				out bool invokeAuthorized, out Refusal)) return false;
			current = SeatEffect(City?.SettlementId, Away?.City?.SettlementId);
			if (current == after)
			{
				if (!Archive.CurrentGraphMatchesAfterSeat(this, shouldSwap, out string failure))
					return QuarantineReturn(Archive, failure ?? "seat poststate differs from intent",
						out Refusal);
				return SettleReturnCallback(Archive, receipt, shouldSwap
					? KingdomRealmCallbackDisposition.Delivered
					: KingdomRealmCallbackDisposition.Skipped, current, out Refusal, shouldSwap);
			}
			string beforeFailure = null;
			if (current != before || !Archive.CurrentGraphMatchesAfterSeat(this, false,
				out beforeFailure))
				return QuarantineReturn(Archive, beforeFailure ??
					"seat callback reached a third topology", out Refusal);
			if (!shouldSwap)
				return SettleReturnCallback(Archive, receipt,
					KingdomRealmCallbackDisposition.Skipped, current, out Refusal);
			if (!invokeAuthorized)
				return QuarantineReturn(Archive,
					"seat callback was interrupted before exact topology publication", out Refusal);
			TrySeat(Site);
			current = SeatEffect(City?.SettlementId, Away?.City?.SettlementId);
			string afterFailure = null;
			if (current != after || !Archive.CurrentGraphMatchesAfterSeat(this, true,
				out afterFailure))
				return QuarantineReturn(Archive, afterFailure ??
					"seat callback did not publish exact frozen topology", out Refusal);
			return SettleReturnCallback(Archive, receipt,
				KingdomRealmCallbackDisposition.Delivered, current, out Refusal,
				SeatSwapped: true);
		}

		private static string SeatEffect(string SeatId, string AwayId)
		{
			return (SeatId ?? "-") + "|" + (AwayId ?? "-");
		}

		private bool DispatchReturnAbility(KingdomRealmArchive Archive,
			out string Refusal)
		{
			Refusal = "";
			if (!TryObserveCharterAbility(out CharterAbilityObservation observation))
				return QuarantineReturn(Archive, "charter return graph cannot be bounded",
					out Refusal);
			KingdomRealmCallbackReceipt receipt = Archive.ReturnAbility;
			string restoreTemplate = observation.TargetTemplateHash;
			if (receipt.Phase == KingdomRealmCallbackPhase.None &&
				observation.State != "player-absent" && restoreTemplate == null)
			{
				if (!TryParseAbilityEffect(Archive.ExileAbility?.BeforeEffect,
					out string ignoredExileFull, out string ignoredExileStable,
					out restoreTemplate, out string ignoredExileState) || restoreTemplate == null)
					return QuarantineReturn(Archive,
						"charter return lacks frozen exact target template", out Refusal);
			}
			string before = receipt.Phase == KingdomRealmCallbackPhase.None
				? AbilityEffect(observation) : receipt.BeforeEffect;
			string after = receipt.Phase == KingdomRealmCallbackPhase.None
				? AbilityIntent(observation.StableHash, restoreTemplate,
					observation.State == "player-absent" ? "player-absent" : "valid")
				: receipt.AfterEffect;
			if (!TryParseAbilityEffect(before, out string beforeFull, out string frozenStable,
				out string beforeTemplate, out string beforeState) ||
				!TryParseAbilityEffect(after, out string ignoredFull, out string expectedStable,
					out string expectedTemplate, out string expectedState) ||
				frozenStable != expectedStable ||
				(expectedState != "valid" && expectedState != "player-absent") ||
				(expectedState == "valid" && expectedTemplate == null))
				return QuarantineReturn(Archive, "charter return intent is malformed", out Refusal);
			if (receipt.Phase == KingdomRealmCallbackPhase.Settled)
				return observation.State == expectedState &&
					observation.StableHash == frozenStable &&
					(expectedState != "valid" ||
					 observation.TargetTemplateHash == expectedTemplate) &&
					SettledCallbackStillMatches(Archive, receipt,
						AbilityEffect(observation), out Refusal);
			if (!PrepareReturnCallback(Archive, receipt, KingdomRealmCallbackScope.Ability,
				before, after,
				out bool invokeAuthorized, out Refusal)) return false;
			if (!TryObserveCharterAbility(out observation) ||
				observation.StableHash != frozenStable)
				return QuarantineReturn(Archive,
					"charter return changed unaffected ability or part graph", out Refusal);
			string current = AbilityEffect(observation);
			if (observation.State == expectedState &&
				(expectedState != "valid" || observation.TargetTemplateHash == expectedTemplate))
				return SettleReturnCallback(Archive, receipt,
					current == before ? KingdomRealmCallbackDisposition.Skipped :
					KingdomRealmCallbackDisposition.Delivered, current, out Refusal);
			if (!observation.Recoverable || current != before ||
				observation.State != beforeState || observation.FullHash != beforeFull ||
				observation.TargetTemplateHash != beforeTemplate)
				return QuarantineReturn(Archive,
					"charter callback reached duplicate or foreign ability state", out Refusal);
			if (!invokeAuthorized)
				return QuarantineReturn(Archive,
					"charter callback was interrupted before exact poststate publication", out Refusal);
			if (!Archive.CurrentGraphMatches(this, out string failure))
				return QuarantineReturn(Archive, failure, out Refusal);
			if (!TryCaptureCharterReferences(out CharterReferenceSnapshot charterReferences))
				return QuarantineReturn(Archive, "charter reference graph is unbounded", out Refusal);
			The.Player.RequirePart<KingdomCharterPart>().EnsureAbility();
			if (!TryObserveCharterAbility(out observation) ||
				!CharterReferencesStillMatch(charterReferences, AllowPartCreation: true) ||
				observation.StableHash != frozenStable || observation.State != expectedState ||
				observation.TargetTemplateHash != expectedTemplate)
				return QuarantineReturn(Archive,
					"charter callback did not settle exact target-only graph", out Refusal);
			return SettleReturnCallback(Archive, receipt,
				KingdomRealmCallbackDisposition.Delivered,
				AbilityEffect(observation), out Refusal);
		}

		private static string InspectCharterAbility(out bool Valid, out bool Recoverable)
		{
			Valid = false;
			Recoverable = false;
			GameObject player = The.Player;
			if (player == null) return "player-absent";
			int partCount = 0;
			KingdomCharterPart exactPart = null;
			for (int i = 0; i < player.PartsList.Count; i++)
			{
				IPart part = player.PartsList[i];
				if (part != null && part.GetType().Name == "KingdomCharterPart")
				{
					partCount++;
					if (part is KingdomCharterPart typed) exactPart = typed;
				}
			}
			int commandCount = 0;
			Guid commandId = Guid.Empty;
			System.Collections.Generic.Dictionary<Guid, ActivatedAbilityEntry> abilities =
				player.ActivatedAbilities?.AbilityByGuid;
			if (abilities != null)
			{
				foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in abilities)
					if (row.Value != null && row.Value.Command == KingdomCharterPart.COMMAND)
					{
						commandCount++;
						commandId = row.Key;
					}
			}
			Guid pointer = exactPart == null ? Guid.Empty : exactPart.ActivatedAbilityID;
			Valid = partCount == 1 && exactPart != null &&
				ReferenceEquals(exactPart.ParentObject, player) && commandCount == 1 &&
				commandId != Guid.Empty && pointer == commandId;
			Recoverable = partCount <= 1 && (partCount == 0 || exactPart != null) &&
				commandCount <= 1 && (pointer == Guid.Empty ||
					(commandId != Guid.Empty && pointer == commandId));
			try
			{
				using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
				using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream,
					new System.Text.UTF8Encoding(false, true), true))
				{
					writer.Write(0x54414331); // TAC1
					WriteProofString(writer, player.IDIfAssigned);
					if (player.PartsList.Count > 4096) return null;
					writer.Write(player.PartsList.Count);
					for (int i = 0; i < player.PartsList.Count; i++)
					{
						IPart part = player.PartsList[i];
						WriteProofString(writer, part?.GetType().FullName);
						if (part != null && part.GetType().Name == "KingdomCharterPart")
							writer.Write((part as KingdomCharterPart)?.ActivatedAbilityID.ToByteArray()
								?? Guid.Empty.ToByteArray());
					}
					writer.Write(partCount); writer.Write(commandCount);
					writer.Write(pointer.ToByteArray()); writer.Write(commandId.ToByteArray());
					ActivatedAbilities activated = player.ActivatedAbilities;
					writer.Write(activated == null ? (byte)0 : (byte)1);
					if (activated != null)
					{
						writer.Write(activated.Silent);
						Dictionary<Guid, ActivatedAbilityEntry> map = activated.AbilityByGuid;
						if (map == null || map.Count > 4096) return null;
						writer.Write(map.Count);
						foreach (KeyValuePair<Guid, ActivatedAbilityEntry> row in map)
						{
							writer.Write(row.Key.ToByteArray());
							writer.Write(row.Value == null ? (byte)0 :
								ReferenceEquals(row.Value.Abilities, activated) ? (byte)1 : (byte)2);
							WriteActivatedAbilityProof(writer, row.Value);
						}
						List<CommandCooldown> cooldowns = activated.Cooldowns;
						if (cooldowns == null || cooldowns.Count > 4096) return null;
						writer.Write(cooldowns.Count);
						for (int i = 0; i < cooldowns.Count; i++)
						{
							CommandCooldown cooldown = cooldowns[i];
							writer.Write(cooldown == null ? (byte)0 : (byte)1);
							if (cooldown != null)
							{
								WriteProofString(writer, cooldown.Command);
								writer.Write(cooldown.Segments); writer.Write(cooldown.Token);
							}
						}
					}
					return FinishProofHash(stream, writer, out string hash) ? hash : null;
				}
			}
			catch { return null; }
		}

	}
}
