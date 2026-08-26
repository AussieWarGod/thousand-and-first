using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		public static void WriteLifecycle(BinaryWriter Writer, KingdomLifecycleBook Book)
		{
			WriteLifecycleCore(Writer, Book, KingdomLifecycleRules.CurrentFormatVersion,
				IncludeGrowth: true);
		}

		#if TAF_TESTS
		internal static void WriteLifecycleV5Fixture(BinaryWriter Writer,
			KingdomLifecycleBook Book)
		{
			WriteLifecycleCore(Writer, Book, KingdomLifecycleRules.LegacyLifecycleFormatVersion,
				IncludeGrowth: false);
		}

		internal static void WriteLifecycleV6Fixture(BinaryWriter Writer,
			KingdomLifecycleBook Book)
		{
			WriteLifecycleCore(Writer, Book, KingdomLifecycleRules.PreviousLifecycleFormatVersion,
				IncludeGrowth: true);
		}

		internal static void WriteLifecycleV7Fixture(BinaryWriter Writer,
			KingdomLifecycleBook Book)
		{
			WriteLifecycleCore(Writer, Book,
				KingdomLifecycleRules.RaidLedgerLifecycleFormatVersion, IncludeGrowth: true);
		}

		internal static byte[] WriteRaidLedgerV1Fixture(KingdomRaidLedger ledger)
		{
			if (!KingdomRaidIncidentRules.ValidLedger(ledger))
				throw new InvalidDataException("raid fixture is malformed");
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
			{
				writer.Write(1); WriteRaidLedgerV1Body(writer, ledger); writer.Flush();
				return stream.ToArray();
			}
		}

		internal static byte[] WriteRaidLedgerV2Fixture(KingdomRaidLedger ledger)
		{
			if (!KingdomRaidIncidentRules.ValidLedger(ledger))
				throw new InvalidDataException("raid fixture is malformed");
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
			{
				WriteRaidLedgerV2(writer, ledger); writer.Flush(); return stream.ToArray();
			}
		}

		internal static byte[] WriteRaidLedgerFixture(KingdomRaidLedger ledger)
		{
			if (!KingdomRaidIncidentRules.ValidLedger(ledger))
				throw new InvalidDataException("raid fixture is malformed");
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
			{
				WriteRaidLedger(writer, ledger); writer.Flush(); return stream.ToArray();
			}
		}

		internal static KingdomRaidLedger ReadRaidLedgerFixture(byte[] bytes)
		{
			using (MemoryStream stream = new MemoryStream(bytes ?? throw new ArgumentNullException("bytes"), false))
			using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
			{
				KingdomRaidLedger ledger = ReadRaidLedger(reader);
				if (stream.Position != stream.Length || !KingdomRaidIncidentRules.ValidLedger(ledger))
					throw new InvalidDataException("raid fixture has trailing or invalid state");
				return ledger;
			}
		}
		#endif

		private static void WriteLifecycleCore(BinaryWriter Writer, KingdomLifecycleBook Book,
			int WireVersion, bool IncludeGrowth)
		{
			if (Writer == null || Book == null || Book.WireRejected
				|| Book.FormatVersion != KingdomLifecycleRules.CurrentFormatVersion)
				throw new InvalidDataException("lifecycle authority is not writable");
			EnsureCount(Book.Resources, KingdomLifecycleRules.MaxResourceRows, "resource rows");
			EnsureCount(Book.RecentProofs, KingdomLifecycleRules.MaxRecentProofs, "proof rows");
			if (!KingdomRaidIncidentRules.ValidLedger(Book.RaidLedger))
				throw new InvalidDataException("raid ledger is malformed");
			EnsureOuterResourceKinds(Book.Resources, Book.PlainGuest, Book.NotableGuest,
				Book.Raid, Book.Petition);
			Writer.Write(LifecycleMagic);
			Writer.Write(WireVersion);
			Writer.Write(Book.LegacyIdentity);
			WriteString(Writer, Book.LegacyMigrationKey, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.Quarantined);
			WriteString(Writer, Book.Fault, KingdomLifecycleRules.MaxTextBytes);
			WriteString(Writer, Book.SettlementId, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.IdentityBound);
			WriteString(Writer, Book.IdentityProof, KingdomLifecycleRules.MaxIdBytes);
			Writer.Write(Book.PlainGuestNextSequence);
			Writer.Write(Book.PlainGuestRetiredThrough);
			Writer.Write(Book.NotableGuestNextSequence);
			Writer.Write(Book.NotableGuestRetiredThrough);
			Writer.Write(Book.RaidNextSequence);
			Writer.Write(Book.RaidRetiredThrough);
			Writer.Write(Book.PetitionNextSequence);
			Writer.Write(Book.PetitionRetiredThrough);
			WriteOption(Writer, Book.LocusOption, Book.LocusOptionTick);
			WriteOption(Writer, Book.NotableOption, Book.NotableOptionTick);
			WriteOption(Writer, Book.RaidOption, Book.RaidOptionTick);
			WriteOption(Writer, Book.PetitionOption, Book.PetitionOptionTick);
			WriteOperation(Writer, Book.PlainGuest, WireVersion);
			WriteOperation(Writer, Book.NotableGuest, WireVersion);
			WriteOperation(Writer, Book.Raid, WireVersion);
			WriteOperation(Writer, Book.Petition, WireVersion);
			Writer.Write(Book.Resources.Count);
			for (int i = 0; i < Book.Resources.Count; i++) WriteResource(Writer, Book.Resources[i]);
			Writer.Write(Book.RecentProofs.Count);
			for (int i = 0; i < Book.RecentProofs.Count; i++) WriteProof(Writer, Book.RecentProofs[i]);
			if (WireVersion >= KingdomLifecycleRules.CurrentFormatVersion)
				WriteRaidLedger(Writer, Book.RaidLedger);
			else if (WireVersion >= KingdomLifecycleRules.RaidLedgerLifecycleFormatVersion)
				WriteRaidLedgerV2(Writer, Book.RaidLedger);
			if (IncludeGrowth)
			{
				byte[] payload = GrowthPayloadForWrite(Book.Growth);
				Writer.Write(payload.Length);
				Writer.Write(payload, 0, payload.Length);
			}
		}
	}
}
