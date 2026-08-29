#if !TAF_TESTS
using XRL;

namespace ThousandAndFirst
{
	/// <summary>Load-only provenance. ProvenFirstInstall remains distinct from a real new game;
	/// only the narrow pure-rule adapter below maps both to lawful marker bootstrap.</summary>
	internal enum KingdomSaveSystemRosterLoadedContext : byte
	{
		PreparedRemoval = 1,
		LegacyDecodedRealm = 2,
		ProvenFirstInstall = 3,
		UnprovenAbsence = 4
	}

	/// <summary>Engine boundary for the versioned roster marker. Every load observation is made
	/// after imported game state exists and before any TAF RequireSystem call; every mutation is a
	/// raw compare-and-swap followed by readback.</summary>
	internal static partial class KingdomSaveSystemRosterRuntime
	{
		internal static bool ValidateAfterImport(XRLGame Game, bool SavedModEvidenceKnown,
			bool SavedModWasPresent, bool InheritanceAuthorityUnreadable,
			out string Failure)
		{
			Failure = null;
			KingdomSaveSystemRosterCounts before = Snapshot(Game);
			bool markerPresent = Marker(Game, out int markerRaw);
			KingdomSaveSystemRosterLoadedContext loadedContext;
			if (IsPreparedRemovalSave(Game, markerPresent, before))
				loadedContext = KingdomSaveSystemRosterLoadedContext.PreparedRemoval;
			else if (!markerPresent && before.Realm == 1
				&& Game.GetSystem<KingdomSystem>()?.SaveRosterHasDecodedRealm == true)
				loadedContext = KingdomSaveSystemRosterLoadedContext.LegacyDecodedRealm;
			else if (CleanFirstInstall(Game, SavedModEvidenceKnown, SavedModWasPresent,
				markerPresent, before))
				loadedContext = KingdomSaveSystemRosterLoadedContext.ProvenFirstInstall;
			else
				loadedContext = KingdomSaveSystemRosterLoadedContext.UnprovenAbsence;
			KingdomSaveSystemRosterContext context = RuleContext(loadedContext);
			KingdomSaveSystemRosterRuntimePlan plan =
				KingdomSaveSystemRosterRuntimePlan.Create(context, markerPresent,
					markerRaw, before);
			int missing = 0;
			string optionalFailure = null;
			if (loadedContext != KingdomSaveSystemRosterLoadedContext.PreparedRemoval)
			{
				missing = MissingRequiredOptionalMask(Game, before,
					InheritanceAuthorityUnreadable, out optionalFailure);
			}
			if (plan.RecoveryRequired || missing != 0)
				return Recover(Game, plan.EnsureMask | missing,
					plan.RecoveryRequired ? Describe(plan.Decision) : optionalFailure, out Failure);
			if (plan.Decision.Disposition == KingdomSaveSystemRosterDisposition.Bootstrap)
			{
				try
				{
					Ensure(Game, plan.EnsureMask,
						loadedContext == KingdomSaveSystemRosterLoadedContext.LegacyDecodedRealm
							&& before.CivicMemory == 0);
				}
				catch (System.Exception error)
				{
					return Recover(Game, plan.EnsureMask,
						"save-system roster could not create a recovery carrier ("
						+ error.Message + ")", out Failure);
				}
				if (!ExactAfterEnsure(plan, Snapshot(Game), out string mismatch))
					return Recover(Game, plan.EnsureMask, mismatch, out Failure);
			}
			if (!TryCommit(Game, plan.Decision, out string commitFailure))
				return Recover(Game, plan.EnsureMask, commitFailure, out Failure);
			if (ProveFinal(Game,
				loadedContext == KingdomSaveSystemRosterLoadedContext.PreparedRemoval,
				out string proofFailure)) return true;
			return Recover(Game, plan.EnsureMask, proofFailure, out Failure);
		}

		internal static bool TryInitializeNewGame(XRLGame Game, out string Failure)
		{
			Failure = null;
			KingdomSaveSystemRosterCounts before = Snapshot(Game);
			bool present = Marker(Game, out int raw);
			KingdomSaveSystemRosterRuntimePlan plan =
				KingdomSaveSystemRosterRuntimePlan.Create(
					KingdomSaveSystemRosterContext.ExplicitNewGame, present, raw, before);
			int missing = MissingRequiredOptionalMask(Game, before, false,
				out string optionalFailure);
			if (plan.RecoveryRequired || missing != 0)
				return Recover(Game, plan.EnsureMask | missing,
					plan.RecoveryRequired ? Describe(plan.Decision) : optionalFailure, out Failure);
			if (plan.Decision.Disposition == KingdomSaveSystemRosterDisposition.Bootstrap)
			{
				try { Ensure(Game, plan.EnsureMask, false); }
				catch (System.Exception error)
				{
					return Recover(Game, plan.EnsureMask,
						"new-game roster bootstrap failed (" + error.Message + ")", out Failure);
				}
				if (!ExactAfterEnsure(plan, Snapshot(Game), out string mismatch))
					return Recover(Game, plan.EnsureMask, mismatch, out Failure);
			}
			if (!TryCommit(Game, plan.Decision, out string commitFailure))
				return Recover(Game, plan.EnsureMask, commitFailure, out Failure);
			if (ProveFinal(Game, false, out string proofFailure)) return true;
			return Recover(Game, plan.EnsureMask, proofFailure, out Failure);
		}

