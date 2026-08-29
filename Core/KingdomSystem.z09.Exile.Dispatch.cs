using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private bool ContinueExileTransition(out string Refusal)
		{
			Refusal = "";
			if (KingdomConstruction.HasNonterminalRoutedInputAuthority(this,
				out string custodyFailure))
			{
				Refusal = custodyFailure;
				return false;
			}
			KingdomRealmArchive archive = ExiledRealmArchive;
			string failure = null;
			if (archive == null || archive.Quarantined || !archive.Validate(out failure))
			{
				if (archive != null && !archive.Quarantined)
					archive.Quarantine(failure ?? "exile mirrors differ from archive intent");
				Refusal = "The exiled realm archive requires inspection.";
				return false;
			}
			if (!TradeTransitionProofMatches(archive, RequireBound: false, out failure))
			{
				archive.Quarantine(failure ??
					"Trade exile receipt no longer matches the archived close tick");
				Refusal = "The settled Trade exile receipt requires inspection.";
				return false;
			}
			if (archive.Phase != KingdomRealmArchivePhase.TradeClosed &&
				archive.Phase != KingdomRealmArchivePhase.MirrorsPublished &&
				archive.Phase != KingdomRealmArchivePhase.ChronicleFrozen &&
				archive.Phase != KingdomRealmArchivePhase.ChronicleCleared &&
				archive.Phase != KingdomRealmArchivePhase.Resetting &&
				archive.Phase != KingdomRealmArchivePhase.Closed)
			{
				archive.Quarantine("persisted exile phase predates the transactional Trade boundary");
				Refusal = "The exiled realm archive carries an impossible transition phase and requires inspection.";
				return false;
			}
			if (!TryEnsureExileMirrors(archive,
				AllowCanonicalMissing: archive.Phase == KingdomRealmArchivePhase.TradeClosed,
				AllowDirectionalMissing: false,
				out failure) || !ExactExileMirrors(archive))
			{
				archive.Quarantine(failure ?? "exile mirrors differ from archive intent");
				Refusal = "The exiled realm mirrors require inspection.";
				return false;
			}
			if (archive.Phase == KingdomRealmArchivePhase.TradeClosed)
				archive.Phase = KingdomRealmArchivePhase.MirrorsPublished;
			if (archive.Phase == KingdomRealmArchivePhase.MirrorsPublished)
			{
				if (!DispatchExileChronicle(archive, out Refusal) ||
					!archive.Validate(out failure))
				{
					if (string.IsNullOrEmpty(Refusal))
						Refusal = "The realm chronicle could not freeze exactly: " + failure + ".";
					return false;
				}
				// Publish the exact clear before/after tuple before the first registry setter.
				// A save after either half of that two-key CAS resumes from these frozen bytes;
				// it never rebuilds a shorter registry from the lone exile event.
				archive.Phase = KingdomRealmArchivePhase.ChronicleFrozen;
			}
			if (archive.Phase == KingdomRealmArchivePhase.ChronicleFrozen)
			{
				if (!KingdomChronicle.TryClearRealmRegistry(archive.ChronicleRegistry,
					archive.ChronicleRegistryFault, out failure))
				{
					Refusal = "The realm chronicle could not close exactly: " + failure + ".";
					return false;
				}
				archive.Phase = KingdomRealmArchivePhase.ChronicleCleared;
			}
			if (archive.Phase == KingdomRealmArchivePhase.ChronicleCleared)
			{
				if (!DispatchExileAbilityRemoval(archive, out Refusal)) return false;
				archive.Phase = KingdomRealmArchivePhase.Resetting;
			}
			if (archive.Phase == KingdomRealmArchivePhase.Resetting)
			{
				if (!KingdomPolityRealmTransitionRuntime.TryAdvanceExile(this, archive,
					out failure))
				{
					Refusal = "The realm's polity authority could not retire exactly: " +
						(failure ?? "transition remains unsettled") + ".";
					return false;
				}
				ResetCurrentRealmAfterExile();
				archive.Phase = KingdomRealmArchivePhase.Closed;
			}
			return archive.Phase == KingdomRealmArchivePhase.Closed;
		}

		private bool DispatchExileChronicle(KingdomRealmArchive Archive,
			out string Refusal)
		{
			string eventId = "taf:realm:exile:v1:" + Archive.RealmId;
			string telling = KingdomExileRules.ExileTelling(
				KingdomPresentation.Rich(Archive.DisplayName),
				Archive.ExileDeed);
			return DispatchRealmChronicle(Archive, Archive.ExileChronicle, eventId, telling,
				"exile", out Refusal);
		}

		private bool DispatchRealmChronicle(KingdomRealmArchive Archive,
			KingdomRealmCallbackReceipt Receipt, string EventId, string Telling,
			string Context, out string Refusal)
		{
			Refusal = "";
			if (!KingdomChronicleReceiptRules.TryFingerprint(EventId, Telling, true, null,
				out string fingerprint) || !TryInspectChronicle(EventId, fingerprint,
				out string registryHash, out bool present, out bool terminal, out bool lost,
				out bool conflict, out string registry, out string registryFault,
				out string otherRegistryHash, out KingdomChronicleReceipt eventReceipt))
				return QuarantineReturn(Archive, Context + " Chronicle cannot be inspected", out Refusal);
			string expected = EventId + "|" + fingerprint;
			KingdomChronicleDeclaration declaration;
			string frozenRegistryHash;
			string frozenOtherHash;
			string frozenRegistryFault;
			string before;
			if (Receipt.Phase == KingdomRealmCallbackPhase.None)
			{
				if (present)
					return QuarantineReturn(Archive, Context +
						" Chronicle row exists without outer declaration intent", out Refusal);
				if (!KingdomChronicle.TryDeclareOnce(this, EventId, Telling, true, null,
					out declaration) || declaration.Fingerprint != fingerprint ||
					!TryCreateChronicleIntent(EventId, declaration, registryHash,
						otherRegistryHash, registryFault, out before))
					return QuarantineReturn(Archive, Context +
						" Chronicle declaration cannot be frozen", out Refusal);
				frozenRegistryHash = registryHash; frozenOtherHash = otherRegistryHash;
				frozenRegistryFault = registryFault;
			}
			else if (!TryParseChronicleIntent(Receipt.BeforeEffect, EventId, Telling, true,
				null, out declaration, out frozenRegistryHash, out frozenOtherHash,
				out frozenRegistryFault))
				return QuarantineReturn(Archive, Context +
					" Chronicle declaration receipt is malformed", out Refusal);
			else before = Receipt.BeforeEffect;
			if (Receipt.Phase != KingdomRealmCallbackPhase.None && Receipt.AfterEffect != expected)
				return QuarantineReturn(Archive,
					Context + " Chronicle intent conflicts with frozen content", out Refusal);
			if (!ChronicleDeclarationMatchesArchive(Archive, declaration, out string proofFailure) ||
				conflict || otherRegistryHash != frozenOtherHash ||
				!TryValidateChronicleLists(declaration, eventReceipt, present, terminal,
					out string officialHash, out string outsiderHash, out bool listLost) ||
				!KingdomRealmCallbackProofRules.ChronicleFaultMatches(present, terminal,
					eventReceipt == null ? KingdomChronicleSinkDisposition.Pending :
						eventReceipt.OfficialState,
					eventReceipt == null ? KingdomChronicleSinkDisposition.Pending :
						eventReceipt.OutsiderState,
					eventReceipt == null ? KingdomChronicleSinkDisposition.Pending :
						eventReceipt.JournalState, registryFault, frozenRegistryFault))
				return QuarantineReturn(Archive, proofFailure ?? Context +
					" Chronicle lists or unrelated rows reached a third state", out Refusal);
			string observed = terminal ? ChronicleObserved(registryHash, otherRegistryHash,
				officialHash, outsiderHash, eventReceipt) : null;
			if (Receipt.Phase == KingdomRealmCallbackPhase.Settled)
				return terminal && EnsureArchiveChronicleState(Archive, declaration,
					eventReceipt, registry, registryFault, frozenRegistryHash, out Refusal) &&
					SettledCallbackStillMatches(Archive, Receipt, observed, out Refusal);
			if (!PrepareReturnCallback(Archive, Receipt, KingdomRealmCallbackScope.Chronicle,
				before, expected,
				out bool invokeAuthorized, out Refusal)) return false;
			if (!present && (registryHash != frozenRegistryHash ||
				officialHash != declaration.OfficialBefore ||
				outsiderHash != declaration.OutsiderBefore))
				return QuarantineReturn(Archive,
					Context + " Chronicle reached a third prestate", out Refusal);
			if (!terminal)
			{
				if (!present && !invokeAuthorized)
					return QuarantineReturn(Archive,
						Context + " Chronicle callback was interrupted before receipt publication",
						out Refusal);
				if (!Archive.CurrentGraphMatchesExceptChronicle(this,
					out string graphFailure) || (!present &&
					(!KingdomRealmArchive.TryCurrentGraphHash(this, out string graph,
						out graphFailure) || graph != Receipt.BeforeGraph)))
					return QuarantineReturn(Archive, graphFailure ??
						Context + " Chronicle Core graph changed before callback", out Refusal);
				List<string> officialReference = ChronicleEntries;
				List<string> outsiderReference = OutsiderEntries;
				if (!KingdomChronicle.RecordDeclaredOnce(this, declaration) ||
					!ReferenceEquals(officialReference, ChronicleEntries) ||
					!ReferenceEquals(outsiderReference, OutsiderEntries) ||
					!Archive.CurrentGraphMatchesExceptChronicle(this, out graphFailure))
				{
					Refusal = "The " + Context + " telling remains in its exact Chronicle receipt.";
					return false;
				}
				if (!TryInspectChronicle(EventId, fingerprint, out registryHash, out present,
					out terminal, out lost, out conflict, out registry, out registryFault,
					out otherRegistryHash, out eventReceipt) || conflict || !terminal ||
					otherRegistryHash != frozenOtherHash ||
					!TryValidateChronicleLists(declaration, eventReceipt, true, true,
						out officialHash, out outsiderHash, out listLost) ||
					!KingdomRealmCallbackProofRules.ChronicleFaultMatches(true, true,
						eventReceipt.OfficialState, eventReceipt.OutsiderState,
						eventReceipt.JournalState, registryFault, frozenRegistryFault))
					return QuarantineReturn(Archive,
						Context + " Chronicle callback lacks exact terminal proof", out Refusal);
			}
			if (!EnsureArchiveChronicleState(Archive, declaration, eventReceipt, registry,
				registryFault, frozenRegistryHash, out Refusal)) return false;
			observed = ChronicleObserved(registryHash, otherRegistryHash, officialHash,
				outsiderHash, eventReceipt);
			return SettleReturnCallback(Archive, Receipt, (listLost ||
				eventReceipt.JournalState == KingdomChronicleSinkDisposition.Lost || lost)
				? KingdomRealmCallbackDisposition.Lost
				: KingdomRealmCallbackDisposition.Delivered,
				observed, out Refusal);
		}

		private bool DispatchExileAbilityRemoval(KingdomRealmArchive Archive,
			out string Refusal)
		{
			Refusal = "";
			if (!TryObserveCharterAbility(out CharterAbilityObservation observation))
				return QuarantineReturn(Archive, "charter removal graph cannot be bounded",
					out Refusal);
			KingdomRealmCallbackReceipt receipt = Archive.ExileAbility;
			string before = receipt.Phase == KingdomRealmCallbackPhase.None
				? AbilityEffect(observation) : receipt.BeforeEffect;
			string after = receipt.Phase == KingdomRealmCallbackPhase.None
				? AbilityIntent(observation.StableHash, observation.TargetTemplateHash,
					observation.State == "player-absent" ? "player-absent" : "removed")
				: receipt.AfterEffect;
			if (!TryParseAbilityEffect(before, out string beforeFull, out string frozenStable,
				out string frozenTemplate, out string beforeState) ||
				!TryParseAbilityEffect(after, out string ignoredFull, out string expectedStable,
					out string expectedTemplate, out string expectedState) ||
				frozenStable != expectedStable || frozenTemplate != expectedTemplate ||
				(expectedState != "removed" && expectedState != "player-absent"))
				return QuarantineReturn(Archive, "charter removal intent is malformed",
					out Refusal);
			if (receipt.Phase == KingdomRealmCallbackPhase.Settled)
				return observation.State == expectedState &&
					observation.StableHash == frozenStable &&
					SettledCallbackStillMatches(Archive, receipt,
						AbilityEffect(observation), out Refusal);
			if (!PrepareReturnCallback(Archive, receipt, KingdomRealmCallbackScope.Ability,
				before, after,
				out bool invokeAuthorized, out Refusal)) return false;
			if (!TryObserveCharterAbility(out observation) ||
				observation.StableHash != frozenStable)
				return QuarantineReturn(Archive,
					"charter removal changed unaffected ability or part graph", out Refusal);
			string current = AbilityEffect(observation);
			if (observation.State == expectedState)
				return SettleReturnCallback(Archive, receipt,
					before == current
						? KingdomRealmCallbackDisposition.Skipped
						: KingdomRealmCallbackDisposition.Delivered,
					current, out Refusal);
			if (!observation.Recoverable || current != before ||
				observation.State != beforeState || observation.FullHash != beforeFull)
				return QuarantineReturn(Archive, "charter removal found duplicate ability state",
					out Refusal);
			if (!invokeAuthorized)
				return QuarantineReturn(Archive,
					"charter removal was interrupted before exact poststate publication", out Refusal);
			if (!TryCaptureCharterReferences(out CharterReferenceSnapshot charterReferences))
				return QuarantineReturn(Archive, "charter removal reference graph is unbounded",
					out Refusal);
			The.Player?.GetPart<KingdomCharterPart>()?.RemoveAbility();
			if (!TryObserveCharterAbility(out observation) ||
				!CharterReferencesStillMatch(charterReferences, AllowPartCreation: false) ||
				observation.StableHash != frozenStable || observation.State != expectedState)
				return QuarantineReturn(Archive,
					"charter removal callback did not settle exact absence", out Refusal);
			return SettleReturnCallback(Archive, receipt,
				KingdomRealmCallbackDisposition.Delivered,
				AbilityEffect(observation), out Refusal);
		}

	}
}
