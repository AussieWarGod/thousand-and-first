#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomSettlementTopologyRulesTests
	{
		private static string Id(char Value)
		{
			return KingdomIdentityRules.SettlementPrefix + new string(Value, 64);
		}

		[Test]
		public void CanonicalTopologyAllowsSeatPlusTwoAndSortsNonSeatIds()
		{
			Assert.That(KingdomSettlementTopologyRules.TryCanonicalize(Id('a'),
				new List<string> { Id('c'), Id('b') }, out List<string> canonical,
				out string failure), Is.True, failure);
			Assert.That(canonical, Is.EqualTo(new[] { Id('b'), Id('c') }));
			Assert.That(KingdomSettlementTopologyRules.MaxOwnedSettlements, Is.EqualTo(3));
			Assert.That(KingdomSettlementTopologyRules.MaxNonSeatSettlements, Is.EqualTo(2));
		}

		[TestCase(true, false, false, 0)]
		[TestCase(false, true, false, 1)]
		[TestCase(false, false, true, 2)]
		[TestCase(false, false, false, -1)]
		[TestCase(true, true, false, -1)]
		public void ClaimOwnerRequiresOneExactCity(bool Seat, bool First, bool Second,
			int Expected)
		{
			Assert.That(KingdomSettlementTopologyRules.UniqueClaimOwner(
				new List<bool> { Seat, First, Second }), Is.EqualTo(Expected));
		}

		[Test]
		public void CanonicalTopologyRejectsEveryIdentityAmbiguityAndOverflow()
		{
			List<string>[] invalid =
			{
				new List<string> { Id('a') },
				new List<string> { Id('b'), Id('b') },
				new List<string> { Id('b'), Id('c'), Id('d') },
				new List<string> { "not-an-id" }
			};
			for (int i = 0; i < invalid.Length; i++)
				Assert.That(KingdomSettlementTopologyRules.TryCanonicalize(Id('a'), invalid[i],
					out List<string> _, out string _), Is.False, "case " + i);
			Assert.That(KingdomSettlementTopologyRules.TryCanonicalize(Id('a'), null,
				out List<string> _, out string _), Is.False);
			Assert.That(KingdomSettlementTopologyRules.UniqueClaimOwner(
				new List<bool> { false, false, false, true }), Is.EqualTo(-1));
		}
	}
}
#endif
