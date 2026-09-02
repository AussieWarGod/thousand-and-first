namespace ThousandAndFirst
{
	/// <summary>Exact observation of the immutable current-realm foundation authority.</summary>
	public static partial class KingdomPolityRules
	{
		public static bool TryObserveCurrentFoundation(KingdomPolityLedger Ledger,
			string RealmId, string FactionId, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure) || Ledger.RealmId != RealmId ||
				RealmId != FactionId)
				return Fail("current foundation identity is not exact", out Failure);
			KingdomPolityRecord current = null;
			for (int i = 0; i < Ledger.Polities.Count; i++)
				if (Ledger.Polities[i].Source == KingdomPolitySource.CurrentRealm)
				{
					if (current != null)
						return Fail("current foundation polity is not unique", out Failure);
					current = Ledger.Polities[i];
				}
			if (current == null || current.PolityId != RealmId ||
				current.ProjectedFactionId != FactionId || current.NameRevision != 1 ||
				current.Lifecycle != KingdomPolityLifecycle.Active ||
				current.ProfileRevision < 1)
				return Fail("current foundation polity is absent or changed", out Failure);
			KingdomPolityProfileRevision profile = null;
			for (int i = 0; i < Ledger.Profiles.Count; i++)
				if (Ledger.Profiles[i].ProfileId == current.ProfileId &&
					Ledger.Profiles[i].Revision == current.ProfileRevision)
				{
					if (profile != null)
						return Fail("current foundation profile is not unique", out Failure);
					profile = Ledger.Profiles[i];
				}
			if (profile == null || profile.PolityId != RealmId || profile.RulesVersion !=
				KingdomPolityProfileRules.RulesVersion)
				return Fail("current foundation profile is absent or foreign", out Failure);
			KingdomPolityProjectionReceipt receipt = null;
			for (int i = 0; i < Ledger.Projections.Count; i++)
				if (Ledger.Projections[i].Kind == KingdomPolityProjectionKind.Faction &&
					Ledger.Projections[i].SourceRef == RealmId)
				{
					if (receipt != null)
						return Fail("current foundation receipt is not unique", out Failure);
					receipt = Ledger.Projections[i];
				}
			if (receipt == null || receipt.Phase != KingdomPolityProjectionPhase.Committed)
				return Fail("current foundation receipt is absent or uncommitted", out Failure);
			KingdomPolityProfileRevision foundation = null;
			for (int i = 0; i < Ledger.Profiles.Count; i++)
				if (Ledger.Profiles[i].ProfileId == current.ProfileId &&
					Ledger.Profiles[i].Revision == 1) foundation = Ledger.Profiles[i];
			if (foundation == null || foundation.PolityId != RealmId)
				return Fail("current foundation profile root is absent", out Failure);
			KingdomPolityProjectionReceipt expected = FoundationProjection(RealmId, FactionId,
				ProfileExpressionDigest(foundation), receipt.PreparedTick, true);
			return ExactFoundationReceipt(receipt, expected) ||
				Fail("current foundation receipt does not prove the profile", out Failure);
		}

		private static bool ExactFoundationReceipt(KingdomPolityProjectionReceipt Actual,
			KingdomPolityProjectionReceipt Expected)
		{
			return Actual.ProjectionId == Expected.ProjectionId &&
				Actual.Kind == Expected.Kind && Actual.SourceRef == Expected.SourceRef &&
				Actual.Phase == Expected.Phase && Actual.ObjectIds.Count == 1 &&
				Actual.ObjectIds[0] == Expected.ObjectIds[0] &&
				Actual.PriorDigest == Expected.PriorDigest &&
				Actual.AppliedDigest == Expected.AppliedDigest &&
				Actual.PreparedTick == Expected.PreparedTick &&
				Actual.CommittedTick == Expected.CommittedTick &&
				string.IsNullOrEmpty(Actual.ZoneId);
		}
	}
}
