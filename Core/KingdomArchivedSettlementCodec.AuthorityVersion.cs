using System;
using System.IO;

namespace ThousandAndFirst
{
	internal static partial class KingdomArchivedSettlementCodec
	{
		/// <summary>Fixed refusal for a schema outside the accepted decode set v1-v19.</summary>
		private const string UnacceptedSchemaFailure =
			"Archived settlement projection schema version is not accepted.";

		/// <summary>Fixed refusal for a projection the current reader cannot prove exact.</summary>
		private const string InexactProjectionFailure =
			"Archived settlement projection does not reproduce the live settlement graph.";

		/// <summary>Writes a settlement at an explicit archive schema so a persisted authority
		/// hash can be re-proved against its own basis version instead of today's CurrentVersion.
		/// Current delegates to TryEncode, whose bytes and behaviour are unchanged. Every older
		/// accepted schema keeps TryEncode's admission, aggregate caps and StrictMutableRoot, then
		/// is re-proved by the current TryDecode plus ExactGraph, so a projection that discards or
		/// clamps live authority is refused with no bytes. Nothing is cached; each call re-derives.</summary>
		internal static bool TryEncodeVersion(KingdomSettlement Value, int Schema,
			out byte[] Bytes, out string Failure)
		{
			if (Schema == CurrentVersion) return TryEncode(Value, out Bytes, out Failure);
			Bytes = null;
			Failure = null;
			if (Schema < LegacyVersion || Schema > CurrentVersion)
			{
				Failure = UnacceptedSchemaFailure;
				return false;
			}
			try
			{
				// Identical admission to TryEncode. An archive the current writer refuses must
				// never become publishable by asking for an older schema instead.
				if (Value != null && Value.City == null)
					throw new InvalidDataException(
						"Archived settlement is missing its subsidence carrier.");
				if (Value != null && (Value.LifecycleBook == null
					|| Value.LifecycleBook.FormatVersion != KingdomLifecycleRules.CurrentFormatVersion
					|| !KingdomRaidIncidentRules.ValidLedger(Value.LifecycleBook.RaidLedger)))
					throw new InvalidDataException(
						"Archived settlement projection raid evidence is malformed.");
				if (!StrictMutableRoot(Value, typeof(KingdomSettlement), out Failure))
					return false;
				byte[] candidate;
				using (CappedWriteStream stream = new CappedWriteStream(MaxPayloadBytes))
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(Magic);
					writer.Write(Schema);
					WriteString(writer, Shape(typeof(KingdomSettlement), Schema), MaxShapeBytes);
					WriteValue(writer, typeof(KingdomSettlement), Value, 0, new Budget(), Schema);
					writer.Flush();
					if (stream.Length > MaxPayloadBytes)
						throw new InvalidDataException(
							"Archived settlement projection payload exceeds cap.");
					candidate = stream.ToArray();
				}
				// The older schema omits fields and clamps others while the current reader plants
				// migration defaults on the way back. Only an exact decode of these very bytes
				// proves those defaults equal the old wire's absence rather than lost authority.
				if (!TryDecode(candidate, out KingdomSettlement projected, out int future,
					out Failure)) return false;
				if (future != 0 || !ExactGraph(Value, projected, out string _))
				{
					Failure = InexactProjectionFailure;
					return false;
				}
				Bytes = candidate;
				return true;
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message);
				Bytes = null;
				return false;
			}
		}
	}
}
