using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomGuestLifecycle
	{
		internal static bool Drive(KingdomSystem system, Zone zone, KingdomLifecycleLane lane)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || zone == null) return false;
			for (int guard = 0; guard < 32; guard++)
			{
				KingdomLifecycleOperation op = lane == KingdomLifecycleLane.PlainGuest
					? book.PlainGuest : book.NotableGuest;
				if (op == null) return true;
				if (op.Phase == KingdomLifecyclePhase.Quarantined) return false;
				try
				{
					if (!SettlePhase(system, zone, book, op)) return false;
					if (op.Phase == KingdomLifecyclePhase.Terminal)
					{
						bool abandoned = KingdomLifecycleRules.LodgeAbandoned(op);
						GameObject marketSource = null;
						r_KingdomMarketHandoffSourceProjection marketMarker = null;
						if (abandoned && !PrepareMarketTerminalClose(system, zone, op,
							out marketSource, out marketMarker)) return false;
						if (abandoned)
						{
							if (!KingdomLifecycleRules.TryReleaseAbandonedLodge(book, op,
								The.Game.TimeTicks)
								|| !CommitMarketTerminalClose(marketSource, marketMarker)) return false;
							if (marketMarker != null && marketMarker.TargetTerminalDead != 1) return true;
							if (!KingdomLifecycleRules.TryRemoveReleasedLodge(book, op,
								The.Game.TimeTicks)) return false;
						}
						else if (!KingdomLifecycleRules.Retire(book, op, The.Game.TimeTicks))
							return false;
						return true;
					}
					KingdomLifecyclePhase next = Next(op);
					if (next == KingdomLifecyclePhase.Invalid
						|| !KingdomLifecycleRules.AdvancePhase(book, op, next, The.Game.TimeTicks))
						return false;
				}
				catch (Exception error)
				{
					MetricsManager.LogError("ThousandAndFirst guest lifecycle", error);
					return false;
				}
			}
			return false;
		}

		private static bool SettlePhase(KingdomSystem system, Zone zone,
			KingdomLifecycleBook book, KingdomLifecycleOperation op)
		{
			switch (op.Phase)
			{
			case KingdomLifecyclePhase.ProjectionIntent:
				return SettleProjection(system, zone, book, op);
			case KingdomLifecyclePhase.WaterIntent:
				return SettleWater(system, zone, book, op);
			case KingdomLifecyclePhase.RemovalIntent:
				return SettleRemoval(system, zone, book, op);
			case KingdomLifecyclePhase.DomainIntent:
				return SettleDomain(system, zone, book, op);
			case KingdomLifecyclePhase.Sinks:
				return SettleSinks(system, book, op);
			case KingdomLifecyclePhase.ScheduleIntent:
				return SettleSchedule(system, zone, book, op);
			default:
				return true;
			}
		}

		private static bool SettleProjection(KingdomSystem system, Zone zone,
			KingdomLifecycleBook book, KingdomLifecycleOperation op)
		{
			KingdomLifecycleProjection projection = op.Projections.Count == 1
				? op.Projections[0] : null;
			if (projection == null) return false;
			GameObject existing = FindExact(projection.ObjectId);
			bool exact = ExactProjection(existing, projection);
			if (projection.State == KingdomLifecyclePhysicalState.Intent)
			{
				if (!KingdomLifecycleRules.GuestRuntimeAdapter.RecoverProjectionIntent(book,
					op, projection, exact, !GameObject.Validate(existing))) return false;
				if (projection.State == KingdomLifecyclePhysicalState.Proved) return true;
			}
			GuestWorld world = new GuestWorld(system, zone, book, op);
			return KingdomLifecycleRules.TrustedAdapter.ProveLifecycleProjection(book, op,
				projection, world);
		}

		private static bool SettleWater(KingdomSystem system, Zone zone,
			KingdomLifecycleBook book, KingdomLifecycleOperation op)
		{
			GuestWorld world = new GuestWorld(system, zone, book, op);
			for (int i = 0; i < op.WaterLegs.Count; i++)
			{
				KingdomLifecycleWaterLeg leg = op.WaterLegs[i];
				KingdomLifecycleResourceLease lease = FindLease(op, leg.LeaseKey);
				GameObject owner = FindExact(leg.OwnerId);
				LiquidVolume liquid = owner == null ? null : owner.GetPart<LiquidVolume>();
				if (leg.State == KingdomLifecyclePhysicalState.Proved) continue;
				if (leg.State == KingdomLifecyclePhysicalState.Intent)
				{
					if (liquid != null && liquid.Volume == leg.After)
					{
						if (!KingdomLifecycleRules.GuestRuntimeAdapter.RecoverWaterIntent(book,
							op, lease, leg, liquid.Volume)) return false;
						continue;
					}
					if (liquid == null || !KingdomLifecycleRules.GuestRuntimeAdapter.ResetWaterIntent(
						book, op, leg, liquid.Volume)) return false;
				}
				if (!KingdomLifecycleRules.TrustedAdapter.ProveWater(book, lease, leg, world))
					return false;
			}
			return true;
		}

		private static bool SettleRemoval(KingdomSystem system, Zone zone,
			KingdomLifecycleBook book, KingdomLifecycleOperation op)
		{
			GameObject exact = FindExact(op.ObjectId);
			if (op.RemovalState == KingdomLifecyclePhysicalState.Intent)
				return KingdomLifecycleRules.GuestRuntimeAdapter.RecoverRemovalIntent(book, op,
					!GameObject.Validate(exact));
			return KingdomLifecycleRules.TrustedAdapter.ProveLifecycleRemoval(book, op,
				new GuestWorld(system, zone, book, op));
		}

		private static bool SettleDomain(KingdomSystem system, Zone zone,
			KingdomLifecycleBook book, KingdomLifecycleOperation op)
		{
			KingdomLifecycleResourceLease lease = DomainLease(op);
			if (lease == null) return false;
			if (op.Action == KingdomLifecycleAction.Lodge)
			{
				long current = Simulation.City.KingdomResidents.OnRollCount(system);
				GameObject guest = FindExact(op.ObjectId);
				if (!GameObject.Validate(guest) || !guest.IsAlive)
					return TrySettleDeadLodge(system, book, op);
				if (lease.State == KingdomLifecycleLeaseState.Intent)
				{
					if (current == lease.After)
					{
						if (!KingdomGuestbook.ApplyLifecycleLodge(system, guest, op)
							|| !KingdomGuestbook.LifecycleLodgeComplete(system, guest, op)) return false;
						KingdomSurvey.ObserveChangedInActive(zone, guest);
						return KingdomLifecycleRules.GuestRuntimeAdapter.RecoverDomainIntent(book,
							op, current);
					}
					if (current != lease.Before || !KingdomLifecycleRules.GuestRuntimeAdapter
						.ResetDomainIntent(book, op, current)) return false;
				}
				if (!KingdomLifecycleRules.GuestRuntimeAdapter.BeginDomain(book, op, current))
					return false;
				if (!KingdomGuestbook.ApplyLifecycleLodge(system, guest, op)) return false;
				KingdomSurvey.ObserveChangedInActive(zone, guest);
				long after = Simulation.City.KingdomResidents.OnRollCount(system);
				return KingdomGuestbook.LifecycleLodgeComplete(system, guest, op)
					&& KingdomLifecycleRules.GuestRuntimeAdapter.CommitDomain(book, op, after);
			}
			if (op.Creed == "causal-pilgrim") ApplyCausalDomain(system, op);
			if (op.Target == 1 && (op.Action == KingdomLifecycleAction.OfferWater
				|| op.Action == KingdomLifecycleAction.Depart)) system.FirstGuestGreeted = true;
			return KingdomLifecycleRules.GuestRuntimeAdapter.ProvePhysicalDomain(book, op);
		}

		private static void ApplyCausalDomain(KingdomSystem system,
			KingdomLifecycleOperation op)
		{
			if (system.City == null || op.Kind != system.City.PilgrimSequence) return;
			if (op.Action == KingdomLifecycleAction.Spawn)
			{
				system.City.PilgrimState = (int)KingdomLocusRules.PilgrimState.Standing;
				system.City.PilgrimObjectId = op.Projections[0].ObjectId;
				system.City.PilgrimName = op.ObjectName;
				return;
			}
			system.City.PilgrimState = (int)KingdomLocusRules.PilgrimState.None;
			system.City.PilgrimCauseTick = 0L;
			system.City.PilgrimCause = "";
			system.City.PilgrimObjectId = "";
			system.City.PilgrimName = "";
			system.City.PilgrimPlaceName = "";
			system.City.PilgrimGreeted = 0;
		}

	}
}
