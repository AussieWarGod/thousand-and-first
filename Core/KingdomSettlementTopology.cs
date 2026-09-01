using System;
using System.Collections.Generic;
using System.IO;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>Bounded persisted collection of the realm's non-seat settlements. The seat stays
	/// in <see cref="KingdomSystem"/>'s flat fields; every other owned city lives here.</summary>
	[Serializable]
	public sealed class KingdomSettlementTopology
#if !TAF_TESTS
		: IComposite
#endif
	{
		private const int Magic = 0x54415431; // TAT1
		private const int CurrentVersion = 1;
		private readonly List<KingdomSettlement> settlements =
			new List<KingdomSettlement>();
		private readonly List<byte[]> opaque = new List<byte[]>();

		public int Count => settlements.Count;
		public bool HasOpaqueEvidence
		{
			get
			{
				for (int i = 0; i < opaque.Count; i++) if (opaque[i] != null) return true;
				return false;
			}
		}

		public KingdomSettlement Get(int Index)
		{
			return Index < 0 || Index >= settlements.Count ? null : settlements[Index];
		}

		public List<KingdomSettlement> Snapshot()
		{
			return new List<KingdomSettlement>(settlements);
		}

		internal bool TryAdd(KingdomSettlement Settlement, out string Failure)
		{
			Failure = null;
			if (HasOpaqueEvidence || Settlement == null || Settlement.City == null ||
				!KingdomIdentityRules.IsSettlementId(Settlement.City.SettlementId) ||
				settlements.Count >= KingdomSettlementTopologyRules.MaxNonSeatSettlements)
			{
				Failure = "Non-seat settlement cannot enter the bounded exact topology.";
				return false;
			}
			for (int i = 0; i < settlements.Count; i++)
				if (string.Equals(settlements[i]?.City?.SettlementId,
					Settlement.City.SettlementId, StringComparison.Ordinal))
				{
					Failure = "Non-seat settlement identity is already owned.";
					return false;
				}
			settlements.Add(Settlement);
			opaque.Add(null);
			SortCurrent();
			return true;
		}

		internal bool TryAdoptLegacy(KingdomSettlement Settlement, out string Failure)
		{
			Failure = null;
			if (HasOpaqueEvidence || Settlement == null || settlements.Count != 0)
			{
				Failure = "Legacy Away evidence cannot enter a non-empty topology.";
				return false;
			}
			settlements.Add(Settlement);
			opaque.Add(null);
			return true;
		}

		internal bool TryReplaceReference(KingdomSettlement Expected,
			KingdomSettlement Replacement, out string Failure)
		{
			Failure = null;
			if (HasOpaqueEvidence || Expected == null || Replacement?.City == null ||
				!KingdomIdentityRules.IsSettlementId(Replacement.City.SettlementId))
			{
				Failure = "Seat exchange lacks exact current settlement values.";
				return false;
			}
			int index = settlements.FindIndex(delegate(KingdomSettlement row)
			{
				return ReferenceEquals(row, Expected);
			});
			if (index < 0)
			{
				Failure = "Seat exchange target is no longer in the topology.";
				return false;
			}
			for (int i = 0; i < settlements.Count; i++)
				if (i != index && string.Equals(settlements[i]?.City?.SettlementId,
					Replacement.City.SettlementId, StringComparison.Ordinal))
				{
					Failure = "Seat exchange would duplicate a settlement identity.";
					return false;
				}
			settlements[index] = Replacement;
			SortCurrent();
			return true;
		}

		internal bool TryRemoveReference(KingdomSettlement Expected, out string Failure)
		{
			Failure = null;
			int index = settlements.FindIndex(delegate(KingdomSettlement row)
			{
				return ReferenceEquals(row, Expected);
			});
			if (HasOpaqueEvidence || index < 0)
			{
				Failure = "Settlement is not an exact mutable topology member.";
				return false;
			}
			settlements.RemoveAt(index);
			opaque.RemoveAt(index);
			return true;
		}

		internal KingdomSettlement FindById(string SettlementId)
		{
			if (string.IsNullOrEmpty(SettlementId) || HasOpaqueEvidence) return null;
			KingdomSettlement found = null;
			for (int i = 0; i < settlements.Count; i++)
			{
				KingdomSettlement row = settlements[i];
				if (!string.Equals(row?.City?.SettlementId, SettlementId,
					StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = row;
			}
			return found;
		}

		internal bool TryFindByName(string SettlementName,
			out KingdomSettlement Settlement)
		{
			Settlement = null;
			if (string.IsNullOrEmpty(SettlementName) || HasOpaqueEvidence) return false;
			for (int i = 0; i < settlements.Count; i++)
			{
				KingdomSettlement row = settlements[i];
				if (!string.Equals(row?.SettlementName, SettlementName,
					StringComparison.Ordinal)) continue;
				if (Settlement != null)
				{
					Settlement = null;
					return false;
				}
				Settlement = row;
			}
			return Settlement != null;
		}

		internal KingdomSettlement FindByZone(string ZoneId)
		{
			if (string.IsNullOrEmpty(ZoneId) || HasOpaqueEvidence) return null;
			KingdomSettlement found = null;
			for (int i = 0; i < settlements.Count; i++)
			{
				KingdomSettlement row = settlements[i];
				if (row?.ClaimedZones == null || !row.ClaimedZones.Contains(ZoneId)) continue;
				if (found != null) return null;
				found = row;
			}
			return found;
		}

		internal bool TryClone(out KingdomSettlementTopology Clone, out string Failure)
		{
			Clone = null;
			Failure = null;
			if (HasOpaqueEvidence)
			{
				Failure = "Opaque settlement topology cannot become live mutable authority.";
				return false;
			}
			KingdomSettlementTopology candidate = new KingdomSettlementTopology();
			for (int i = 0; i < settlements.Count; i++)
			{
				if (!KingdomArchivedSettlementCodec.TryClone(settlements[i],
					out KingdomSettlement row, out Failure) ||
					!candidate.TryAdd(row, out Failure)) return false;
			}
			Clone = candidate;
			return true;
		}

		internal bool NormalizeCurrent(out string Failure)
		{
			Failure = null;
			if (settlements.Count != opaque.Count ||
				settlements.Count > KingdomSettlementTopologyRules.MaxNonSeatSettlements)
			{
				Failure = "Non-seat topology is ragged or exceeds its bound.";
				return false;
			}
			if (HasOpaqueEvidence)
			{
				Failure = "Non-seat topology contains future opaque settlement evidence.";
				return false;
			}
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < settlements.Count; i++)
			{
				KingdomSettlement row = settlements[i];
				row?.Normalize();
				if (row?.City == null || !KingdomIdentityRules.IsSettlementId(row.City.SettlementId)
					|| !ids.Add(row.City.SettlementId))
				{
					Failure = "Non-seat topology contains an invalid or duplicate city.";
					return false;
				}
			}
			SortCurrent();
			return true;
		}

		internal void NormalizeMembers()
		{
			if (HasOpaqueEvidence) return;
			for (int i = 0; i < settlements.Count; i++) settlements[i]?.Normalize();
		}

		private void SortCurrent()
		{
			settlements.Sort(delegate(KingdomSettlement left, KingdomSettlement right)
			{
				return string.CompareOrdinal(left?.City?.SettlementId,
					right?.City?.SettlementId);
			});
			while (opaque.Count < settlements.Count) opaque.Add(null);
		}

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			if (settlements.Count != opaque.Count ||
				settlements.Count > KingdomSettlementTopologyRules.MaxNonSeatSettlements)
				throw new InvalidDataException("Settlement topology exceeds its bound.");
			Writer.Write(Magic); Writer.Write(CurrentVersion); Writer.Write(settlements.Count);
			for (int i = 0; i < settlements.Count; i++)
			{
				byte[] payload = opaque[i];
				if (payload == null && !KingdomArchivedSettlementCodec.TryEncode(settlements[i],
					out payload, out string failure)) throw new InvalidDataException(failure);
				if (payload == null || payload.Length < 8 ||
					payload.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes)
					throw new InvalidDataException("Settlement topology payload exceeds its bound.");
				Writer.Write(payload.Length); Writer.Write(payload, 0, payload.Length);
			}
		}

		public void Read(SerializationReader Reader)
		{
			if (Reader.ReadInt32() != Magic || Reader.ReadInt32() != CurrentVersion)
				throw new InvalidDataException("Settlement topology marker or version is invalid.");
			int count = Reader.ReadInt32();
			if (count < 0 || count > KingdomSettlementTopologyRules.MaxNonSeatSettlements)
				throw new InvalidDataException("Settlement topology count exceeds its bound.");
			settlements.Clear(); opaque.Clear();
			for (int i = 0; i < count; i++)
			{
				int length = Reader.ReadInt32();
				if (length < 8 || length > KingdomArchivedSettlementCodec.MaxPayloadBytes)
					throw new InvalidDataException("Settlement topology row exceeds its bound.");
				byte[] payload = Reader.ReadBytesDirect(length);
				if (payload.Length != length) throw new EndOfStreamException(
					"Settlement topology row is truncated.");
				if (KingdomArchivedSettlementCodec.TryDecode(payload, out KingdomSettlement row,
					out int future, out string failure))
				{
					settlements.Add(row); opaque.Add(null);
				}
				else if (future > KingdomArchivedSettlementCodec.CurrentVersion)
				{
					settlements.Add(null); opaque.Add(payload);
				}
				else throw new InvalidDataException(failure);
			}
			if (!HasOpaqueEvidence && !NormalizeCurrent(out string topologyFailure))
				throw new InvalidDataException(topologyFailure);
		}
#endif
	}
}
