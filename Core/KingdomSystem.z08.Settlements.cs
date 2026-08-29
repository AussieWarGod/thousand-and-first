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
		/// <summary>How many cities the realm holds, seat included.</summary>
		public int SettlementCount => !Founded ? 0 : 1 + NonSeatSettlementCount;

		/// <summary>
		/// Copies the seated settlement out of the flat fields into a record. The flat fields are
		/// left as they are; the caller is expected to write another settlement over them
		/// immediately, because the two now share their rosters, ledger and claim lists.
		/// </summary>
		/// <returns>The seated settlement, never null.</returns>
		/// <exception cref="KingdomSeatMismatchException">A settlement field has no flat
		/// counterpart here. Nothing is read when this is thrown.</exception>
		public KingdomSettlement Capture()
		{
			KingdomSettlement settlement = new KingdomSettlement();
			settlement.ReadFrom(this);
			return settlement;
		}

		/// <summary>
		/// Seats a settlement: writes it over the flat fields, so every consumer that reads
		/// <c>Population</c>, <c>ClaimedZones</c> or <c>Ledger</c> is now reading this city.
		/// </summary>
		/// <param name="Settlement">The settlement to seat. Null is rejected.</param>
		/// <exception cref="KingdomSeatMismatchException">A settlement field has no flat
		/// counterpart here. Nothing is written when this is thrown.</exception>
		public void Restore(KingdomSettlement Settlement)
		{
			if (Settlement == null)
			{
				throw new KingdomSeatMismatchException("There is no settlement to seat.");
			}
			Settlement.WriteTo(this);
		}

		/// <summary>
		/// Exchanges the seat with the unique non-seat settlement which owns the activated zone.
		/// Called before the claim guard in <see cref="HandleEvent(ZoneActivatedEvent)"/>,
		/// because until the exchange has happened that city's ground is not in
		/// <see cref="ClaimedZones"/> and reads as a stranger's zone.
		/// </summary>
		/// <param name="Z">The activated zone. Null is tolerated.</param>
		/// <returns>True if the seat moved.</returns>
		public bool TrySeat(Zone Z)
		{
			if (!Founded || Z == null || ClaimedZones.Contains(Z.ZoneID))
			{
				return false;
			}
			KingdomSettlement target = FindNonSeatSettlementByZone(Z.ZoneID);
			if (target == null) return false;
			KingdomSettlement wasSeated = Capture();
			Restore(target);
			if (!TryReplaceNonSeatSettlement(target, wasSeated, out string failure))
			{
				Restore(wasSeated);
				throw new KingdomSeatMismatchException("Seat topology changed during exchange: " +
					failure);
			}
			if (KingdomLog.Enabled) KingdomLog.Log("seat moved to " + SeatName + " (" +
				Z.ZoneID + "); " + NonSeatSettlementCount + " non-seat settlements remain");
			return true;
		}

		/// <summary>
		/// The realm's regard for its founder, read from the founder's own reputation with the
		/// realm's faction &mdash; the one number the world, the reputation screen and this system
		/// already agree on. No second economy is kept for it.
		/// </summary>
		/// <returns>Raw reputation on the vanilla scale; 0 when nothing is founded.</returns>
		public int FounderRegard()
		{
			return RegardWith(KingdomFactionName);
		}

		/// <summary>The expelled-from realm's regard for the founder, or 0 if there is none.</summary>
		public int ExiledRealmRegard()
		{
			return RegardWith(ExiledFactionName);
		}

		/// <summary>Whether the expelled-from realm holds this ground.</summary>
		/// <param name="ZoneID">A zone id. Null and empty read as false.</param>
		public bool ExiledRealmHolds(string ZoneID)
		{
			if (!Exiled || string.IsNullOrEmpty(ZoneID))
			{
				return false;
			}
			if (ExiledSeat != null && ExiledSeat.ClaimedZones.Contains(ZoneID)) return true;
			return ExiledSettlementTopology?.FindByZone(ZoneID) != null;
		}

		/// <summary>Whether the expelled-from realm kept ground the founder could walk back to.</summary>
		public bool ExiledRealmKeptGround => Exiled
			&& ((ExiledSeat != null && ExiledSeat.ClaimedZones.Count > 0)
				|| ExiledSettlementTopology != null && ExiledSettlementTopology.Count > 0);

		/// <summary>
		/// Puts the founder out of the realm they founded.
		/// <para>
		/// Preconditions: a realm is founded, and either the regard has reached
		/// <see cref="RealmRegard.Repudiated"/> or <paramref name="Forced"/> is set. Side effects:
		/// the realm's identity, both of its cities and its whole standings ledger move to the
		/// exile slot, the Charter ability is taken from the founder, both chronicle registers
		/// record the day in their own words, and a modal states what has changed. Failure mode:
		/// returns false with a founder-facing refusal and changes nothing.
		/// </para>
		/// <para>
		/// Deliberately does <b>not</b> write reputation. The realm's grudge is whatever the
		/// founder's own deeds already put in the engine's reputation cell; manufacturing a worse
		/// one here would turn every citizen hostile and wall off the return path, which is the one
		/// thing this feature may not do.
		/// </para>
		/// </summary>
		/// <param name="Deed">The clause naming what was counted against the founder, from
		/// <see cref="KingdomExileRules.DeedClause"/>. Empty takes the unnamed-deed clause.</param>
		/// <param name="Forced">True for the debug path, which skips the regard requirement and
		/// nothing else.</param>
		/// <param name="Refusal">Founder-facing reason, or empty on success.</param>
		/// <returns>True if the founder was put out.</returns>
		public bool Exile(string Deed, bool Forced, out string Refusal)
		{
			Refusal = "";
			if (KingdomConstruction.HasNonterminalRoutedInputAuthority(this,
				out string custodyFailure))
			{
				Refusal = custodyFailure;
				return false;
			}
			if (ExiledRealmArchive != null &&
				(ExiledRealmArchive.Phase == KingdomRealmArchivePhase.TradeClosed ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.MirrorsPublished ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.ChronicleFrozen ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.ChronicleCleared ||
				 ExiledRealmArchive.Phase == KingdomRealmArchivePhase.Resetting))
			{
				return ContinueExileTransition(out Refusal);
			}
			ExileVerdict verdict = KingdomExileRules.JudgeExile(Founded, Exiled, KingdomExileRules.ClassifyRegard(FounderRegard()), Forced);
			if (verdict != ExileVerdict.Warranted)
			{
				Refusal = ExileRefusal(verdict);
				return false;
			}
			string realmName = KingdomDisplayName;
			string deed = string.IsNullOrEmpty(Deed) ? KingdomExileRules.DeedClause(null) : Deed;
			int cities = SettlementCount;
			string chronicleRegistry;
			string chronicleFault;
			string archiveFailure;
			List<string> exactSettlements;
			long proposedTick = The.Game.TimeTicks;
			if (!KingdomChronicle.TryCaptureRealmRegistry(out chronicleRegistry,
				out chronicleFault, out archiveFailure) ||
				!TryExactSettlementIds(RequirePublishedClaims: true,
					out exactSettlements, out archiveFailure))
			{
				Refusal = "The realm's exact history cannot be archived: " + archiveFailure + ".";
				return false;
			}
			List<string> authoritySettlements;
			if (!TryRetainedSettlementIds(RequirePublishedClaims: true,
				IncludePending: false, out authoritySettlements, out archiveFailure))
			{
				Refusal = "The realm's retained authority topology cannot be archived: " +
					archiveFailure + ".";
				return false;
			}
			// A save may cut after Trade atomically unbound the realm but before Core published its
			// archive. Authenticate that exact receipt first and reuse its original close tick; the
			// current wall clock is never substitute authority on retry.
			if (TradeBook != null && !TradeBook.IdentityBound &&
				!KingdomTradeRules.TryAuthenticateExactExileClosedTick(TradeBook, RealmId,
					authoritySettlements, out proposedTick, out archiveFailure))
			{
				Refusal = "The settled Trade exile receipt cannot be authenticated: " +
					archiveFailure + ".";
				return false;
			}
			if (!KingdomRealmArchive.TryCapture(this, chronicleRegistry, chronicleFault,
				proposedTick, deed, out KingdomRealmArchive archive, out archiveFailure))
			{
				Refusal = "The realm graph cannot be captured exactly: " + archiveFailure + ".";
				return false;
			}
			if (!ExactArchivedSettlements(archive.RealmId, archive.Seat,
				archive.SettlementTopology,
				archive.SettlementIds) || !archive.CurrentGraphMatches(this, out archiveFailure) ||
				!TryExactSettlementIds(RequirePublishedClaims: true,
					out List<string> preTradeSettlements, out archiveFailure) ||
				!ExactStringRows(preTradeSettlements, exactSettlements) ||
				!ExactStringRows(preTradeSettlements, archive.SettlementIds))
			{
				Refusal = "The complete realm graph or city identity set changed during archive preparation: " +
					(archiveFailure ?? "exact topology differs") + ".";
				return false;
			}
			// Trade is the first mutating boundary. Its detached preflight either refuses with
			// the entire Core/Trade graph unchanged, or atomically replaces only TradeBook with
			// the exact old-realm receipt. No Chronicle callback or exile mirror exists before it.
			if (!KingdomTrade.TryOnExile(this, proposedTick, archive.RealmId,
				authoritySettlements, out long settledTick, out archiveFailure))
			{
				Refusal = "Trade could not close the exact realm; no realm state was changed: " +
					archiveFailure;
				return false;
			}
			if (settledTick < 0L || (TradeBook == null || TradeBook.IdentityBound) ||
				!KingdomTradeRules.TryAuthenticateExactExileClosedTick(TradeBook, archive.RealmId,
					authoritySettlements, out long provedTick, out archiveFailure) ||
				provedTick != settledTick || !archive.CurrentGraphMatches(this, out archiveFailure) ||
				!TryExactSettlementIds(RequirePublishedClaims: true,
					out List<string> postTradeSettlements, out archiveFailure) ||
				!ExactStringRows(postTradeSettlements, exactSettlements) ||
				!ExactStringRows(postTradeSettlements, archive.SettlementIds))
			{
				Refusal = "Trade closed, but its exact settled tick or unchanged Core graph cannot be reproved: " +
					archiveFailure + ".";
				return false;
			}
			archive.ClosedTick = settledTick;
			archive.Phase = KingdomRealmArchivePhase.TradeClosed;
			ExiledRealmArchive = archive;
			if (!ContinueExileTransition(out Refusal))
			{
				return false;
			}
			KingdomLog.Log("exile: " + ExiledFactionName + " (" + cities + " cities, " + ExiledStandings.Count + " standings) put the founder out at regard " + ExiledRealmRegard() + "; deed=" + deed);
			Popup.Show(KingdomExileRules.ExileNotice(
				KingdomPresentation.Rich(realmName), deed, cities));
			return true;
		}

	}
}
