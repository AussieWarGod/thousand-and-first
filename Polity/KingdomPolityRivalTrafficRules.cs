namespace ThousandAndFirst
{
	/// <summary>Assigns ambient work only when exact source-settlement authority exists.
	/// V8 has no external settlement/zone carrier, so external traffic remains unavailable.</summary>
	public static class KingdomPolityRivalTrafficRules
	{
		public static bool TryAssign(KingdomPolityLedger Ledger, KingdomPolityDueWork Due,
			out KingdomPolityTrafficAssignment Assignment, out string Failure)
		{
			Assignment = null; Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || !ValidDue(Due))
				return Fail(Failure ?? "ambient polity due row is invalid", out Failure);
			KingdomPolityRecord current = FindCurrent(Ledger);
			if (current == null) return Fail("ambient traffic has no active current polity",
				out Failure);
			Assignment = Current(Due, current.PolityId);
			// Imported/owned polity rows deliberately carry no old realm, settlement, or zone ids.
			// A relation/faction receipt cannot substitute for exact endpoint provenance.
			return true;
		}

		public static bool ValidAssignment(KingdomPolityTrafficAssignment Value)
		{
			if (Value == null || !ValidDue(Value.Work) ||
				!KingdomPolityRules.SemanticId(Value.PolityId)) return false;
			return !Value.External && string.IsNullOrEmpty(Value.RelationId) &&
				string.IsNullOrEmpty(Value.CauseDigest);
		}

		private static KingdomPolityTrafficAssignment Current(KingdomPolityDueWork Due,
			string PolityId)
		{
			return new KingdomPolityTrafficAssignment
			{
				Work = Copy(Due), PolityId = PolityId
			};
		}

		private static KingdomPolityRecord FindCurrent(KingdomPolityLedger Ledger)
		{
			for (int i = 0; i < Ledger.Polities.Count; i++)
				if (Ledger.Polities[i].Source == KingdomPolitySource.CurrentRealm &&
					Ledger.Polities[i].Lifecycle == KingdomPolityLifecycle.Active)
					return Ledger.Polities[i];
			return null;
		}

		private static bool ValidDue(KingdomPolityDueWork Due)
		{
			return Due != null && Due.EndpointOrdinal >= 0 &&
				Due.EndpointOrdinal < KingdomPolityDispatchRules.MaximumEndpoints &&
				KingdomPolityRules.TypedId(Due.CohortId, "taf:cohort:") &&
				KingdomPolityRules.TypedId(Due.EventStreamId, "taf:stream:") &&
				KingdomPolityRules.SemanticId(Due.SourceRef) &&
				KingdomPolityRules.TypedId(Due.SettlementId, "taf:settlement:v1:") &&
				Due.Purpose >= KingdomPolityCohortPurpose.Guard &&
				Due.Purpose <= KingdomPolityCohortPurpose.Migrant &&
				Due.Purpose != KingdomPolityCohortPurpose.Envoy &&
				Due.Purpose != KingdomPolityCohortPurpose.Warband &&
				Due.CauseTick >= 0L && Due.StayUntilTick >= Due.CauseTick &&
				Due.MemberCount >= 1 && Due.MemberCount <=
					KingdomPolityCohortRules.MaximumVisibleMembers &&
				Due.EndpointVerb == KingdomPolityDispatchRules.EndpointVerb(Due.Purpose);
		}

		private static KingdomPolityDueWork Copy(KingdomPolityDueWork Source)
		{
			return new KingdomPolityDueWork
			{
				EndpointOrdinal = Source.EndpointOrdinal, EndpointDigest = Source.EndpointDigest,
				CauseRef = Source.CauseRef, DueFacts = Source.DueFacts,
				FairnessTicket = Source.FairnessTicket, CohortId = Source.CohortId,
				EventStreamId = Source.EventStreamId, SourceRef = Source.SourceRef,
				SettlementId = Source.SettlementId, Purpose = Source.Purpose,
				WindowOrdinal = Source.WindowOrdinal, CauseTick = Source.CauseTick,
				StayUntilTick = Source.StayUntilTick, MemberCount = Source.MemberCount,
				EndpointVerb = Source.EndpointVerb,
				AmbientTransaction = KingdomPolityAmbientTransactionRules.Copy(
					Source.AmbientTransaction)
			};
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
