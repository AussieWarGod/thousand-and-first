using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomHappeningLifecycleRules
	{
		private static void WriteOperation(BinaryWriter writer,
			KingdomHappeningOperation operation, int version)
		{
			writer.Write(operation.Sequence);
			WriteString(writer, operation.EventId);
			writer.Write((byte)operation.Kind);
			writer.Write((byte)operation.Phase);
			writer.Write(operation.EventTick);
			writer.Write(operation.StartedTick);
			writer.Write(operation.UpdatedTick);
			writer.Write(operation.HoldUntilTick);
			writer.Write(operation.SubjectA);
			writer.Write(operation.SubjectB);
			writer.Write(operation.Outcome);
			WriteString(writer, operation.SettlementId);
			WriteString(writer, operation.ZoneId);
			WriteString(writer, operation.FixtureObjectId);
			WriteString(writer, operation.FixtureBlueprint);
			writer.Write(operation.FixtureX);
			writer.Write(operation.FixtureY);
			writer.Write(operation.Physical);
			writer.Write(operation.ExternalSemantic);
			writer.Write(operation.Attended);
			if (version >= CurrentVersion) writer.Write(operation.FixtureRestored);
			WriteString(writer, operation.ChronicleAttended);
			WriteString(writer, operation.ChronicleUnattended);
			WriteString(writer, operation.LedgerAttended);
			WriteString(writer, operation.LedgerUnattended);
			WriteString(writer, operation.MessageAttended);
			WriteString(writer, operation.MessageUnattended);
			WriteString(writer, operation.Effect);
			WriteString(writer, operation.DisplayName);
			WriteString(writer, operation.PlanQuote);
			writer.Write((byte)operation.ChronicleState);
			writer.Write((byte)operation.ToldState);
			writer.Write((byte)operation.EffectState);
			writer.Write((byte)operation.LedgerState);
			writer.Write((byte)operation.MessageState);
			writer.Write(operation.Participants.Length);
			for (int i = 0; i < operation.Participants.Length; i++)
				WriteParticipant(writer, operation.Participants[i], version);
		}

		private static KingdomHappeningOperation ReadOperation(BinaryReader reader, int version)
		{
			int sequence = reader.ReadInt32();
			string eventId = ReadString(reader);
			KingdomPhysicalHappeningKind kind = ReadEnum<KingdomPhysicalHappeningKind>(reader);
			KingdomHappeningLifecyclePhase phase = ReadEnum<KingdomHappeningLifecyclePhase>(reader);
			long eventTick = reader.ReadInt64();
			long started = reader.ReadInt64();
			long updated = reader.ReadInt64();
			long hold = reader.ReadInt64();
			int subjectA = reader.ReadInt32();
			int subjectB = reader.ReadInt32();
			int outcome = reader.ReadInt32();
			string settlementId = ReadString(reader);
			string zoneId = ReadString(reader);
			string fixtureId = ReadString(reader);
			string fixtureBlueprint = ReadString(reader);
			int fixtureX = reader.ReadInt32();
			int fixtureY = reader.ReadInt32();
			bool physical = ReadBool(reader);
			bool external = ReadBool(reader);
			bool attended = ReadBool(reader);
			bool fixtureRestored = version >= CurrentVersion ? ReadBool(reader) : !physical;
			string chronicleAttended = ReadString(reader);
			string chronicleUnattended = ReadString(reader);
			string ledgerAttended = ReadString(reader);
			string ledgerUnattended = ReadString(reader);
			string messageAttended = ReadString(reader);
			string messageUnattended = ReadString(reader);
			string effect = ReadString(reader);
			string display = ReadString(reader);
			string plan = ReadString(reader);
			KingdomHappeningSinkState chronicle = ReadEnum<KingdomHappeningSinkState>(reader);
			KingdomHappeningSinkState told = ReadEnum<KingdomHappeningSinkState>(reader);
			KingdomHappeningSinkState effectState = ReadEnum<KingdomHappeningSinkState>(reader);
			KingdomHappeningSinkState ledger = ReadEnum<KingdomHappeningSinkState>(reader);
			KingdomHappeningSinkState message = ReadEnum<KingdomHappeningSinkState>(reader);
			int count = reader.ReadInt32();
			if (count < 0 || count > MaxParticipants) throw new InvalidDataException();
			KingdomHappeningParticipant[] participants = new KingdomHappeningParticipant[count];
			for (int i = 0; i < count; i++) participants[i] = ReadParticipant(reader, version);
			return new KingdomHappeningOperation(sequence, eventId, kind, phase, eventTick,
				started, updated, hold, subjectA, subjectB, outcome, settlementId, zoneId,
				fixtureId, fixtureBlueprint, fixtureX, fixtureY, physical, external, attended,
				fixtureRestored,
				chronicleAttended, chronicleUnattended, ledgerAttended, ledgerUnattended,
				messageAttended, messageUnattended, effect, display, plan, participants,
				chronicle, told, effectState, ledger, message);
		}

		private static void WriteParticipant(BinaryWriter writer,
			KingdomHappeningParticipant participant, int version)
		{
			writer.Write(participant.ResidentId);
			WriteString(writer, participant.ObjectId);
			WriteString(writer, participant.Name);
			WriteString(writer, participant.Home);
			WriteString(writer, participant.Anchor);
			writer.Write(participant.OriginalX);
			writer.Write(participant.OriginalY);
			writer.Write(participant.TargetX);
			writer.Write(participant.TargetY);
			writer.Write(participant.PostWorkId);
			writer.Write(participant.PostKind);
			writer.Write(participant.Wanders);
			writer.Write(participant.WandersRandomly);
			writer.Write(participant.Staying);
			if (version >= CurrentVersion) writer.Write(participant.Restored);
		}

		private static KingdomHappeningParticipant ReadParticipant(BinaryReader reader, int version)
		{
			return new KingdomHappeningParticipant(reader.ReadInt32(), ReadString(reader),
				ReadString(reader), ReadString(reader), ReadString(reader), reader.ReadInt32(),
				reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
				reader.ReadInt32(), ReadBool(reader), ReadBool(reader), ReadBool(reader),
				version >= CurrentVersion && ReadBool(reader));
		}

		private static bool ReadBool(BinaryReader reader)
		{
			byte value = reader.ReadByte();
			if (value > 1) throw new InvalidDataException();
			return value == 1;
		}

		private static void WriteString(BinaryWriter writer, string value)
		{
			byte[] bytes = StrictUtf8.GetBytes(value ?? "");
			if (bytes.Length > MaxStringBytes) throw new InvalidDataException();
			writer.Write(bytes.Length);
			writer.Write(bytes);
		}

		private static string ReadString(BinaryReader reader)
		{
			int count = reader.ReadInt32();
			if (count < 0 || count > MaxStringBytes) throw new InvalidDataException();
			byte[] bytes = reader.ReadBytes(count);
			if (bytes.Length != count) throw new EndOfStreamException();
			return StrictUtf8.GetString(bytes);
		}

		private static T ReadEnum<T>(BinaryReader reader) where T : struct
		{
			T value = (T)Enum.ToObject(typeof(T), reader.ReadByte());
			if (!Enum.IsDefined(typeof(T), value)) throw new InvalidDataException();
			return value;
		}
	}
}
