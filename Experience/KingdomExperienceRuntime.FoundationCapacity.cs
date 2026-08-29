using System;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>Narrow engine seam for the shared Foundation/Experience body union. It reads
	/// bounded registries only; it never resolves a body or loads a zone.</summary>
	public static partial class KingdomExperienceRuntime
	{
		internal static bool TryAdmitFoundationTransientClaim(KingdomSystem System,
			int CandidateKey, out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			return TryAdmitFoundationTransientClaims(System, new int[] { CandidateKey },
				out Fault, out Failure);
		}

		internal static bool TryAdmitFoundationTransientClaims(KingdomSystem System,
			int[] CandidateKeys, out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.InvalidLedger; Failure = null;
			if (!TryReadFoundationKeys(System, out int[] bindings, out int[] deliveries,
				out Failure)) return false;
			if (System.Experience != null && !KingdomExperienceRules.TryValidate(
				System.Experience, out Failure)) return false;
			int optional = KingdomExperienceRules.ReservedBodies(System.Experience);
			return KingdomSharedBodyCapacityRules.TryAdmitFoundationClaims(bindings,
				deliveries, CandidateKeys, optional, out int _, out Fault, out Failure);
		}

		internal static bool TryAdmitNewFoundationTransientClaims(KingdomSystem System,
			int NewClaimCount, out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.InvalidLedger; Failure = null;
			if (!TryReadFoundationKeys(System, out int[] bindings, out int[] deliveries,
				out Failure)) return false;
			if (System.Experience != null && !KingdomExperienceRules.TryValidate(
				System.Experience, out Failure)) return false;
			return KingdomSharedBodyCapacityRules.TryAdmitNewFoundationClaims(bindings,
				deliveries, NewClaimCount,
				KingdomExperienceRules.ReservedBodies(System.Experience), out int _,
				out Fault, out Failure);
		}

		internal static bool FoundationOwnsCarrierClaim(KingdomSystem System, int Key)
		{
			if (Key <= 0 || !TryReadFoundationKeys(System, out int[] _,
				out int[] deliveries, out string _)) return false;
			for (int i = 0; i < deliveries.Length; i++)
				if (deliveries[i] == Key) return true;
			return false;
		}

		private static bool TryCountProtectedFoundationBodies(KingdomSystem System,
			string Context, out int Count, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			Count = 0; Fault = KingdomExperienceCapacityFault.InvalidLedger; Failure = null;
			if (!TryReadFoundationKeys(System, out int[] bindings, out int[] deliveries,
				out Failure))
			{
				Failure = "foundation authority refused " + Context + " capacity: " + Failure;
				return false;
			}
			return KingdomSharedBodyCapacityRules.TryCountFoundationClaims(bindings,
				deliveries, out Count, out Fault, out Failure);
		}

		private static bool TryReadFoundationKeys(KingdomSystem System, out int[] BindingKeys,
			out int[] DeliveryKeys, out string Failure)
		{
			BindingKeys = null; DeliveryKeys = null; Failure = null;
			if (System?.Bindings == null || System.Jobs == null)
			{
				Failure = "binding or delivery authority is absent"; return false;
			}
			if (!System.Bindings.TryRead(out KingdomBindingTable bindings,
				out KingdomCityFault bindingFault))
			{
				Failure = "binding registry is unreadable: " + bindingFault; return false;
			}
			if (!System.Jobs.TryRead(out KingdomJobTable jobs, out KingdomCityFault jobFault))
			{
				Failure = "delivery registry is unreadable: " + jobFault; return false;
			}
			int[] bindingBuffer = new int[KingdomSharedBodyCapacityRules.MaxBodySlots];
			int bindingCount = 0;
			for (int i = 0; i < bindings.Count; i++)
			{
				if (!bindings.TryAt(i, out KingdomBinding row))
				{
					Failure = "binding registry row is unreadable"; return false;
				}
				if (row.Kind != KingdomBindingKind.Transient) continue;
				if (bindingCount >= bindingBuffer.Length)
				{
					Failure = "binding registry exceeds transient capacity"; return false;
				}
				bindingBuffer[bindingCount++] = row.BindingKey;
			}
			int[] deliveryBuffer = new int[KingdomSharedBodyCapacityRules.MaxBodySlots];
			int deliveryCount = 0;
			for (int i = 0; i < jobs.Count; i++)
			{
				if (!jobs.TryAt(i, out KingdomJobRow row))
				{
					Failure = "delivery registry row is unreadable"; return false;
				}
				if (row.Kind != KingdomJobKind.Delivery) continue;
				if (deliveryCount >= deliveryBuffer.Length)
				{
					Failure = "delivery registry exceeds carrier capacity"; return false;
				}
				deliveryBuffer[deliveryCount++] = row.DeliveryTripId > 0
					? row.DeliveryTripId : row.JobId;
			}
			BindingKeys = Copy(bindingBuffer, bindingCount);
			DeliveryKeys = Copy(deliveryBuffer, deliveryCount); return true;
		}

		private static int[] Copy(int[] Source, int Count)
		{
			int[] result = new int[Count];
			if (Count > 0) Array.Copy(Source, result, Count);
			return result;
		}
	}
}
