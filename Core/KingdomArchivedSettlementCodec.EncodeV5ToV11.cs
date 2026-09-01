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
#if TAF_TESTS
		/// <summary>Test-only producer for the exact archive-v5 surface. It carries extension
		/// identity counts but predates causal pilgrims, expedition job columns, and expedition
		/// homecoming lines. Its golden prevents future additive fields from rewriting old bytes.</summary>
		internal static bool TryEncodeExtensionIdentityV5ForTests(KingdomSettlement Value,
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
						"Archived settlement v5 raid evidence is malformed.");
				if (!StrictMutableRoot(Value, typeof(KingdomSettlement), out Failure))
					return false;
				using (CappedWriteStream stream = new CappedWriteStream(MaxPayloadBytes))
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(Magic);
					writer.Write(ExtensionIdentityVersion);
					WriteString(writer, Shape(typeof(KingdomSettlement),
						ExtensionIdentityVersion), MaxShapeBytes);
					WriteValue(writer, typeof(KingdomSettlement), Value, 0, new Budget(),
						ExtensionIdentityVersion);
					writer.Flush();
					if (stream.Length > MaxPayloadBytes)
						throw new InvalidDataException(
							"Archived settlement v5 payload exceeds cap.");
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

		/// <summary>Test-only producer for the exact archive-v6 surface. It carries causal pilgrims,
		/// expedition job columns, and homecoming lines, but predates the API-v3 behaviour sidecar.
		/// Its independent golden prevents future additive fields from rewriting salvage-era bytes.</summary>
		internal static bool TryEncodeSalvageV6ForTests(KingdomSettlement Value,
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
						"Archived settlement v6 raid evidence is malformed.");
				if (!StrictMutableRoot(Value, typeof(KingdomSettlement), out Failure))
					return false;
				using (CappedWriteStream stream = new CappedWriteStream(MaxPayloadBytes))
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(Magic);
					writer.Write(SalvageVersion);
					WriteString(writer, Shape(typeof(KingdomSettlement), SalvageVersion),
						MaxShapeBytes);
					WriteValue(writer, typeof(KingdomSettlement), Value, 0, new Budget(),
						SalvageVersion);
					writer.Flush();
					if (stream.Length > MaxPayloadBytes)
						throw new InvalidDataException(
							"Archived settlement v6 payload exceeds cap.");
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

		/// <summary>Test-only producer for the exact archive-v7 surface. It carries API-v3
		/// behaviour but predates the physical-happening lifecycle sidecar.</summary>
		internal static bool TryEncodeBehaviourV7ForTests(KingdomSettlement Value,
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
						"Archived settlement v7 raid evidence is malformed.");
				if (!StrictMutableRoot(Value, typeof(KingdomSettlement), out Failure))
					return false;
				using (CappedWriteStream stream = new CappedWriteStream(MaxPayloadBytes))
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(Magic);
					writer.Write(BehaviourVersion);
					WriteString(writer, Shape(typeof(KingdomSettlement), BehaviourVersion),
						MaxShapeBytes);
					WriteValue(writer, typeof(KingdomSettlement), Value, 0, new Budget(),
						BehaviourVersion);
					writer.Flush();
					if (stream.Length > MaxPayloadBytes)
						throw new InvalidDataException(
							"Archived settlement v7 payload exceeds cap.");
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

		/// <summary>Test-only producer for the exact archive-v8 surface. It carries physical
		/// happening authority but predates exact central-logistics delivery columns.</summary>
		internal static bool TryEncodePhysicalHappeningV8ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV13ForTests(Value, PhysicalHappeningVersion,
				"v8", out Payload, out Failure);
		}

		/// <summary>Test-only producer for the exact archive-v9 surface. It carries exact
		/// central-logistics authority but predates defensive WorkId/resident reservations.</summary>
		internal static bool TryEncodeExactLogisticsV9ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV13ForTests(Value, ExactLogisticsVersion,
				"v9", out Payload, out Failure);
		}

		/// <summary>Test-only producer for the exact archive-v10 surface. It carries defensive
		/// reservations but predates frozen semantic person plans and stable office identity.</summary>
		internal static bool TryEncodeDefensiveReservationV10ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV13ForTests(Value, DefensiveReservationVersion,
				"v10", out Payload, out Failure);
		}

		/// <summary>Test-only producer for archive v11, before per-source happening cursors.</summary>
		internal static bool TryEncodeSemanticSelectionV11ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV13ForTests(Value, SemanticSelectionVersion,
				"v11", out Payload, out Failure);
		}

		/// <summary>Test-only producer for archive v12, before the delivery enum domain widened.</summary>
		internal static bool TryEncodeHappeningCursorV12ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV13ForTests(Value, HappeningCursorVersion,
				"v12", out Payload, out Failure);
		}

		/// <summary>Test-only producer for archive v13, before city-local cook and moot receipts.</summary>
		internal static bool TryEncodeDeliveryDomainV13ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV13ForTests(Value, DeliveryDomainVersion,
				"v13", out Payload, out Failure);
		}

		/// <summary>Test-only producer for archive v14, before Growth-owned first-guest
		/// correspondence authority.</summary>
		internal static bool TryEncodeCivicAuthorityV14ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV14ForTests(Value, CivicAuthorityVersion,
				"v14", out Payload, out Failure);
		}

		private static bool TryEncodeHistoricalV8ToV13ForTests(KingdomSettlement Value,
			int Version, string Label, out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV14ForTests(Value, Version, Label,
				out Payload, out Failure);
		}

		private static bool TryEncodeHistoricalV8ToV14ForTests(KingdomSettlement Value,
			int Version, string Label, out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV17ForTests(Value, Version, Label,
				out Payload, out Failure);
		}

		private static bool TryEncodeHistoricalV8ToV17ForTests(KingdomSettlement Value,
			int Version, string Label, out byte[] Payload, out string Failure)
		{
			Payload = null;
			Failure = null;
			try
			{
				if (Version < PhysicalHappeningVersion || Version > ArrivalCadenceVersion)
					throw new ArgumentOutOfRangeException(nameof(Version));
				if (Value != null && (Value.LifecycleBook == null
					|| Value.LifecycleBook.FormatVersion != KingdomLifecycleRules.CurrentFormatVersion
					|| !KingdomRaidIncidentRules.ValidLedger(Value.LifecycleBook.RaidLedger)))
					throw new InvalidDataException(
						"Archived settlement " + Label + " raid evidence is malformed.");
				if (!StrictMutableRoot(Value, typeof(KingdomSettlement), out Failure))
					return false;
				using (CappedWriteStream stream = new CappedWriteStream(MaxPayloadBytes))
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(Magic);
					writer.Write(Version);
					WriteString(writer, Shape(typeof(KingdomSettlement), Version), MaxShapeBytes);
					WriteValue(writer, typeof(KingdomSettlement), Value, 0, new Budget(), Version);
					writer.Flush();
					if (stream.Length > MaxPayloadBytes)
						throw new InvalidDataException(
							"Archived settlement " + Label + " payload exceeds cap.");
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

		/// <summary>Test-only hostile-envelope producer. It bypasses semantic raid validation
		/// while retaining all graph and aggregate bounds so the production reader can prove that
		/// malformed current evidence is rejected rather than normalized into authority.</summary>
		internal static bool TryEncodeUncheckedCurrentForTests(KingdomSettlement Value,
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
					writer.Write(CurrentVersion);
					WriteString(writer, Shape(typeof(KingdomSettlement)), MaxShapeBytes);
					WriteValue(writer, typeof(KingdomSettlement), Value, 0, new Budget());
					writer.Flush();
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
#endif
	}
}
