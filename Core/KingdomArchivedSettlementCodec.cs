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
	/// <summary>
	/// Bounded, versioned wire for a settlement held inside a realm archive. This deliberately
	/// does not call the engine's reflected composite reader: an archive must be able to reject a
	/// hostile nested length before that reader allocates, and a clone used as frozen evidence must
	/// not share any mutable list, dictionary, ledger, lifecycle row, or city column with live state.
	/// </summary>
	internal static class KingdomArchivedSettlementCodec
	{
		public const int Magic = 0x54415331; // TAS1
		public const int LegacyVersion = 1;
		public const int PreviousVersion = 2;
		/// <summary>First archive surface carrying the lifecycle raid ledger and its
		/// appended actions. Kept explicit after later settlement fields are added so a
		/// v3 archive is not mistaken for the older, pre-ledger v2 contract.</summary>
		public const int RaidVersion = 3;
		/// <summary>First archive surface carrying vanilla culture/species tallies.</summary>
		public const int ResidentIdentityVersion = 4;
		/// <summary>First archive surface carrying built-in/extension live identity tallies.</summary>
		public const int ExtensionIdentityVersion = 5;
		/// <summary>First archive surface carrying causal-pilgrim state and the exact named-settler
		/// salvage-expedition columns added during the first full brief-completion pass.</summary>
		public const int SalvageVersion = 6;
		/// <summary>First archive surface carrying the bounded API-v3 settlement behaviour sidecar.</summary>
		public const int BehaviourVersion = 7;
		/// <summary>First archive surface carrying durable physical-happening lifecycle authority.</summary>
		public const int PhysicalHappeningVersion = 8;
		/// <summary>First archive surface carrying explicit exact-delivery planner authority.</summary>
		public const int ExactLogisticsVersion = 9;
		/// <summary>First archive surface carrying exact defensive WorkId/resident reservations.</summary>
		public const int DefensiveReservationVersion = 10;
		/// <summary>First archive surface carrying frozen semantic person plans and stable
		/// office-holder resident identity.</summary>
		public const int SemanticSelectionVersion = 11;
		/// <summary>First archive surface carrying independent extension-happening cursors.</summary>
		public const int HappeningCursorVersion = 12;
		public const int CurrentVersion = HappeningCursorVersion;
		public const int MaxPayloadBytes = 2 * 1024 * 1024;
		public const int MaxStringBytes = 16 * 1024;
		public const int MaxByteArrayBytes = 512 * 1024;
		public const int MaxCollectionCount = 1024;
		private const int MaxDepth = 12;
		private const int MaxObjects = 16384;
		private const int MaxShapeBytes = 64 * 1024;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		private sealed class Budget
		{
			public int Objects;
		}

		/// <summary>Pre-allocation aggregate bound. Per-field caps are not an aggregate cap:
		/// 1,024 individually legal strings can otherwise grow a MemoryStream far beyond the
		/// archive envelope before the post-write length check runs.</summary>
		private sealed class CappedWriteStream : Stream
		{
			private readonly MemoryStream Inner = new MemoryStream();
			private readonly long Maximum;

			public CappedWriteStream(long Maximum)
			{
				if (Maximum < 0L) throw new ArgumentOutOfRangeException(nameof(Maximum));
				this.Maximum = Maximum;
			}

			public byte[] ToArray()
			{
				return Inner.ToArray();
			}

			private void RequireCapacity(long Count)
			{
				if (Count < 0L || Position > Maximum - Count)
					throw new InvalidDataException(
						"Archived settlement aggregate cap reached before write.");
			}

			public override bool CanRead => true;
			public override bool CanSeek => true;
			public override bool CanWrite => true;
			public override long Length => Inner.Length;
			public override long Position
			{
				get { return Inner.Position; }
				set
				{
					if (value < 0L || value > Maximum)
						throw new InvalidDataException(
							"Archived settlement stream position exceeds cap.");
					Inner.Position = value;
				}
			}

			public override void Flush() { Inner.Flush(); }
			public override int Read(byte[] Buffer, int Offset, int Count)
			{
				return Inner.Read(Buffer, Offset, Count);
			}
			public override long Seek(long Offset, SeekOrigin Origin)
			{
				long target;
				switch (Origin)
				{
				case SeekOrigin.Begin: target = Offset; break;
				case SeekOrigin.Current: target = Position + Offset; break;
				case SeekOrigin.End: target = Length + Offset; break;
				default: throw new ArgumentOutOfRangeException(nameof(Origin));
				}
				Position = target;
				return target;
			}
			public override void SetLength(long Value)
			{
				if (Value < 0L || Value > Maximum)
					throw new InvalidDataException(
						"Archived settlement stream length exceeds cap.");
				Inner.SetLength(Value);
			}
			public override void Write(byte[] Buffer, int Offset, int Count)
			{
				RequireCapacity(Count);
				Inner.Write(Buffer, Offset, Count);
			}
			public override void WriteByte(byte Value)
			{
				RequireCapacity(1L);
				Inner.WriteByte(Value);
			}

			protected override void Dispose(bool Disposing)
			{
				if (Disposing) Inner.Dispose();
				base.Dispose(Disposing);
			}
		}

		private sealed class ReferenceComparer : IEqualityComparer<object>
		{
			public new bool Equals(object Left, object Right)
			{
				return ReferenceEquals(Left, Right);
			}

			public int GetHashCode(object Value)
			{
				return RuntimeHelpers.GetHashCode(Value);
			}
		}

		private static readonly Type[] ApprovedObjects = new Type[]
		{
			typeof(KingdomSettlement),
			typeof(KingdomLedger),
			typeof(KingdomLifecycleBook),
			typeof(KingdomLifecycleOperation),
			typeof(KingdomLifecycleWaterLeg),
			typeof(KingdomLifecycleProjection),
			typeof(KingdomLifecycleOutbox),
			typeof(KingdomLifecycleResourceLease),
			typeof(KingdomLifecycleResourceRevision),
			typeof(KingdomLifecycleProof),
			typeof(KingdomRaidLedger),
			typeof(KingdomRaidGrievance),
			typeof(KingdomRaidIncident),
			typeof(KingdomRaidDefenceReservation),
			typeof(KingdomGrowthBook),
			typeof(KingdomGrowthOperation),
			typeof(KingdomGrowthWaterLeg),
			typeof(KingdomGrowthObjectLeg),
			typeof(KingdomGrowthCropRow),
			typeof(KingdomGrowthFieldState),
			typeof(KingdomGrowthDomainStep),
			typeof(KingdomGrowthFieldSlot),
			typeof(KingdomGrowthProof),
			typeof(KingdomGrowthScarcitySnapshot),
			typeof(KingdomGrowthAccountingSnapshot),
			typeof(KingdomGrowthOutboxEvent),
			typeof(KingdomGrowthObjectCallbackStep),
			typeof(KingdomGrowthArrivalCandidate),
			typeof(KingdomCarryBook),
			typeof(KingdomCarryOperation),
			typeof(KingdomCarrySource),
			typeof(Simulation.City.KingdomCityBook),
			typeof(Simulation.City.KingdomBindingRegistry),
			typeof(Simulation.City.KingdomJobRegistry)
		};

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
			return TryEncodeHistoricalV8ToV11ForTests(Value, PhysicalHappeningVersion,
				"v8", out Payload, out Failure);
		}

		/// <summary>Test-only producer for the exact archive-v9 surface. It carries exact
		/// central-logistics authority but predates defensive WorkId/resident reservations.</summary>
		internal static bool TryEncodeExactLogisticsV9ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV11ForTests(Value, ExactLogisticsVersion,
				"v9", out Payload, out Failure);
		}

		/// <summary>Test-only producer for the exact archive-v10 surface. It carries defensive
		/// reservations but predates frozen semantic person plans and stable office identity.</summary>
		internal static bool TryEncodeDefensiveReservationV10ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV11ForTests(Value, DefensiveReservationVersion,
				"v10", out Payload, out Failure);
		}

		/// <summary>Test-only producer for archive v11, before per-source happening cursors.</summary>
		internal static bool TryEncodeSemanticSelectionV11ForTests(KingdomSettlement Value,
			out byte[] Payload, out string Failure)
		{
			return TryEncodeHistoricalV8ToV11ForTests(Value, SemanticSelectionVersion,
				"v11", out Payload, out Failure);
		}

		private static bool TryEncodeHistoricalV8ToV11ForTests(KingdomSettlement Value,
			int Version, string Label, out byte[] Payload, out string Failure)
		{
			Payload = null;
			Failure = null;
			try
			{
				if (Version < PhysicalHappeningVersion || Version > SemanticSelectionVersion)
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

		/// <summary>Returns false for malformed/current-unsupported data. A strictly newer version
		/// returns false with <paramref name="FutureVersion"/> set so the caller can retain the exact
		/// opaque bytes and quarantine instead of interpreting a prefix.</summary>
		public static bool TryDecode(byte[] Payload, out KingdomSettlement Value,
			out int FutureVersion, out string Failure)
		{
			Value = null;
			FutureVersion = 0;
			Failure = null;
			try
			{
				if (Payload == null || Payload.Length < 8 || Payload.Length > MaxPayloadBytes)
					throw new InvalidDataException("Archived settlement payload length is invalid.");
				using (MemoryStream stream = new MemoryStream(Payload, false))
				using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
				{
					if (reader.ReadInt32() != Magic)
						throw new InvalidDataException("Archived settlement marker is invalid.");
					int version = reader.ReadInt32();
					if (version > CurrentVersion)
					{
						FutureVersion = version;
						Failure = "Archived settlement uses future version " + version + ".";
						return false;
					}
					if (version != LegacyVersion && version != PreviousVersion
						&& version != RaidVersion
						&& version != ResidentIdentityVersion
						&& version != ExtensionIdentityVersion
						&& version != SalvageVersion
						&& version != BehaviourVersion
						&& version != PhysicalHappeningVersion
						&& version != ExactLogisticsVersion
						&& version != DefensiveReservationVersion
						&& version != SemanticSelectionVersion
						&& version != CurrentVersion)
						throw new InvalidDataException("Archived settlement version is unsupported.");
					string shape = ReadString(reader, MaxShapeBytes, Required: true);
					if (!string.Equals(shape, Shape(typeof(KingdomSettlement), version),
						StringComparison.Ordinal))
						throw new InvalidDataException("Archived settlement schema shape is unknown.");
					object decoded = ReadValue(reader, typeof(KingdomSettlement), 0,
						new Budget(), version);
					if (stream.Position != stream.Length)
						throw new InvalidDataException("Archived settlement has trailing bytes.");
					Value = (KingdomSettlement)decoded;
					if (version < SemanticSelectionVersion)
						StageHistoricalSemanticPlan(Value);
					if (version == LegacyVersion && Value != null)
					{
						if (!KingdomLifecycleRules.StageLegacyGrowthMigration(Value.LifecycleBook))
							throw new InvalidDataException(
								"Archived settlement legacy lifecycle could not stage Growth migration.");
						KingdomLifecycleRules.QuarantineLegacyRaidAuthority(Value.LifecycleBook);
					}
					else if (version == PreviousVersion && Value != null
						&& !KingdomLifecycleRules.StageRaidMigrationFromV6(Value.LifecycleBook))
						throw new InvalidDataException(
							"Archived settlement v2 lifecycle could not stage raid migration.");
					else if (version >= RaidVersion && version < CurrentVersion && Value != null
						&& !KingdomLifecycleWireCodec.UpgradeArchivedRaidLedgerV1(
							Value.LifecycleBook))
						throw new InvalidDataException(
							"Archived settlement historical raid ledger could not migrate.");
					if (Value != null && (Value.LifecycleBook == null
						|| Value.LifecycleBook.FormatVersion != KingdomLifecycleRules.CurrentFormatVersion
						|| !KingdomRaidIncidentRules.ValidLedger(Value.LifecycleBook.RaidLedger)))
						throw new InvalidDataException(
							"Archived settlement raid evidence is malformed.");
					return true;
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message);
				Value = null;
				return false;
			}
		}

		private static void StageHistoricalSemanticPlan(KingdomSettlement Value)
		{
			// Archive v1-v10 wrote the city-v2 carrier and omitted v3's exact resident-origin
			// and frozen-arrival columns. Normalize that carrier at the decoder boundary so a
			// successfully decoded historical settlement is immediately readable by current city
			// rules; callers must not need an unrelated save-load normalization pass to finish the
			// archive migration.
			Value?.City?.Normalize();
			KingdomGrowthBook growth = Value?.LifecycleBook?.Growth;
			if (growth == null) return;
			if (growth.FormatVersion == KingdomLifecycleRules.PreviousGrowthFormatVersion)
				growth.FormatVersion = KingdomLifecycleRules.CurrentGrowthFormatVersion;
			KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
			if (candidate == null) return;
			candidate.LegacySemanticPlan = true;
			candidate.SemanticPlanVersion = 0;
			candidate.SemanticStreamId = null;
			candidate.SemanticEventKind = 0U;
			candidate.PlannedOrigin = null;
			candidate.PlannedCreed = null;
			candidate.PlannedName = null;
			candidate.PlannedArrived = null;
			candidate.ArrivalX = -1;
			candidate.ArrivalY = -1;
		}

		public static bool TryClone(KingdomSettlement Source, out KingdomSettlement Clone,
			out string Failure)
		{
			Clone = null;
			if (!TryEncode(Source, out byte[] payload, out Failure)) return false;
			int future;
			return TryDecode(payload, out Clone, out future, out Failure) && future == 0;
		}

		public static bool TryHash(KingdomSettlement Value, out string Hash,
			out string Failure)
		{
			Hash = null;
			if (!TryEncode(Value, out byte[] payload, out Failure)) return false;
			using (SHA256 algorithm = SHA256.Create())
			{
				byte[] digest = algorithm.ComputeHash(payload);
				StringBuilder text = new StringBuilder(digest.Length * 2);
				for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
				Hash = text.ToString();
				return true;
			}
		}

		public static bool ExactGraph(KingdomSettlement Left, KingdomSettlement Right,
			out string Failure)
		{
			Failure = null;
			if (!StrictMutableRoot(Left, typeof(KingdomSettlement), out Failure) ||
				!StrictMutableRoot(Right, typeof(KingdomSettlement), out Failure) ||
				!ExactReferenceTopology(Left, Right, typeof(KingdomSettlement), 0,
					new Budget(),
					new Dictionary<object, object>(new ReferenceComparer()),
					new Dictionary<object, object>(new ReferenceComparer()), out Failure))
				return false;
			if (!TryEncode(Left, out byte[] left, out Failure) ||
				!TryEncode(Right, out byte[] right, out Failure)) return false;
			if (left.Length != right.Length)
			{
				Failure = "Settlement graph lengths differ.";
				return false;
			}
			int difference = 0;
			for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
			if (difference != 0) Failure = "Settlement graphs differ.";
			return difference == 0;
		}

		/// <summary>Rejects any mutable reference shared anywhere between two bounded realm
		/// graphs. Pairwise value comparison misses cross-root aliases (for example an archived
		/// seat list installed into the live away city); this scan treats the complete roots as
		/// one graph and uses reference identity, never value equality.</summary>
		public static bool DisjointMutableGraphs(object[] ArchivedRoots, object[] LiveRoots,
			out string Failure)
		{
			Failure = null;
			if (ArchivedRoots == null || LiveRoots == null)
			{
				Failure = "Realm graph roots are absent.";
				return false;
			}
			HashSet<object> archived = new HashSet<object>(new ReferenceComparer());
			Budget budget = new Budget();
			for (int i = 0; i < ArchivedRoots.Length; i++)
			{
				object root = ArchivedRoots[i];
				if (root == null) continue;
				HashSet<object> seen = new HashSet<object>(new ReferenceComparer());
				HashSet<object> collected = new HashSet<object>(new ReferenceComparer());
				if (!ScanMutable(root, root.GetType(), 0, budget, seen, collected,
					archived, out Failure)) return false;
				archived.UnionWith(collected);
			}
			budget.Objects = 0;
			HashSet<object> forbidden = new HashSet<object>(archived,
				new ReferenceComparer());
			for (int i = 0; i < LiveRoots.Length; i++)
			{
				object root = LiveRoots[i];
				if (root == null) continue;
				HashSet<object> seen = new HashSet<object>(new ReferenceComparer());
				HashSet<object> collected = new HashSet<object>(new ReferenceComparer());
				if (!ScanMutable(root, root.GetType(), 0, budget, seen, collected,
					forbidden, out Failure)) return false;
				forbidden.UnionWith(collected);
			}
			return true;
		}

		internal static bool EmptyRegistries(
			Simulation.City.KingdomBindingRegistry Bindings,
			Simulation.City.KingdomJobRegistry Jobs)
		{
			return Bindings != null && Bindings.Keys != null && Bindings.Keys.Count == 0 &&
				Bindings.Kinds != null && Bindings.Kinds.Count == 0 &&
				Bindings.ZoneIds != null && Bindings.ZoneIds.Count == 0 &&
				Bindings.ObjectIds != null && Bindings.ObjectIds.Count == 0 &&
				Bindings.MintedTicks != null && Bindings.MintedTicks.Count == 0 && Jobs != null &&
				Jobs.JobCounter == 0 && Jobs.JobIds != null && Jobs.JobIds.Count == 0 &&
				Jobs.Kinds != null && Jobs.Kinds.Count == 0 && Jobs.Cargos != null &&
				Jobs.Cargos.Count == 0 && Jobs.CargoAmounts != null &&
				Jobs.CargoAmounts.Count == 0 && Jobs.SourceZoneIds != null &&
				Jobs.SourceZoneIds.Count == 0 && Jobs.DestZoneIds != null &&
				Jobs.DestZoneIds.Count == 0 && Jobs.StartTicks != null &&
				Jobs.StartTicks.Count == 0 && Jobs.WalkTicksPerCell != null &&
				Jobs.WalkTicksPerCell.Count == 0 && Jobs.Statuses != null &&
				Jobs.Statuses.Count == 0 && Jobs.OriginCodes != null &&
				Jobs.OriginCodes.Count == 0 && Jobs.DepositLegIndexes != null &&
				Jobs.DepositLegIndexes.Count == 0 && Jobs.SubjectIds != null &&
				Jobs.SubjectIds.Count == 0 && Jobs.SubjectNames != null &&
				Jobs.SubjectNames.Count == 0 && Jobs.TargetNames != null &&
				Jobs.TargetNames.Count == 0 && Jobs.DueTicks != null &&
				Jobs.DueTicks.Count == 0 && Jobs.WaterCosts != null &&
				Jobs.WaterCosts.Count == 0 && Jobs.ProvisionCosts != null &&
				Jobs.ProvisionCosts.Count == 0 && Jobs.OutcomeCodes != null &&
				Jobs.OutcomeCodes.Count == 0 && Jobs.DeliverySourceEndpointIds != null &&
				Jobs.DeliverySourceEndpointIds.Count == 0 &&
				Jobs.DeliverySourceObjectIds != null && Jobs.DeliverySourceObjectIds.Count == 0 &&
				Jobs.DeliverySourceXs != null && Jobs.DeliverySourceXs.Count == 0 &&
				Jobs.DeliverySourceYs != null && Jobs.DeliverySourceYs.Count == 0 &&
				Jobs.DeliveryTargetEndpointIds != null &&
				Jobs.DeliveryTargetEndpointIds.Count == 0 &&
				Jobs.DeliveryTargetObjectIds != null && Jobs.DeliveryTargetObjectIds.Count == 0 &&
				Jobs.DeliveryTargetXs != null && Jobs.DeliveryTargetXs.Count == 0 &&
				Jobs.DeliveryTargetYs != null && Jobs.DeliveryTargetYs.Count == 0 &&
				Jobs.DeliverySourceBeforeAmounts != null &&
				Jobs.DeliverySourceBeforeAmounts.Count == 0 && Jobs.DeliveryTripIds != null &&
				Jobs.DeliveryTripIds.Count == 0 && Jobs.DeliveryStopOrdinals != null &&
				Jobs.DeliveryStopOrdinals.Count == 0 && Jobs.DeliveryPhases != null &&
				Jobs.DeliveryPhases.Count == 0 && Jobs.DeliveryCargoAuthorityKinds != null &&
				Jobs.DeliveryCargoAuthorityKinds.Count == 0 &&
				Jobs.DeliveryOwnerOperationIds != null && Jobs.DeliveryOwnerOperationIds.Count == 0 &&
				Jobs.DeliveryOwnerManifestVersions != null && Jobs.DeliveryOwnerManifestVersions.Count == 0 &&
				Jobs.DeliveryOwnerManifestDigests != null && Jobs.DeliveryOwnerManifestDigests.Count == 0 &&
				Jobs.DeliveryOwnerManifestRevisions != null && Jobs.DeliveryOwnerManifestRevisions.Count == 0 &&
				Jobs.DeliveryManifestSourceStarts != null && Jobs.DeliveryManifestSourceStarts.Count == 0 &&
				Jobs.DeliveryManifestSourceCounts != null && Jobs.DeliveryManifestSourceCounts.Count == 0 &&
				Jobs.DeliveryTargetBeforeAmounts != null && Jobs.DeliveryTargetBeforeAmounts.Count == 0 &&
				Jobs.DeliveryTargetReceiptStates != null && Jobs.DeliveryTargetReceiptStates.Count == 0 &&
				Jobs.LegCounts != null &&
				Jobs.LegCounts.Count == 0 && Jobs.LegZoneIds != null &&
				Jobs.LegZoneIds.Count == 0 && Jobs.LegEnterX != null &&
				Jobs.LegEnterX.Count == 0 && Jobs.LegEnterY != null &&
				Jobs.LegEnterY.Count == 0 && Jobs.LegExitX != null &&
				Jobs.LegExitX.Count == 0 && Jobs.LegExitY != null &&
				Jobs.LegExitY.Count == 0 && Jobs.LegLengths != null &&
				Jobs.LegLengths.Count == 0 && Jobs.LegDepartTicks != null &&
				Jobs.LegDepartTicks.Count == 0 && Jobs.LegArriveTicks != null &&
				Jobs.LegArriveTicks.Count == 0;
		}

		internal static bool EmptyCarry(KingdomCarryBook Value)
		{
			return TryCarryBytes(new KingdomCarryBook(), out byte[] expected) &&
				TryCarryBytes(Value, out byte[] actual) && ExactBytes(expected, actual);
		}

		private static bool TryCarryBytes(KingdomCarryBook Value, out byte[] Bytes)
		{
			Bytes = null;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					KingdomLifecycleWireCodec.WriteCarry(writer, Value);
					writer.Flush();
					if (stream.Length > MaxPayloadBytes) return false;
					Bytes = stream.ToArray();
					return true;
				}
			}
			catch
			{
				return false;
			}
		}

		private static bool ExactBytes(byte[] Left, byte[] Right)
		{
			if (Left == null || Right == null || Left.Length != Right.Length) return false;
			int difference = 0;
			for (int i = 0; i < Left.Length; i++) difference |= Left[i] ^ Right[i];
			return difference == 0;
		}

		private static bool ScanMutable(object Value, Type Type, int Depth, Budget Budget,
			HashSet<object> Seen, HashSet<object> Collected, HashSet<object> Forbidden,
			out string Failure)
		{
			Failure = null;
			if (Value == null || Type == typeof(string) || Type.IsPrimitive || Type.IsEnum)
				return true;
			if (Value.GetType() != Type)
			{
				Failure = "Realm reference runtime type is not exact: " + Type.FullName + ".";
				return false;
			}
			if (Forbidden != null && Forbidden.Contains(Value))
			{
				Failure = "Archived and live realm graphs share mutable " + Type.FullName + ".";
				return false;
			}
			if (!Seen.Add(Value))
			{
				Failure = "Realm graph repeats mutable " + Type.FullName + ".";
				return false;
			}
			if (++Budget.Objects > MaxObjects || Depth > MaxDepth)
			{
				Failure = "Realm reference graph exceeds proof bounds.";
				return false;
			}
			Collected?.Add(Value);
			if (Type == typeof(byte[]))
			{
				if (((byte[])Value).Length > MaxByteArrayBytes)
				{
					Failure = "Realm byte array exceeds proof cap.";
					return false;
				}
				return true;
			}
			if (IsList(Type))
			{
				IList list = (IList)Value;
				if (list.Count > MaxCollectionCount)
				{
					Failure = "Realm reference list exceeds proof cap.";
					return false;
				}
				Type item = Type.GetGenericArguments()[0];
				for (int i = 0; i < list.Count; i++)
					if (!ScanMutable(list[i], item, Depth + 1, Budget, Seen, Collected,
						Forbidden, out Failure)) return false;
				return true;
			}
			if (IsDictionary(Type))
			{
				IDictionary dictionary = (IDictionary)Value;
				if (!CanonicalDictionaryComparer(Type, dictionary))
				{
					Failure = "Realm dictionary comparer is noncanonical.";
					return false;
				}
				if (dictionary.Count > MaxCollectionCount)
				{
					Failure = "Realm reference dictionary exceeds proof cap.";
					return false;
				}
				Type[] arguments = Type.GetGenericArguments();
				foreach (DictionaryEntry row in dictionary)
				{
					if (!ScanMutable(row.Key, arguments[0], Depth + 1, Budget, Seen,
						Collected, Forbidden, out Failure) ||
						!ScanMutable(row.Value, arguments[1], Depth + 1, Budget, Seen,
							Collected, Forbidden, out Failure)) return false;
				}
				return true;
			}
			if (!Approved(Type))
			{
				Failure = "Realm reference field type is unsupported: " + Type.FullName + ".";
				return false;
			}
			FieldInfo[] fields = Fields(Type);
			for (int i = 0; i < fields.Length; i++)
				if (!ScanMutable(fields[i].GetValue(Value), fields[i].FieldType, Depth + 1,
					Budget, Seen, Collected, Forbidden, out Failure)) return false;
			return true;
		}

		private static bool StrictMutableRoot(object Value, Type Type, out string Failure)
		{
			Failure = null;
			if (Value == null) return true;
			return ScanMutable(Value, Type, 0, new Budget(),
				new HashSet<object>(new ReferenceComparer()),
				new HashSet<object>(new ReferenceComparer()), null, out Failure);
		}

		private static bool ExactReferenceTopology(object Left, object Right, Type Type,
			int Depth, Budget Budget, Dictionary<object, object> LeftToRight,
			Dictionary<object, object> RightToLeft, out string Failure)
		{
			Failure = null;
			if (Left == null || Right == null)
			{
				if (Left == null && Right == null) return true;
				Failure = "Settlement reference topology differs at " + Type.FullName + ".";
				return false;
			}
			if (Type == typeof(string) || Type.IsPrimitive || Type.IsEnum) return true;
			if (Left.GetType() != Type || Right.GetType() != Type)
			{
				Failure = "Settlement runtime type differs from its declared schema type.";
				return false;
			}
			if (ReferenceEquals(Left, Right))
			{
				Failure = "Archived and live settlement graphs share mutable " + Type.FullName + ".";
				return false;
			}
			if (Depth > MaxDepth || ++Budget.Objects > MaxObjects)
			{
				Failure = "Settlement reference topology exceeds proof bounds.";
				return false;
			}
			bool leftMapped = LeftToRight.TryGetValue(Left, out object mappedRight);
			bool rightMapped = RightToLeft.TryGetValue(Right, out object mappedLeft);
			if (leftMapped || rightMapped)
			{
				if (ReferenceEquals(mappedRight, Right) && ReferenceEquals(mappedLeft, Left))
					return true;
				Failure = "Settlement reference topology is not one-to-one.";
				return false;
			}
			LeftToRight.Add(Left, Right);
			RightToLeft.Add(Right, Left);
			if (Type == typeof(byte[]))
			{
				byte[] left = (byte[])Left;
				byte[] right = (byte[])Right;
				if (left.Length != right.Length || left.Length > MaxByteArrayBytes)
				{
					Failure = "Settlement byte-array topology or bound differs.";
					return false;
				}
				int difference = 0;
				for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
				if (difference != 0) Failure = "Settlement byte arrays differ.";
				return difference == 0;
			}
			if (IsList(Type))
			{
				IList left = (IList)Left;
				IList right = (IList)Right;
				if (left.Count != right.Count || left.Count > MaxCollectionCount)
				{
					Failure = "Settlement list topology or bound differs.";
					return false;
				}
				Type item = Type.GetGenericArguments()[0];
				for (int i = 0; i < left.Count; i++)
					if (!ExactReferenceTopology(left[i], right[i], item, Depth + 1,
						Budget, LeftToRight, RightToLeft, out Failure)) return false;
				return true;
			}
			if (IsDictionary(Type))
			{
				IDictionary left = (IDictionary)Left;
				IDictionary right = (IDictionary)Right;
				if (!CanonicalDictionaryComparer(Type, left)
					|| !CanonicalDictionaryComparer(Type, right))
				{
					Failure = "Settlement dictionary comparer is noncanonical.";
					return false;
				}
				if (left.Count != right.Count || left.Count > MaxCollectionCount)
				{
					Failure = "Settlement dictionary topology or bound differs.";
					return false;
				}
				Type[] arguments = Type.GetGenericArguments();
				if (arguments[0] != typeof(string))
				{
					Failure = "Settlement dictionary key topology is unsupported.";
					return false;
				}
				foreach (DictionaryEntry row in left)
				{
					if (!(row.Key is string key) || !right.Contains(key) ||
						!ExactReferenceTopology(row.Value, right[key], arguments[1],
							Depth + 1, Budget, LeftToRight, RightToLeft, out Failure))
					{
						Failure = Failure ?? "Settlement dictionary keys differ.";
						return false;
					}
				}
				return true;
			}
			if (!Approved(Type))
			{
				Failure = "Settlement reference field type is unsupported: " + Type.FullName + ".";
				return false;
			}
			FieldInfo[] fields = Fields(Type);
			for (int i = 0; i < fields.Length; i++)
				if (!ExactReferenceTopology(fields[i].GetValue(Left), fields[i].GetValue(Right),
					fields[i].FieldType, Depth + 1, Budget, LeftToRight, RightToLeft,
					out Failure)) return false;
			return true;
		}

		private static void WriteValue(BinaryWriter Writer, Type Type, object Value,
			int Depth, Budget Budget)
		{
			WriteValue(Writer, Type, Value, Depth, Budget, CurrentVersion);
		}

		private static void WriteValue(BinaryWriter Writer, Type Type, object Value,
			int Depth, Budget Budget, bool Legacy)
		{
			WriteValue(Writer, Type, Value, Depth, Budget,
				Legacy ? LegacyVersion : CurrentVersion);
		}

		private static void WriteValue(BinaryWriter Writer, Type Type, object Value,
			int Depth, Budget Budget, int SchemaVersion)
		{
			if (Depth > MaxDepth) throw new InvalidDataException("Archived settlement graph is too deep.");
			if (Value != null && !Type.IsValueType && Value.GetType() != Type)
				throw new InvalidDataException(
					"Archived settlement runtime type is not exact: " + Type.FullName + ".");
			if (Type == typeof(string))
			{
				WriteString(Writer, (string)Value, MaxStringBytes);
				return;
			}
			if (Type.IsEnum)
			{
				long raw = Convert.ToInt64(Value);
				if (!EnumRawFits(Type, raw) || !Enum.IsDefined(Type, Value))
					throw new InvalidDataException("Archived settlement enum value is unknown.");
				if (SchemaVersion == LegacyVersion
					&& Type == typeof(KingdomLifecycleResourceKind) &&
					raw > (long)KingdomLifecycleResourceKind.Raid)
					throw new InvalidDataException(
						"Archived settlement v1 resource kind is unknown.");
				if (SchemaVersion < RaidVersion
					&& Type == typeof(KingdomLifecycleAction)
					&& raw > (long)KingdomLifecycleAction.PetitionExpire)
					throw new InvalidDataException(
						"Archived settlement historical lifecycle action is unknown.");
				if (SchemaVersion >= RaidVersion && SchemaVersion < PhysicalHappeningVersion
					&& Type == typeof(KingdomLifecycleAction)
					&& raw > (long)KingdomLifecycleAction.RaidResolve)
					throw new InvalidDataException(
						"Archived settlement historical raid action is unknown.");
				if (SchemaVersion < PhysicalHappeningVersion
					&& Type == typeof(KingdomRaidIncidentState)
					&& raw > (long)KingdomRaidIncidentState.Queued)
					throw new InvalidDataException(
						"Archived settlement historical raid state is unknown.");
				Writer.Write(raw);
				return;
			}
			if (Type == typeof(bool)) { Writer.Write((bool)Value ? (byte)1 : (byte)0); return; }
			if (Type == typeof(byte)) { Writer.Write((byte)Value); return; }
			if (Type == typeof(short)) { Writer.Write((short)Value); return; }
			if (Type == typeof(int)) { Writer.Write((int)Value); return; }
			if (Type == typeof(long)) { Writer.Write((long)Value); return; }
			if (Type == typeof(ushort)) { Writer.Write((ushort)Value); return; }
			if (Type == typeof(uint)) { Writer.Write((uint)Value); return; }
			if (Type == typeof(ulong)) { Writer.Write((ulong)Value); return; }
			if (Type == typeof(byte[]))
			{
				if (Value == null) { Writer.Write(-1); return; }
				byte[] bytes = (byte[])Value;
				if (bytes.Length > MaxByteArrayBytes)
					throw new InvalidDataException("Archived settlement byte array exceeds cap.");
				Writer.Write(bytes.Length);
				Writer.Write(bytes);
				return;
			}
			if (IsList(Type))
			{
				if (Value == null) { Writer.Write(-1); return; }
				IList list = (IList)Value;
				if (list.Count > MaxCollectionCount)
					throw new InvalidDataException("Archived settlement list exceeds cap.");
				Writer.Write(list.Count);
				Type itemType = Type.GetGenericArguments()[0];
				for (int i = 0; i < list.Count; i++)
					WriteValue(Writer, itemType, list[i], Depth + 1, Budget, SchemaVersion);
				return;
			}
			if (IsDictionary(Type))
			{
				if (Value == null) { Writer.Write(-1); return; }
				IDictionary dictionary = (IDictionary)Value;
				if (!CanonicalDictionaryComparer(Type, dictionary))
					throw new InvalidDataException(
						"Archived settlement dictionary comparer is noncanonical.");
				if (dictionary.Count > MaxCollectionCount)
					throw new InvalidDataException("Archived settlement dictionary exceeds cap.");
				Type[] arguments = Type.GetGenericArguments();
				if (arguments[0] != typeof(string))
					throw new InvalidDataException("Archived settlement dictionary key type is unsupported.");
				List<string> keys = new List<string>(dictionary.Count);
				foreach (object key in dictionary.Keys)
				{
					if (!(key is string)) throw new InvalidDataException("Archived dictionary key is null.");
					keys.Add((string)key);
				}
				keys.Sort(StringComparer.Ordinal);
				Writer.Write(keys.Count);
				for (int i = 0; i < keys.Count; i++)
				{
					WriteString(Writer, keys[i], MaxStringBytes);
					WriteValue(Writer, arguments[1], dictionary[keys[i]], Depth + 1, Budget,
						SchemaVersion);
				}
				return;
			}
			if (!Approved(Type))
				throw new InvalidDataException("Archived settlement field type is unsupported: " + Type.FullName);
			Writer.Write(Value == null ? (byte)0 : (byte)1);
			if (Value == null) return;
			if (++Budget.Objects > MaxObjects)
				throw new InvalidDataException("Archived settlement object count exceeds cap.");
			FieldInfo[] fields = Fields(Type, SchemaVersion);
			for (int i = 0; i < fields.Length; i++)
			{
				if (Type == typeof(Simulation.City.KingdomCityBook)
					&& (string.Equals(fields[i].Name, "ExtensionModel", StringComparison.Ordinal)
						|| string.Equals(fields[i].Name, "HappeningModel", StringComparison.Ordinal)
						|| string.Equals(fields[i].Name, "ExtensionHappeningCursors", StringComparison.Ordinal)))
				{
					int maximum = string.Equals(fields[i].Name, "ExtensionModel",
						StringComparison.Ordinal)
						? Simulation.City.KingdomCityBook.MaxExtensionModelChars
						: string.Equals(fields[i].Name, "HappeningModel", StringComparison.Ordinal)
							? Simulation.City.KingdomCityBook.MaxHappeningModelChars
							: Simulation.City.KingdomCityBook.MaxExtensionHappeningCursorChars;
					WriteString(Writer, (string)fields[i].GetValue(Value), maximum);
					continue;
				}
				object fieldValue = fields[i].GetValue(Value);
				if (Type == typeof(KingdomLifecycleBook)
					&& string.Equals(fields[i].Name, "FormatVersion", StringComparison.Ordinal)
					&& SchemaVersion < DefensiveReservationVersion)
					fieldValue = SchemaVersion == LegacyVersion
						? KingdomLifecycleRules.LegacyLifecycleFormatVersion
						: SchemaVersion == PreviousVersion
							? KingdomLifecycleRules.PreviousLifecycleFormatVersion
							: KingdomLifecycleRules.RaidLedgerLifecycleFormatVersion;
				if (Type == typeof(KingdomRaidLedger)
					&& string.Equals(fields[i].Name, "Version", StringComparison.Ordinal)
					&& SchemaVersion < DefensiveReservationVersion)
					fieldValue = SchemaVersion < PhysicalHappeningVersion ? 1 : 2;
				if (Type == typeof(Simulation.City.KingdomCityBook)
					&& string.Equals(fields[i].Name, "SchemaVersion", StringComparison.Ordinal)
					&& SchemaVersion < SemanticSelectionVersion)
					fieldValue = 2;
				if (Type == typeof(KingdomGrowthBook)
					&& string.Equals(fields[i].Name, "FormatVersion", StringComparison.Ordinal)
					&& SchemaVersion < SemanticSelectionVersion)
					fieldValue = KingdomLifecycleRules.PreviousGrowthFormatVersion;
				WriteValue(Writer, fields[i].FieldType, fieldValue,
					Depth + 1, Budget, SchemaVersion);
			}
		}

		private static object ReadValue(BinaryReader Reader, Type Type, int Depth,
			Budget Budget)
		{
			return ReadValue(Reader, Type, Depth, Budget, CurrentVersion);
		}

		private static object ReadValue(BinaryReader Reader, Type Type, int Depth,
			Budget Budget, int SchemaVersion)
		{
			if (Depth > MaxDepth) throw new InvalidDataException("Archived settlement graph is too deep.");
			if (Type == typeof(string)) return ReadString(Reader, MaxStringBytes, Required: false);
			if (Type.IsEnum)
			{
				long raw = Reader.ReadInt64();
				if (!EnumRawFits(Type, raw))
					throw new InvalidDataException(
						"Archived settlement enum encoding is noncanonical.");
				object value = Enum.ToObject(Type, raw);
				if (!Enum.IsDefined(Type, value))
					throw new InvalidDataException("Archived settlement enum value is unknown.");
				if (SchemaVersion == LegacyVersion
					&& Type == typeof(KingdomLifecycleResourceKind) &&
					raw > (long)KingdomLifecycleResourceKind.Raid)
					throw new InvalidDataException(
						"Archived settlement v1 resource kind is unknown.");
				if (SchemaVersion < RaidVersion
					&& Type == typeof(KingdomLifecycleAction)
					&& raw > (long)KingdomLifecycleAction.PetitionExpire)
					throw new InvalidDataException(
						"Archived settlement historical lifecycle action is unknown.");
				if (SchemaVersion >= RaidVersion && SchemaVersion < PhysicalHappeningVersion
					&& Type == typeof(KingdomLifecycleAction)
					&& raw > (long)KingdomLifecycleAction.RaidResolve)
					throw new InvalidDataException(
						"Archived settlement historical raid action is unknown.");
				if (SchemaVersion < PhysicalHappeningVersion
					&& Type == typeof(KingdomRaidIncidentState)
					&& raw > (long)KingdomRaidIncidentState.Queued)
					throw new InvalidDataException(
						"Archived settlement historical raid state is unknown.");
				return value;
			}
			if (Type == typeof(bool))
			{
				byte raw = Reader.ReadByte();
				if (raw > 1) throw new InvalidDataException("Archived settlement bool is noncanonical.");
				return raw == 1;
			}
			if (Type == typeof(byte)) return Reader.ReadByte();
			if (Type == typeof(short)) return Reader.ReadInt16();
			if (Type == typeof(int)) return Reader.ReadInt32();
			if (Type == typeof(long)) return Reader.ReadInt64();
			if (Type == typeof(ushort)) return Reader.ReadUInt16();
			if (Type == typeof(uint)) return Reader.ReadUInt32();
			if (Type == typeof(ulong)) return Reader.ReadUInt64();
			if (Type == typeof(byte[]))
			{
				int count = ReadCount(Reader, MaxByteArrayBytes, AllowNull: true);
				if (count == -1) return null;
				byte[] bytes = Reader.ReadBytes(count);
				if (bytes.Length != count)
					throw new EndOfStreamException("Archived settlement byte array is truncated.");
				return bytes;
			}
			if (IsList(Type))
			{
				int count = ReadCount(Reader, MaxCollectionCount, AllowNull: true);
				if (count == -1) return null;
				IList list = (IList)Activator.CreateInstance(Type, count);
				Type itemType = Type.GetGenericArguments()[0];
				for (int i = 0; i < count; i++)
					list.Add(ReadValue(Reader, itemType, Depth + 1, Budget, SchemaVersion));
				return list;
			}
			if (IsDictionary(Type))
			{
				int count = ReadCount(Reader, MaxCollectionCount, AllowNull: true);
				if (count == -1) return null;
				Type[] arguments = Type.GetGenericArguments();
				if (arguments[0] != typeof(string))
					throw new InvalidDataException("Archived settlement dictionary key type is unsupported.");
				IDictionary dictionary = (IDictionary)Activator.CreateInstance(Type, count);
				string previous = null;
				for (int i = 0; i < count; i++)
				{
					string key = ReadString(Reader, MaxStringBytes, Required: true);
					if (previous != null && string.CompareOrdinal(previous, key) >= 0)
						throw new InvalidDataException("Archived settlement dictionary order is noncanonical.");
					dictionary.Add(key, ReadValue(Reader, arguments[1], Depth + 1, Budget,
						SchemaVersion));
					previous = key;
				}
				return dictionary;
			}
			if (!Approved(Type))
				throw new InvalidDataException("Archived settlement field type is unsupported: " + Type.FullName);
			byte present = Reader.ReadByte();
			if (present > 1) throw new InvalidDataException("Archived settlement object flag is noncanonical.");
			if (present == 0) return null;
			if (++Budget.Objects > MaxObjects)
				throw new InvalidDataException("Archived settlement object count exceeds cap.");
			object result = Activator.CreateInstance(Type);
			FieldInfo[] fields = Fields(Type, SchemaVersion);
			for (int i = 0; i < fields.Length; i++)
			{
				if (Type == typeof(Simulation.City.KingdomCityBook)
					&& (string.Equals(fields[i].Name, "ExtensionModel", StringComparison.Ordinal)
						|| string.Equals(fields[i].Name, "HappeningModel", StringComparison.Ordinal)
						|| string.Equals(fields[i].Name, "ExtensionHappeningCursors", StringComparison.Ordinal)))
				{
					int maximum = string.Equals(fields[i].Name, "ExtensionModel",
						StringComparison.Ordinal)
						? Simulation.City.KingdomCityBook.MaxExtensionModelChars
						: string.Equals(fields[i].Name, "HappeningModel", StringComparison.Ordinal)
							? Simulation.City.KingdomCityBook.MaxHappeningModelChars
							: Simulation.City.KingdomCityBook.MaxExtensionHappeningCursorChars;
					fields[i].SetValue(result, ReadString(Reader, maximum, Required: false));
					continue;
				}
				fields[i].SetValue(result, ReadValue(Reader, fields[i].FieldType,
					Depth + 1, Budget, SchemaVersion));
			}
			return result;
		}

		private static int ReadCount(BinaryReader Reader, int Maximum, bool AllowNull)
		{
			int count = Reader.ReadInt32();
			if ((AllowNull && count == -1) || (count >= 0 && count <= Maximum)) return count;
			throw new InvalidDataException("Archived settlement collection count exceeds cap.");
		}

		private static bool EnumRawFits(Type EnumType, long Raw)
		{
			Type underlying = Enum.GetUnderlyingType(EnumType);
			if (underlying == typeof(byte)) return Raw >= byte.MinValue && Raw <= byte.MaxValue;
			if (underlying == typeof(sbyte)) return Raw >= sbyte.MinValue && Raw <= sbyte.MaxValue;
			if (underlying == typeof(short)) return Raw >= short.MinValue && Raw <= short.MaxValue;
			if (underlying == typeof(ushort)) return Raw >= ushort.MinValue && Raw <= ushort.MaxValue;
			if (underlying == typeof(int)) return Raw >= int.MinValue && Raw <= int.MaxValue;
			if (underlying == typeof(uint)) return Raw >= uint.MinValue && Raw <= uint.MaxValue;
			if (underlying == typeof(long)) return true;
			if (underlying == typeof(ulong)) return Raw >= 0L;
			return false;
		}

		private static void WriteString(BinaryWriter Writer, string Value, int MaximumBytes)
		{
			if (Value == null) { Writer.Write(-1); return; }
			int count = StrictUtf8.GetByteCount(Value);
			if (count > MaximumBytes) throw new InvalidDataException("Archived settlement string exceeds cap.");
			Writer.Write(count);
			byte[] bytes = StrictUtf8.GetBytes(Value);
			Writer.Write(bytes);
		}

		private static string ReadString(BinaryReader Reader, int MaximumBytes, bool Required)
		{
			int length = Reader.ReadInt32();
			if (!Required && length == -1) return null;
			if (length < 0 || length > MaximumBytes)
				throw new InvalidDataException("Archived settlement string length exceeds cap.");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException("Archived settlement string is truncated.");
			return StrictUtf8.GetString(bytes);
		}

		private static bool IsList(Type Type)
		{
			return Type.IsGenericType && Type.GetGenericTypeDefinition() == typeof(List<>);
		}

		private static bool IsDictionary(Type Type)
		{
			return Type.IsGenericType && Type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
		}

		private static bool CanonicalDictionaryComparer(Type Type, IDictionary Value)
		{
			if (Value == null || !IsDictionary(Type)) return false;
			Type[] arguments = Type.GetGenericArguments();
			if (arguments[0] != typeof(string)) return false;
			PropertyInfo property = Type.GetProperty("Comparer", BindingFlags.Instance |
				BindingFlags.Public);
			if (property == null) return false;
			object comparer = property.GetValue(Value, null);
			return ReferenceEquals(comparer, EqualityComparer<string>.Default)
				|| ReferenceEquals(comparer, StringComparer.Ordinal);
		}

		private static bool Approved(Type Type)
		{
			// KingdomCarryHaul lives in the engine-coupled Guestbook file, which the pure test
			// project intentionally omits. Runtime reference scans still admit that exact type name.
			if (Type != null && Type.FullName == "ThousandAndFirst.KingdomCarryHaul") return true;
			for (int i = 0; i < ApprovedObjects.Length; i++)
				if (ApprovedObjects[i] == Type) return true;
			return false;
		}

		private static FieldInfo[] Fields(Type Type)
		{
			return Fields(Type, CurrentVersion);
		}

		private static FieldInfo[] Fields(Type Type, int SchemaVersion)
		{
			FieldInfo[] source = Type.GetFields(BindingFlags.Instance | BindingFlags.Public);
			List<FieldInfo> fields = new List<FieldInfo>(source.Length);
			for (int i = 0; i < source.Length; i++)
				if (!source[i].IsDefined(typeof(NonSerializedAttribute), false)
					&& SchemaField(Type, source[i].Name, SchemaVersion))
					fields.Add(source[i]);
			fields.Sort(delegate(FieldInfo Left, FieldInfo Right)
			{
				return string.CompareOrdinal(Left.Name, Right.Name);
			});
			return fields.ToArray();
		}

		/// <summary>Archive v1 predates nested Growth; v2 predates RaidLedger; v3 predates
		/// resident culture/species tallies; v4 predates extension-identity tallies; v5 predates
		/// causal pilgrims and expeditions; v6 predates the behaviour sidecar; v7 predates physical
		/// happenings; v8 predates exact central logistics; v9 predates exact defensive
		/// WorkId/resident reservations; v10 predates frozen semantic person plans; v11 predates
		/// independent extension-happening cursors. Historical
		/// readers retain exactly those surfaces
		/// rather than interpreting new default fields.</summary>
		private static bool SchemaField(Type Type, string Name, int SchemaVersion)
		{
			if (Type == typeof(KingdomLifecycleBook))
			{
				if (SchemaVersion == LegacyVersion
					&& string.Equals(Name, "Growth", StringComparison.Ordinal)) return false;
				if (SchemaVersion < RaidVersion
					&& string.Equals(Name, "RaidLedger", StringComparison.Ordinal)) return false;
			}
			if (Type == typeof(KingdomSettlement))
			{
				if (SchemaVersion < ResidentIdentityVersion
					&& (string.Equals(Name, "CultureCounts", StringComparison.Ordinal)
						|| string.Equals(Name, "SpeciesCounts", StringComparison.Ordinal))) return false;
				if (SchemaVersion < ExtensionIdentityVersion
					&& string.Equals(Name, "IdentityCounts", StringComparison.Ordinal)) return false;
				if (SchemaVersion < SemanticSelectionVersion
					&& string.Equals(Name, "OfficeHolderResidentId", StringComparison.Ordinal)) return false;
			}
			if (SchemaVersion < SalvageVersion
				&& Type == typeof(Simulation.City.KingdomCityBook)
				&& (string.Equals(Name, "PilgrimCause", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimCauseTick", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimGreeted", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimLoudness", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimName", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimObjectId", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimPlaceName", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimSequence", StringComparison.Ordinal)
					|| string.Equals(Name, "PilgrimState", StringComparison.Ordinal))) return false;
			if (SchemaVersion < SalvageVersion
				&& Type == typeof(Simulation.City.KingdomJobRegistry)
				&& (string.Equals(Name, "SubjectIds", StringComparison.Ordinal)
					|| string.Equals(Name, "SubjectNames", StringComparison.Ordinal)
					|| string.Equals(Name, "TargetNames", StringComparison.Ordinal)
					|| string.Equals(Name, "DueTicks", StringComparison.Ordinal)
					|| string.Equals(Name, "WaterCosts", StringComparison.Ordinal)
					|| string.Equals(Name, "ProvisionCosts", StringComparison.Ordinal)
					|| string.Equals(Name, "OutcomeCodes", StringComparison.Ordinal))) return false;
			if (SchemaVersion < SalvageVersion && Type == typeof(KingdomLedger)
				&& string.Equals(Name, "ExpeditionLines", StringComparison.Ordinal)) return false;
			if (SchemaVersion < BehaviourVersion
				&& Type == typeof(Simulation.City.KingdomCityBook)
				&& string.Equals(Name, "ExtensionModel", StringComparison.Ordinal)) return false;
			if (SchemaVersion < PhysicalHappeningVersion
				&& Type == typeof(Simulation.City.KingdomCityBook)
				&& string.Equals(Name, "HappeningModel", StringComparison.Ordinal)) return false;
			if (SchemaVersion < HappeningCursorVersion
				&& Type == typeof(Simulation.City.KingdomCityBook)
				&& string.Equals(Name, "ExtensionHappeningCursors", StringComparison.Ordinal)) return false;
			if (SchemaVersion < PhysicalHappeningVersion && Type == typeof(KingdomRaidLedger)
				&& string.Equals(Name, "OpaqueFuturePayload", StringComparison.Ordinal)) return false;
			if (SchemaVersion < PhysicalHappeningVersion && Type == typeof(KingdomRaidIncident)
				&& (string.Equals(Name, "AttackOperationId", StringComparison.Ordinal)
					|| string.Equals(Name, "ChannelRevision", StringComparison.Ordinal)
					|| string.Equals(Name, "ChannelState", StringComparison.Ordinal)
					|| string.Equals(Name, "DemandChannelId", StringComparison.Ordinal)
					|| string.Equals(Name, "DemandLeadTicks", StringComparison.Ordinal)
					|| string.Equals(Name, "DemandObjectId", StringComparison.Ordinal)
					|| string.Equals(Name, "FortifyOrderedTick", StringComparison.Ordinal)
					|| string.Equals(Name, "RecoveryNotice", StringComparison.Ordinal)
					|| string.Equals(Name, "RecoveryOpenedTick", StringComparison.Ordinal)
					|| string.Equals(Name, "RecoveryQuestId", StringComparison.Ordinal)
					|| string.Equals(Name, "RecoveryResolvedTick", StringComparison.Ordinal)
					|| string.Equals(Name, "RecoveryState", StringComparison.Ordinal)
					|| string.Equals(Name, "RecoveryStepId", StringComparison.Ordinal)
					|| string.Equals(Name, "RemainingLeadTicks", StringComparison.Ordinal))) return false;
			if (SchemaVersion < DefensiveReservationVersion
				&& Type == typeof(KingdomRaidIncident)
				&& (string.Equals(Name, "DefenceReservationVersion", StringComparison.Ordinal)
					|| string.Equals(Name, "DefenceReservations", StringComparison.Ordinal))) return false;
			if (SchemaVersion < SemanticSelectionVersion
				&& Type == typeof(KingdomGrowthArrivalCandidate)
				&& (string.Equals(Name, "LegacySemanticPlan", StringComparison.Ordinal)
					|| string.Equals(Name, "SemanticPlanVersion", StringComparison.Ordinal)
					|| string.Equals(Name, "SemanticStreamId", StringComparison.Ordinal)
					|| string.Equals(Name, "SemanticEventKind", StringComparison.Ordinal)
					|| string.Equals(Name, "PlannedOrigin", StringComparison.Ordinal)
					|| string.Equals(Name, "PlannedCreed", StringComparison.Ordinal)
					|| string.Equals(Name, "PlannedName", StringComparison.Ordinal)
					|| string.Equals(Name, "PlannedArrived", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalX", StringComparison.Ordinal)
					|| string.Equals(Name, "ArrivalY", StringComparison.Ordinal))) return false;
			if (SchemaVersion < SemanticSelectionVersion
				&& Type == typeof(KingdomRaidDefenceReservation)
				&& string.Equals(Name, "CrewSemanticIds", StringComparison.Ordinal)) return false;
			if (SchemaVersion < SemanticSelectionVersion
				&& Type == typeof(Simulation.City.KingdomCityBook)
				&& (string.Equals(Name, "ResidentOrigins", StringComparison.Ordinal)
					|| string.Equals(Name, "ResidentArrived", StringComparison.Ordinal))) return false;
			if (SchemaVersion < ExactLogisticsVersion
				&& Type == typeof(Simulation.City.KingdomJobRegistry)
				&& (string.Equals(Name, "DeliverySourceEndpointIds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliverySourceObjectIds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliverySourceXs", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliverySourceYs", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTargetEndpointIds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTargetObjectIds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTargetXs", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTargetYs", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliverySourceBeforeAmounts", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTripIds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryStopOrdinals", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryPhases", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryCargoAuthorityKinds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryOwnerOperationIds", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryOwnerManifestVersions", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryOwnerManifestDigests", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryOwnerManifestRevisions", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryManifestSourceStarts", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryManifestSourceCounts", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTargetBeforeAmounts", StringComparison.Ordinal)
					|| string.Equals(Name, "DeliveryTargetReceiptStates", StringComparison.Ordinal))) return false;
			return true;
		}

		private static string Shape(Type Root)
		{
			return Shape(Root, CurrentVersion);
		}

		private static string Shape(Type Root, bool Legacy)
		{
			return Shape(Root, Legacy ? LegacyVersion : CurrentVersion);
		}

		private static string Shape(Type Root, int SchemaVersion)
		{
			StringBuilder shape = new StringBuilder();
			HashSet<Type> visited = new HashSet<Type>();
			AppendShape(shape, Root, visited, SchemaVersion);
			if (StrictUtf8.GetByteCount(shape.ToString()) > MaxShapeBytes)
				throw new InvalidDataException("Archived settlement schema shape exceeds cap.");
			return shape.ToString();
		}

		private static void AppendShape(StringBuilder Shape, Type Type,
			HashSet<Type> Visited, int SchemaVersion)
		{
			if (Type.IsEnum)
			{
				if (SchemaVersion == LegacyVersion)
				{
					Shape.Append(Type.FullName).Append(';');
					return;
				}
				Type underlying = Enum.GetUnderlyingType(Type);
				Shape.Append("enum:").Append(Type.FullName).Append('<')
					.Append(underlying.FullName).Append(">{");
				string[] names = Enum.GetNames(Type);
				Array.Sort(names, StringComparer.Ordinal);
				bool unsigned = underlying == typeof(byte) || underlying == typeof(ushort) ||
					underlying == typeof(uint) || underlying == typeof(ulong);
				for (int i = 0; i < names.Length; i++)
				{
					object value = Enum.Parse(Type, names[i]);
					if (SchemaVersion < RaidVersion
						&& Type == typeof(KingdomLifecycleAction)
						&& Convert.ToInt64(value) > (long)KingdomLifecycleAction.PetitionExpire)
						continue;
					if (SchemaVersion >= RaidVersion && SchemaVersion < PhysicalHappeningVersion
						&& Type == typeof(KingdomLifecycleAction)
						&& Convert.ToInt64(value) > (long)KingdomLifecycleAction.RaidResolve)
						continue;
					if (SchemaVersion < PhysicalHappeningVersion
						&& Type == typeof(KingdomRaidIncidentState)
						&& Convert.ToInt64(value) > (long)KingdomRaidIncidentState.Queued)
						continue;
					Shape.Append(names[i]).Append('=');
					if (unsigned)
						Shape.Append(Convert.ToUInt64(value).ToString(CultureInfo.InvariantCulture));
					else
						Shape.Append(Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture));
					Shape.Append(';');
				}
				Shape.Append("};");
				return;
			}
			if (Type.IsPrimitive || Type == typeof(string))
			{
				Shape.Append(Type.FullName).Append(';');
				return;
			}
			if (Type == typeof(byte[]))
			{
				Shape.Append("bytes;");
				return;
			}
			if (IsList(Type))
			{
				Shape.Append("list<"); AppendShape(Shape, Type.GetGenericArguments()[0],
					Visited, SchemaVersion);
				Shape.Append(">;"); return;
			}
			if (IsDictionary(Type))
			{
				Type[] arguments = Type.GetGenericArguments();
				Shape.Append("map<"); AppendShape(Shape, arguments[0], Visited, SchemaVersion);
				AppendShape(Shape, arguments[1], Visited, SchemaVersion); Shape.Append(">;"); return;
			}
			if (!Approved(Type)) throw new InvalidDataException(
				"Archived settlement schema includes unsupported type " + Type.FullName + ".");
			if (!Visited.Add(Type)) { Shape.Append("ref:").Append(Type.FullName).Append(';'); return; }
			Shape.Append("object:").Append(Type.FullName).Append('{');
			FieldInfo[] fields = Fields(Type, SchemaVersion);
			for (int i = 0; i < fields.Length; i++)
			{
				Shape.Append(fields[i].Name).Append(':');
				AppendShape(Shape, fields[i].FieldType, Visited, SchemaVersion);
			}
			Shape.Append("};");
		}

		private static string Bound(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "Archived settlement codec failed.";
			return Value.Length <= 512 ? Value : Value.Substring(0, 512);
		}
	}
}
