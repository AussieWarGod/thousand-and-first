#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	public class CounterRandomTests
	{
		private const string Draw0 = "6b2a09dd2a020d8e5aa08c6c327a8d2c7aeab45efa6886af5a020cba7a900c23";
		private const string Draw1 = "506b0507d47bec5f0021b715decbd172744710836083d1d5d193e9152ed09f81";
		private const string Draw7 = "c60ccd5d4e13c16e9d14cc185343169ae797d04f9d7d2fe8e97e41562f1743ab";
		private const string Draw8 = "6d73bfbff273248244edc87fb95d60b04659ddc38987423c4b699820868afbb0";

		private const ulong Draw7Raw = 14271007119855829358uL;

		// 2^63 + 1, chosen because its threshold is 2^63 - 1: the highest rejection rate any
		// bound can produce, and therefore the worst case for the rejection loop.
		private const ulong RejectionBound = 9223372036854775809uL;
		private const ulong RejectionThreshold = 9223372036854775807uL;
		private const ulong RejectBlock0Sample = 7721995356577336718uL;
		private const ulong RejectBlock1Sample = 15161002067840581987uL;
		private const ulong RejectionResult = 5937630030985806178uL;

		private static string BlockDigest(uint drawIndex, uint blockIndex)
		{
			byte[] preimage;
			KernelFaultCode fault;
			Assert.IsTrue(KernelCanonicalEncoding.TryEncodeRandomBlockPreimage(
				KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), drawIndex, blockIndex, out preimage, out fault));
			byte[] digest;
			Assert.IsTrue(KernelDigest.TryComputeSha256(preimage, out digest, out fault));
			return KernelDigest.ToLowercaseHex(digest);
		}

		[TestCase(0u, Draw0)]
		[TestCase(1u, Draw1)]
		[TestCase(7u, Draw7)]
		[TestCase(8u, Draw8)]
		public void RandomBlockGoldens(uint drawIndex, string expected)
		{
			Assert.AreEqual(expected, BlockDigest(drawIndex, 0u));
		}

		[Test]
		public void RawDrawTakesDigestBytesZeroThroughSevenBigEndian()
		{
			ulong value;
			KernelFaultCode fault;
			Assert.IsTrue(CounterRandom.TryDrawUInt64(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), 7u, out value, out fault));
			Assert.AreEqual(Draw7Raw, value);
		}

		/// <summary>
		/// Random access is the whole point: there is no cursor, so the order in which draws are
		/// requested cannot matter. Any drift here means something is carrying state between calls.
		/// </summary>
		[Test]
		public void DrawOrderNeverAffectsAnyValue()
		{
			Dictionary<uint, ulong> forward = new Dictionary<uint, ulong>();
			KernelFaultCode fault;
			for (uint i = 0; i < 16; i++)
			{
				ulong v;
				Assert.IsTrue(CounterRandom.TryDrawUInt64(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), i, out v, out fault));
				forward[i] = v;
			}

			for (int i = 15; i >= 0; i--)
			{
				ulong v;
				CounterRandom.TryDrawUInt64(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), (uint)i, out v, out fault);
				Assert.AreEqual(forward[(uint)i], v, "reverse order, index " + i);
			}

			uint[] shuffled = { 9u, 2u, 15u, 0u, 7u, 7u, 3u, 11u, 2u, 14u };
			foreach (uint index in shuffled)
			{
				ulong v;
				CounterRandom.TryDrawUInt64(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), index, out v, out fault);
				Assert.AreEqual(forward[index], v, "shuffled/duplicated, index " + index);
			}

			// Interleaving a different event must not perturb this one.
			SemanticEventKey other;
			Assert.IsTrue(SemanticEventKey.TryCreate(3, "taf:settlement:test", "taf:stream:other", 1u, 42uL, out other, out fault));
			for (uint i = 0; i < 16; i++)
			{
				ulong ignored;
				CounterRandom.TryDrawUInt64(KernelCanonicalTests.GoldenSeed(), other, i, out ignored, out fault);
				ulong v;
				CounterRandom.TryDrawUInt64(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), i, out v, out fault);
				Assert.AreEqual(forward[i], v, "interleaved, index " + i);
			}
		}

		[Test]
		public void EveryPreimageFieldChangesTheDigest()
		{
			string baseline = BlockDigest(0u, 0u);
			Assert.AreNotEqual(baseline, BlockDigest(1u, 0u), "draw index");
			Assert.AreNotEqual(baseline, BlockDigest(0u, 1u), "block index");

			KernelFaultCode fault;
			SemanticEventKey key;
			byte[] bytes;

			SemanticEventKey.TryCreate(4, "taf:settlement:test", "taf:stream:test", 1u, 42uL, out key, out fault);
			KernelCanonicalEncoding.TryEncodeRandomBlockPreimage(KernelCanonicalTests.GoldenSeed(), key, 0u, 0u, out bytes, out fault);
			Assert.AreNotEqual(baseline, DigestOf(bytes), "rules version");

			SemanticEventKey.TryCreate(3, "taf:settlement:other", "taf:stream:test", 1u, 42uL, out key, out fault);
			KernelCanonicalEncoding.TryEncodeRandomBlockPreimage(KernelCanonicalTests.GoldenSeed(), key, 0u, 0u, out bytes, out fault);
			Assert.AreNotEqual(baseline, DigestOf(bytes), "settlement");

			SemanticEventKey.TryCreate(3, "taf:settlement:test", "taf:stream:other", 1u, 42uL, out key, out fault);
			KernelCanonicalEncoding.TryEncodeRandomBlockPreimage(KernelCanonicalTests.GoldenSeed(), key, 0u, 0u, out bytes, out fault);
			Assert.AreNotEqual(baseline, DigestOf(bytes), "event stream");

			SemanticEventKey.TryCreate(3, "taf:settlement:test", "taf:stream:test", 2u, 42uL, out key, out fault);
			KernelCanonicalEncoding.TryEncodeRandomBlockPreimage(KernelCanonicalTests.GoldenSeed(), key, 0u, 0u, out bytes, out fault);
			Assert.AreNotEqual(baseline, DigestOf(bytes), "kind");

			SemanticEventKey.TryCreate(3, "taf:settlement:test", "taf:stream:test", 1u, 43uL, out key, out fault);
			KernelCanonicalEncoding.TryEncodeRandomBlockPreimage(KernelCanonicalTests.GoldenSeed(), key, 0u, 0u, out bytes, out fault);
			Assert.AreNotEqual(baseline, DigestOf(bytes), "ordinal");

			KernelCanonicalEncoding.TryEncodeRandomBlockPreimage(new KernelSeed128(1uL, 0uL), KernelCanonicalTests.GoldenKey(), 0u, 0u, out bytes, out fault);
			Assert.AreNotEqual(baseline, DigestOf(bytes), "seed");
		}

		[Test]
		public void TwoStreamsAtTheSameOrdinalAndKindDrawDifferently()
		{
			KernelFaultCode fault;
			SemanticEventKey alpha;
			SemanticEventKey beta;
			SemanticEventKey.TryCreate(3, "taf:settlement:test", "taf:route:alpha", 1u, 0uL, out alpha, out fault);
			SemanticEventKey.TryCreate(3, "taf:settlement:test", "taf:route:beta", 1u, 0uL, out beta, out fault);
			ulong a;
			ulong b;
			CounterRandom.TryDrawUInt64(KernelCanonicalTests.GoldenSeed(), alpha, 0u, out a, out fault);
			CounterRandom.TryDrawUInt64(KernelCanonicalTests.GoldenSeed(), beta, 0u, out b, out fault);
			Assert.AreNotEqual(a, b);
		}

		[Test]
		public void BoundedDrawMatchesTheGolden()
		{
			ulong value;
			KernelFaultCode fault;
			Assert.IsTrue(CounterRandom.TryDrawBelow(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), 7u, 100uL, out value, out fault));
			Assert.AreEqual(58uL, value);
		}

		/// <summary>
		/// The rejection path, pinned end to end: block 0 must be rejected, block 1 must be the
		/// one that succeeds, and the result must come from block 1. Pinning the block-1 digest is
		/// what stops a retry silently regressing to reusing block 0.
		/// </summary>
		[Test]
		public void RejectionGoldenWalksTheWorstCaseBound()
		{
			Assert.AreEqual(RejectionThreshold, unchecked(0uL - RejectionBound) % RejectionBound, "threshold identity");

			ulong block0;
			ulong block1;
			KernelFaultCode fault;
			Assert.IsTrue(CounterRandom.TryDrawUInt64(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), 0u, out block0, out fault));
			Assert.AreEqual(RejectBlock0Sample, block0);
			Assert.IsTrue(block0 < RejectionThreshold, "block 0 must reject");

			ulong ignored;
			Assert.IsFalse(CounterRandom.TryAcceptBoundedSample(block0, RejectionBound, out ignored));

			Assert.AreEqual("d266b30de499c16367f143feea3c08e6ef1c805b5689873c703effe4d28a57ef", BlockDigest(0u, 1u));

			byte[] preimage;
			KernelCanonicalEncoding.TryEncodeRandomBlockPreimage(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), 0u, 1u, out preimage, out fault);
			byte[] digest;
			KernelDigest.TryComputeSha256(preimage, out digest, out fault);
			ulong sample1 = 0uL;
			for (int i = 0; i < 8; i++)
			{
				sample1 = (sample1 << 8) | digest[i];
			}
			Assert.AreEqual(RejectBlock1Sample, sample1);
			Assert.IsTrue(sample1 >= RejectionThreshold, "block 1 must accept");
			Assert.AreEqual(block1 = sample1 % RejectionBound, RejectionResult);

			ulong drawn;
			Assert.IsTrue(CounterRandom.TryDrawBelow(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), 0u, RejectionBound, out drawn, out fault));
			Assert.AreEqual(RejectionResult, drawn, "the loop must land on the block-1 result");
			Assert.AreEqual(block1, drawn);
		}

		[Test]
		public void ZeroBoundIsInvalidAndPublishesNothing()
		{
			ulong value;
			KernelFaultCode fault;
			Assert.IsFalse(CounterRandom.TryDrawBelow(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), 0u, 0uL, out value, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidRandomBound, fault);
			Assert.AreEqual(0uL, value);

			ulong mapped;
			Assert.IsFalse(CounterRandom.TryAcceptBoundedSample(123uL, 0uL, out mapped));
			Assert.AreEqual(0uL, mapped);
		}

		[Test]
		public void BoundOfOneAlwaysYieldsZeroAndNeverRejects()
		{
			// Threshold is 0, so every sample is accepted on the first block.
			for (uint i = 0; i < 64; i++)
			{
				ulong value;
				KernelFaultCode fault;
				Assert.IsTrue(CounterRandom.TryDrawBelow(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), i, 1uL, out value, out fault));
				Assert.AreEqual(0uL, value);
			}
		}

		[Test]
		public void BlockIndexAdvancesExactlyAndFailsClosedAtTheMaximum()
		{
			uint next;
			KernelFaultCode fault;
			Assert.IsTrue(CounterRandom.TryNextRejectionBlockIndex(0u, out next, out fault));
			Assert.AreEqual(1u, next);
			Assert.AreEqual(KernelFaultCode.None, fault);

			Assert.IsTrue(CounterRandom.TryNextRejectionBlockIndex(uint.MaxValue - 1u, out next, out fault));
			Assert.AreEqual(uint.MaxValue, next);

			// Never wraps to zero: wrapping would re-test samples already rejected, forever.
			Assert.IsFalse(CounterRandom.TryNextRejectionBlockIndex(uint.MaxValue, out next, out fault));
			Assert.AreEqual(0u, next);
			Assert.AreEqual(KernelFaultCode.CounterExhausted, fault);
		}

		[Test]
		public void AnInvalidKeyFailsBeforeAnyDrawing()
		{
			ulong value;
			KernelFaultCode fault;
			Assert.IsFalse(CounterRandom.TryDrawUInt64(KernelCanonicalTests.GoldenSeed(), default(SemanticEventKey), 0u, out value, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidEventKey, fault);
			Assert.AreEqual(0uL, value);
		}

		[Test]
		public void BoundedDrawIsUniformEnoughToRuleOutModuloBias()
		{
			// A biased implementation using a bare modulus over a bound near 2^63 would put
			// roughly two thirds of its mass in the low half. This is a coarse smoke test, not a
			// statistical proof: it exists to catch the specific bug of dropping the rejection.
			const ulong bound = 3uL;
			int[] counts = new int[3];
			KernelFaultCode fault;
			for (uint i = 0; i < 3000; i++)
			{
				ulong value;
				Assert.IsTrue(CounterRandom.TryDrawBelow(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), i, bound, out value, out fault));
				Assert.IsTrue(value < bound);
				counts[(int)value]++;
			}
			for (int i = 0; i < counts.Length; i++)
			{
				Assert.IsTrue(counts[i] > 800 && counts[i] < 1200, "residue " + i + " appeared " + counts[i] + " times in 3000");
			}
		}

		/// <summary>
		/// The pinned bound list, including the three a naive implementation gets wrong:
		/// <c>2^63</c> (half the space, worst-case rejection), <c>2^32-1</c> (where a 32-bit
		/// intermediate would truncate), and <c>ulong.MaxValue</c> (where the threshold
		/// computation itself can overflow). Determinism and range only; nothing here is a
		/// distribution claim.
		/// </summary>
		[Test]
		public void EveryPinnedBoundProducesAValueInsideIt()
		{
			ulong[] bounds = { 1uL, 2uL, 3uL, 10uL, 100uL, 4294967295uL, 9223372036854775808uL, ulong.MaxValue };
			KernelFaultCode fault;
			foreach (ulong bound in bounds)
			{
				for (uint drawIndex = 0u; drawIndex < 64u; drawIndex++)
				{
					ulong value;
					Assert.IsTrue(CounterRandom.TryDrawBelow(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), drawIndex, bound, out value, out fault),
						"bound " + bound + ", draw " + drawIndex + ", fault " + fault);
					Assert.IsTrue(value < bound, "bound " + bound + ", draw " + drawIndex + " produced " + value);

					// Same question, same answer, every time it is asked.
					ulong again;
					Assert.IsTrue(CounterRandom.TryDrawBelow(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), drawIndex, bound, out again, out fault));
					Assert.AreEqual(value, again);
				}
			}
		}

		/// <summary>
		/// Forces the rejection branch deterministically instead of hunting for a SHA-256 output
		/// that happens to land in it. For a small bound the threshold is tiny, so samples on both
		/// sides of it can be enumerated exactly.
		/// </summary>
		[Test]
		public void AcceptanceIsExhaustiveAroundTheThresholdForSmallBounds()
		{
			for (ulong bound = 1uL; bound <= 64uL; bound++)
			{
				// Candidates at or above this are usable; below it they would bias the result.
				ulong threshold = (0uL - bound) % bound;

				for (ulong offset = 0uL; offset <= 4uL; offset++)
				{
					if (threshold >= offset + 1uL)
					{
						ulong below = threshold - offset - 1uL;
						ulong ignored;
						Assert.IsFalse(CounterRandom.TryAcceptBoundedSample(below, bound, out ignored),
							"bound " + bound + ": sample " + below + " is under threshold " + threshold + " and must be rejected");
						Assert.AreEqual(0uL, ignored, "a rejected sample must publish nothing");
					}

					ulong at = threshold + offset;
					ulong value;
					Assert.IsTrue(CounterRandom.TryAcceptBoundedSample(at, bound, out value),
						"bound " + bound + ": sample " + at + " is at or above threshold " + threshold + " and must be accepted");
					Assert.AreEqual(at % bound, value, "an accepted sample maps by plain modulus");
				}
			}
		}

		/// <summary>
		/// The threshold identity <c>(0 - bound) % bound == 2^64 mod bound</c> holds in modular
		/// arithmetic but is easy to get wrong by one; an exact <see cref="BigInteger"/> oracle
		/// settles it without sharing the implementation's arithmetic.
		/// </summary>
		[Test]
		public void AcceptanceMatchesABigIntegerOracleOverAHundredThousandPairs()
		{
			BigInteger twoToTheSixtyFour = BigInteger.One << 64;
			ulong state = 0xD1B54A32D192ED03uL;
			int compared = 0;

			for (int i = 0; i < 100000; i++)
			{
				state = unchecked((state * 6364136223846793005uL) + 1442695040888963407uL);
				ulong x = state;
				x ^= x >> 33;
				state = unchecked((state * 6364136223846793005uL) + 1442695040888963407uL);
				ulong y = state;
				y ^= y >> 33;

				// Mix small, huge, and arbitrary bounds; bound 0 is invalid and tested elsewhere.
				ulong bound;
				switch (i & 3)
				{
				case 0: bound = 1uL + (x % 64uL); break;
				case 1: bound = ulong.MaxValue - (x % 64uL); break;
				case 2: bound = 9223372036854775808uL + (x % 64uL); break;
				default: bound = x == ulong.MaxValue ? 1uL : 1uL + x; break;
				}

				ulong sample = y;
				ulong value;
				bool accepted = CounterRandom.TryAcceptBoundedSample(sample, bound, out value);

				BigInteger expectedThreshold = twoToTheSixtyFour % bound;
				bool expectedAccepted = (BigInteger)sample >= expectedThreshold;
				Assert.AreEqual(expectedAccepted, accepted, "sample " + sample + ", bound " + bound);
				if (expectedAccepted)
				{
					Assert.AreEqual((ulong)((BigInteger)sample % bound), value, "sample " + sample + ", bound " + bound);
					Assert.IsTrue(value < bound);
				}
				else
				{
					Assert.AreEqual(0uL, value);
				}
				compared++;
			}
			Assert.AreEqual(100000, compared);
		}

		/// <summary>
		/// A draw is a function of its inputs and nothing else. If any ambient generator could
		/// perturb it, a settlement would not survive being observed at a different moment.
		/// </summary>
		[Test]
		public void AmbientRandomnessAndDiagnosticsCannotPerturbADraw()
		{
			KernelFaultCode fault;
			ulong before;
			Assert.IsTrue(CounterRandom.TryDrawUInt64(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), 4u, out before, out fault));

			Random ambient = new Random(12345);
			for (int i = 0; i < 1000; i++)
			{
				ambient.Next();
			}
			string ignored = KernelDigest.ToLowercaseHex(new byte[] { 1, 2, 3 });
			Assert.IsNotNull(ignored);

			ulong after;
			Assert.IsTrue(CounterRandom.TryDrawUInt64(KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), 4u, out after, out fault));
			Assert.AreEqual(before, after, "an unrelated generator must not be able to move a kernel draw");
		}

		/// <summary>
		/// Identity is settled before arithmetic, so a request that names nothing reports that it
		/// named nothing — even when its bound is also invalid. The reverse order would tell a
		/// caller its bound was wrong when the real defect is that the event does not exist, and
		/// it would disagree with <c>AdvanceThrough</c>, which resolves state before arithmetic.
		/// </summary>
		[Test]
		public void AnInvalidKeyOutranksAnInvalidBound()
		{
			KernelFaultCode fault;
			ulong value;

			// Both wrong at once: the key wins.
			Assert.IsFalse(CounterRandom.TryDrawBelow(
				KernelCanonicalTests.GoldenSeed(), default(SemanticEventKey), 0u, 0uL, out value, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidEventKey, fault, "the key is judged first");
			Assert.AreEqual(0uL, value, "nothing partial is published");

			// The key alone.
			Assert.IsFalse(CounterRandom.TryDrawBelow(
				KernelCanonicalTests.GoldenSeed(), default(SemanticEventKey), 0u, 100uL, out value, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidEventKey, fault);
			Assert.AreEqual(0uL, value);

			// The bound alone, with a key that is fine — this is the only way to see the bound fault.
			Assert.IsFalse(CounterRandom.TryDrawBelow(
				KernelCanonicalTests.GoldenSeed(), KernelCanonicalTests.GoldenKey(), 0u, 0uL, out value, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidRandomBound, fault);
			Assert.AreEqual(0uL, value);

			// The same ordering on the unbounded draw, which has no bound to compete with.
			Assert.IsFalse(CounterRandom.TryDrawUInt64(
				KernelCanonicalTests.GoldenSeed(), default(SemanticEventKey), 0u, out value, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidEventKey, fault);
			Assert.AreEqual(0uL, value);
		}

		/// <summary>
		/// Every remaining <c>Try*</c> given more than one invalid input at a time, with the
		/// resolved code written out by hand. Fault selection is part of the reviewed API, so it is
		/// asserted rather than left to whichever check happens to run first.
		/// </summary>
		[Test]
		public void EveryTryApiResolvesCombinedInvalidInputsToOneFrozenCode()
		{
			KernelFaultCode fault;

			// Identity creation: a bad rules version and two bad identifiers together.
			SemanticEventKey key;
			Assert.IsFalse(SemanticEventKey.TryCreate(0, "NOPE", "ALSO BAD", 0u, 0uL, out key, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidEventKey, fault);

			// A zero event kind is invalid even when everything else is well formed.
			Assert.IsFalse(SemanticEventKey.TryCreate(3, "taf:a", "taf:b", 0u, 0uL, out key, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidEventKey, fault);

			// Identity rendering from a default key.
			string id;
			Assert.IsFalse(SemanticEventIdentity.TryCreateId(
				KernelCanonicalTests.GoldenSeed(), default(SemanticEventKey), out id, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidEventKey, fault);
			Assert.IsNull(id, "no partial identity");

			// Both encoders refuse a default key rather than encoding nulls.
			byte[] bytes;
			Assert.IsFalse(KernelCanonicalEncoding.TryEncodeEventIdentityPreimage(
				KernelCanonicalTests.GoldenSeed(), default(SemanticEventKey), out bytes, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidEventKey, fault);
			Assert.IsNull(bytes);

			Assert.IsFalse(KernelCanonicalEncoding.TryEncodeRandomBlockPreimage(
				KernelCanonicalTests.GoldenSeed(), default(SemanticEventKey), 0u, 0u, out bytes, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidEventKey, fault);
			Assert.IsNull(bytes);

			// The digest refuses a null input rather than hashing the empty string, which would
			// give absent data a real and reusable identity.
			byte[] digest;
			Assert.IsFalse(KernelDigest.TryComputeSha256(null, out digest, out fault));
			Assert.AreEqual(KernelFaultCode.InvalidEventKey, fault);
			Assert.IsNull(digest);

			// Block-index exhaustion at the top of the counter, which cannot wrap.
			uint next;
			Assert.IsFalse(CounterRandom.TryNextRejectionBlockIndex(uint.MaxValue, out next, out fault));
			Assert.AreEqual(KernelFaultCode.CounterExhausted, fault);
			Assert.AreEqual(0u, next, "a refused advance publishes no index");

			// The mapping helper has no fault channel, so its contract is the value it does not set.
			ulong mapped;
			Assert.IsFalse(CounterRandom.TryAcceptBoundedSample(0uL, 0uL, out mapped));
			Assert.AreEqual(0uL, mapped);
		}

		private static string DigestOf(byte[] preimage)
		{
			byte[] digest;
			KernelFaultCode fault;
			Assert.IsTrue(KernelDigest.TryComputeSha256(preimage, out digest, out fault));
			return KernelDigest.ToLowercaseHex(digest);
		}
	}
}
#endif
