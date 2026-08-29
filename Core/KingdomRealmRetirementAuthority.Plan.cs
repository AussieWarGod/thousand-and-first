using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst
{
	public static partial class KingdomRealmRetirementAuthority
	{
		public static bool TryBegin(KingdomSystem System,
			out KingdomRealmRetirementState State,
			out KingdomRealmRetirementReport Report, out string Failure)
		{
			State = null; Report = null; Failure = null;
			if (System == null) return Fail("realm system is absent", out Failure);
			if (!string.IsNullOrEmpty(System.RealmRetirementWire))
			{
				if (!System.TryReadRealmRetirement(out State, out Failure)) return false;
				Report = FromState(State);
				return true;
			}
			if (!TryInspect(System, out Report, out List<KingdomRemovalLocator> locators,
				out Failure) || !Report.CanBegin)
				return Fail(Failure ?? "active authority must be resolved before planning",
					out Failure);

			if (!TryCurrentDigests(System, locators, out string realmDigest,
				out string authorityDigest, out Failure)) return false;
			long tick = Math.Max(0L, The.Game?.TimeTicks ?? 0L);
			if (!KingdomRealmRetirementRules.TryPlan(Guid.NewGuid().ToString("N"),
				System.RealmId, System.KingdomFactionName, The.Game.GameID,
				System.RealmIncarnation, tick, authorityDigest, locators,
				out KingdomRealmRetirementState planned, out Failure)) return false;
			KingdomRemovalRecord legacyDisclosure = new KingdomRemovalRecord
			{
				Kind = KingdomRemovalProjectionKind.LegacyArtifact,
				Id = "taf:untracked-legacy-ground:v1",
				Disposition = KingdomRemovalDisposition.Untracked,
				Detail = "pre-ledger basins, scaffolds, or overwritten shared values may exist outside the frozen locator set"
			};
			if (!KingdomRealmRetirementRules.TryRecord(planned, planned.Revision,
				legacyDisclosure, tick, out KingdomRealmRetirementState disclosed, out Failure)
				|| !TryBuildFinalPlan(System, disclosed,
					out KingdomRealmRemovalFinalPlan frozenPlan, out Failure)) return false;
			KingdomRealmRetirementState frozen = disclosed;
			for (int i = 0; i < frozenPlan.PreviewRecords.Count; i++)
				if (!KingdomRealmRetirementRules.TryRecord(frozen, frozen.Revision,
					frozenPlan.PreviewRecords[i], tick,
					out frozen, out Failure)) return false;
			if (!KingdomRealmRetirementRules.TrySetPhase(frozen, frozen.Revision,
					KingdomRealmRetirementPhase.Planning,
					KingdomRealmRetirementPhase.Paused, tick,
					out KingdomRealmRetirementState paused, out Failure)
				|| !KingdomRealmRetirementRules.TrySetPhase(paused, paused.Revision,
					KingdomRealmRetirementPhase.Paused,
					KingdomRealmRetirementPhase.CleaningGround, tick,
					out State, out Failure)) return false;
			string wire = KingdomRealmRetirementCodec.Encode(State);
			if (!string.IsNullOrEmpty(System.RealmRetirementWire))
				return Fail("a removal plan appeared before publication", out Failure);
			KingdomSeal seal = The.Game?.GetSystem<KingdomSeal>();
			if (seal == null || !seal.TryPrepareRealmRemoval(out Failure))
				return Fail("profile seal is not quiescent: " + (Failure ?? "absent"), out Failure);
			if (!TryBuildFinalPlan(System, frozen,
				out KingdomRealmRemovalFinalPlan _, out string freezeFailure))
				return FailAfterSeal("frozen realm projections diverged before publication: "
					+ freezeFailure, out Failure);
			if (!TryCurrentDigests(System, locators, out string afterRealm,
				out string afterAuthority, out Failure) || afterRealm != realmDigest
				|| afterAuthority != authorityDigest)
				return FailAfterSeal("realm authority diverged before removal plan publication",
					out Failure);
			if (!string.IsNullOrEmpty(System.RealmRetirementWire))
				return FailAfterSeal("a removal plan appeared before publication", out Failure);
			System.RealmRetirementWire = wire;
			if (System.RealmRetirementWire != wire
				|| !System.TryReadRealmRetirement(out KingdomRealmRetirementState proved,
					out Failure) || proved.ReceiptId != State.ReceiptId)
				return FailAfterSeal("removal plan did not retain its exact receipt", out Failure);
			Report = FromState(State);
			return true;
		}

		private static bool FailAfterSeal(string Message, out string Failure)
		{
			Failure = Message + "; the profile seal was made quiescent before publication, "
				+ "but no realm-removal receipt was reported as committed";
			return false;
		}

		internal static bool TryPublish(KingdomSystem System,
			KingdomRealmRetirementState Expected, KingdomRealmRetirementState Next,
			out string Failure)
		{
			Failure = null;
			if (System == null || Expected == null || Next == null
				|| !KingdomRealmRetirementRules.Valid(Next, out Failure))
				return Fail(Failure ?? "removal receipt publication is invalid", out Failure);
			string expected = KingdomRealmRetirementCodec.Encode(Expected);
			string next = KingdomRealmRetirementCodec.Encode(Next);
			if (System.RealmRetirementWire != expected)
				return Fail("removal receipt changed before compare-and-swap", out Failure);
			System.RealmRetirementWire = next;
			return System.RealmRetirementWire == next
				|| Fail("removal receipt CAS did not retain its write", out Failure);
		}

		internal static bool TryCurrentDigests(KingdomSystem System,
			IList<KingdomRemovalLocator> Locators, out string RealmDigest,
			out string AuthorityDigest, out string Failure)
		{
			RealmDigest = null; AuthorityDigest = null;
			if (!KingdomIdentityFenceRuntime.TryRealmDigest(System, out string realm,
				out long incarnation, out RealmDigest, out Failure)
				|| realm != System?.RealmId || incarnation != System?.RealmIncarnation) return false;
			AuthorityDigest = KingdomRetirementDigestRules.RetirementAuthority(
				RealmDigest, Locators);
			return KingdomRealmRetirementRules.Digest(AuthorityDigest)
				|| Fail("retirement authority digest could not be formed", out Failure);
		}

		public static KingdomRealmRetirementReport FromState(
			KingdomRealmRetirementState State)
		{
			KingdomRealmRetirementReport report = NewReport();
			if (State == null)
			{
				report.Summary = "No readable realm-removal receipt exists.";
				report.Blockers.Add("The receipt is absent or unreadable."); return report;
			}
			report.CanBegin = State.Phase == KingdomRealmRetirementPhase.CleaningGround;
			for (int i = 0; i < State.Locators.Count; i++)
				if (State.Locators[i].State != KingdomRemovalLocatorState.Cleaned)
					report.OutstandingGround.Add(State.Locators[i].ZoneId + " ["
						+ State.Locators[i].State + "]");
			for (int i = 0; i < State.Records.Count; i++)
				if (State.Records[i].Disposition == KingdomRemovalDisposition.PriorUnknown
					|| State.Records[i].Disposition == KingdomRemovalDisposition.Untracked)
					report.Disclosures.Add(State.Records[i].Kind + " " + State.Records[i].Id
						+ ": " + State.Records[i].Detail);
			report.KnownProjectionsClosed =
				KingdomRealmRetirementRules.KnownProjectionClosurePermitsPreparation(State,
					out string _);
			report.CleanRemovalProvable = KingdomRealmRetirementRules.CleanRemovalProvable(State);
			report.Summary = "Realm-removal phase: " + State.Phase + ". "
				+ report.OutstandingGround.Count + " tracked ground visit(s) remain.";
			return report;
		}
	}
}
