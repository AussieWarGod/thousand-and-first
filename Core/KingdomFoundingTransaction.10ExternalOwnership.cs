using ThousandAndFirst.Api;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransaction
	{
		private static bool TryChooseExternalBinding(Zone Site,
			KingdomFoundingKind Kind, out string Encoded, out string Failure)
		{
			Encoded = null;
			Failure = null;
			if (Kind == KingdomFoundingKind.VillageCharter)
			{
				Encoded = KingdomExternalOwnershipRules.Encode(
					KingdomExternalOwnershipRules.None());
				return Encoded != null;
			}
			KingdomExternalOwnershipReading reading =
				KingdomExternalOwnership.Inspect(Site);
			if (reading.State == KingdomExternalOwnershipState.ProviderFailed ||
				reading.State == KingdomExternalOwnershipState.Conflicting)
			{
				Failure = string.IsNullOrEmpty(reading.Failure)
					? "External ownership could not be proved safely." : reading.Failure;
				return false;
			}
			KingdomExternalOwnershipBinding binding;
			if (reading.State == KingdomExternalOwnershipState.Unowned)
			{
				binding = KingdomExternalOwnershipRules.None();
			}
			else
			{
				KingdomExternalOwnershipObservation owner = reading.Observation;
				string sector = string.IsNullOrEmpty(owner.SectorGuid)
					? "none registered for this exact zone" : owner.SectorGuid;
				int choice = Popup.PickOption(
					Title: "This ground already belongs to another settlement system",
					Intro: owner.ProviderId + " " + owner.ProviderVersion +
						" reports settlement " + owner.OwnerGuid + ".\n\n" +
						"Evidence: " + owner.Evidence + "\nSector: " + sector +
						"\nZone: " + owner.ZoneId + "\nParasang: " + owner.ParasangId +
						"\n\nBinding lets this city share that exact external owner. " +
						"Refusing or escaping spends no water and changes nothing.",
					Options: new string[]
					{
						"Bind this city to that exact settlement",
						"Leave this ground unchanged"
					}, AllowEscape: true);
				if (choice != 0)
				{
					Failure = "The ground was left unchanged; no water was spent.";
					return false;
				}
				binding = KingdomExternalOwnershipRules.Bind(owner);
			}
			Encoded = KingdomExternalOwnershipRules.Encode(binding);
			if (Encoded != null) return true;
			Failure = "The external-owner decision could not be encoded exactly.";
			return false;
		}

		internal static bool TryChooseClaimExternalBinding(Zone Site,
			out string Encoded, out string Failure)
		{
			return TryChooseExternalBinding(Site, KingdomFoundingKind.SecondCity,
				out Encoded, out Failure);
		}

		private static bool TryStageExternalBinding(Zone Site,
			KingdomFoundingKind Kind, string Authority, string Encoded,
			out string Failure)
		{
			Failure = null;
			if (Kind == KingdomFoundingKind.VillageCharter) return true;
			return KingdomExternalOwnershipBindingRuntime.TryStage(
				Site, Authority, Encoded, out Failure);
		}

		private static bool RevalidateExternalBinding(r_FounderBasin Basin,
			Zone Site, out string Failure)
		{
			Failure = null;
			if (Basin == null || Site == null)
			{
				Failure = "External-owner receipt ground is unavailable.";
				return false;
			}
			if (Basin.PendingKind == KingdomFoundingKind.VillageCharter) return true;
			if (!Basin.HasExternalBindingField)
				return LegacyGroundStillUnowned(Site, out Failure);
			return KingdomExternalOwnershipBindingRuntime.RevalidateStage(Site,
				Basin.PendingAuthority, Basin.PendingExternalBinding, out Failure);
		}

		private static bool CommitExternalBinding(r_FounderBasin Basin,
			Zone Site, out string Failure)
		{
			Failure = null;
			if (Basin.PendingKind == KingdomFoundingKind.VillageCharter) return true;
			if (!Basin.HasExternalBindingField)
				return LegacyGroundStillUnowned(Site, out Failure);
			return KingdomExternalOwnershipBindingRuntime.TryCommit(Site,
				Basin.PendingAuthority, Basin.PendingExternalBinding, out Failure);
		}

		private static bool ExternalBindingCompletionObserved(r_FounderBasin Basin,
			Zone Site)
		{
			if (Basin.PendingKind == KingdomFoundingKind.VillageCharter) return true;
			if (!Basin.HasExternalBindingField)
				return LegacyGroundStillUnowned(Site, out string ignored);
			return KingdomExternalOwnershipBindingRuntime.CompletionMatches(Site,
				Basin.PendingAuthority, Basin.PendingExternalBinding);
		}

		private static bool FinishExternalBinding(r_FounderBasin Basin, Zone Site)
		{
			if (Basin.PendingKind == KingdomFoundingKind.VillageCharter ||
				!Basin.HasExternalBindingField) return true;
			return KingdomExternalOwnershipBindingRuntime.FinishStage(Site,
				Basin.PendingAuthority, Basin.PendingExternalBinding);
		}

		private static bool RollbackExternalBinding(Zone Site, string Authority,
			string Encoded, bool PublicationObserved)
		{
			if (string.IsNullOrEmpty(Encoded))
				return !KingdomExternalOwnershipBindingRuntime.HasStage(Site);
			return KingdomExternalOwnershipBindingRuntime.RollbackStage(Site,
				Authority, Encoded, PublicationObserved);
		}

		private static bool LegacyGroundStillUnowned(Zone Site, out string Failure)
		{
			KingdomExternalOwnershipReading reading =
				KingdomExternalOwnership.Inspect(Site);
			if (reading.State == KingdomExternalOwnershipState.Unowned)
			{
				Failure = null;
				return true;
			}
			Failure = string.IsNullOrEmpty(reading.Failure)
				? "A legacy receipt cannot accept newly observed external ownership."
				: reading.Failure;
			return false;
		}

		private static bool TryResolveDirectExternalBinding(Zone Site,
			KingdomFoundingAuthority Authority, string Name, string Vocation,
			out string Encoded, out string Failure)
		{
			Encoded = null;
			Failure = null;
			if (KingdomExternalOwnershipBindingRuntime.HasStage(Site))
			{
				string formatted = KingdomFoundingTransactionRules.FormatAuthority(Authority);
				if (!KingdomExternalOwnershipBindingRuntime.TryReadStage(Site,
						out string stagedAuthority, out Encoded))
				{
					Encoded = Site.GetZoneProperty(
						KingdomExternalOwnershipBindingRuntime.StageProperty, null);
					if (!KingdomExternalOwnershipRules.TryDecode(Encoded, out _)
						|| Authority.PayloadDigest != DirectPayloadDigest(Authority.Kind,
							Name, Vocation, null, null, Encoded)
						|| !KingdomExternalOwnershipBindingRuntime.TryStage(
							Site, formatted, Encoded, out Failure)
						|| !KingdomExternalOwnershipBindingRuntime.TryReadStage(Site,
							out stagedAuthority, out Encoded))
					{
						Failure = string.IsNullOrEmpty(Failure)
							? "The direct external-owner receipt is partial or malformed."
							: Failure;
						return false;
					}
				}
				if (stagedAuthority != formatted || Authority.PayloadDigest !=
						DirectPayloadDigest(Authority.Kind, Name, Vocation,
							null, null, Encoded))
				{
					Failure = "The direct external-owner receipt is foreign or malformed.";
					return false;
				}
				return KingdomExternalOwnershipBindingRuntime.RevalidateStage(
					Site, formatted, Encoded, out Failure);
			}
			if (Authority.PayloadDigest != DirectPayloadDigest(Authority.Kind,
				Name, Vocation, null, null))
			{
				Failure = "The legacy direct founding digest differs.";
				return false;
			}
			return LegacyGroundStillUnowned(Site, out Failure);
		}

		private static bool FinishDirectReservations(Zone Site, string Authority,
			string Realm, string ExternalBinding)
		{
			if (!ReleaseGlobalReservation(Authority, Realm, null)) return false;
			if (!string.IsNullOrEmpty(ExternalBinding) &&
				!KingdomExternalOwnershipBindingRuntime.FinishStage(
					Site, Authority, ExternalBinding)) return false;
			if (HasSiteReservation(Site) && !ReleaseSiteReservation(Site, Authority))
				return false;
			return !HasSiteReservation(Site)
				&& !KingdomExternalOwnershipBindingRuntime.HasStage(Site)
				&& GlobalReservationMarkersAbsent(Realm, null);
		}

		private static bool TryPassExternalPourBarrier(r_FounderBasin Basin,
			Zone Site, out KingdomFoundingResult Failure)
		{
			Failure = default(KingdomFoundingResult);
			if (RevalidateExternalBinding(Basin, Site, out string externalFailure))
				return true;
			string village = Basin.PendingKind == KingdomFoundingKind.VillageCharter
				? Basin.PendingVillageFaction : null;
			bool cleaned = ClearExactReservationSet(Site, Basin.PendingAuthority,
				Basin.PendingRealmFaction, village, Basin.PendingExternalBinding)
				&& SafeClearReceipt(Basin);
			Failure = Result(cleaned ? KingdomFoundingOutcome.Refused
					: KingdomFoundingOutcome.RecoverableFailure,
				KingdomFoundingWaterDisposition.Untouched,
				KingdomFoundingProjection.None,
				"External ownership changed before the pour: " + externalFailure);
			return false;
		}
	}
}