		/// <summary>Runs after SaveSystems has performed its own first operation early: removal of
		/// flagged systems (XRL/XRLGame.cs:1580-1587). No RequireSystem is lawful on this path.</summary>
		internal static bool TryPrepareBeforeSave(XRLGame Game, out string Failure)
		{
			Failure = null;
			KingdomSaveSystemRosterCounts counts = Snapshot(Game);
			bool present = Marker(Game, out int raw);
			bool prepared = IsPreparedRemovalSave(Game, present, counts);
			KingdomSaveSystemRosterRuntimePlan plan =
				KingdomSaveSystemRosterRuntimePlan.Create(prepared
					? KingdomSaveSystemRosterContext.PreparedRemoval
					: KingdomSaveSystemRosterContext.UnprovenAbsence,
					present, raw, counts);
			if (plan.RecoveryRequired)
				return Fail(Describe(plan.Decision), out Failure);
			if (!prepared && MissingRequiredOptionalMask(Game, counts, false,
				out string optionalFailure) != 0)
				return Fail(optionalFailure, out Failure);
			if (plan.Decision.Disposition == KingdomSaveSystemRosterDisposition.Bootstrap)
				return Fail("save-system roster bootstrap is not lawful during a save",
					out Failure);
			if (!TryCommit(Game, plan.Decision, out Failure)) return false;
			return ProveFinal(Game, prepared, out Failure);
		}

		private static KingdomSaveSystemRosterContext RuleContext(
			KingdomSaveSystemRosterLoadedContext Context)
		{
			switch (Context)
			{
				case KingdomSaveSystemRosterLoadedContext.PreparedRemoval:
					return KingdomSaveSystemRosterContext.PreparedRemoval;
				case KingdomSaveSystemRosterLoadedContext.LegacyDecodedRealm:
					return KingdomSaveSystemRosterContext.LegacyDecodedRealm;
				case KingdomSaveSystemRosterLoadedContext.ProvenFirstInstall:
					// Positive saved-mod-header proof, not inference from an empty registry.
					return KingdomSaveSystemRosterContext.ExplicitNewGame;
				default:
					return KingdomSaveSystemRosterContext.UnprovenAbsence;
			}
		}

		private static bool ProveFinal(XRLGame Game, bool Prepared, out string Failure)
		{
			Failure = null;
			KingdomSaveSystemRosterCounts counts = Snapshot(Game);
			bool present = Marker(Game, out int raw);
			if (Prepared)
				return IsPreparedRemovalSave(Game, present, counts)
					|| Fail("prepared-removal roster absence lost its exact fence proof",
						out Failure);
			KingdomSaveSystemRosterDecision proof = KingdomSaveSystemRosterRules.Decide(
				KingdomSaveSystemRosterContext.UnprovenAbsence, present, raw, counts);
			return proof.Disposition == KingdomSaveSystemRosterDisposition.Verified
				|| Fail(Describe(proof), out Failure);
		}

		private static bool ExactAfterEnsure(KingdomSaveSystemRosterRuntimePlan Plan,
			KingdomSaveSystemRosterCounts Counts, out string Failure)
		{
			return Plan.ExactAfterEnsure(Counts,
				out KingdomSaveSystemRosterSystem _, out int _, out int _, out Failure);
		}

		private static bool Recover(XRLGame Game, int EnsureMask, string Cause,
			out string Failure)
		{
			Failure = string.IsNullOrEmpty(Cause)
				? "the save-system roster could not be proved" : Cause;
			try { Ensure(Game, EnsureMask | KingdomSaveSystemRosterRules.MandatoryMask, false); }
			catch (System.Exception error)
			{
				Failure += "; a recovery shell could not be created (" + error.Message + ")";
			}
			KingdomSaveSystemRosterRecoveryCallback refuse =
				KingdomSaveSystemRosterRecoveryBindings.Refuse;
			refuse(Game, Failure);
			KingdomSaveSystemRosterCounts shells = Snapshot(Game);
			if (shells.Realm < 1 || shells.Seal < 1 || shells.CivicMemory < 1)
				throw new System.InvalidOperationException(
					"ThousandAndFirst could not construct mandatory inert recovery shells: "
					+ Failure);
			return false;
		}
	}
}
#endif
