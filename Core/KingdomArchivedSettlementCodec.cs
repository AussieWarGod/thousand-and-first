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
	internal static partial class KingdomArchivedSettlementCodec
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
		/// <summary>First archive surface admitting construction-input delivery authority and its
		/// landed-awaiting-owner phase. The reflected field layout is unchanged from v12.</summary>
		public const int DeliveryDomainVersion = 13;
		/// <summary>First archive surface carrying city-local named-cook and assenting-moot
		/// receipts. Both are bounded semantic authorities; their native parts remain projections.</summary>
		public const int CivicAuthorityVersion = 14;
		/// <summary>First archive surface carrying Growth-owned first-guest correspondence facts,
		/// choice state, and the exact shared-capacity lease proof. Older archives retain their
		/// immutable candidate shape and migrate only from decoded evidence.</summary>
		public const int FirstGuestVersion = 15;
		/// <summary>First archive surface carrying the durable physical pre-citizen guest phase,
		/// its explicit player action receipt, and observed loaded-body terminal evidence.</summary>
		public const int PhysicalFirstGuestVersion = 16;
		/// <summary>First archive surface carrying fixed-rate arrival epochs, compressed semantic
		/// debt, one frozen opportunity, and exact candidate/operation opportunity bindings.</summary>
		public const int ArrivalCadenceVersion = 17;
		/// <summary>First archive surface carrying exact final expedition/deed outbox receipts.</summary>
		public const int ExpeditionResultVersion = 18;
		public const int CurrentVersion = ExpeditionResultVersion;
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
			typeof(KingdomLifecycleLodgeTerminalReceipt),
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
			typeof(KingdomGrowthArrivalDebtRange),
			typeof(KingdomGrowthArrivalOpportunity),
			typeof(KingdomGrowthFirstGuestOpportunity),
			typeof(KingdomGrowthFirstGuestTerminalReceipt),
			typeof(KingdomCarryBook),
			typeof(KingdomCarryOperation),
			typeof(KingdomCarrySource),
			typeof(KingdomNamedCookReceipt),
			typeof(KingdomAssentingMootReceipt),
			typeof(Simulation.City.KingdomCityBook),
			typeof(Simulation.City.KingdomBindingRegistry),
			typeof(Simulation.City.KingdomJobRegistry)
		};

	}
}
