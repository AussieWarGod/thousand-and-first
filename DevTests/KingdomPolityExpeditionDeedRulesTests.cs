using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityExpeditionDeedRulesTests
	{
		private const string Polity =
			"taf:polity:v1:1111111111111111111111111111111111111111111111111111111111111111";
		private const string Settlement =
			"taf:settlement:v1:2222222222222222222222222222222222222222222222222222222222222222";

		[Test]
		public void ExactExpeditionIdentityMintsStableTypedDeed()
		{
			Assert.IsTrue(KingdomPolityExpeditionDeedRules.TryCauseRef(Polity, Settlement,
				17, 4, "taf:expedition:17:3", out string first));
			Assert.IsTrue(KingdomPolityExpeditionDeedRules.TryCauseRef(Polity, Settlement,
				17, 4, "taf:expedition:17:3", out string retry));
			Assert.AreEqual(first, retry);
			StringAssert.StartsWith(KingdomPolityExpeditionDeedRules.CausePrefix, first);
			Assert.LessOrEqual(first.Length, 128);
			Assert.AreEqual("returned from a salvage expedition with a rich find",
				KingdomPolityExpeditionDeedRules.Summary);
		}

		[Test]
		public void AnyChangedOrMalformedAuthorityCannotAlias()
		{
			Assert.IsTrue(KingdomPolityExpeditionDeedRules.TryCauseRef(Polity, Settlement,
				17, 4, "taf:expedition:17:3", out string expected));
			Assert.IsTrue(KingdomPolityExpeditionDeedRules.TryCauseRef(Polity, Settlement,
				18, 4, "taf:expedition:18:3", out string other));
			Assert.AreNotEqual(expected, other);
			Assert.IsFalse(KingdomPolityExpeditionDeedRules.TryCauseRef(Polity, Settlement,
				0, 4, "taf:expedition:17:3", out _));
			Assert.IsFalse(KingdomPolityExpeditionDeedRules.TryCauseRef(Polity, Settlement,
				17, 0, "taf:expedition:17:3", out _));
			Assert.IsFalse(KingdomPolityExpeditionDeedRules.TryCauseRef(Polity, Settlement,
				17, 4, "taf:event:not-an-expedition", out _));
			Assert.IsFalse(KingdomPolityExpeditionDeedRules.TryCauseRef(Polity, Settlement,
				17, 4, "taf:expedition:18:3", out _));
			Assert.IsFalse(KingdomPolityExpeditionDeedRules.TryCauseRef(Polity, Settlement,
				17, 4, "taf:expedition:17:2", out _));
		}

		[Test]
		public void TerminalReceiptRemainsExactAfterResidentBridgeEnds()
		{
			Assert.IsTrue(KingdomPolityExpeditionDeedRules.TryFigureRef(Polity, Settlement,
				17, 4, "taf:expedition:17:3", out string cause, out string figure));
			KingdomPolityNamedFigureRecord row = new KingdomPolityNamedFigureRecord
			{
				FigureId = figure, PolityId = Polity, DisplayName = "Nara", RoleKey = "patrol",
				Origin = KingdomPolityFigureOrigin.PromotedByDeed,
				Phase = KingdomPolityFigurePhase.Dead, CauseRef = cause,
				ChronicleRef = "taf:expedition:17:3",
				ConclusionRef = "taf:conclusion:resident-transition:v1:dead",
				DeedSummary = KingdomPolityExpeditionDeedRules.Summary
			};
			Assert.IsTrue(KingdomPolityExpeditionDeedRules.ExactReceipt(row, Polity,
				Settlement, 17, 4, "Nara", "taf:expedition:17:3"));
			row.ChronicleRef = "taf:expedition:18:3";
			Assert.IsFalse(KingdomPolityExpeditionDeedRules.ExactReceipt(row, Polity,
				Settlement, 17, 4, "Nara", "taf:expedition:17:3"));
		}
	}
}
