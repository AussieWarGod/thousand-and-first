using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private bool DispatchRealmChronicle(KingdomRealmArchive Archive,
			KingdomRealmCallbackReceipt Receipt, string EventId, string Telling,
			string OutsiderTelling, string Context, out string Refusal)
		{
			Refusal = "";
			KingdomChronicleDeclaration declaration;
			string frozenRegistryHash;
			string frozenOtherHash;
			string frozenRegistryFault;
			string before;
			bool legacy = false;
			if (Receipt == null || string.IsNullOrEmpty(OutsiderTelling))
				return QuarantineReturn(Archive,
					Context + " Chronicle counter-account is absent", out Refusal);
			if (Receipt.Phase == KingdomRealmCallbackPhase.None)
			{
				if (!KingdomChronicle.TryDeclareDisputedOnce(this, EventId, Telling,
					OutsiderTelling, false, null, out declaration) ||
					!TryInspectChronicle(EventId, declaration.Fingerprint,
						out frozenRegistryHash, out bool declaredPresent, out bool ignoredTerminal,
						out bool ignoredLost, out bool declaredConflict, out string ignoredRegistry,
						out frozenRegistryFault, out frozenOtherHash,
						out KingdomChronicleReceipt ignoredReceipt) || declaredPresent ||
					declaredConflict || !TryCreateChronicleIntent(EventId, declaration,
						frozenRegistryHash, frozenOtherHash, frozenRegistryFault, out before))
					return QuarantineReturn(Archive, Context +
						" Chronicle disputed declaration cannot be frozen", out Refusal);
			}
			else
			{
				before = Receipt.BeforeEffect;
				if (!TryParseChronicleIntent(before, EventId, Telling, true, null,
					out declaration, out frozenRegistryHash, out frozenOtherHash,
					out frozenRegistryFault, out legacy))
					return QuarantineReturn(Archive, Context +
						" Chronicle declaration receipt is malformed", out Refusal);
			}
			string fingerprint = declaration.Fingerprint;
			string expected = EventId + "|" + fingerprint;
			if (!TryInspectChronicle(EventId, fingerprint, out string registryHash,
				out bool present, out bool terminal, out bool lost, out bool conflict,
				out string registry, out string registryFault, out string otherRegistryHash,
				out KingdomChronicleReceipt eventReceipt) ||
				(Receipt.Phase != KingdomRealmCallbackPhase.None && Receipt.AfterEffect != expected))
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
				officialHash, outsiderHash, eventReceipt, legacy) : null;
			if (Receipt.Phase == KingdomRealmCallbackPhase.Settled)
				return terminal && EnsureArchiveChronicleState(Archive, declaration,
					eventReceipt, registry, registryFault, frozenRegistryHash, out Refusal) &&
					SettledCallbackStillMatches(Archive, Receipt, observed, out Refusal);
			if (!PrepareReturnCallback(Archive, Receipt, KingdomRealmCallbackScope.Chronicle,
				before, expected, out bool invokeAuthorized, out Refusal)) return false;
			if (!present && (registryHash != frozenRegistryHash ||
				officialHash != declaration.OfficialBefore ||
				outsiderHash != declaration.OutsiderBefore))
				return QuarantineReturn(Archive,
					Context + " Chronicle reached a third prestate", out Refusal);
			if (!terminal && !TryDeliverRealmChronicle(Archive, Receipt, declaration, EventId,
				fingerprint, frozenOtherHash, frozenRegistryFault, invokeAuthorized,
				ref registryHash, ref present, ref terminal, ref lost, ref conflict,
				ref registry, ref registryFault, ref otherRegistryHash, ref eventReceipt,
				out officialHash, out outsiderHash, out listLost, Context, out Refusal)) return false;
			if (!EnsureArchiveChronicleState(Archive, declaration, eventReceipt, registry,
				registryFault, frozenRegistryHash, out Refusal)) return false;
			observed = ChronicleObserved(registryHash, otherRegistryHash, officialHash,
				outsiderHash, eventReceipt, legacy);
			return SettleReturnCallback(Archive, Receipt, (listLost ||
				eventReceipt.JournalState == KingdomChronicleSinkDisposition.Lost || lost)
				? KingdomRealmCallbackDisposition.Lost
				: KingdomRealmCallbackDisposition.Delivered, observed, out Refusal);
		}

		private bool TryDeliverRealmChronicle(KingdomRealmArchive Archive,
			KingdomRealmCallbackReceipt Receipt, KingdomChronicleDeclaration Declaration,
			string EventId, string Fingerprint,
			string FrozenOtherHash, string FrozenRegistryFault, bool InvokeAuthorized,
			ref string RegistryHash, ref bool Present, ref bool Terminal, ref bool Lost,
			ref bool Conflict, ref string Registry, ref string RegistryFault,
			ref string OtherRegistryHash, ref KingdomChronicleReceipt EventReceipt,
			out string OfficialHash, out string OutsiderHash, out bool ListLost,
			string Context, out string Refusal)
		{
			OfficialHash = null; OutsiderHash = null; ListLost = false; Refusal = "";
			if (!Present && !InvokeAuthorized)
				return QuarantineReturn(Archive, Context +
					" Chronicle callback was interrupted before receipt publication", out Refusal);
			if (!Archive.CurrentGraphMatchesExceptChronicle(this, out string graphFailure) ||
				(!Present && (!KingdomRealmArchive.TryCurrentGraphHash(this, out string graph,
				out graphFailure) || graph != Receipt.BeforeGraph)))
				return QuarantineReturn(Archive, graphFailure ??
					Context + " Chronicle Core graph changed before callback", out Refusal);
			List<string> officialReference = ChronicleEntries;
			List<string> outsiderReference = OutsiderEntries;
			if (!KingdomChronicle.RecordDeclaredOnce(this, Declaration) ||
				!ReferenceEquals(officialReference, ChronicleEntries) ||
				!ReferenceEquals(outsiderReference, OutsiderEntries) ||
				!Archive.CurrentGraphMatchesExceptChronicle(this, out graphFailure))
			{
				Refusal = "The " + Context + " telling remains in its exact Chronicle receipt.";
				return false;
			}
			bool proved = TryInspectChronicle(EventId, Fingerprint, out RegistryHash, out Present,
				out Terminal, out Lost, out Conflict, out Registry, out RegistryFault,
				out OtherRegistryHash, out EventReceipt) && !Conflict && Terminal &&
				OtherRegistryHash == FrozenOtherHash &&
				TryValidateChronicleLists(Declaration, EventReceipt, true, true,
					out OfficialHash, out OutsiderHash, out ListLost) &&
				KingdomRealmCallbackProofRules.ChronicleFaultMatches(true, true,
					EventReceipt.OfficialState, EventReceipt.OutsiderState,
					EventReceipt.JournalState, RegistryFault, FrozenRegistryFault);
			if (proved) return true;
			return QuarantineReturn(Archive,
				Context + " Chronicle callback lacks exact terminal proof", out Refusal);
		}
	}
}
