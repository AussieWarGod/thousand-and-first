using System;

namespace ThousandAndFirst
{
	public static partial class KingdomSaveSystemRosterRules
	{
		/// <summary>Classifies one pre-require system snapshot. No input is retained and no
		/// engine state is mutated.</summary>
		public static KingdomSaveSystemRosterDecision Decide(
			KingdomSaveSystemRosterContext Context, bool MarkerPresent, int MarkerRaw,
			KingdomSaveSystemRosterCounts Counts)
		{
			if (!Enum.IsDefined(typeof(KingdomSaveSystemRosterContext), Context)
				|| Context == KingdomSaveSystemRosterContext.Unknown)
				return Refused(KingdomSaveSystemRosterFault.InvalidContext,
					"save-system roster context is not explicit", MarkerPresent, MarkerRaw);
			if (Counts == null)
				return Refused(KingdomSaveSystemRosterFault.InvalidObservation,
					"save-system roster observation is absent", MarkerPresent, MarkerRaw);

			if (MarkerPresent)
			{
				if (!TryDecode(MarkerRaw, out int _, out int mask,
					out KingdomSaveSystemRosterFault fault,
					out KingdomSaveSystemRosterSystem system, out string failure))
					return Refused(fault, failure, true, MarkerRaw, system);
				KingdomSaveSystemRosterDecision exact = ExactCounts(mask, Counts,
					true, MarkerRaw);
				if (exact != null) return exact;
				if (Context == KingdomSaveSystemRosterContext.PreparedRemoval)
					return Accepted(KingdomSaveSystemRosterDisposition.ClearForPreparedRemoval,
						true, MarkerRaw, false, 0);
				return Accepted(KingdomSaveSystemRosterDisposition.Verified,
					true, MarkerRaw, true, MarkerRaw);
			}

			KingdomSaveSystemRosterDecision multiplicity = BoundedCounts(Counts,
				false, MarkerRaw);
			if (multiplicity != null) return multiplicity;
			if (Context == KingdomSaveSystemRosterContext.PreparedRemoval)
				return Accepted(KingdomSaveSystemRosterDisposition.LeaveAbsent,
					false, 0, false, 0);
			if (Context == KingdomSaveSystemRosterContext.UnprovenAbsence)
				return Recovery(KingdomSaveSystemRosterFault.MissingMarkerUnproven,
					"save-system roster marker is absent without new-game, legacy, or removal proof",
					false, 0);
			if (Context == KingdomSaveSystemRosterContext.LegacyDecodedRealm)
			{
				if (Counts.Realm != 1)
					return Recovery(KingdomSaveSystemRosterFault.LegacyRealmMissing,
						"legacy bootstrap lacks exactly one decoded Realm system", false, 0,
						KingdomSaveSystemRosterSystem.Realm, 1, Counts.Realm);
				if (Counts.Seal != 1)
					return Recovery(KingdomSaveSystemRosterFault.LegacySealMissing,
						"legacy bootstrap lacks exactly one decoded Seal system", false, 0,
						KingdomSaveSystemRosterSystem.Seal, 1, Counts.Seal);
			}

			int nextMask = MandatoryMask;
			if (Counts.Succession == 1) nextMask |= SuccessionBit;
			if (Counts.Inheritance == 1) nextMask |= InheritanceBit;
			if (!TryEncode(nextMask, out int nextRaw, out string encodeFailure))
				return Refused(KingdomSaveSystemRosterFault.InvalidObservation,
					encodeFailure, false, 0);
			return Accepted(KingdomSaveSystemRosterDisposition.Bootstrap,
				false, 0, true, nextRaw);
		}

		/// <summary>Pure second half of the exact raw compare-and-swap. Runtime writes or
		/// removes the returned marker only after this succeeds, then must read it back.</summary>
		public static bool TryResolveCas(KingdomSaveSystemRosterDecision Decision,
			bool CurrentMarkerPresent, int CurrentMarkerRaw, out bool NextMarkerPresent,
			out int NextMarkerRaw, out KingdomSaveSystemRosterFault Fault, out string Failure)
		{
			NextMarkerPresent = CurrentMarkerPresent;
			NextMarkerRaw = CurrentMarkerRaw;
			Fault = KingdomSaveSystemRosterFault.None;
			Failure = null;
			if (Decision == null || !Decision.Committable)
			{
				Fault = KingdomSaveSystemRosterFault.DecisionNotCommittable;
				Failure = "save-system roster decision is not committable";
				return false;
			}
			if (CurrentMarkerPresent != Decision.ExpectedMarkerPresent
				|| (CurrentMarkerPresent && CurrentMarkerRaw != Decision.ExpectedMarkerRaw))
			{
				Fault = KingdomSaveSystemRosterFault.CasChanged;
				Failure = "save-system roster marker changed before compare-and-swap";
				return false;
			}
			if (Decision.NextMarkerPresent && !TryDecode(Decision.NextMarkerRaw,
				out int _, out int _, out Fault, out KingdomSaveSystemRosterSystem _,
				out Failure)) return false;
			NextMarkerPresent = Decision.NextMarkerPresent;
			NextMarkerRaw = Decision.NextMarkerRaw;
			return true;
		}

