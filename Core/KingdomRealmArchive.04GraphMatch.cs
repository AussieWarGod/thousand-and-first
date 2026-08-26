using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
		public void Quarantine(string Failure)
		{
			Quarantined = true;
			Phase = KingdomRealmArchivePhase.Quarantined;
			Fault = Bound(Failure, 4096);
		}

		/// <summary>Exact full-graph comparison used after every engine callback. It compares
		/// values and rejects shared mutable references; identity/topology alone is insufficient
		/// because a callback can retain the same ids while replacing or editing a city graph.</summary>
		internal bool CurrentGraphMatches(KingdomSystem System, out string Failure)
		{
			bool swapped = ReturnSeat != null &&
				ReturnSeat.Phase == KingdomRealmCallbackPhase.Settled &&
				!string.Equals(ReturnSeat.BeforeEffect, ReturnSeat.AfterEffect,
					StringComparison.Ordinal);
			return CurrentGraphMatches(System, swapped, IgnoreChronicle: false, out Failure);
		}

		internal bool CurrentGraphMatchesAfterSeat(KingdomSystem System, bool Swapped,
			out string Failure)
		{
			return CurrentGraphMatches(System, Swapped, IgnoreChronicle: false, out Failure);
		}

		internal bool CurrentGraphMatchesExceptChronicle(KingdomSystem System,
			out string Failure)
		{
			bool swapped = ReturnSeat != null &&
				ReturnSeat.Phase == KingdomRealmCallbackPhase.Settled &&
				!string.Equals(ReturnSeat.BeforeEffect, ReturnSeat.AfterEffect,
					StringComparison.Ordinal);
			return CurrentGraphMatches(System, swapped, IgnoreChronicle: true, out Failure);
		}

		private bool CurrentGraphMatches(KingdomSystem System, bool SeatSwapped,
			bool IgnoreChronicle, out string Failure)
		{
			Failure = null;
			if (System == null || Quarantined ||
				!string.IsNullOrEmpty(System.IdentityFault) ||
				!string.IsNullOrEmpty(System.PendingSettlementId) ||
				!string.IsNullOrEmpty(System.PendingSettlementTransactionId) ||
				!string.IsNullOrEmpty(System.PendingSettlementZoneId) ||
				!string.IsNullOrEmpty(System.PendingSettlementAuthority) ||
				!string.Equals(System.RealmId, RealmId, StringComparison.Ordinal) ||
				!string.Equals(System.KingdomFactionName, FactionName, StringComparison.Ordinal) ||
				!string.Equals(System.KingdomDisplayName, DisplayName, StringComparison.Ordinal) ||
				System.RealmIdentityVersion != RealmIdentityVersion ||
				System.RealmIdentityOrigin != RealmIdentityOrigin ||
				!string.Equals(System.RealmIdentityTransactionId,
					RealmIdentityTransactionId, StringComparison.Ordinal) ||
				!string.Equals(System.RealmIdentityLegacyFaction,
					RealmIdentityLegacyFaction, StringComparison.Ordinal) ||
				System.RealmIdentityFoundedTick != RealmIdentityFoundedTick ||
				System.RealmIdentitySeedHigh != RealmIdentitySeedHigh ||
				System.RealmIdentitySeedLow != RealmIdentitySeedLow ||
				!string.Equals(System.RealmIdentityFirstClaimedZone,
					RealmIdentityFirstClaimedZone, StringComparison.Ordinal))
				return Refuse("current realm scalar identity differs from archive", out Failure);
			KingdomSettlement currentSeat;
			try { currentSeat = System.Capture(); }
			catch (Exception ex) { return Refuse(Bound(ex.Message, 512), out Failure); }
			KingdomSettlement expectedSeat = SeatSwapped ? Away : Seat;
			KingdomSettlement expectedAway = SeatSwapped ? Seat : Away;
			if (!KingdomArchivedSettlementCodec.ExactGraph(expectedSeat, currentSeat, out Failure) ||
				!KingdomArchivedSettlementCodec.ExactGraph(expectedAway, System.Away, out Failure) ||
				!KingdomArchivedSettlementCodec.ExactGraph(Seceded, System.Seceded, out Failure))
				return false;
			if (!ExactDictionary(Standings, System.Standings) ||
				ReferenceEquals(Standings, System.Standings))
				return Refuse("current standings differ from or alias archive", out Failure);
			if (IgnoreChronicle && (ReferenceEquals(ChronicleEntries, System.ChronicleEntries) ||
				ReferenceEquals(ChronicleEntries, System.OutsiderEntries) ||
				ReferenceEquals(OutsiderEntries, System.ChronicleEntries) ||
				ReferenceEquals(OutsiderEntries, System.OutsiderEntries)))
				return Refuse("current Chronicle registers alias archive evidence", out Failure);
			if (!ExactBindings(Bindings, System.Bindings) ||
				!ExactJobs(Jobs, System.Jobs) ||
				(!IgnoreChronicle &&
				 (!ExactStrings(ChronicleEntries, System.ChronicleEntries) ||
				  !ExactStrings(OutsiderEntries, System.OutsiderEntries))) ||
				!ExactHaul(Haul, System.Haul) ||
				!ExactCarry(CarryBook, System.CarryBook))
				return Refuse("current realm mutable graph differs from or aliases archive", out Failure);
			// IgnoreChronicle suppresses only the two value comparisons while their declared
			// callback is in flight. Chronicle roots remain in the reference proof so they
			// cannot alias a seat, registry, carry, haul, or opposite-realm root.
			object[] archivedRoots = { Seat, Away, Seceded, Standings, Bindings, Jobs,
				ChronicleEntries, OutsiderEntries, Haul, CarryBook };
			object[] liveRoots = { currentSeat, System.Away, System.Seceded, System.Standings,
				System.Bindings, System.Jobs, System.ChronicleEntries, System.OutsiderEntries,
				System.Haul, System.CarryBook };
			if (!KingdomArchivedSettlementCodec.DisjointMutableGraphs(archivedRoots, liveRoots,
				out Failure)) return false;
			if (SimulationSeedHigh != System.SimulationSeedHigh ||
				SimulationSeedLow != System.SimulationSeedLow ||
				ResidentCounter != System.ResidentCounter || LastSliceTick != System.LastSliceTick ||
				ReifyTick != System.ReifyTick || ReifyThirdsSpent != System.ReifyThirdsSpent ||
				ReifyHeavySpent != System.ReifyHeavySpent ||
				ReifyQuietUntilTick != System.ReifyQuietUntilTick ||
				DedicationCounter != System.DedicationCounter || RegardSpoken != System.RegardSpoken ||
				Dissent != System.Dissent || DissentSpoken != System.DissentSpoken ||
				LastDissentTick != System.LastDissentTick || DeclaredCreed != System.DeclaredCreed ||
				DishName != System.DishName || DishText != System.DishText ||
				DishStaple != System.DishStaple || DishSource != System.DishSource ||
				LastRiteTick != System.LastRiteTick || LastSoulRiteTick != System.LastSoulRiteTick ||
				SecededTick != System.SecededTick)
				return Refuse("current realm counters differ from archive", out Failure);
			return true;
		}

		internal bool ExactMirrors(string MirrorFaction, string MirrorDisplay,
			string MirrorDeed, long MirrorTick, KingdomSettlement MirrorSeat,
			KingdomSettlement MirrorAway, Dictionary<string, int> MirrorStandings,
			out string Failure)
		{
			Failure = null;
			if (!string.Equals(FactionName, MirrorFaction, StringComparison.Ordinal) ||
				!string.Equals(DisplayName, MirrorDisplay, StringComparison.Ordinal) ||
				!string.Equals(ExileDeed, MirrorDeed, StringComparison.Ordinal) ||
				ClosedTick != MirrorTick || !KingdomArchivedSettlementCodec.ExactGraph(Seat,
					MirrorSeat, out Failure) || !KingdomArchivedSettlementCodec.ExactGraph(Away,
					MirrorAway, out Failure)) return false;
			if (ReferenceEquals(Standings, MirrorStandings) ||
				!ExactDictionary(Standings, MirrorStandings))
				return Refuse("exile standings mirror differs from or aliases archive", out Failure);
			object[] archivedRoots = { Seat, Away, Standings };
			object[] mirrorRoots = { MirrorSeat, MirrorAway, MirrorStandings };
			if (!KingdomArchivedSettlementCodec.DisjointMutableGraphs(archivedRoots, mirrorRoots,
				out Failure)) return false;
			return true;
		}

		internal static bool TryCurrentGraphHash(KingdomSystem System, out string Hash,
			out string Failure)
		{
			Hash = null;
			Failure = null;
			if (System == null) { Failure = "current realm is absent"; return false; }
			try
			{
				KingdomSettlement seat = System.Capture();
				if (!KingdomArchivedSettlementCodec.TryEncode(seat, out byte[] seatBytes, out Failure) ||
					!KingdomArchivedSettlementCodec.TryEncode(System.Away, out byte[] awayBytes,
						out Failure) ||
					!KingdomArchivedSettlementCodec.TryEncode(System.Seceded, out byte[] secededBytes,
						out Failure) ||
					!TryCarryBytes(System.CarryBook, out byte[] carryBytes, out Failure)) return false;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(0x54414731); // TAG1
					WriteGraphBytes(writer, seatBytes); WriteGraphBytes(writer, awayBytes);
					WriteGraphBytes(writer, secededBytes); WriteGraphBytes(writer, carryBytes);
					WriteGraphString(writer, System.RealmId); WriteGraphString(writer, System.KingdomFactionName);
					WriteGraphString(writer, System.KingdomDisplayName);
					writer.Write(System.RealmIdentityVersion);
					writer.Write((byte)System.RealmIdentityOrigin);
					WriteGraphString(writer, System.RealmIdentityTransactionId);
					WriteGraphString(writer, System.RealmIdentityLegacyFaction);
					writer.Write(System.RealmIdentityFoundedTick); writer.Write(System.RealmIdentitySeedHigh);
					writer.Write(System.RealmIdentitySeedLow);
					WriteGraphString(writer, System.RealmIdentityFirstClaimedZone);
					WriteGraphString(writer, System.IdentityFault);
					WriteGraphString(writer, System.PendingSettlementId);
					WriteGraphString(writer, System.PendingSettlementTransactionId);
					WriteGraphString(writer, System.PendingSettlementZoneId);
					WriteGraphString(writer, System.PendingSettlementAuthority);
					writer.Write(System.SimulationSeedHigh); writer.Write(System.SimulationSeedLow);
					WriteGraphBindings(writer, System.Bindings); WriteGraphJobs(writer, System.Jobs);
					writer.Write(System.ResidentCounter); writer.Write(System.LastSliceTick);
					writer.Write(System.ReifyTick); writer.Write(System.ReifyThirdsSpent);
					writer.Write(System.ReifyHeavySpent); writer.Write(System.ReifyQuietUntilTick);
					writer.Write(System.DedicationCounter);
					WriteGraphDictionary(writer, System.Standings);
					WriteGraphStrings(writer, System.ChronicleEntries);
					WriteGraphStrings(writer, System.OutsiderEntries);
					writer.Write(System.RegardSpoken); writer.Write(System.Dissent);
					writer.Write(System.DissentSpoken); writer.Write(System.LastDissentTick);
					WriteGraphString(writer, System.DeclaredCreed); WriteGraphString(writer, System.DishName);
					WriteGraphString(writer, System.DishText); WriteGraphString(writer, System.DishStaple);
					WriteGraphString(writer, System.DishSource); writer.Write(System.LastRiteTick);
					writer.Write(System.LastSoulRiteTick); writer.Write(System.SecededTick);
					WriteGraphHaul(writer, System.Haul);
					writer.Flush();
					if (stream.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes * 4L)
						throw new InvalidDataException("Current realm graph exceeds proof cap.");
					using (global::System.Security.Cryptography.SHA256 sha =
						global::System.Security.Cryptography.SHA256.Create())
					{
						byte[] digest = sha.ComputeHash(stream.ToArray());
						StringBuilder text = new StringBuilder(64);
						for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
						Hash = text.ToString();
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message, 512);
				return false;
			}
		}

	}
}
