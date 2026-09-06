using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Self-naming codec: a malformed wire claiming this variant is refused, never decoded as
	/// another. STRUCTURAL law only; the live protocol and receipt are the witness's actual proofs.</summary>
	internal static partial class KingdomSubsidenceRungSaveSnapshotCodec
	{
		internal const int MaxWireChars = 524288;
		internal const int MaxStepWireChars = 131072;
		internal const int MaxRungWireChars = 65536;
		private const int MaxIdChars = 512;
		private const int MaxCoordinate = 1048576;
		private const int MaxWearValue = 1000;
		private const int MaxRegisterCount = 65536;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static bool Valid(KingdomSubsidenceRungSaveSnapshot value)
		{ return Valid(value, StepVersion(value?.StepWire)); }

		private static bool Valid(KingdomSubsidenceRungSaveSnapshot value, int version)
		{
			if (version != 1 && version != 2 || value == null || !CanonicalId(value.GameId) || !Text(value.ZoneId, 128, false, false)
				|| !Wire(value.StepWire, version == 1 ? LegacyStepWirePrefix : StepWirePrefix, MaxStepWireChars)
				|| !Wire(value.RungWire, RungWirePrefix, MaxRungWireChars)
				|| !Text(value.StepId, MaxIdChars, false, false) || !Digest(value.TellingDigest)
				|| value.Now < 0 || value.AnchorTick < 0 || value.DueTick < 0 || value.Sequence < 1
				|| value.LastSubsidenceTick < 0 || value.AnchorTick > value.DueTick
				|| value.LedgerDepartures < KingdomSubsidenceRungSaveSnapshot.AbsentCount
				|| value.Population != KingdomSubsidenceRungSaveSnapshot.SurvivorCount
				|| !Defined(typeof(GrowthStage), value.Stage)
				|| value.ChronicleCount < 0 || value.ChronicleCount > MaxRegisterCount
				|| value.OutsiderCount < 0 || value.OutsiderCount > MaxRegisterCount
				|| !ValidWork(value.Work) || !ValidRoof(value.Roof)
				|| !Bodies(value.ResidentIds, value.ObjectIds, KingdomSubsidenceRungSaveSnapshot.SurvivorCount)
				|| !Bodies(value.AbsentResidentIds, value.AbsentObjectIds,
					KingdomSubsidenceRungSaveSnapshot.AbsentCount)) return false;
			HashSet<int> residents = new HashSet<int>();
			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> carriers = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < value.ResidentIds.Count; i++)
				if (!residents.Add(value.ResidentIds[i]) || !objects.Add(value.ObjectIds[i]) || !carriers.Add(
					KingdomSubsidenceRungSaveBody.Compose(value.ResidentIds[i], value.ObjectIds[i]))) return false;
			for (int i = 0; i < value.AbsentResidentIds.Count; i++)
				if (!residents.Add(value.AbsentResidentIds[i]) || !objects.Add(value.AbsentObjectIds[i])) return false;
			// The roof must name one EXACT surviving pair, not a resident and a body drawn separately.
			return carriers.Contains(value.Roof.Carrier.Key) && !objects.Contains(value.Work.ObjectId);
		}

		internal static bool TryEncode(KingdomSubsidenceRungSaveSnapshot value, out string wire)
		{
			wire = null;
			int version = StepVersion(value?.StepWire);
			if (!Valid(value, version)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
				{
					writer.Write(Magic); writer.Write(version);
					WriteText(writer, value.GameId); WriteText(writer, value.ZoneId);
					WriteText(writer, value.StepWire); WriteText(writer, value.RungWire);
					WriteText(writer, value.StepId); WriteText(writer, value.TellingDigest);
					writer.Write(value.Now); writer.Write(value.AnchorTick); writer.Write(value.DueTick);
					writer.Write(value.Sequence); writer.Write(value.LastSubsidenceTick);
					writer.Write(value.LedgerDepartures); writer.Write(value.Population);
					writer.Write(value.Stage); writer.Write(value.ChronicleCount);
					writer.Write(value.OutsiderCount);
					WriteWork(writer, value.Work); WriteRoof(writer, value.Roof);
					writer.Write(KingdomSubsidenceRungSaveSnapshot.SurvivorCount);
					for (int i = 0; i < value.ResidentIds.Count; i++)
					{ writer.Write(value.ResidentIds[i]); WriteText(writer, value.ObjectIds[i]); }
					writer.Write(KingdomSubsidenceRungSaveSnapshot.AbsentCount);
					for (int i = 0; i < value.AbsentResidentIds.Count; i++)
					{ writer.Write(value.AbsentResidentIds[i]); WriteText(writer, value.AbsentObjectIds[i]); }
					writer.Flush();
					string encoded = VersionPrefix(version) + Convert.ToBase64String(stream.ToArray());
					if (encoded.Length > MaxWireChars) return false;
					wire = encoded; return true;
				}
			}
			catch (Exception) { return false; }
		}

		internal static bool TryDecode(string wire, out KingdomSubsidenceRungSaveSnapshot value)
		{
			value = null;
			int version = EnvelopeVersion(wire);
			if (version == 0 || wire.Length > MaxWireChars) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream(
					Convert.FromBase64String(wire.Substring(VersionPrefix(version).Length)), false))
				using (BinaryReader reader = new BinaryReader(stream, Utf8, true))
				{
					if (reader.ReadInt32() != Magic || reader.ReadInt32() != version) return false;
					string gameId = ReadText(reader, 36, false, false), zone = ReadText(reader, 128, false, false);
					string step = ReadText(reader, MaxStepWireChars, false, false);
					string rung = ReadText(reader, MaxRungWireChars, false, false);
					string stepId = ReadText(reader, MaxIdChars, false, false);
					string telling = ReadText(reader, 64, false, false);
					long now = reader.ReadInt64(), anchor = reader.ReadInt64(), due = reader.ReadInt64();
					long sequence = reader.ReadInt64(), checkpoint = reader.ReadInt64();
					int departures = reader.ReadInt32(), population = reader.ReadInt32();
					int stage = reader.ReadInt32(), chronicle = reader.ReadInt32(), outsider = reader.ReadInt32();
					KingdomSubsidenceRungSaveWork work = ReadWork(reader);
					KingdomSubsidenceRungSaveRoof roof = ReadRoof(reader);
					int[] residents; string[] objects; int[] absentIds; string[] absentObjects;
					if (!ReadBodies(reader, KingdomSubsidenceRungSaveSnapshot.SurvivorCount,
						out residents, out objects)) return false;
					if (!ReadBodies(reader, KingdomSubsidenceRungSaveSnapshot.AbsentCount,
						out absentIds, out absentObjects)) return false;
					KingdomSubsidenceRungSaveSnapshot decoded = new KingdomSubsidenceRungSaveSnapshot(
						gameId, zone, step, rung, stepId, telling, now, anchor, due, sequence, checkpoint,
						departures, population, stage, chronicle, outsider, work, roof, residents, objects,
						absentIds, absentObjects);
					if (stream.Position != stream.Length || !Valid(decoded, version) || !TryEncode(decoded, out string canonical)
						|| canonical != wire) return false;
					value = decoded; return true;
				}
			}
			catch (Exception) { return false; }
		}

		private static bool ValidWork(KingdomSubsidenceRungSaveWork work)
		{
			return work != null && Text(work.ObjectId, MaxIdChars, false, false)
				&& Text(work.Blueprint, 256, false, false) && Text(work.PlotId, MaxIdChars, false, true)
				&& Text(work.DesignStamp, MaxIdChars, true, false)
				&& work.X >= 0 && work.X <= MaxCoordinate && work.Y >= 0 && work.Y <= MaxCoordinate
				&& work.PartCopies == 1 && work.BeforeWear >= 0 && work.BeforeWear <= MaxWearValue
				&& work.AfterWear >= work.BeforeWear && work.AfterWear <= MaxWearValue
				&& Defined(typeof(KingdomWearIncidentPhase), work.IncidentPhase)
				&& Defined(typeof(KingdomWearRules.WearCause), work.IncidentCause)
				&& Defined(typeof(KingdomWearRules.WearCause), work.LastCause)
				&& Defined(typeof(KingdomWearSinkDisposition), work.IncidentMessageState)
				&& work.IncidentBeforeWear >= 0 && work.IncidentBeforeWear <= MaxWearValue && work.IncidentAfterWear >= 0
				&& work.IncidentAfterWear <= MaxWearValue && work.Wear >= 0 && work.Wear <= MaxWearValue
				&& Text(work.IncidentId, MaxIdChars, true, false)
				&& Text(work.LastCompletedIncidentId, MaxIdChars, true, false)
				&& Text(work.IncidentLine, 65536, true, false);
		}

		private static bool ValidRoof(KingdomSubsidenceRungSaveRoof roof)
		{
			return roof != null && roof.Carrier != null && roof.Carrier.ResidentId > 0
				&& Text(roof.Carrier.ObjectId, MaxIdChars, false, false)
				&& Text(roof.ZoneId, 128, false, false) && DefinedByte(typeof(KingdomResidentStanding), roof.Standing)
				&& roof.Reached >= 0 && roof.Warned >= 0;
		}

		private static void WriteWork(BinaryWriter writer, KingdomSubsidenceRungSaveWork work)
		{
			WriteText(writer, work.ObjectId); WriteText(writer, work.Blueprint);
			WriteText(writer, work.PlotId); WriteText(writer, work.DesignStamp);
			writer.Write(work.X); writer.Write(work.Y); writer.Write(work.PartCopies);
			writer.Write(work.BeforeWear); writer.Write(work.AfterWear);
			writer.Write(work.IncidentPhase); WriteText(writer, work.IncidentId);
			writer.Write(work.IncidentCause); writer.Write(work.IncidentBeforeWear);
			writer.Write(work.IncidentAfterWear); writer.Write(work.Wear); writer.Write(work.LastCause);
			WriteText(writer, work.LastCompletedIncidentId); WriteText(writer, work.IncidentLine);
			writer.Write(work.IncidentMessageState); writer.Write((byte)(work.Quarantined ? 1 : 0));
		}

		private static KingdomSubsidenceRungSaveWork ReadWork(BinaryReader reader)
		{
			string objectId = ReadText(reader, MaxIdChars, false, false);
			string blueprint = ReadText(reader, 256, false, false);
			string plot = ReadText(reader, MaxIdChars, false, true);
			string design = ReadText(reader, MaxIdChars, true, false);
			int x = reader.ReadInt32(), y = reader.ReadInt32(), copies = reader.ReadInt32();
			int before = reader.ReadInt32(), after = reader.ReadInt32(), phase = reader.ReadInt32();
			string incident = ReadText(reader, MaxIdChars, true, false);
			int cause = reader.ReadInt32(), incidentBefore = reader.ReadInt32();
			int incidentAfter = reader.ReadInt32(), wear = reader.ReadInt32(), lastCause = reader.ReadInt32();
			string completed = ReadText(reader, MaxIdChars, true, false);
			string line = ReadText(reader, 65536, true, false);
			return new KingdomSubsidenceRungSaveWork(objectId, blueprint, plot, design, x, y, copies,
				before, after, phase, incident, cause, incidentBefore, incidentAfter, wear, lastCause,
				completed, line, reader.ReadInt32(), Flag(reader));
		}

		private static void WriteRoof(BinaryWriter writer, KingdomSubsidenceRungSaveRoof roof)
		{
			WriteBody(writer, roof.Carrier); writer.Write(roof.HomeWorkId);
			writer.Write(roof.Standing); WriteText(writer, roof.ZoneId);
			writer.Write((byte)(roof.RoofStanding ? 1 : 0)); writer.Write(roof.Reached); writer.Write(roof.Warned);
		}

		private static KingdomSubsidenceRungSaveRoof ReadRoof(BinaryReader reader)
		{
			KingdomSubsidenceRungSaveBody carrier = ReadBody(reader);
			int home = reader.ReadInt32(), standing = reader.ReadInt32();
			string zone = ReadText(reader, 128, false, false);
			bool stands = Flag(reader);
			return new KingdomSubsidenceRungSaveRoof(carrier.ResidentId, carrier.ObjectId, home,
				standing, zone, stands, reader.ReadInt64(), reader.ReadInt64());
		}

		private static void WriteBody(BinaryWriter writer, KingdomSubsidenceRungSaveBody body)
		{ writer.Write(body.ResidentId); WriteText(writer, body.ObjectId); }

		private static KingdomSubsidenceRungSaveBody ReadBody(BinaryReader reader)
		{ return new KingdomSubsidenceRungSaveBody(reader.ReadInt32(), ReadText(reader, MaxIdChars, false, false)); }

		private static bool ReadBodies(BinaryReader reader, int count, out int[] residents, out string[] objects)
		{
			residents = null; objects = null;
			if (reader.ReadInt32() != count) return false;
			int[] ids = new int[count]; string[] bodies = new string[count];
			for (int i = 0; i < count; i++)
			{ KingdomSubsidenceRungSaveBody body = ReadBody(reader); ids[i] = body.ResidentId; bodies[i] = body.ObjectId; }
			residents = ids; objects = bodies; return true;
		}

		private static bool Bodies(IReadOnlyList<int> residents, IReadOnlyList<string> objects, int count)
		{
			if (residents == null || objects == null || residents.Count != count || objects.Count != count)
				return false;
			for (int i = 0; i < count; i++)
				if (residents[i] <= 0 || !Text(objects[i], MaxIdChars, false, false)) return false;
			return true;
		}

		private static bool Wire(string value, string prefix, int maxChars)
			=> Text(value, maxChars, false, false) && value.StartsWith(prefix, StringComparison.Ordinal);

		private static bool CanonicalId(string value)
		{
			return Text(value, 36, false, false) && value.Length == 36
				&& Guid.TryParseExact(value, "D", out Guid parsed) && parsed.ToString("D") == value;
		}

		private static bool Digest(string value)
		{
			if (value == null || value.Length != 64) return false;
			for (int i = 0; i < value.Length; i++)
				if (!(value[i] >= '0' && value[i] <= '9' || value[i] >= 'a' && value[i] <= 'f')) return false;
			return true;
		}

		private static bool Defined(Type vocabulary, int value) => Enum.IsDefined(vocabulary, value);

		private static bool DefinedByte(Type vocabulary, int value)
			=> value >= 0 && value <= byte.MaxValue && Enum.IsDefined(vocabulary, (byte)value);

		private static bool Flag(BinaryReader reader)
		{ byte raw = reader.ReadByte(); if (raw > 1) throw new InvalidDataException(); return raw == 1; }

		private static bool Text(string value, int maxChars, bool allowNull, bool allowEmpty)
		{
			if (value == null) return allowNull;
			if (value.Length == 0) return allowEmpty;
			if (value.Length > maxChars) return false;
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (char.IsControl(c)) return false;
				if (char.IsHighSurrogate(c))
				{
					if (i + 1 == value.Length || !char.IsLowSurrogate(value[++i])) return false;
				}
				else if (char.IsLowSurrogate(c)) return false;
			}
			return true;
		}

		private static void WriteText(BinaryWriter writer, string value)
		{
			if (value == null) { writer.Write(-1); return; }
			byte[] bytes = Utf8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes);
		}

		private static string ReadText(BinaryReader reader, int maxChars, bool allowNull, bool allowEmpty)
		{
			int length = reader.ReadInt32();
			if (length == -1) { if (!allowNull) throw new InvalidDataException(); return null; }
			if (length < 0 || (long)length > (long)maxChars * 4L
				|| length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException();
			string value = Utf8.GetString(reader.ReadBytes(length));
			if (!Text(value, maxChars, allowNull, allowEmpty)) throw new InvalidDataException();
			return value;
		}
	}
}
