using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomFirstFeastRuntime
	{
		internal sealed class CityContext
		{
			internal KingdomCityBook Book;
			internal string SettlementId;
			internal string SettlementName;
			internal int IdentityVersion;
			internal KingdomIdentityOrigin IdentityOrigin;
			internal string FoundingTransactionId;
			internal long FoundedTick;
			internal string FirstClaimedZone;
		}

		internal static bool TryCurrentCity(KingdomSystem System, GameObject Founder,
			out CityContext Context, out string Failure)
		{
			Context = null;
			Failure = "Stand on the held ground of the city whose First Feast you mean to review.";
			Zone zone = Founder?.CurrentZone;
			if (System == null || !System.Founded || Founder == null || !Founder.IsPlayer()
				|| zone == null || !System.OwnedZone(zone.ZoneID)) return false;
			bool seated = System.ClaimedZones != null && System.ClaimedZones.Contains(zone.ZoneID);
			KingdomSettlement other = seated ? null : System.FindNonSeatSettlementByZone(zone.ZoneID);
			Context = seated ? new CityContext
			{
				Book = System.City, SettlementId = System.City?.SettlementId,
				SettlementName = System.SeatName,
				IdentityVersion = System.SettlementIdentityVersion,
				IdentityOrigin = System.SettlementIdentityOrigin,
				FoundingTransactionId = System.SettlementIdentityTransactionId,
				FoundedTick = System.SettlementIdentityFoundedTick,
				FirstClaimedZone = System.SettlementIdentityFirstClaimedZone
			} : new CityContext
			{
				Book = other?.City, SettlementId = other?.City?.SettlementId,
				SettlementName = other?.SettlementName,
				IdentityVersion = other == null ? 0 : other.SettlementIdentityVersion,
				IdentityOrigin = other == null ? KingdomIdentityOrigin.None
					: other.SettlementIdentityOrigin,
				FoundingTransactionId = other?.SettlementIdentityTransactionId,
				FoundedTick = other == null ? 0L : other.SettlementIdentityFoundedTick,
				FirstClaimedZone = other?.SettlementIdentityFirstClaimedZone
			};
			if (!ValidContext(System, Context))
			{
				Context = null;
				Failure = "The exact founding deed and current city identity cannot be reproved.";
				return false;
			}
			return true;
		}

		private static bool ValidContext(KingdomSystem System, CityContext C)
		{
			if (C?.Book == null || C.IdentityOrigin != KingdomIdentityOrigin.FoundingTransaction
				|| !KingdomIdentityRules.IsFoundingTransaction(C.FoundingTransactionId)) return false;
			KingdomIdentityFault fault;
			return KingdomIdentityRules.ReproveSettlement(C.SettlementId, System.RealmId,
				C.IdentityVersion, C.IdentityOrigin, C.FoundingTransactionId, C.FoundedTick,
				C.FirstClaimedZone, out fault) && System.SettlementIdForOwnedZone(
					C.FirstClaimedZone) == C.SettlementId
				&& KingdomExperienceRules.CivicText(C.SettlementName, true);
		}

		private static bool TryDeed(KingdomSystem System, CityContext Context,
			out KingdomFirstFeastDeed Deed, out string Failure)
		{
			Deed = null; Failure = null;
			if (!KingdomGuestFeastRuntime.TryJoinedAwaitingPractice(System,
				Context.SettlementId, out KingdomGuestFeastReceipt guest, out Failure)) return false;
			if (guest == null)
			{
				Failure = "The First Feast waits for this city's First Guest to join."; return false;
			}
			string terminalDigest = KingdomGuestFeastRules.TerminalDigest(guest);
			if (terminalDigest == null || !TryAdventureAfter(guest.GuestTerminalTick,
				out KingdomChronicleReceipt adventure, out Failure)) return false;
			KingdomFirstFeastDeed candidate = new KingdomFirstFeastDeed
			{
				SettlementId = Context.SettlementId, SettlementName = Context.SettlementName,
				DeedText = KingdomFirstFeastRules.AuthoredDeed, DeedTick = adventure.Updated,
				GuestTerminalReceiptId = guest.GrowthTerminalReceiptId,
				GuestTerminalDigest = terminalDigest, GuestTerminalTick = guest.GuestTerminalTick,
				AdventureEventId = adventure.EventId,
				AdventureFingerprint = adventure.Fingerprint
			};
			if (!KingdomFirstFeastRules.TryBuildDeedId(candidate, out candidate.DeedId))
			{
				Failure = "The later adventure deed cannot be bound to the joined guest."; return false;
			}
			Deed = candidate; return true;
		}

		private static bool TryAdventureAfter(long guestTick,
			out KingdomChronicleReceipt Adventure, out string Failure)
		{
			Adventure = null; Failure = null;
			KingdomChronicleRegistryFault parseFault = KingdomChronicleRegistryFault.None;
			List<KingdomChronicleReceipt> rows = null; bool migrated = false;
			if (!KingdomChronicle.TryCaptureRealmRegistry(out string registry, out string fault,
				out Failure) || !string.IsNullOrEmpty(fault)
				|| !KingdomChronicleReceiptRules.TryParseRegistry(registry,
					out rows, out migrated, out parseFault) || migrated)
			{
				Failure = Failure ?? "The realm Chronicle cannot prove a current later adventure deed ("
					+ parseFault + ")."; return false;
			}
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomChronicleReceipt row = rows[i];
				if (row == null || row.LegacyBlocked || row.Updated <= guestTick
					|| !KingdomChronicleReceiptRules.IsTerminal(row)
					|| string.IsNullOrEmpty(row.EventId) || row.Fingerprint == null
					|| row.EventId.StartsWith("taf:experience:first-feast:",
						StringComparison.Ordinal)) continue;
				if (Adventure == null || row.Updated < Adventure.Updated
					|| row.Updated == Adventure.Updated && string.CompareOrdinal(
						row.EventId, Adventure.EventId) < 0) Adventure = row;
			}
			if (Adventure != null) return true;
			Failure = "The First Feast waits for a later completed adventure deed."; return false;
		}

		private static bool TryCandidates(CityContext Context,
			out KingdomFirstFeastCandidate[] Candidates, out string Failure)
		{
			Candidates = null; Failure = null;
			if (!Context.Book.TryRead(out KingdomCityState state, out KingdomCityFault fault))
			{
				Failure = "The current resident roll cannot be read (" + fault + ")."; return false;
			}
			List<KingdomFirstFeastCandidate> rows = new List<KingdomFirstFeastCandidate>();
			for (int i = 0; i < state.ResidentCount; i++)
				if (state.TryResident(i, out KingdomResidentRow row)
					&& row.Standing == KingdomResidentStanding.Resident
					&& KingdomExperienceRules.CivicText(row.Name, true))
					rows.Add(new KingdomFirstFeastCandidate(row.ResidentId, row.Name));
			if (rows.Count < 2)
			{
				Failure = "Two exact standing residents are required to make this proposal.";
				return false;
			}
			Candidates = rows.ToArray(); return true;
		}

		private static bool ResidentAvailable(KingdomSystem System, string SettlementId,
			int ResidentId)
		{
			return KingdomResidents.TryResolveBoundBody(System, ResidentId, false,
				out GameObject _, out string zoneId)
				&& System.SettlementIdForOwnedZone(zoneId) == SettlementId;
		}
	}
}