		private static KingdomSaveSystemRosterDecision BoundedCounts(
			KingdomSaveSystemRosterCounts Counts, bool MarkerPresent, int MarkerRaw)
		{
			for (int i = 0; i < OrderedSystems.Length; i++)
			{
				KingdomSaveSystemRosterSystem system = OrderedSystems[i];
				int actual = Counts.Count(system);
				if (actual < 0)
					return Refused(KingdomSaveSystemRosterFault.InvalidObservation,
						"save-system roster observed a negative " + Name(system) + " count",
						MarkerPresent, MarkerRaw, system, 0, actual);
				if (actual > 1)
					return Recovery(KingdomSaveSystemRosterFault.UnexpectedMultiplicity,
						"save-system roster observed " + Name(system) + " more than once",
						MarkerPresent, MarkerRaw, system, 1, actual);
			}
			return null;
		}

		private static KingdomSaveSystemRosterDecision ExactCounts(int Mask,
			KingdomSaveSystemRosterCounts Counts, bool MarkerPresent, int MarkerRaw)
		{
			KingdomSaveSystemRosterDecision bounded = BoundedCounts(Counts,
				MarkerPresent, MarkerRaw);
			if (bounded != null) return bounded;
			for (int i = 0; i < OrderedSystems.Length; i++)
			{
				KingdomSaveSystemRosterSystem system = OrderedSystems[i];
				int expected = (Mask & Bit(system)) == 0 ? 0 : 1;
				int actual = Counts.Count(system);
				if (actual == expected) continue;
				KingdomSaveSystemRosterFault fault = expected == 1 && actual == 0
					? KingdomSaveSystemRosterFault.MarkerExpectedSystemMissing
					: KingdomSaveSystemRosterFault.UnexpectedMultiplicity;
				return Recovery(fault, "save-system roster marker and " + Name(system)
					+ " multiplicity disagree", MarkerPresent, MarkerRaw, system,
					expected, actual);
			}
			return null;
		}

		private static KingdomSaveSystemRosterDecision Accepted(
			KingdomSaveSystemRosterDisposition Disposition, bool ExpectedPresent,
			int ExpectedRaw, bool NextPresent, int NextRaw)
		{
			return new KingdomSaveSystemRosterDecision(Disposition,
				KingdomSaveSystemRosterFault.None, KingdomSaveSystemRosterSystem.None,
				0, 0, ExpectedPresent, ExpectedRaw, NextPresent, NextRaw, "");
		}

		private static KingdomSaveSystemRosterDecision Recovery(
			KingdomSaveSystemRosterFault Fault, string Failure, bool MarkerPresent,
			int MarkerRaw, KingdomSaveSystemRosterSystem System = KingdomSaveSystemRosterSystem.None,
			int Expected = 0, int Actual = 0)
		{
			return new KingdomSaveSystemRosterDecision(
				KingdomSaveSystemRosterDisposition.RecoveryRequired, Fault, System,
				Expected, Actual, MarkerPresent, MarkerRaw, MarkerPresent, MarkerRaw, Failure);
		}

		private static KingdomSaveSystemRosterDecision Refused(
			KingdomSaveSystemRosterFault Fault, string Failure, bool MarkerPresent,
			int MarkerRaw, KingdomSaveSystemRosterSystem System = KingdomSaveSystemRosterSystem.None,
			int Expected = 0, int Actual = 0)
		{
			return new KingdomSaveSystemRosterDecision(
				KingdomSaveSystemRosterDisposition.Refused, Fault, System, Expected, Actual,
				MarkerPresent, MarkerRaw, MarkerPresent, MarkerRaw, Failure);
		}
	}
}
