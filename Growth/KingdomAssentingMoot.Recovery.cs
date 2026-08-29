using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.ZoneParts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMoot
	{
		internal static bool Reconcile(KingdomSystem System, KingdomCityBook Book,
			GameObject Building, bool LoadMemberZones, out string Failure)
		{
			Failure = null;
			if (System == null || Book == null) return false;
			Book.Normalize();
			KingdomAssentingMootReceipt receipt = Book.AssentingMoot;
			if (!KingdomAssentingMootRules.Validate(receipt, out string invalid))
				return Quarantine(Book, receipt, invalid, out Failure);
			if (receipt.Phase == KingdomAssentingMootPhase.None) return true;
			if (receipt.Phase == KingdomAssentingMootPhase.Quarantined)
			{
				CleanupLoaded(receipt);
				Failure = receipt.Fault;
				return false;
			}
			if (Building == null && !TryExactBuilding(receipt, out Building))
				return SuspendBook(Book, receipt, "The exact moot building is absent.", out Failure);
			KingdomAssentingMootContext context;
			if (!TryContext(System, Building, out context, out string contextFailure)
				|| !ReferenceEquals(context.Book, Book))
				return SuspendBook(Book, receipt, contextFailure, out Failure);
			if (!BuildingReady(context, receipt, out string readiness))
				return Suspend(context, receipt, readiness, out Failure);
			int valid = ValidAssentCount(context, receipt, LoadMemberZones);
			int strength = KingdomAssentingMootRules.StrengthFor(valid,
				receipt.ExemptResidentIds.Count);
			if (strength <= 0)
				return Suspend(context, receipt,
					"Not enough current assenting voices remain after exemptions.", out Failure);
			if (receipt.Phase == KingdomAssentingMootPhase.Applied
				&& receipt.Strength == strength)
			{
				if (!EnsureMemberProjections(context, receipt, LoadMemberZones, out Failure)
					|| !EnsureZoneProjection(context.Zone, Building, receipt, out Failure))
					return ProjectionFailure(context, receipt, Failure, out Failure);
				return true;
			}
			if (receipt.Phase == KingdomAssentingMootPhase.Applied
				&& !Suspend(context, receipt, "The current voices changed.", out Failure))
				return false;
			receipt = Book.AssentingMoot;
			KingdomAssentingMootReceipt prepared = receipt.Phase ==
				KingdomAssentingMootPhase.Prepared ? receipt
				: KingdomAssentingMootRules.PrepareProjection(receipt, Now(receipt.PreparedTick));
			if (prepared == null)
				return Quarantine(Book, receipt, "Ward preparation could not advance.", out Failure);
			Book.AssentingMoot = prepared;
			KingdomAssentingMootReceipt applied = KingdomAssentingMootRules.Applied(
				prepared, strength, Now(prepared.PreparedTick));
			if (applied == null)
				return Quarantine(Book, prepared, "Ward application receipt could not advance.",
					out Failure);
			if (!EnsureMemberProjections(context, applied, LoadMemberZones, out Failure)
				|| !EnsureZoneProjection(context.Zone, Building, applied, out Failure))
				return ProjectionFailure(context, prepared, Failure, out Failure);
			Book.AssentingMoot = applied;
			return true;
		}

		internal static bool Suspend(KingdomAssentingMootContext Context,
			KingdomAssentingMootReceipt Receipt, string Reason, out string Failure)
		{
			Failure = null;
			KingdomAssentingMootReceipt suspended = Receipt.Phase ==
				KingdomAssentingMootPhase.Suspended ? Receipt
				: KingdomAssentingMootRules.Suspended(Receipt, Reason,
					Now(Math.Max(Receipt.PreparedTick, Receipt.AppliedTick)));
			if (suspended == null)
				return Quarantine(Context.Book, Receipt, "Ward suspension could not advance.",
					out Failure);
			Context.Book.AssentingMoot = suspended;
			if (!RemoveZoneProjection(Context.Zone, suspended, out Failure))
				return Quarantine(Context.Book, suspended, Failure, out Failure);
			RemoveMemberProjections(suspended);
			return true;
		}

		private static bool SuspendBook(KingdomCityBook Book,
			KingdomAssentingMootReceipt Receipt, string Reason, out string Failure)
		{
			Failure = null;
			KingdomAssentingMootReceipt suspended = Receipt.Phase ==
				KingdomAssentingMootPhase.Suspended ? Receipt
				: KingdomAssentingMootRules.Suspended(Receipt, Reason,
					Now(Math.Max(Receipt.PreparedTick, Receipt.AppliedTick)));
			if (suspended == null) return Quarantine(Book, Receipt, Reason, out Failure);
			Book.AssentingMoot = suspended;
			if (TryCachedZone(suspended.ZoneId, out Zone zone)
				&& !RemoveZoneProjection(zone, suspended, out Failure))
				return Quarantine(Book, suspended, Failure, out Failure);
			RemoveMemberProjections(suspended);
			return true;
		}

		private static bool ProjectionFailure(KingdomAssentingMootContext Context,
			KingdomAssentingMootReceipt Receipt, string Reason, out string Failure)
		{
			if (Divergence(Reason)) return Quarantine(Context.Book, Receipt, Reason, out Failure);
			return Suspend(Context, Receipt, Reason, out Failure);
		}

		private static bool Divergence(string Reason)
		{
			return !string.IsNullOrEmpty(Reason) && (Reason.IndexOf("different",
				StringComparison.OrdinalIgnoreCase) >= 0 || Reason.IndexOf("separated",
				StringComparison.OrdinalIgnoreCase) >= 0 || Reason.IndexOf("did not match",
				StringComparison.OrdinalIgnoreCase) >= 0);
		}

		private static bool TryCachedZone(string ZoneId, out Zone Zone)
		{
			Zone = null;
			return !string.IsNullOrEmpty(ZoneId) && The.ZoneManager?.CachedZones != null
				&& The.ZoneManager.CachedZones.TryGetValue(ZoneId, out Zone);
		}
	}
}
