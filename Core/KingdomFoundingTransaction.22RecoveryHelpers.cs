using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransaction
	{
		private static bool OriginalSnapshotStillExact(r_FounderBasin Basin,
			LiquidVolume Vessel)
		{
			return Basin != null && Vessel != null &&
				Vessel.Volume == Basin.PendingOriginalVolume &&
				Vessel.MaxVolume == Basin.PendingOriginalMaxVolume &&
				Same(Vessel.ComponentLiquids, Basin.PendingOriginalComponents);
		}

		private static bool CommittedSnapshotStillExact(r_FounderBasin Basin,
			LiquidVolume Vessel)
		{
			return Basin != null && Vessel != null &&
				Vessel.Volume == Basin.PendingCommittedVolume &&
				Vessel.MaxVolume == Basin.PendingCommittedMaxVolume &&
				Same(Vessel.ComponentLiquids, Basin.PendingCommittedComponents);
		}

		private static void PoisonReceipt(r_FounderBasin Basin, LiquidVolume Vessel)
		{
			if (Basin == null)
			{
				return;
			}
			// Never rewrite the paid snapshot to fit corrupt live water. Its strict algebra is
			// the only evidence a later recovery may trust.
			Basin.PendingPhase = KingdomFoundingPhase.RecoveryRequired;
		}

		private static bool RestorePrePublication(r_FounderBasin Basin,
			LiquidVolume Vessel)
		{
			if (OriginalSnapshotStillExact(Basin, Vessel))
			{
				return true;
			}
			return CommittedSnapshotStillExact(Basin, Vessel) &&
				RestoreOriginal(Basin, Vessel, TrustCurrent: false);
		}

		private static bool RestoreOriginal(r_FounderBasin Basin, LiquidVolume Vessel,
			bool TrustCurrent)
		{
			if (Basin == null || Vessel == null)
			{
				return false;
			}
			if (!TrustCurrent && !CommittedSnapshotStillExact(Basin, Vessel))
			{
				return false;
			}
			try
			{
				Vessel.MaxVolume = Basin.PendingOriginalMaxVolume;
				Vessel.Volume = Basin.PendingOriginalVolume;
				Vessel.ComponentLiquids = Copy(Basin.PendingOriginalComponents);
				Vessel.Update();
				return Vessel.MaxVolume == Basin.PendingOriginalMaxVolume &&
					Vessel.Volume == Basin.PendingOriginalVolume &&
					Same(Vessel.ComponentLiquids, Basin.PendingOriginalComponents);
			}
			catch
			{
				return Vessel.MaxVolume == Basin.PendingOriginalMaxVolume &&
					Vessel.Volume == Basin.PendingOriginalVolume &&
					Same(Vessel.ComponentLiquids, Basin.PendingOriginalComponents);
			}
		}

		private static Dictionary<string, int> Copy(Dictionary<string, int> Source)
		{
			return Source == null
				? new Dictionary<string, int>()
				: new Dictionary<string, int>(Source);
		}

		private static bool Same(Dictionary<string, int> A, Dictionary<string, int> B)
		{
			if (ReferenceEquals(A, B))
			{
				return true;
			}
			if (A == null || B == null || A.Count != B.Count)
			{
				return false;
			}
			foreach (KeyValuePair<string, int> item in A)
			{
				if (!B.TryGetValue(item.Key, out var value) || value != item.Value)
				{
					return false;
				}
			}
			return true;
		}

		private static KingdomFoundingResult Result(KingdomFoundingOutcome Outcome,
			KingdomFoundingWaterDisposition Water, KingdomFoundingProjection Projection,
			string Failure = null)
		{
			return KingdomFoundingResult.From(Outcome, Water, Projection, Failure);
		}

		private static string Describe(Exception Exception)
		{
			return Exception == null || string.IsNullOrEmpty(Exception.Message)
				? "The engine refused the founding projection."
				: Exception.Message;
		}
	}
}
