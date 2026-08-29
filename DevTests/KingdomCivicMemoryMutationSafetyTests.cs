#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomCivicMemoryMutationSafetyTests
	{
		private sealed class SwitchingList : IList<KingdomCivicMemorySection>
		{
			private readonly KingdomCivicMemorySection First;
			private readonly KingdomCivicMemorySection Later;
			private int Reads;

			internal SwitchingList(KingdomCivicMemorySection First,
				KingdomCivicMemorySection Later)
			{
				this.First = First; this.Later = Later;
			}

			public KingdomCivicMemorySection this[int index]
			{
				get
				{
					if (index != 0) throw new ArgumentOutOfRangeException("index");
					return Reads++ == 0 ? First : Later;
				}
				set { throw new NotSupportedException(); }
			}
			public int Count => 1;
			public bool IsReadOnly => true;
			public IEnumerator<KingdomCivicMemorySection> GetEnumerator()
			{
				yield return this[0];
			}
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
			public int IndexOf(KingdomCivicMemorySection item) => -1;
			public bool Contains(KingdomCivicMemorySection item) => false;
			public void CopyTo(KingdomCivicMemorySection[] array, int arrayIndex)
			{
				array[arrayIndex] = this[0];
			}
			public void Add(KingdomCivicMemorySection item) => throw new NotSupportedException();
			public void Clear() => throw new NotSupportedException();
			public void Insert(int index, KingdomCivicMemorySection item) =>
				throw new NotSupportedException();
			public bool Remove(KingdomCivicMemorySection item) => throw new NotSupportedException();
			public void RemoveAt(int index) => throw new NotSupportedException();
		}

		private sealed class CallbackList : IList<KingdomCivicMemorySection>
		{
			private readonly Func<int> ReadCount;
			private readonly Func<int, KingdomCivicMemorySection> ReadItem;

			internal CallbackList(Func<int> ReadCount,
				Func<int, KingdomCivicMemorySection> ReadItem)
			{
				this.ReadCount = ReadCount;
				this.ReadItem = ReadItem;
			}

			public KingdomCivicMemorySection this[int index]
			{
				get { return ReadItem(index); }
				set { throw new NotSupportedException(); }
			}
			public int Count => ReadCount();
			public bool IsReadOnly => true;
			public IEnumerator<KingdomCivicMemorySection> GetEnumerator()
			{
				for (int i = 0; i < Count; i++) yield return this[i];
			}
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
			public int IndexOf(KingdomCivicMemorySection item) => -1;
			public bool Contains(KingdomCivicMemorySection item) => false;
			public void CopyTo(KingdomCivicMemorySection[] array, int arrayIndex) =>
				throw new NotSupportedException();
			public void Add(KingdomCivicMemorySection item) => throw new NotSupportedException();
			public void Clear() => throw new NotSupportedException();
			public void Insert(int index, KingdomCivicMemorySection item) =>
				throw new NotSupportedException();
			public bool Remove(KingdomCivicMemorySection item) => throw new NotSupportedException();
			public void RemoveAt(int index) => throw new NotSupportedException();
		}

		[Test]
		public void CallerListCannotSwitchRowsBetweenValidationAndInstall()
		{
			int id = KingdomCivicMemoryLimits.SectionCivicArtifacts;
			KingdomCivicMemorySection sound = new KingdomCivicMemorySection(id,
				KingdomCivicMemoryTestFamilies.Sound(8));
			KingdomCivicMemorySection malformed = new KingdomCivicMemorySection(id,
				KingdomCivicMemoryTestFamilies.Payload(
					KingdomCivicMemoryTestFamilies.Malformed, 8));
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(
				KingdomCivicMemoryTestFamilies.Table());

			Assert.IsTrue(authority.TryCommit(new SwitchingList(sound, malformed), 0L,
				out string failure), failure);
			CollectionAssert.AreEqual(sound.Payload(), authority.Read().Section(id).Payload());
		}

		[Test]
		public void FamilyReaderCannotReenterTheCommitCas()
		{
			KingdomCivicMemoryAuthority authority = null;
			bool nestedAttempted = false;
			KingdomCivicMemoryFamilyReader reader = delegate(byte[] payload, out string fault)
			{
				fault = "";
				if (!nestedAttempted)
				{
					nestedAttempted = true;
					authority.TryCommit(new List<KingdomCivicMemorySection>
					{
						new KingdomCivicMemorySection(
							KingdomCivicMemoryLimits.SectionCivicPractice,
							KingdomCivicMemoryTestFamilies.Sound(4))
					}, 0L, out string _);
				}
				return KingdomCivicMemoryNested.Current;
			};
			KingdomCivicMemoryFamilyTable table = new KingdomCivicMemoryFamilyTable();
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++) table.Add(id, reader);
			authority = new KingdomCivicMemoryAuthority(table);

			Assert.IsFalse(authority.TryCommit(new List<KingdomCivicMemorySection>
			{
				new KingdomCivicMemorySection(KingdomCivicMemoryLimits.SectionCivicArtifacts,
					KingdomCivicMemoryTestFamilies.Sound(4))
			}, 0L, out string failure));
			Assert.IsTrue(nestedAttempted);
			Assert.IsTrue(authority.Latch.Tripped);
			StringAssert.Contains("re-entrant", failure);
			Assert.IsTrue(authority.IsEmpty);
		}

		[Test]
		public void CandidateCountCannotMutateAuthorityBeforeTheGuard()
		{
			int id = KingdomCivicMemoryLimits.SectionCivicArtifacts;
			KingdomCivicMemorySection loaded = new KingdomCivicMemorySection(id,
				KingdomCivicMemoryTestFamilies.Sound(12));
			KingdomCivicMemorySection proposed = new KingdomCivicMemorySection(id,
				KingdomCivicMemoryTestFamilies.Sound(4));
			byte[] envelope = KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(
				new List<KingdomCivicMemorySection> { loaded }, 0L));
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(
				KingdomCivicMemoryTestFamilies.Table());
			CallbackList hostile = new CallbackList(delegate
			{
				authority.AdoptSaved(envelope);
				return 1;
			}, index => proposed);

			Assert.IsFalse(authority.TryCommit(hostile, 0L, out string failure));
			Assert.IsTrue(authority.Latch.Tripped);
			StringAssert.Contains("re-entrant", failure);
			Assert.IsTrue(authority.IsEmpty,
				"the guarded nested adoption and the outer commit must both leave state untouched");
		}

		[Test]
		public void ImpossibleCandidateCountIsRefusedBeforeAnyIndexerRead()
		{
			bool indexed = false;
			CallbackList hostile = new CallbackList(() => int.MaxValue, delegate(int index)
			{
				indexed = true;
				throw new InvalidOperationException();
			});
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(
				KingdomCivicMemoryTestFamilies.Table());

			Assert.IsFalse(authority.TryCommit(hostile, 0L, out string failure));
			StringAssert.Contains("outside", failure);
			Assert.IsFalse(indexed);
		}

		[Test]
		public void ThrowingCandidateAccessorReturnsARefusalInsteadOfEscaping()
		{
			CallbackList hostile = new CallbackList(
				() => throw new ApplicationException("count exploded"), index => null);
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(
				KingdomCivicMemoryTestFamilies.Table());
			bool accepted = true;
			string failure = null;

			Assert.DoesNotThrow(() => accepted = authority.TryCommit(hostile, 0L, out failure));
			Assert.IsFalse(accepted);
			StringAssert.Contains("count exploded", failure);
			Assert.IsTrue(authority.IsEmpty);
		}

		[Test]
		public void StateFactoryReadsEachUntrustedIndexOnlyOnce()
		{
			int id = KingdomCivicMemoryLimits.SectionCivicArtifacts;
			KingdomCivicMemorySection sound = new KingdomCivicMemorySection(id,
				KingdomCivicMemoryTestFamilies.Sound(8));

			KingdomCivicMemoryState state = KingdomCivicMemoryState.Of(
				new SwitchingList(sound, null), 0L);

			Assert.AreEqual(1, state.Count);
			CollectionAssert.AreEqual(sound.Payload(), state.Section(id).Payload());
		}

		[Test]
		public void StateFactoryRejectsImpossibleCountsBeforeIndexing()
		{
			foreach (int count in new[] { -1, int.MaxValue })
			{
				bool indexed = false;
				CallbackList hostile = new CallbackList(() => count, delegate(int index)
				{
					indexed = true;
					return null;
				});
				Assert.Throws<ArgumentOutOfRangeException>(
					() => KingdomCivicMemoryState.Of(hostile, 0L));
				Assert.IsFalse(indexed);
			}
		}

		[Test]
		public void ThrowingFamilyReaderCannotEscapeACommitOrLeaveItWritable()
		{
			int id = KingdomCivicMemoryLimits.SectionCivicArtifacts;
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(
				KingdomCivicMemoryTestFamilies.TableThrowing(id));
			bool accepted = true;
			string failure = null;

			Assert.DoesNotThrow(() => accepted = authority.TryCommit(
				new List<KingdomCivicMemorySection>
				{
					new KingdomCivicMemorySection(id,
						KingdomCivicMemoryTestFamilies.Sound(8))
				}, 0L, out failure));
			Assert.IsFalse(accepted);
			StringAssert.Contains("inspection exploded", failure);
			Assert.IsTrue(authority.Latch.Tripped);
			Assert.IsTrue(authority.IsEmpty);
		}
	}
}
#endif
