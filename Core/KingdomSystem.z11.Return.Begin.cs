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
		private void ResetCurrentRealmAfterExile()
		{
			KingdomFactionName = null;
			KingdomDisplayName = null;
			Restore(new KingdomSettlement());
			Away = null;
			Standings = new Dictionary<string, int>();
			RealmId = null;
			RealmIdentityVersion = 0;
			RealmIdentityOrigin = KingdomIdentityOrigin.None;
			RealmIdentityTransactionId = null;
			RealmIdentityLegacyFaction = null;
			RealmIdentityFoundedTick = 0L;
			RealmIdentitySeedHigh = 0UL;
			RealmIdentitySeedLow = 0UL;
			RealmIdentityFirstClaimedZone = null;
			IdentityFault = null;
			PendingSettlementId = null;
			PendingSettlementTransactionId = null;
			PendingSettlementZoneId = null;
			PendingSettlementAuthority = null;
			SimulationSeedHigh = 0UL;
			SimulationSeedLow = 0UL;
			Bindings = new Simulation.City.KingdomBindingRegistry();
			ResidentCounter = 0;
			Jobs = new Simulation.City.KingdomJobRegistry();
			LastSliceTick = 0L;
			ReifyTick = 0L;
			ReifyThirdsSpent = 0;
			ReifyHeavySpent = 0;
			ReifyQuietUntilTick = 0L;
			DedicationCounter = 0;
			ChronicleEntries = new List<string>();
			OutsiderEntries = new List<string>();
			RegardSpoken = (int)RealmRegard.Beloved;
			Dissent = 0;
			DissentSpoken = 0;
			LastDissentTick = 0L;
			DeclaredCreed = null;
			DishName = null;
			DishText = null;
			DishStaple = null;
			DishSource = null;
			LastRiteTick = 0L;
			LastSoulRiteTick = 0L;
			Seceded = null;
			SecededTick = 0L;
			Haul = null;
			CarryBook = new KingdomCarryBook();
			ReturnAskedRegard = int.MinValue;
			DoorClosedTold = false;
		}

		/// <summary>
		/// Asks the realm that expelled the founder to take them back.
		/// <para>
		/// Preconditions: an expulsion is on the record, no realm has been founded since, the
		/// founder is standing on the old realm's own ground, and its regard for them is no longer
		/// <see cref="RealmRegard.Repudiated"/>. Side effects: the realm, both of its cities and
		/// its standings ledger are restored exactly as they stood, regard is raised to the
		/// indifference floor if it stands below it, the Charter comes back, and both registers
		/// record the day. Failure mode: returns false with a founder-facing refusal and changes
		/// nothing.
		/// </para>
		/// </summary>
		/// <param name="Site">The zone the founder is standing in. Null reads as the wrong ground.</param>
		/// <param name="Refusal">Founder-facing reason, or empty on success.</param>
		/// <returns>True if the founder was taken back.</returns>
		public bool TryReturn(Zone Site, out string Refusal)
		{
			Refusal = "";
			if (ExiledRealmArchive != null &&
				(ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Restoring ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Restored ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.ReturnCleaning))
				return ContinueReturnTransition(Site, out Refusal);
			int regard = ExiledRealmRegard();
			ReturnVerdict verdict = KingdomExileRules.JudgeReturn(Exiled, Founded, ExiledRealmKeptGround, Site != null && ExiledRealmHolds(Site.ZoneID), regard);
			if (verdict != ReturnVerdict.Allowed)
			{
				Refusal = KingdomExileRules.ReturnRefusal(verdict,
					KingdomPresentation.Rich(ExiledDisplayName),
					KingdomPresentation.Rich(KingdomDisplayName));
				return false;
			}
			KingdomRealmArchive archive = ExiledRealmArchive;
			string failure = null;
			if (archive == null || archive.Phase != KingdomRealmArchivePhase.Closed ||
				archive.Quarantined || !archive.Validate(out failure) ||
				!ExactExileMirrors(archive))
			{
				if (archive != null && !archive.Quarantined)
					archive.Quarantine(failure ?? "return mirrors differ from archived identity");
				Refusal = "The exiled realm archive cannot be reproved and requires inspection.";
				return false;
			}
			if (!CurrentRealmIsCanonicalBlank(archive))
			{
				archive.Quarantine("return found a third current-realm identity before intent");
				Refusal = "A different current realm state blocks exact return.";
				return false;
			}
			if (TradeBook == null || TradeBook.IdentityBound ||
				!KingdomTradeRules.TryAuthenticateExactExileClosedTick(TradeBook, archive.RealmId,
				archive.SettlementIds, out long provedClosedTick, out failure) ||
				provedClosedTick != archive.ClosedTick)
			{
				Refusal = "The settled Trade exile receipt cannot authorize return: " +
					(failure ?? "close tick differs") + ".";
				return false;
			}
			archive.ReturnRegard = KingdomExileRules.RegardOnReturn(regard);
			archive.Phase = KingdomRealmArchivePhase.Restoring;
			return ContinueReturnTransition(Site, out Refusal);
		}

		private bool ContinueReturnTransition(Zone Site, out string Refusal)
		{
			Refusal = "";
			KingdomRealmArchive archive = ExiledRealmArchive;
			string failure = null;
			if (archive == null || archive.Quarantined ||
				(archive.Phase != KingdomRealmArchivePhase.Restoring &&
				 archive.Phase != KingdomRealmArchivePhase.Restored &&
				 archive.Phase != KingdomRealmArchivePhase.ReturnCleaning) ||
				!archive.Validate(out failure) ||
				archive.ReturnRegard == int.MinValue)
			{
				if (archive != null && !archive.Quarantined)
					archive.Quarantine(failure ?? "return receipt or exact mirrors are malformed");
				Refusal = "The exiled realm return receipt requires inspection.";
				return false;
			}
			if (archive.Phase == KingdomRealmArchivePhase.ReturnCleaning)
			{
				if (!archive.CurrentGraphMatches(this, out failure) ||
					!TradeTransitionProofMatches(archive, RequireBound: true, out failure))
					return QuarantineReturn(archive, failure ??
						"return cleanup authority no longer matches", out Refusal);
				return FinishReturnCleanup(archive, out Refusal);
			}
			if (!ExactExileMirrors(archive))
				return QuarantineReturn(archive, "return mirrors differ from archive intent",
					out Refusal);
			if (TradeBook == null || TradeBook.IdentityBound ||
				!KingdomTradeRules.TryAuthenticateExactExileClosedTick(TradeBook, archive.RealmId,
				archive.SettlementIds, out long provedClosedTick, out failure) ||
				provedClosedTick != archive.ClosedTick)
				return QuarantineReturn(archive, failure ??
					"Trade exile receipt no longer matches the archived close tick", out Refusal);
			if (archive.Phase == KingdomRealmArchivePhase.Restoring)
			{
				if (!RestoreArchivedRealmCore(archive, out failure) ||
					!KingdomChronicle.TryRestoreRealmRegistry(archive.ChronicleRegistry,
					archive.ChronicleRegistryFault, out failure) ||
					!TryBindTradeIdentity(out failure) ||
					!TradeTransitionProofMatches(archive, RequireBound: true, out failure) ||
					!CurrentRealmMatchesArchive(archive))
				{
					Refusal = "The archived realm did not restore exactly: " + failure + ".";
					return false;
				}
				archive.Phase = KingdomRealmArchivePhase.Restored;
			}
			return FinishReturnedRealm(Site, archive, out Refusal);
		}

		private bool FinishReturnedRealm(Zone Site, KingdomRealmArchive Archive,
			out string Refusal)
		{
			Refusal = "";
			if (!DispatchReturnChronicle(Archive, out Refusal) ||
				!DispatchReturnReputation(Archive, out Refusal) ||
				!DispatchReturnFeelings(Archive, out Refusal) ||
				!DispatchReturnSeat(Site, Archive, out Refusal) ||
				!DispatchReturnAbility(Archive, out Refusal)) return false;
			string factionName = KingdomFactionName;
			string seatName = SeatName;
			string displayName = KingdomDisplayName;
			int restored = Archive.ReturnRegard;
			Archive.Phase = KingdomRealmArchivePhase.ReturnCleaning;
			return FinishReturnCleanup(Archive, out Refusal, factionName, seatName,
				displayName, restored);
		}

		private bool FinishReturnCleanup(KingdomRealmArchive Archive, out string Refusal,
			string FactionName = null, string SeatNameValue = null,
			string DisplayName = null, int Restored = int.MinValue)
		{
			Refusal = "";
			if (Archive == null || Archive.Phase != KingdomRealmArchivePhase.ReturnCleaning)
				return false;
			if (FactionName == null) FactionName = KingdomFactionName;
			if (SeatNameValue == null) SeatNameValue = SeatName;
			if (DisplayName == null) DisplayName = KingdomDisplayName;
			if (Restored == int.MinValue) Restored = Archive.ReturnRegard;
			if (!TryClearExileMirrors(Archive, out string failure))
				return QuarantineReturn(Archive, failure, out Refusal);
			ReturnAskedRegard = int.MinValue;
			DoorClosedTold = false;
			ExiledRealmArchive = null;
			KingdomLog.Log("return: " + FactionName + " took the founder back -> " + Restored
				+ "; seated " + SeatNameValue);
			Popup.Show(KingdomExileRules.ReturnNotice(
				KingdomPresentation.Rich(DisplayName),
				KingdomPresentation.Rich(SeatNameValue)));
			return true;
		}

	}
}
