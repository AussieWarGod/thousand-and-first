using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomGuestLifecycle
	{
		internal static bool FreezeLifecycleLodgeSource(KingdomSystem system, GameObject guest,
			KingdomCityBook expectedBook, int residentId, KingdomLifecycleOperation op)
		{
			if (system == null || !GameObject.Validate(guest) || expectedBook == null || op == null
				|| guest.IDIfAssigned != op.ObjectId || residentId <= 0
				|| !TryUniqueResident(system, op.SettlementId, residentId,
					out KingdomCityBook book, out KingdomResidentRow row)
				|| !ReferenceEquals(book, expectedBook) || row.Standing != KingdomResidentStanding.Resident
				|| !KingdomResidents.TryLocate(system, guest, out KingdomCityBook bound, out int boundId)
				|| !ReferenceEquals(bound, book) || boundId != residentId) return false;
			return KingdomLifecycleRules.TryFreezeLodgeResident(system.LifecycleBook, op, residentId,
				row.Name, row.Origin, row.Arrived, row.ArrivedTick, row.BoundZoneId);
		}

		internal static void ObserveLodgeTargetDeath(GameObject body)
		{
			try
			{
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				KingdomLifecycleOperation op = Open(system, KingdomLifecycleLane.NotableGuest);
				string objectId = body?.IDIfAssigned;
				string zoneId = body?.CurrentZone?.ZoneID;
				long tick = The.Game == null ? 0L : The.Game.TimeTicks;
				if (op == null || string.IsNullOrEmpty(objectId) || string.IsNullOrEmpty(zoneId)
					|| objectId != op.ObjectId || body.Blueprint != op.Blueprint
					|| zoneId != op.ZoneId) return;
				int residentId = Simulation.City.KingdomResidents.IdOf(body);
				if (residentId > 0)
				{
					if (TryUniqueResident(system, op.SettlementId, residentId, out _,
						out KingdomResidentRow row))
						KingdomLifecycleRules.TryFreezeLodgeResident(system.LifecycleBook, op,
							residentId, row.Name, row.Origin, row.Arrived, row.ArrivedTick,
							row.BoundZoneId);
					return;
				}
				KingdomLifecycleRules.TryObserveLodgeBodyDeath(system.LifecycleBook, op,
					objectId, body.Blueprint, zoneId, tick);
			}
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst Lodge death receipt", error);
			}
		}

		private static bool TrySettleDeadLodge(KingdomSystem system, KingdomLifecycleBook book,
			KingdomLifecycleOperation op)
		{
			KingdomLifecycleLodgeTerminalReceipt receipt = op?.LodgeTerminal;
			if (receipt == null || !TryResolveMarketSource(system, op,
				out GameObject marketSource, out r_KingdomMarketHandoffSourceProjection marketMarker)
				|| marketMarker != null && !KingdomGrowth.TrySealCompletedDeadHandoffOutcome(
					system, op, marketSource, marketMarker)) return false;
			receipt = op.LodgeTerminal;
			long tick = The.Game == null ? op.UpdatedTick : The.Game.TimeTicks;
			if (tick < op.UpdatedTick) return false;
			int matches = 0; int residentId = 0; string name = null; string origin = null;
			string arrived = null; long arrivedTick = 0L; string boundZone = null;
			byte standing = 0; byte cause = 0;
			if (receipt.ResidentId > 0)
			{
				residentId = receipt.ResidentId;
				if (TryUniqueResident(system, op.SettlementId, residentId, out _,
					out KingdomResidentRow row))
				{
					matches = 1; name = row.Name; origin = row.Origin; arrived = row.Arrived;
					arrivedTick = row.ArrivedTick; boundZone = row.BoundZoneId;
					standing = (byte)row.Standing; cause = (byte)row.Cause;
				}
			}
			if (!KingdomLifecycleRules.TryBeginLodgeAbandon(book, op, matches, residentId,
				name, origin, arrived, arrivedTick, boundZone, standing, cause, tick)) return false;
			long current = CurrentSchedule(system, op);
			KingdomLifecycleMutationAction action = KingdomLifecycleRules
				.LodgeAbandonScheduleAction(book, op, current);
			if (action == KingdomLifecycleMutationAction.InvokeOnce)
			{
				if (!KingdomLifecycleRules.BeginLodgeAbandonSchedule(book, op, current)) return false;
				SetSchedule(system, op, op.DueAfter); current = CurrentSchedule(system, op);
				action = KingdomLifecycleRules.LodgeAbandonScheduleAction(book, op, current);
			}
			if (action == KingdomLifecycleMutationAction.ConfirmAfter
				&& !KingdomLifecycleRules.CommitLodgeAbandonSchedule(book, op, current)) return false;
			if (KingdomLifecycleRules.LodgeAbandonScheduleAction(book, op, current)
				!= KingdomLifecycleMutationAction.Settled) return false;
			return KingdomLifecycleRules.TryCommitLodgeAbandon(book, op, tick);
		}

		private static bool TryUniqueResident(KingdomSystem system, string settlementId,
			int residentId, out KingdomCityBook foundBook, out KingdomResidentRow found)
		{
			foundBook = null; found = default(KingdomResidentRow); int matches = 0;
			List<KingdomCityBook> books = system?.OwnedCityBooks();
			for (int i = 0; books != null && i < books.Count; i++)
			{
				KingdomCityBook book = books[i];
				if (book?.SettlementId != settlementId
					|| !KingdomResidents.TryResident(book, residentId, out KingdomResidentRow row))
					continue;
				foundBook = book; found = row; matches++;
			}
			return matches == 1;
		}

		private static bool PrepareMarketTerminalClose(KingdomSystem system, Zone zone,
			KingdomLifecycleOperation op, out GameObject source,
			out r_KingdomMarketHandoffSourceProjection marker)
		{
			if (zone == null || !TryResolveMarketSource(system, op, out source, out marker))
				return false;
			if (marker == null) return op.LodgeTerminal?.MarketSourcePrepared
				!= KingdomLifecycleLodgeTerminalReceipt.MarketPrepared;
			return (marker.Exact(system, source) || marker.ExactTerminal(system, source))
				&& marker.LifecyclePlanHash == op.PlanHash
				&& marker.LifecycleSequence == op.Sequence
				&& marker.TargetBodyObjectId == op.ObjectId
				&& marker.TargetResidentId == op.LodgeTerminal.ResidentId
				&& marker.SourceBodyObjectId == op.LodgeTerminal.MarketSourceBodyObjectId
				&& marker.SourceResidentId == op.LodgeTerminal.MarketSourceResidentId
				&& marker.Tier == op.LodgeTerminal.MarketTier
				&& marker.Intent == op.LodgeTerminal.MarketIntent
				&& (marker.LifecycleTerminalClosed == 0 && marker.TargetTerminalDead == 0
					|| marker.LifecycleTerminalClosed == 1
						&& (marker.TargetTerminalDead == 0 || marker.TargetTerminalDead == 1));
		}

		private static bool CommitMarketTerminalClose(GameObject source,
			r_KingdomMarketHandoffSourceProjection marker)
		{
			if (marker == null) return true;
			if (marker.LifecycleTerminalClosed == 0) marker.LifecycleTerminalClosed = 1;
			return source?.GetPart<r_KingdomMarketHandoffSourceProjection>() == marker
				&& marker.LifecycleTerminalClosed == 1;
		}

		private static bool TryResolveMarketSource(KingdomSystem system,
			KingdomLifecycleOperation op, out GameObject source,
			out r_KingdomMarketHandoffSourceProjection marker)
		{
			source = null; marker = null;
			KingdomLifecycleLodgeTerminalReceipt receipt = op?.LodgeTerminal;
			if (system == null || receipt == null
				|| !KingdomMarketHandoffGlobalIndex.TryLoaded(out IList<GameObject> loaded)
				|| !KingdomMarketHandoffGraphAuthority.TryPreflight(system, loaded,
					op.SettlementId, out _)
				|| !TryLocateMarketSource(op, receipt, loaded, out source, out marker)) return false;
			if (marker == null) return receipt.MarketSourcePrepared
				!= KingdomLifecycleLodgeTerminalReceipt.MarketPrepared;
			bool exact = marker.LifecycleOperationId == op.Id
				&& marker.LifecyclePlanHash == op.PlanHash && marker.LifecycleSequence == op.Sequence
				&& marker.TargetBodyObjectId == op.ObjectId
				&& marker.TargetResidentId == receipt.ResidentId
				&& (marker.Exact(system, source) || marker.ExactTerminal(system, source));
			if (!exact) return false;
			if (receipt.MarketSourcePrepared == KingdomLifecycleLodgeTerminalReceipt.MarketNone
				&& !KingdomLifecycleRules.TryFreezeLodgeMarketSource(system.LifecycleBook, op,
					marker.SourceBodyObjectId, marker.SourceResidentId, marker.Tier, marker.Intent))
				return false;
			receipt = op.LodgeTerminal;
			return receipt.MarketSourcePrepared != KingdomLifecycleLodgeTerminalReceipt.MarketNone
				&& receipt.MarketSourceBodyObjectId == marker.SourceBodyObjectId
				&& receipt.MarketSourceResidentId == marker.SourceResidentId
				&& receipt.MarketTier == marker.Tier && receipt.MarketIntent == marker.Intent;
		}

		private static bool TryLocateMarketSource(KingdomLifecycleOperation op,
			KingdomLifecycleLodgeTerminalReceipt receipt, IList<GameObject> loaded,
			out GameObject source,
			out r_KingdomMarketHandoffSourceProjection marker)
		{
			source = null; marker = null;
			int identities = 0; int targetIdentities = 0; int receipts = 0;
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject candidate = loaded[i];
				try
				{
					r_KingdomMarketHandoffSourceProjection held = candidate
						.GetPart<r_KingdomMarketHandoffSourceProjection>();
					if (receipt.MarketSourcePrepared
						!= KingdomLifecycleLodgeTerminalReceipt.MarketNone
						&& candidate.IDIfAssigned == receipt.MarketSourceBodyObjectId) identities++;
					if (candidate.IDIfAssigned == op.ObjectId) targetIdentities++;
					if (held?.LifecycleOperationId == op.Id)
					{
						receipts++; source = candidate; marker = held;
					}
				}
				catch { return false; }
			}
			if (receipts > 1 || identities > 1 || targetIdentities > 1) return false;
			if (receipt.MarketSourcePrepared == KingdomLifecycleLodgeTerminalReceipt.MarketNone)
				return receipts <= 1;
			if (receipt.MarketSourcePrepared == KingdomLifecycleLodgeTerminalReceipt.MarketPrepared)
				return receipts == 1 && identities == 1
					&& source.IDIfAssigned == receipt.MarketSourceBodyObjectId;
			return receipts == 0 || receipts == 1 && identities == 1
				&& source.IDIfAssigned == receipt.MarketSourceBodyObjectId;
		}
	}
}
