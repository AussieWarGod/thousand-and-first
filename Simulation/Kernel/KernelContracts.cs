using System;
using System.Text;

namespace ThousandAndFirst.Simulation.Kernel
{
	/// <summary>
	/// Why a kernel operation refused. Every <c>Try*</c> API is total over CLR-representable
	/// input: it returns false, sets one of these, and publishes no partial value.
	/// </summary>
	internal enum KernelFaultCode : byte
	{
		None = 0,
		InvalidTick = 1,
		InvalidInterval = 2,
		ClockRegression = 3,
		ArithmeticOverflow = 4,
		InvalidOptionLatch = 5,
		InvalidEventKey = 6,
		InvalidToyState = 7,
		CounterExhausted = 8,
		InvalidRandomBound = 9,
		CryptographicFailure = 10
	}

	/// <summary>
	/// The simulation seed, as input data only.
	/// <para>
	/// This slice never generates one. An all-zero value is legal so that default-value and
	/// golden tests are unambiguous; live seed generation belongs to the founding slice, and
	/// whatever mints it must domain-separate on realm incarnation.
	/// </para>
	/// </summary>
	internal readonly struct KernelSeed128 : IEquatable<KernelSeed128>
	{
		internal readonly ulong High;

		internal readonly ulong Low;

		internal KernelSeed128(ulong high, ulong low)
		{
			High = high;
			Low = low;
		}

		/// <summary>
		/// Value equality only. There is deliberately no runtime hash override and no
		/// <c>Equals(object)</c> override: runtime hashing is not stable across processes, and a
		/// hash on an identity-bearing kernel type is an invitation to key a collection by it and
		/// then persist or compare that ordering. Identity in this kernel travels one way, through
		/// the canonical encoder. Nothing here is used as a dictionary or set key.
		/// <para>
		/// The method name is deliberately not written out anywhere below this namespace: the
		/// release gate greps for it, and a comment that names the thing it forbids fails the same
		/// scan as the thing itself. Do not "clarify" this by spelling it.
		/// </para>
		/// </summary>
		public bool Equals(KernelSeed128 other)
		{
			return High == other.High && Low == other.Low;
		}
	}

	/// <summary>
	/// The frozen grammar for semantic identifiers.
	/// <para>
	/// 5 to 128 bytes, an exact lowercase <c>taf:</c> prefix, then only ASCII <c>a</c>-<c>z</c>,
	/// <c>0</c>-<c>9</c>, <c>.</c>, <c>_</c>, <c>:</c>, or <c>-</c>. Frozen now because every
	/// future generated or migrated ID must fit it, and later validation may not narrow it for
	/// IDs already persisted under it.
	/// </para>
	/// </summary>
	internal static class KernelSemanticId
	{
		internal const int MaxUtf8Bytes = 128;

		/// <summary>
		/// Private: the card names the upper bound but not this one, and nothing outside this
		/// class reads it. The grammar is enforced by <see cref="IsValid"/>, not by callers
		/// comparing against the constant themselves.
		/// </summary>
		private const int MinUtf8Bytes = 5;

		private const string RequiredPrefix = "taf:";

		internal static bool IsValid(string value)
		{
			if (value == null)
			{
				return false;
			}
			// Length is measured in bytes, and the grammar is ASCII-only, so char count and byte
			// count coincide for anything that can pass. Checking chars first means a hostile
			// long string never reaches an allocation.
			int length = value.Length;
			if (length < MinUtf8Bytes || length > MaxUtf8Bytes)
			{
				return false;
			}
			if (!StartsWithPrefix(value))
			{
				return false;
			}
			// "taf:" alone is a prefix, not an identifier.
			if (length == RequiredPrefix.Length)
			{
				return false;
			}
			for (int i = RequiredPrefix.Length; i < length; i++)
			{
				if (!IsAllowedBodyChar(value[i]))
				{
					return false;
				}
			}
			return true;
		}

		private static bool StartsWithPrefix(string value)
		{
			if (value.Length < RequiredPrefix.Length)
			{
				return false;
			}
			for (int i = 0; i < RequiredPrefix.Length; i++)
			{
				if (value[i] != RequiredPrefix[i])
				{
					return false;
				}
			}
			return true;
		}

		private static bool IsAllowedBodyChar(char c)
		{
			if (c >= 'a' && c <= 'z')
			{
				return true;
			}
			if (c >= '0' && c <= '9')
			{
				return true;
			}
			return c == '.' || c == '_' || c == ':' || c == '-';
		}
	}

	/// <summary>
	/// Identity of one semantic event: which rules minted it, which settlement owns it, which
	/// ordinal lane it belongs to, and where in that lane it sits.
	/// <para>
	/// The key owns its rules version forever. A global rules upgrade cannot rewrite an existing
	/// key, because doing so would change every event ID and every draw derived from it.
	/// </para>
	/// <para>
	/// <see cref="EventStreamId"/> exists because ordinal ownership is one
	/// <c>(EventStreamId, EventKindCode)</c> lane. Two routes sitting at ordinal zero must not
	/// collide merely because they share a settlement and an event kind — and the repair is a
	/// distinct stream per source, never a stream-global counter whose other kinds could shift
	/// this lane.
	/// </para>
	/// </summary>
	internal readonly struct SemanticEventKey : IEquatable<SemanticEventKey>
	{
		internal readonly int RulesVersionAtCreation;

		internal readonly string SettlementId;

		internal readonly string EventStreamId;

		internal readonly uint EventKindCode;

		internal readonly ulong EventOrdinal;

		private SemanticEventKey(
			int rulesVersionAtCreation,
			string settlementId,
			string eventStreamId,
			uint eventKindCode,
			ulong eventOrdinal)
		{
			RulesVersionAtCreation = rulesVersionAtCreation;
			SettlementId = settlementId;
			EventStreamId = eventStreamId;
			EventKindCode = eventKindCode;
			EventOrdinal = eventOrdinal;
		}

		/// <summary>
		/// Validation order is frozen so that combined-invalid input cannot vary by
		/// implementation: rules version, then either ID grammar, then kind.
		/// </summary>
		internal static bool TryCreate(
			int rulesVersionAtCreation,
			string settlementId,
			string eventStreamId,
			uint eventKindCode,
			ulong eventOrdinal,
			out SemanticEventKey key,
			out KernelFaultCode fault)
		{
			key = default(SemanticEventKey);
			if (rulesVersionAtCreation < 1
				|| !KernelSemanticId.IsValid(settlementId)
				|| !KernelSemanticId.IsValid(eventStreamId)
				|| eventKindCode == 0u)
			{
				fault = KernelFaultCode.InvalidEventKey;
				return false;
			}
			key = new SemanticEventKey(rulesVersionAtCreation, settlementId, eventStreamId, eventKindCode, eventOrdinal);
			fault = KernelFaultCode.None;
			return true;
		}

		/// <summary>
		/// Value equality only, for the same reason as <see cref="KernelSeed128"/>: no runtime
		/// hash override and no <c>Equals(object)</c>. An event key is an identity, and the only
		/// identity this kernel recognises is the one the canonical encoder produces. A runtime
		/// hash beside it is a second, unstable answer to the same question.
		/// </summary>
		public bool Equals(SemanticEventKey other)
		{
			return RulesVersionAtCreation == other.RulesVersionAtCreation
				&& EventKindCode == other.EventKindCode
				&& EventOrdinal == other.EventOrdinal
				&& string.Equals(SettlementId, other.SettlementId, StringComparison.Ordinal)
				&& string.Equals(EventStreamId, other.EventStreamId, StringComparison.Ordinal);
		}
	}
}
