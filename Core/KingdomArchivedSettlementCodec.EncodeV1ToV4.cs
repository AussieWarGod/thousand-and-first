using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomArchivedSettlementCodec
	{
		public static bool TryEncode(KingdomSettlement Value, out byte[] Payload,
			out string Failure)
		{
			Payload = null;
			Failure = null;
			try
			{
				if (Value != null && (Value.LifecycleBook == null
					|| Value.LifecycleBook.FormatVersion != KingdomLifecycleRules.CurrentFormatVersion
					|| !KingdomRaidIncidentRules.ValidLedger(Value.LifecycleBook.RaidLedger)))
					throw new InvalidDataException(
						"Archived settlement current raid evidence is malformed.");
				if (!StrictMutableRoot(Value, typeof(KingdomSettlement), out Failure))
					return false;
				using (CappedWriteStream stream = new CappedWriteStream(MaxPayloadBytes))
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(Magic);
					writer.Write(CurrentVersion);
					WriteString(writer, Shape(typeof(KingdomSettlement)), MaxShapeBytes);
					WriteValue(writer, typeof(KingdomSettlement), Value, 0, new Budget());
					writer.Flush();
					if (stream.Length > MaxPayloadBytes)
						throw new InvalidDataException("Archived settlement payload exceeds cap.");
					Payload = stream.ToArray();
					return true;
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message);
				Payload = null;
				return false;
			}
		}

#if TAF_TESTS
		/// <summary>Test-only producer for the immutable archive-v1 field envelope. Production
		/// never writes v1 again; keeping this independent write path lets the migration reader
		/// prove that adding nested Growth did not strand an already archived settlement.</summary>
		internal static bool TryEncodeLegacyV1ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			Payload = null;
			Failure = null;
			try
			{
				if (!StrictMutableRoot(Value, typeof(KingdomSettlement), out Failure))
					return false;
				using (CappedWriteStream stream = new CappedWriteStream(MaxPayloadBytes))
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(Magic);
					writer.Write(LegacyVersion);
					WriteString(writer, Shape(typeof(KingdomSettlement), Legacy: true),
						MaxShapeBytes);
					WriteValue(writer, typeof(KingdomSettlement), Value, 0, new Budget(),
						Legacy: true);
					writer.Flush();
					if (stream.Length > MaxPayloadBytes)
						throw new InvalidDataException(
							"Archived settlement v1 payload exceeds cap.");
					Payload = stream.ToArray();
					return true;
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message);
				Payload = null;
				return false;
			}
		}

		/// <summary>Test-only producer for the exact archive-v2 surface. RaidLedger did not
		/// exist in that schema; action values appended with lifecycle v7 are likewise refused.</summary>
		internal static bool TryEncodePreviousV2ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			Payload = null;
			Failure = null;
			try
			{
				if (!StrictMutableRoot(Value, typeof(KingdomSettlement), out Failure))
					return false;
				using (CappedWriteStream stream = new CappedWriteStream(MaxPayloadBytes))
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(Magic);
					writer.Write(PreviousVersion);
					WriteString(writer, Shape(typeof(KingdomSettlement), PreviousVersion),
						MaxShapeBytes);
					WriteValue(writer, typeof(KingdomSettlement), Value, 0, new Budget(),
						PreviousVersion);
					writer.Flush();
					if (stream.Length > MaxPayloadBytes)
						throw new InvalidDataException(
							"Archived settlement v2 payload exceeds cap.");
					Payload = stream.ToArray();
					return true;
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message);
				Payload = null;
				return false;
			}
		}

		/// <summary>Test-only producer for the exact archive-v3 surface. CultureCounts,
		/// SpeciesCounts, and built-in/extension IdentityCounts did not exist in that schema; the current
		/// reader must restore all as empty live facts without reinterpreting older bytes.</summary>
		internal static bool TryEncodeRaidV3ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			Payload = null;
			Failure = null;
			try
			{
				if (Value != null && (Value.LifecycleBook == null
					|| Value.LifecycleBook.FormatVersion != KingdomLifecycleRules.CurrentFormatVersion
					|| !KingdomRaidIncidentRules.ValidLedger(Value.LifecycleBook.RaidLedger)))
					throw new InvalidDataException(
						"Archived settlement v3 raid evidence is malformed.");
				if (!StrictMutableRoot(Value, typeof(KingdomSettlement), out Failure))
					return false;
				using (CappedWriteStream stream = new CappedWriteStream(MaxPayloadBytes))
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(Magic);
					writer.Write(RaidVersion);
					WriteString(writer, Shape(typeof(KingdomSettlement), RaidVersion),
						MaxShapeBytes);
					WriteValue(writer, typeof(KingdomSettlement), Value, 0, new Budget(),
						RaidVersion);
					writer.Flush();
					if (stream.Length > MaxPayloadBytes)
						throw new InvalidDataException(
							"Archived settlement v3 payload exceeds cap.");
					Payload = stream.ToArray();
					return true;
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message);
				Payload = null;
				return false;
			}
		}

		/// <summary>Test-only producer for the exact archive-v4 surface. It carries vanilla
		/// culture/species tallies but predates built-in/extension IdentityCounts.</summary>
		internal static bool TryEncodeResidentIdentityV4ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			Payload = null;
			Failure = null;
			try
			{
				if (Value != null && (Value.LifecycleBook == null
					|| Value.LifecycleBook.FormatVersion != KingdomLifecycleRules.CurrentFormatVersion
					|| !KingdomRaidIncidentRules.ValidLedger(Value.LifecycleBook.RaidLedger)))
					throw new InvalidDataException(
						"Archived settlement v4 raid evidence is malformed.");
				if (!StrictMutableRoot(Value, typeof(KingdomSettlement), out Failure))
					return false;
				using (CappedWriteStream stream = new CappedWriteStream(MaxPayloadBytes))
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(Magic);
					writer.Write(ResidentIdentityVersion);
					WriteString(writer, Shape(typeof(KingdomSettlement), ResidentIdentityVersion),
						MaxShapeBytes);
					WriteValue(writer, typeof(KingdomSettlement), Value, 0, new Budget(),
						ResidentIdentityVersion);
					writer.Flush();
					if (stream.Length > MaxPayloadBytes)
						throw new InvalidDataException(
							"Archived settlement v4 payload exceeds cap.");
					Payload = stream.ToArray();
					return true;
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message);
				Payload = null;
				return false;
			}
		}

		/// <summary>Test-only producer for archive v15, before durable physical guest evidence.</summary>
		internal static bool TryEncodeFirstGuestV15ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV16ForTests(Value, FirstGuestVersion,
				"v15", out Payload, out Failure);
		}

		/// <summary>Test-only producer for archive v16, before fixed-rate arrival cadence.</summary>
		internal static bool TryEncodePhysicalFirstGuestV16ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV16ForTests(Value, PhysicalFirstGuestVersion,
				"v16", out Payload, out Failure);
		}

#endif
	}
}
