using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal enum KingdomPolityDeathAttribution : byte
	{
		Unattributed = 1,
		PlayerWitnessed = 2
	}

	internal enum KingdomPolityDeathVisibility : byte
	{
		PhysicalOnly = 1,
		PlayerVisible = 2
	}

	internal enum KingdomPolityDeathIntentState : byte
	{
		Clear = 0,
		Outstanding = 1,
		Ambiguous = 2
	}

	internal enum KingdomPolityDeathIntentAction : byte
	{
		Clear = 0,
		ReplayWarband = 1,
		ReplayEnvoy = 2,
		Abandon = 3,
		Refuse = 4
	}

	internal sealed class KingdomPolityDeathIntentRecord
	{
		internal string Kind;
		internal string RealmId;
		internal string CohortId;
		internal string ProjectionId;
		internal string ZoneId;
		internal string ObjectId;
		internal int Ordinal;
		internal KingdomPolityCohortPurpose Purpose;
		internal bool Representative;
		internal long Tick;
		internal KingdomPolityDeathAttribution Attribution;
		internal KingdomPolityDeathVisibility Visibility;
		internal string IncidentPlanId;
		internal string IncidentId;
		internal string IncidentDigest;
		internal bool LegacyV1;
	}

	/// <summary>Bounded canonical wire and pure decisions for zone-owned death intents.</summary>
	internal static partial class KingdomPolityDeathIntentRules
	{
		internal const string WirePrefix = "taf:intent:polity-visible-death:v2:";
		internal const string LegacyWirePrefix = "taf:intent:polity-visible-death:v1:";
		internal const int MaximumFieldBytes = 512;
		internal const int MaximumPayloadBytes = 4096;
		internal const int MaximumWireCharacters = 8192;
		private const byte WireVersion = 2;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		internal static bool TryEncode(KingdomPolityDeathIntentRecord Record,
			out string Wire, out string Failure)
		{
			Wire = null; Failure = null;
			if (!Valid(Record)) return Fail("death intent record is invalid or unbounded", out Failure);
			try
			{
				byte[] payload;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8))
				{
					writer.Write(WireVersion);
					WriteText(writer, Record.Kind); WriteText(writer, Record.RealmId);
					WriteText(writer, Record.CohortId); WriteText(writer, Record.ProjectionId);
					WriteText(writer, Record.ZoneId); WriteText(writer, Record.ObjectId);
					writer.Write(Record.Ordinal); writer.Write((byte)Record.Purpose);
					writer.Write(Record.Representative ? (byte)1 : (byte)0);
					writer.Write(Record.Tick); writer.Write((byte)Record.Attribution);
					writer.Write((byte)Record.Visibility); writer.Flush(); payload = stream.ToArray();
					WriteText(writer, Record.IncidentPlanId); WriteText(writer, Record.IncidentId);
					WriteText(writer, Record.IncidentDigest); writer.Flush(); payload = stream.ToArray();
				}
				if (payload.Length > MaximumPayloadBytes)
					return Fail("death intent payload exceeds its bound", out Failure);
				string body = Convert.ToBase64String(payload);
				string digest = KingdomPolityRules.ActivationDigest(
					"polity-visible-death-intent-envelope-v2", body);
				Wire = WirePrefix + body + ":" + digest;
				return Wire.Length <= MaximumWireCharacters ||
					Fail("death intent wire exceeds its bound", out Failure);
			}
			catch (Exception ex)
			{
				Wire = null; return Fail("death intent encoding failed: " + ex.Message, out Failure);
			}
		}

		internal static bool TryDecode(string Wire, out KingdomPolityDeathIntentRecord Record,
			out string Failure)
		{
			Record = null; Failure = null;
			bool legacy = Wire != null && Wire.StartsWith(LegacyWirePrefix, StringComparison.Ordinal);
			string prefix = legacy ? LegacyWirePrefix : WirePrefix;
			if (string.IsNullOrEmpty(Wire) || Wire.Length > MaximumWireCharacters ||
				!Wire.StartsWith(prefix, StringComparison.Ordinal) || Wire.Length <
					prefix.Length + 1 + 1 + 64)
				return Fail("death intent wire is malformed, future, or unbounded", out Failure);
			try
			{
				int digestSeparator = Wire.Length - 65;
				if (Wire[digestSeparator] != ':')
					return Fail("death intent wire is malformed, future, or unbounded", out Failure);
				string body = Wire.Substring(prefix.Length, digestSeparator - prefix.Length);
				string digest = Wire.Substring(digestSeparator + 1);
				if (!KingdomPolityRules.Digest(digest) || digest !=
					KingdomPolityRules.ActivationDigest(
						(legacy ? "polity-visible-death-intent-envelope-v1" :
						 "polity-visible-death-intent-envelope-v2"), body))
					return Fail("death intent wire failed its exact digest", out Failure);
				byte[] payload = Convert.FromBase64String(body);
				if (payload.Length > MaximumPayloadBytes || Convert.ToBase64String(payload) != body)
					return Fail("death intent payload is noncanonical or unbounded", out Failure);
				KingdomPolityDeathIntentRecord decoded = new KingdomPolityDeathIntentRecord();
				using (MemoryStream stream = new MemoryStream(payload, false))
				using (BinaryReader reader = new BinaryReader(stream, StrictUtf8))
				{
					if (reader.ReadByte() != (legacy ? (byte)1 : WireVersion))
						return Fail("death intent wire version is not owned", out Failure);
					decoded.Kind = ReadText(reader, stream); decoded.RealmId = ReadText(reader, stream);
					decoded.CohortId = ReadText(reader, stream);
					decoded.ProjectionId = ReadText(reader, stream);
					decoded.ZoneId = ReadText(reader, stream); decoded.ObjectId = ReadText(reader, stream);
					decoded.Ordinal = reader.ReadInt32(); decoded.Purpose =
						(KingdomPolityCohortPurpose)reader.ReadByte();
					byte representative = reader.ReadByte();
					if (representative > 1) return Fail("death intent boolean is noncanonical", out Failure);
					decoded.Representative = representative == 1; decoded.Tick = reader.ReadInt64();
					decoded.Attribution = (KingdomPolityDeathAttribution)reader.ReadByte();
					decoded.Visibility = (KingdomPolityDeathVisibility)reader.ReadByte();
					if (!legacy) { decoded.IncidentPlanId = ReadText(reader, stream);
						decoded.IncidentId = ReadText(reader, stream);
						decoded.IncidentDigest = ReadText(reader, stream); }
					else { decoded.IncidentPlanId = decoded.IncidentId = decoded.IncidentDigest = "";
						decoded.LegacyV1 = true; }
					if (stream.Position != stream.Length)
						return Fail("death intent payload has trailing bytes", out Failure);
				}
				if (!Valid(decoded, legacy) || (!legacy && (!TryEncode(decoded,
					out string canonical, out Failure) || canonical != Wire)))
					return Fail(Failure ?? "death intent payload is noncanonical", out Failure);
				Record = decoded; return true;
			}
			catch (Exception ex)
			{
				Record = null; return Fail("death intent decoding failed: " + ex.Message, out Failure);
			}
		}

		internal static KingdomPolityDeathIntentState Classify(bool AnyPresence,
			bool ExactStringPresence, bool Decodes, bool ExactBinding)
		{
			if (!AnyPresence) return KingdomPolityDeathIntentState.Clear;
			return ExactStringPresence && Decodes && ExactBinding
				? KingdomPolityDeathIntentState.Outstanding
				: KingdomPolityDeathIntentState.Ambiguous;
		}

		internal static bool ExactBinding(KingdomPolityDeathIntentRecord Record, string RealmId,
			string CohortId, string ProjectionId, string ZoneId, string ObjectId, int Ordinal,
			KingdomPolityCohortPurpose Purpose, bool Representative)
		{
			return Record != null && Record.Kind == KingdomPolityPhysicalCustodyRules.DeathRemovalKind &&
				Record.RealmId == RealmId && Record.CohortId == CohortId &&
				Record.ProjectionId == ProjectionId && Record.ZoneId == ZoneId &&
				Record.ObjectId == ObjectId && Record.Ordinal == Ordinal &&
				Record.Purpose == Purpose && Record.Representative == Representative;
		}

		internal static bool CausalTick(KingdomPolityDeathIntentRecord Record,
			long ProjectionCommittedTick, long CurrentTick)
		{
			return Record != null && ProjectionCommittedTick >= 0L && CurrentTick >= 0L &&
				Record.Tick >= ProjectionCommittedTick && Record.Tick <= CurrentTick;
		}

		internal static KingdomPolityDeathIntentAction Decide(
			KingdomPolityDeathIntentRecord Record, KingdomPolityCohortPhase Phase)
		{
			if (Record == null) return KingdomPolityDeathIntentAction.Refuse;
			if (Phase == KingdomPolityCohortPhase.Abandoned)
				return KingdomPolityDeathIntentAction.Clear;
			if (Phase != KingdomPolityCohortPhase.Materialized &&
				Phase != KingdomPolityCohortPhase.Concluded)
				return KingdomPolityDeathIntentAction.Refuse;
			if (Record.Visibility == KingdomPolityDeathVisibility.PhysicalOnly)
				return Phase == KingdomPolityCohortPhase.Materialized
					? KingdomPolityDeathIntentAction.Abandon
					: KingdomPolityDeathIntentAction.Clear;
			if (!Record.Representative) return KingdomPolityDeathIntentAction.Clear;
			if (Record.Purpose == KingdomPolityCohortPurpose.Warband)
				return KingdomPolityDeathIntentAction.ReplayWarband;
			if (Record.Purpose == KingdomPolityCohortPurpose.Envoy)
				return KingdomPolityDeathIntentAction.ReplayEnvoy;
			return KingdomPolityDeathIntentAction.Clear;
		}

		private static bool Valid(KingdomPolityDeathIntentRecord Record, bool Legacy = false)
		{
			return Record != null && Record.Kind == KingdomPolityPhysicalCustodyRules.DeathRemovalKind &&
				Bounded(Record.Kind) && KingdomPolityRules.SemanticId(Record.RealmId) &&
				Bounded(Record.RealmId) && KingdomPolityRules.SemanticId(Record.CohortId) &&
				Bounded(Record.CohortId) && KingdomPolityRules.SemanticId(Record.ProjectionId) &&
				Bounded(Record.ProjectionId) && KingdomPolityRules.Text(Record.ZoneId, true) &&
				Bounded(Record.ZoneId) && KingdomPolityRules.SemanticId(Record.ObjectId) &&
				Bounded(Record.ObjectId) && Record.Ordinal >= 0 &&
				(byte)Record.Purpose >= 1 && (byte)Record.Purpose <= 7 &&
				Record.Representative == (Record.Ordinal == 0) && Record.Tick >= 0L &&
				(Record.Attribution == KingdomPolityDeathAttribution.Unattributed ||
				 Record.Attribution == KingdomPolityDeathAttribution.PlayerWitnessed) &&
				(Record.Visibility == KingdomPolityDeathVisibility.PhysicalOnly ||
				 Record.Visibility == KingdomPolityDeathVisibility.PlayerVisible) &&
				(Record.Visibility == KingdomPolityDeathVisibility.PlayerVisible ||
				 Record.Attribution == KingdomPolityDeathAttribution.Unattributed) &&
				(Record.Attribution != KingdomPolityDeathAttribution.PlayerWitnessed ||
				 Record.Visibility == KingdomPolityDeathVisibility.PlayerVisible) &&
				(Legacy ? Record.IncidentPlanId == "" && Record.IncidentId == "" &&
				 Record.IncidentDigest == "" : ValidIncidentBinding(Record));
		}

		private static bool ValidIncidentBinding(KingdomPolityDeathIntentRecord Record)
		{
			bool requires = Record.Visibility == KingdomPolityDeathVisibility.PlayerVisible &&
				Record.Representative && (Record.Purpose ==
				KingdomPolityCohortPurpose.Envoy || Record.Purpose == KingdomPolityCohortPurpose.Warband);
			if (!requires) return Record.IncidentPlanId == "" && Record.IncidentId == "" &&
				Record.IncidentDigest == "";
			return KingdomPolityRules.SemanticId(Record.IncidentPlanId) &&
				KingdomPolityRules.SemanticId(Record.IncidentId) &&
				KingdomPolityRules.Digest(Record.IncidentDigest) && Bounded(Record.IncidentPlanId) &&
				Bounded(Record.IncidentId) && Bounded(Record.IncidentDigest);
		}

		private static bool Bounded(string Value)
		{
			if (Value == null) return false;
			try { return StrictUtf8.GetByteCount(Value) <= MaximumFieldBytes; }
			catch (EncoderFallbackException) { return false; }
		}

		private static void WriteText(BinaryWriter Writer, string Value)
		{
			byte[] bytes = StrictUtf8.GetBytes(Value); Writer.Write(bytes.Length);
			Writer.Write(bytes, 0, bytes.Length);
		}

		private static string ReadText(BinaryReader Reader, Stream Stream)
		{
			int length = Reader.ReadInt32();
			if (length < 0 || length > MaximumFieldBytes || length > Stream.Length - Stream.Position)
				throw new InvalidDataException("death intent text length is invalid");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			return StrictUtf8.GetString(bytes);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
