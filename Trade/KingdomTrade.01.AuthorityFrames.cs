using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		public static bool Enabled => XRL.UI.Options.GetOption("r_TAF_OptionTrade") != "No";

		private static bool TryEnter(KingdomSystem System, out TradeLease Lease)
		{
			lock (InFlightSync)
			{
				if (System == null || InFlight != null)
				{
					Lease = null;
					return false;
				}
				Lease = new TradeLease { System = System };
				InFlight = Lease;
				return true;
			}
		}

		private static bool BindOperationSettlement(KingdomSystem System,
			KingdomTradeBook Book, KingdomTradeOperation Operation, Zone Z)
		{
			if (System == null || Book == null || Operation == null || Z == null
				|| !System.Founded || System.Ledger == null
				|| System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID)
				|| System.City == null || System.City.ZoneIds == null
				|| !System.City.ZoneIds.Contains(Z.ZoneID)
				|| !KingdomTradeRules.IdentityContainsSettlement(Book, System.City.SettlementId)
				|| !KingdomTradeRules.ValidName(System.SeatName)) return false;
			Operation.ZoneId = Z.ZoneID;
			Operation.SettlementName = System.SeatName;
			Operation.SettlementId = System.City.SettlementId;
			return KingdomTradeRules.ValidId(Operation.SettlementId);
		}

		private static bool TryBindFrame(KingdomSystem System, KingdomTradeBook Book,
			KingdomTradeOperation Operation, Zone Z, out TradeLiveFrame Frame)
		{
			Frame = null;
			if (System == null || Book == null || !KingdomTradeRules.BookUsable(Book)
				|| !ReferenceEquals(System.TradeBook, Book) || Book.Charters == null
				|| System.Ledger == null || System.Ledger.Notes == null
				|| System.ClaimedZones == null || System.City == null
				|| System.Standings == null) return false;
			TradeLiveFrame frame = new TradeLiveFrame
			{
				System = System,
				Book = Book,
				Operation = Operation,
				Charters = Book.Charters,
				WaterLegs = Operation?.WaterLegs,
				MaterialOutputs = Operation?.MaterialOutputs,
				WaterRows = Operation?.WaterLegs?.ToArray(),
				MaterialRows = Operation?.MaterialOutputs?.ToArray(),
				ProjectionRows = Book.Projections,
				ProjectionRowValues = CaptureProjectionRows(Book.Projections),
				Manifest = CaptureManifest(Book.Manifest),
				RetainedEscrow = Book.RetainedEscrowDrams,
				LegacyProjectionId = Book.ActiveProjectionId,
				LegacyProjectionObjectId = Book.ActiveProjectionObjectId,
				RealmId = Book.RealmId,
				Zone = Z,
				SettlementId = Operation == null ? System.City.SettlementId
					: Operation.SettlementId,
					SettlementName = Operation == null ? System.SeatName
						: Operation.SettlementName,
					KeepersRoster = System.KeepersRoster ?? "",
				City = System.City,
				Ledger = System.Ledger,
				LedgerNotes = System.Ledger.Notes,
				LedgerNoteRows = System.Ledger.Notes.ToArray(),
				LedgerDelivered = System.Ledger.Delivered,
				Standings = System.Standings,
				StandingRows = new Dictionary<string, int>(System.Standings),
				ClaimedZones = System.ClaimedZones,
				ClaimedZoneRows = System.ClaimedZones.ToArray(),
				CityZones = System.City.ZoneIds,
				CityZoneRows = System.City.ZoneIds?.ToArray(),
				SettlementIds = Book.SettlementIds,
				SettlementIdRows = Book.SettlementIds?.ToArray()
			};
			if (Operation != null)
			{
				if (!ReferenceEquals(Book.OpenOperation, Operation) || Z == null
					|| System.Ledger == null || System.ClaimedZones == null) return false;
				if (!ExactSettlement(frame)) return false;
			}
			Frame = frame;
			return true;
		}

		private static bool ExactSettlement(TradeLiveFrame Frame)
		{
			if (Frame == null) return false;
			KingdomSystem system = Frame.System;
			bool common = system != null && system.Founded
				&& KingdomTradeRules.ValidId(Frame.SettlementId)
				&& KingdomTradeRules.ValidName(Frame.SettlementName)
				&& ReferenceEquals(system.Ledger, Frame.Ledger)
				&& ReferenceEquals(system.ClaimedZones, Frame.ClaimedZones)
				&& ExactStrings(Frame.ClaimedZones, Frame.ClaimedZoneRows)
				&& ReferenceEquals(system.City, Frame.City)
				&& ReferenceEquals(Frame.City.ZoneIds, Frame.CityZones)
				&& ExactStrings(Frame.CityZones, Frame.CityZoneRows)
				&& ReferenceEquals(Frame.Book.SettlementIds, Frame.SettlementIds)
				&& ExactStrings(Frame.SettlementIds, Frame.SettlementIdRows)
				&& ReferenceEquals(system.Standings, Frame.Standings)
				&& ExactDictionary(Frame.Standings, Frame.StandingRows)
				&& ExactLedger(Frame)
					&& string.Equals(system.SeatName, Frame.SettlementName,
						StringComparison.Ordinal)
					&& string.Equals(system.KeepersRoster ?? "", Frame.KeepersRoster,
						StringComparison.Ordinal)
				&& string.Equals(Frame.City.SettlementId, Frame.SettlementId,
					StringComparison.Ordinal);
			if (!common || Frame.Operation == null) return common;
			return Frame.Zone != null && Frame.ClaimedZones.Contains(Frame.Zone.ZoneID)
				&& Frame.City.ZoneIds != null && Frame.City.ZoneIds.Contains(Frame.Zone.ZoneID)
				&& string.Equals(Frame.Zone.ZoneID, Frame.Operation.ZoneId,
					StringComparison.Ordinal)
				&& string.Equals(Frame.Operation.SettlementName, Frame.SettlementName,
					StringComparison.Ordinal)
				&& string.Equals(Frame.Operation.SettlementId, Frame.SettlementId,
					StringComparison.Ordinal);
		}

		private static bool ExactLedger(TradeLiveFrame Frame)
		{
			if (Frame == null || Frame.Ledger == null
				|| !ReferenceEquals(Frame.System?.Ledger, Frame.Ledger)
				|| !ReferenceEquals(Frame.Ledger.Notes, Frame.LedgerNotes)
				|| Frame.LedgerNotes == null || Frame.LedgerNoteRows == null
				|| Frame.Ledger.Delivered != Frame.LedgerDelivered
				|| Frame.LedgerNotes.Count != Frame.LedgerNoteRows.Length) return false;
			for (int i = 0; i < Frame.LedgerNoteRows.Length; i++)
				if (!string.Equals(Frame.LedgerNotes[i], Frame.LedgerNoteRows[i],
					StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ExactDictionary(Dictionary<string, int> Current,
			Dictionary<string, int> Expected)
		{
			if (Current == null || Expected == null || Current.Count != Expected.Count)
				return false;
			foreach (KeyValuePair<string, int> pair in Expected)
			{
				int value;
				if (!Current.TryGetValue(pair.Key, out value) || value != pair.Value) return false;
			}
			return true;
		}

		private static bool ExactStandingWithOverride(TradeLiveFrame Frame,
			string Faction, int Value)
		{
			if (Frame?.Standings == null || Frame.StandingRows == null
				|| !ReferenceEquals(Frame.System.Standings, Frame.Standings)) return false;
			int expectedCount = Frame.StandingRows.ContainsKey(Faction)
				? Frame.StandingRows.Count : Frame.StandingRows.Count + 1;
			if (Frame.Standings.Count != expectedCount) return false;
			foreach (KeyValuePair<string, int> pair in Frame.StandingRows)
			{
				int current;
				int expected = string.Equals(pair.Key, Faction, StringComparison.Ordinal)
					? Value : pair.Value;
				if (!Frame.Standings.TryGetValue(pair.Key, out current)
					|| current != expected) return false;
			}
			int after;
			return Frame.Standings.TryGetValue(Faction, out after) && after == Value;
		}

		private static bool ExactAuthority(TradeLiveFrame Frame,
			KingdomTradePhase ExpectedPhase)
		{
			if (Frame == null || Frame.System == null || Frame.Book == null
				|| !ReferenceEquals(Frame.System.TradeBook, Frame.Book)
				|| !ReferenceEquals(Frame.Book.Charters, Frame.Charters)
				|| !string.Equals(Frame.Book.RealmId, Frame.RealmId,
					StringComparison.Ordinal) || !KingdomTradeRules.BookUsable(Frame.Book)
				|| !ExactProjectionRows(Frame) || !ExactBookDomain(Frame)) return false;
			if (!ExactSettlement(Frame)) return false;
			if (Frame.Operation == null) return true;
			return ReferenceEquals(Frame.Book.OpenOperation, Frame.Operation)
				&& ReferenceEquals(Frame.Operation.WaterLegs, Frame.WaterLegs)
				&& ReferenceEquals(Frame.Operation.MaterialOutputs, Frame.MaterialOutputs)
				&& ExactReceiptRows(Frame)
				&& Frame.Operation.Phase == ExpectedPhase && ExactSettlement(Frame);
		}

		private static CallbackWitness CaptureCallbackWitness(TradeLiveFrame Frame)
		{
			try
			{
				KingdomTradeBook book = Frame?.Book;
				if (book == null || Frame.System == null || Frame.City?.ZoneIds == null
					|| Frame.System.ClaimedZones == null || book.SettlementIds == null
					|| book.Charters == null || book.Projections == null || book.RecentProofs == null
					|| book.CompactedProofs == null
					|| book.Archives == null || book.Incidents == null) return null;
				return new CallbackWitness
				{
					Seal = KingdomTradeRules.CaptureAuthoritySeal(book,
						Frame.System.ClaimedZones, Frame.City.ZoneIds),
					AuthorityBytes = KingdomTradeCodec.EncodePayload(book),
					ClaimedZones = Frame.System.ClaimedZones,
					ClaimedRows = Frame.System.ClaimedZones.ToArray(),
					CityZones = Frame.City.ZoneIds,
					CityZoneRows = Frame.City.ZoneIds.ToArray(),
					SettlementIds = book.SettlementIds,
					SettlementRows = book.SettlementIds.ToArray(),
					Charters = book.Charters, CharterRows = book.Charters.ToArray(),
					Projections = book.Projections, ProjectionRows = book.Projections.ToArray(),
					Proofs = book.RecentProofs, ProofRows = book.RecentProofs.ToArray(),
					CompactedProofs = book.CompactedProofs,
					CompactedProofRows = book.CompactedProofs.ToArray(),
					Archives = book.Archives, ArchiveRows = book.Archives.ToArray(),
					Incidents = book.Incidents, IncidentRows = book.Incidents.ToArray(),
					Manifest = book.Manifest, Operation = book.OpenOperation,
					Standing = book.OpenOperation?.Standing, Outbox = book.OpenOperation?.Outbox
				};
			}
			catch { return null; }
		}

		private static bool ExactCallbackWitness(TradeLiveFrame Frame, CallbackWitness Witness)
		{
			if (Frame == null || Witness == null || Frame.Book == null
				|| !KingdomTradeRules.ExactAuthoritySeal(Frame.Book,
					Frame.System?.ClaimedZones, Frame.City?.ZoneIds, Witness.Seal)
				|| !ReferenceEquals(Frame.System?.ClaimedZones, Witness.ClaimedZones)
				|| !ReferenceEquals(Frame.City?.ZoneIds, Witness.CityZones)
				|| !ReferenceEquals(Frame.Book.SettlementIds, Witness.SettlementIds)
				|| !ReferenceEquals(Frame.Book.Charters, Witness.Charters)
				|| !ReferenceEquals(Frame.Book.Projections, Witness.Projections)
				|| !ReferenceEquals(Frame.Book.RecentProofs, Witness.Proofs)
				|| !ReferenceEquals(Frame.Book.CompactedProofs, Witness.CompactedProofs)
				|| !ReferenceEquals(Frame.Book.Archives, Witness.Archives)
				|| !ReferenceEquals(Frame.Book.Incidents, Witness.Incidents)
				|| !ReferenceEquals(Frame.Book.Manifest, Witness.Manifest)
				|| !ReferenceEquals(Frame.Book.OpenOperation, Witness.Operation)
				|| !ReferenceEquals(Frame.Book.OpenOperation?.Standing, Witness.Standing)
				|| !ReferenceEquals(Frame.Book.OpenOperation?.Outbox, Witness.Outbox)
				|| !ExactStrings(Witness.ClaimedZones, Witness.ClaimedRows)
				|| !ExactStrings(Witness.CityZones, Witness.CityZoneRows)
				|| !ExactStrings(Witness.SettlementIds, Witness.SettlementRows)
				|| !ExactReferences(Witness.Charters, Witness.CharterRows)
				|| !ExactReferences(Witness.Projections, Witness.ProjectionRows)
				|| !ExactReferences(Witness.Proofs, Witness.ProofRows)
				|| !ExactReferences(Witness.CompactedProofs, Witness.CompactedProofRows)
				|| !ExactReferences(Witness.Archives, Witness.ArchiveRows)
				|| !ExactReferences(Witness.Incidents, Witness.IncidentRows)) return false;
			byte[] current;
			try { current = KingdomTradeCodec.EncodePayload(Frame.Book); }
			catch { return false; }
			if (current.Length != Witness.AuthorityBytes.Length) return false;
			for (int i = 0; i < current.Length; i++)
				if (current[i] != Witness.AuthorityBytes[i]) return false;
			return true;
		}

		private static bool ExactStrings(List<string> Current, string[] Expected)
		{
			if (Current == null || Expected == null || Current.Count != Expected.Length) return false;
			for (int i = 0; i < Expected.Length; i++)
				if (!string.Equals(Current[i], Expected[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ExactBytes(byte[] Current, byte[] Expected)
		{
			if (Current == null || Expected == null || Current.Length != Expected.Length) return false;
			for (int i = 0; i < Expected.Length; i++)
				if (Current[i] != Expected[i]) return false;
			return true;
		}

	}
}
