using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityRemovalRulesTests
	{
		[Test]
		public void EmptyAndCompletedDispatchWithTerminalTransitionAreQuiescent()
		{
			KingdomPolityDispatchState dispatch = EmptyDispatch();
			Assert.IsTrue(KingdomPolityRemovalRules.TryDescribeRealmRemovalBlocker(
				dispatch, new KingdomPolityRealmTransition(), out string blocker,
				out string failure), failure);
			Assert.IsNull(blocker);

			dispatch.HasWindow = true;
			dispatch.LastWindowOrdinal = 2UL;
			dispatch.WindowCauseTick = 16800L;
			dispatch.EndpointDigest = new string('a', 64);
			dispatch.EndpointCount = 3;
			dispatch.CompletedMask = 7;
			Assert.IsTrue(KingdomPolityRemovalRules.TryDescribeRealmRemovalBlocker(
				dispatch, new KingdomPolityRealmTransition(), out blocker, out failure), failure);
			Assert.IsNull(blocker);
		}

		[Test]
		public void OpenMalformedAndQuarantinedAuthorityBlockWithoutMutation()
		{
			KingdomPolityDispatchState dispatch = OpenDispatch();
			Assert.IsTrue(KingdomPolityRemovalRules.TryDescribeRealmRemovalBlocker(
				dispatch, new KingdomPolityRealmTransition(), out string blocker,
				out string failure), failure);
			StringAssert.Contains("uncommitted endpoint", blocker);

			dispatch.CompletedMask = 8;
			Assert.IsTrue(KingdomPolityRemovalRules.TryDescribeRealmRemovalBlocker(
				dispatch, new KingdomPolityRealmTransition(), out blocker, out failure), failure);
			StringAssert.Contains("malformed", blocker);

			dispatch = EmptyDispatch();
			dispatch.Fault = "inspection required";
			Assert.IsTrue(KingdomPolityRemovalRules.TryDescribeRealmRemovalBlocker(
				dispatch, new KingdomPolityRealmTransition(), out blocker, out failure), failure);
			StringAssert.Contains("quarantined", blocker);
		}

		[Test]
		public void NonterminalAndMalformedTransitionBlock()
		{
			KingdomPolityRealmTransition transition = new KingdomPolityRealmTransition
			{
				Phase = KingdomPolityRealmTransitionPhase.Quarantined,
				Fault = "torn return receipt"
			};
			Assert.IsTrue(KingdomPolityRemovalRules.TryDescribeRealmRemovalBlocker(
				EmptyDispatch(), transition, out string blocker, out string failure), failure);
			StringAssert.Contains("malformed or quarantined", blocker);
		}

		private static KingdomPolityDispatchState EmptyDispatch()
		{
			return new KingdomPolityDispatchState
			{
				RealmId = KingdomPolityTestData.Realm,
				FutureCauseFloorTick = 0L
			};
		}

		private static KingdomPolityDispatchState OpenDispatch()
		{
			KingdomPolityDispatchState state = EmptyDispatch();
			KingdomPolityDispatchOffer offer = new KingdomPolityDispatchOffer
			{
				RealmId = KingdomPolityTestData.Realm,
				Tick = KingdomPolityDispatchRules.PeriodTicks,
				Endpoints = new System.Collections.Generic.List<KingdomPolityEndpointFacts>
				{
					new KingdomPolityEndpointFacts
					{
						SettlementId = KingdomPolityTestData.Settlement, IsSeat = true,
						Population = 10, GuardCauseRef = "taf:fact:removal-open"
					}
				}
			};
			Assert.IsTrue(KingdomPolityDispatchRules.TryOpen(state, state.Revision, offer,
				out System.Collections.Generic.List<KingdomPolityDueWork> _, out string failure),
				failure);
			return state;
		}
	}
}
