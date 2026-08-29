#if !TAF_TESTS
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomCommunalRiteRuntime
	{
		private static bool TryCancel(KingdomSystem system,
			KingdomFirstFeastRuntime.CityContext context, KingdomFirstFeastReceipt practice,
			long now, out string failure)
		{
			failure = null;
			if (!TryRead(system, out KingdomCommunalRiteBook book, out failure)
				|| !KingdomCommunalRiteRules.TryFind(book, context.SettlementId,
					out KingdomCommunalRiteReceipt row) || row == null
				|| row.PracticeId != practice.PracticeId
				|| !KingdomCommunalRiteRules.TryPracticeSubject(row.PracticeId, out int subject)
				|| !TryPhysical(context.Book, row, subject, now,
					out KingdomCommunalRitePhysicalState physical, out failure))
				return Fail(failure ?? "exact communal expression is absent", out failure);
			// A completed physical attendance cannot be renamed cancellation.
			if (physical == KingdomCommunalRitePhysicalState.Ready)
				return TryPublishTerminalThenAcknowledge(system, context.Book, row, subject,
					now, out failure);
			return TrySuppressThenClear(system, context.Book, row, subject, physical,
				now, out failure);
		}

		private static bool TryPublishTerminalThenAcknowledge(KingdomSystem system,
			KingdomCityBook city, KingdomCommunalRiteReceipt row, int subject, long now,
			out string failure)
		{
			if (!TryFinish(system, row, true, now, out bool changed, out failure)) return false;
			if (changed) KingdomExperienceRuntime.TryRecord(system,
				KingdomExperienceExperiment.FirstFeastPractice,
				KingdomExperienceTrialArm.Projected,
				KingdomExperienceObservationKind.Committed, 1);
			// C18 terminal publication is above; only now may physical owner restore/clear.
			return TryAcknowledgeAttended(system, city, row, subject,
				KingdomCommunalRitePhysicalState.Ready, now, out failure);
		}

		private static bool TrySuppressThenClear(KingdomSystem system, KingdomCityBook city,
			KingdomCommunalRiteReceipt row, int subject,
			KingdomCommunalRitePhysicalState physical, long now, out string failure)
		{
			if (!TryFinish(system, row, false, now, out bool changed, out failure)) return false;
			if (changed) KingdomExperienceRuntime.TryRecord(system,
				KingdomExperienceExperiment.FirstFeastPractice,
				KingdomExperienceTrialArm.FactsOnly,
				KingdomExperienceObservationKind.Closed, 0);
			return TryClearSuppressed(system, city, row, subject, physical, now, out failure);
		}

		private static bool TryFinish(KingdomSystem system, KingdomCommunalRiteReceipt row,
			bool attended, long now, out bool changed, out string failure)
		{
			changed = false; failure = null;
			if (!TryRead(system, out KingdomCommunalRiteBook book, out failure)) return false;
			KingdomCommunalRiteBook terminal = KingdomCommunalRiteRules.Clone(book);
			long tick = now < row.EventTick ? row.EventTick : now;
			bool cut = attended && row.Phase == KingdomCommunalRitePhase.Suppressed
				? KingdomCommunalRiteRules.TryRecoverReady(terminal, terminal.Revision,
					row.PracticeId, row.EventId, tick, out _, out failure)
				: KingdomCommunalRiteRules.TryFinish(terminal, terminal.Revision,
					row.PracticeId, row.EventId, attended, tick, out _, out failure);
			if (!cut) return false;
			changed = terminal.Revision != book.Revision;
			if (changed
				&& !TryPublish(system, terminal, out failure)) return false;
			if (!TryRead(system, out book, out failure)
				|| !KingdomCommunalRiteRules.TryFind(book, row.SettlementId,
					out KingdomCommunalRiteReceipt standing)
				|| standing == null || standing.Phase != (attended
					? KingdomCommunalRitePhase.Attended : KingdomCommunalRitePhase.Suppressed))
				return Fail(failure ?? "communal terminal C18 cut was not reproved", out failure);
			return true;
		}

		private static bool TryAcknowledgeAttended(KingdomSystem system, KingdomCityBook city,
			KingdomCommunalRiteReceipt row, int subject,
			KingdomCommunalRitePhysicalState physical, long now, out string failure)
		{
			failure = null;
			if (physical == KingdomCommunalRitePhysicalState.Missing)
				return TryReleaseBodyLease(system, row, out failure);
			if (physical != KingdomCommunalRitePhysicalState.Ready
				&& physical != KingdomCommunalRitePhysicalState.Restoring)
				return Fail("attended C18 cut conflicts with pending physical state", out failure);
			if (KingdomPhysicalHappenings.AcknowledgeCommunalRite(system, city,
				row.EventId, subject, now))
			{
				KingdomExperienceRuntime.TryRecord(system,
					KingdomExperienceExperiment.FirstFeastPractice,
					KingdomExperienceTrialArm.Projected,
					KingdomExperienceObservationKind.QuietCompletion, 1);
				return TryReleaseBodyLease(system, row, out failure);
			}
			return Fail("physical acknowledgement remains incomplete", out failure);
		}

		private static bool TryClearSuppressed(KingdomSystem system, KingdomCityBook city,
			KingdomCommunalRiteReceipt row, int subject,
			KingdomCommunalRitePhysicalState physical, long now, out string failure)
		{
			failure = null;
			if (physical == KingdomCommunalRitePhysicalState.Missing)
				return TryReleaseBodyLease(system, row, out failure);
			if (physical == KingdomCommunalRitePhysicalState.Ready)
				return Fail("suppressed C18 cut conflicts with attended physical proof", out failure);
			if (KingdomPhysicalHappenings.CancelCommunalRite(system, city,
				row.EventId, subject, now)) return TryReleaseBodyLease(system, row, out failure);
			return Fail("physical communal expression cleanup remains incomplete", out failure);
		}

		private static KingdomExperienceBodyReservation BodyLease(KingdomSystem system,
			KingdomCommunalRiteReceipt row)
		{
			string proof = BodyLeaseProof(row);
			return new KingdomExperienceBodyReservation
			{
				ReservationId = "taf:experience-body:" + proof,
				RealmId = system.RealmId, SettlementId = row.SettlementId,
				SourceId = "taf:communal-rite-body:" + proof,
				Lane = KingdomExperienceLane.CommunalRite,
				OptionKind = KingdomExperienceOptionKind.CivicStory,
				CauseTick = row.EventTick, ReservedTick = row.EventTick,
				EnableEpoch = row.EnableEpoch,
				BodyCount = KingdomHappeningLifecycleRules.MaxParticipants
			};
		}

		private static bool TryEnsureBodyLease(KingdomSystem system,
			KingdomCommunalRiteReceipt row, out string failure)
		{
			failure = null; KingdomExperienceBodyReservation expected = BodyLease(system, row);
			if (!KingdomExperienceRules.TryReadBodyLease(system.Experience,
				expected.ReservationId, out KingdomExperienceBodyReservation actual,
				out KingdomExperienceLeaseState state, out failure)) return false;
			if (state == KingdomExperienceLeaseState.Missing)
				return KingdomExperienceRuntime.TryReserveBodies(system, expected,
					out KingdomExperienceCapacityFault _, out failure);
			return actual != null && actual.SourceId == expected.SourceId
				&& actual.RealmId == expected.RealmId && actual.SettlementId == expected.SettlementId
				&& actual.Lane == expected.Lane && actual.OptionKind == expected.OptionKind
				&& actual.CauseTick == expected.CauseTick && actual.ReservedTick == expected.ReservedTick
				&& actual.EnableEpoch == expected.EnableEpoch && actual.BodyCount == expected.BodyCount
				|| Fail("communal expression body lease differs from frozen event", out failure);
		}

		private static bool TryReleaseBodyLease(KingdomSystem system,
			KingdomCommunalRiteReceipt row, out string failure)
		{
			KingdomExperienceBodyReservation lease = BodyLease(system, row);
			return KingdomExperienceRuntime.TryReleaseBodies(system,
				lease.ReservationId, lease.SourceId,
				out KingdomExperienceCapacityFault _, out failure);
		}

		private static string BodyLeaseProof(KingdomCommunalRiteReceipt row)
		{
			string exact = row.EventId + "\n" + row.PracticeId + "\n"
				+ row.EnableEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture);
			byte[] digest;
			using (System.Security.Cryptography.SHA256 sha =
				System.Security.Cryptography.SHA256.Create())
				digest = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(exact));
			System.Text.StringBuilder text = new System.Text.StringBuilder(64);
			for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
			return text.ToString();
		}

		private static bool TryPhysical(KingdomCityBook city,
			KingdomCommunalRiteReceipt row, int subject, long now,
			out KingdomCommunalRitePhysicalState state, out string failure)
		{
			failure = null;
			if (!KingdomPhysicalHappenings.TryReadCommunalRite(city, subject, now,
				out state, out string eventId, out string practiceId, out long eventTick,
				out long epoch)) return Fail("physical communal expression cannot be read", out failure);
			if (state == KingdomCommunalRitePhysicalState.Missing) return true;
			if (eventId == row.EventId && practiceId == row.PracticeId
				&& eventTick == row.EventTick && epoch == row.EnableEpoch) return true;
			return Fail("physical communal expression differs from its exact C18 receipt",
				out failure);
		}

		private static bool Fail(string text, out string failure)
		{
			failure = text; return false;
		}
	}
}
#endif
