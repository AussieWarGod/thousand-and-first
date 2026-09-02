using System;
using System.IO;
using HarmonyLib;
using XRL;
using XRL.Core;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// One process can own one active target game. Strong owner prevents GC from silently dropping
	/// an unsaved reservation; a new GameID deterministically abandons and unlocks the old game.
	/// </summary>
	internal static class KingdomInheritanceLeaseOwner
	{
		private static readonly object Sync = new object();

		private static string TargetGameId = "";

		private static KingdomSealReservationLease Lease;

		internal static void BeginGame(string GameId)
		{
			lock (Sync)
			{
				string next = GameId ?? "";
				if (Lease != null && TargetGameId != next)
				{
					Lease.Dispose();
					Lease = null;
				}
				if (Lease == null)
				{
					TargetGameId = next;
				}
			}
		}

		internal static KingdomSealReservationLease Hold(string GameId,
			KingdomSealReceipt Receipt, KingdomSealReservationLease Candidate)
		{
			if (Candidate == null || !Candidate.IsHeld || Receipt == null
				|| !Candidate.Matches(Receipt) || Receipt.TargetGameId != (GameId ?? ""))
			{
				throw new InvalidOperationException("the inheritance lease did not match its exact receipt");
			}
			lock (Sync)
			{
				if (Lease != null && Lease.IsHeld)
				{
					if (TargetGameId == GameId && Lease.Matches(Receipt))
					{
						if (!ReferenceEquals(Lease, Candidate))
						{
							Candidate.Dispose();
						}
						return Lease;
					}
					throw new InvalidOperationException("another live inheritance reservation is already owned");
				}
				Lease = Candidate;
				TargetGameId = GameId ?? "";
				return Lease;
			}
		}

		internal static KingdomSealReservationLease HoldUnknown(string GameId,
			KingdomSealReservationLease Candidate)
		{
			if (Candidate == null || !Candidate.IsHeld)
			{
				throw new InvalidOperationException("the inheritance lease is not live");
			}
			lock (Sync)
			{
				if (Lease != null && Lease.IsHeld && !ReferenceEquals(Lease, Candidate))
				{
					throw new InvalidOperationException(
						"another live inheritance reservation is already owned");
				}
				Lease = Candidate;
				TargetGameId = GameId ?? "";
				return Lease;
			}
		}

		internal static KingdomSealReservationLease Get(string GameId,
			KingdomSealReceipt Receipt)
		{
			lock (Sync)
			{
				return Lease != null && Lease.IsHeld && TargetGameId == (GameId ?? "")
					&& Receipt != null && Lease.Matches(Receipt) ? Lease : null;
			}
		}

		internal static void Forget(KingdomSealReservationLease Exact)
		{
			lock (Sync)
			{
				if (ReferenceEquals(Lease, Exact))
				{
					Lease = null;
					TargetGameId = "";
				}
			}
		}

		internal static void Finish(string GameId, KingdomSealReceipt Receipt)
		{
			lock (Sync)
			{
				if (Lease != null && TargetGameId == (GameId ?? "")
					&& Receipt != null && Lease.Matches(Receipt))
				{
					Lease.Dispose();
					Lease = null;
					TargetGameId = "";
				}
			}
		}
	}
}
