using System;

namespace ThousandAndFirst
{
	/// <summary>Pure realm-wide union law for mandatory carrier claims and optional bodies.
	/// A delivery row and its transient binding name one claim, never two.</summary>
	public static class KingdomSharedBodyCapacityRules
	{
		public const int MaxBodySlots = KingdomExperienceRules.MaxTransientBodySlots;
		public const int MaxClaimKeysPerSource = MaxBodySlots;
		// Pre-D10 saves could independently fill both bounded owners. Existing claims remain
		// recoverable while retiring; every changed/new admission still obeys MaxBodySlots.
		public const int MaxLegacyFoundationClaims = MaxClaimKeysPerSource * 2;

		public static bool TryCountFoundationClaims(int[] TransientBindingKeys,
			int[] DeliveryCarrierKeys, out int Count,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			return TryUnion(TransientBindingKeys, DeliveryCarrierKeys, Empty,
				out Count, out bool _, out Fault, out Failure);
		}

		/// <summary>Checks new mandatory claims without mutating either owner. Callers publish
		/// their exact delivery rows only after this succeeds.</summary>
		public static bool TryAdmitFoundationClaims(int[] TransientBindingKeys,
			int[] DeliveryCarrierKeys, int[] CandidateCarrierKeys, int OptionalReservedBodies,
			out int FoundationClaimCount, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			FoundationClaimCount = 0; Fault = KingdomExperienceCapacityFault.None;
			Failure = null;
			if (OptionalReservedBodies < 0 || OptionalReservedBodies > MaxBodySlots)
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"optional body count is invalid", out Fault, out Failure);
			if (!TryUnion(TransientBindingKeys, DeliveryCarrierKeys, CandidateCarrierKeys,
				out FoundationClaimCount, out bool changed, out Fault, out Failure)) return false;
			if (changed && FoundationClaimCount > MaxBodySlots - OptionalReservedBodies)
				return Refuse(KingdomExperienceCapacityFault.LiveBodyCapacityFull,
					"CapacityFull(foundation-bodies:realm)", out Fault, out Failure);
			return true;
		}

		public static bool TryAdmitNewFoundationClaims(int[] TransientBindingKeys,
			int[] DeliveryCarrierKeys, int NewClaimCount, int OptionalReservedBodies,
			out int FoundationClaimCount, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			FoundationClaimCount = 0; Fault = KingdomExperienceCapacityFault.None;
			Failure = null;
			if (NewClaimCount < 1 || NewClaimCount > MaxBodySlots
				|| OptionalReservedBodies < 0 || OptionalReservedBodies > MaxBodySlots)
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"foundation body claim count is invalid", out Fault, out Failure);
			if (!TryCountFoundationClaims(TransientBindingKeys, DeliveryCarrierKeys,
				out FoundationClaimCount, out Fault, out Failure)) return false;
			if (FoundationClaimCount > MaxBodySlots - OptionalReservedBodies
				|| NewClaimCount > MaxBodySlots - OptionalReservedBodies - FoundationClaimCount)
				return Refuse(KingdomExperienceCapacityFault.LiveBodyCapacityFull,
					"CapacityFull(foundation-bodies:realm)", out Fault, out Failure);
			return true;
		}

		private static readonly int[] Empty = new int[0];

		private static bool TryUnion(int[] Bindings, int[] Deliveries, int[] Candidates,
			out int Count, out bool CandidateChanged,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Count = 0; CandidateChanged = false;
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			if (!ValidInput(Bindings) || !ValidInput(Deliveries) || !ValidInput(Candidates))
				return Refuse(KingdomExperienceCapacityFault.InvalidRequest,
					"foundation body claim input is invalid", out Fault, out Failure);
			int[] union = new int[MaxLegacyFoundationClaims];
			if (!AddAll(Bindings, union, ref Count, out Fault, out Failure)
				|| !AddAll(Deliveries, union, ref Count, out Fault, out Failure)) return false;
			int before = Count;
			if (!AddAll(Candidates, union, ref Count, out Fault, out Failure)) return false;
			CandidateChanged = Count != before; return true;
		}

		private static bool ValidInput(int[] Values)
		{
			if (Values == null || Values.Length > MaxClaimKeysPerSource) return false;
			for (int i = 0; i < Values.Length; i++) if (Values[i] <= 0) return false;
			return true;
		}

		private static bool AddAll(int[] Values, int[] Union, ref int Count,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.None; Failure = null;
			for (int i = 0; i < Values.Length; i++)
			{
				bool held = false;
				for (int j = 0; j < Count; j++) if (Union[j] == Values[i]) { held = true; break; }
				if (held) continue;
				if (Count >= Union.Length)
					return Refuse(KingdomExperienceCapacityFault.LiveBodyCapacityFull,
						"CapacityFull(foundation-bodies:realm)", out Fault, out Failure);
				Union[Count++] = Values[i];
			}
			return true;
		}

		private static bool Refuse(KingdomExperienceCapacityFault Value, string Message,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = Value; Failure = Message; return false;
		}
	}
}
