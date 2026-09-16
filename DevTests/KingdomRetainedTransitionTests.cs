using System;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomRetainedTransitionTests
	{
		private static KingdomSocketTransition Parse(XElement Row)
		{
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TryParse((string)Row.Attribute("Key"),
				(string)Row.Attribute("From"), (string)Row.Attribute("To"), (string)Row.Attribute("Type"),
				(string)Row.Attribute("Size"), (string)Row.Attribute("Mode"), (string)Row.Attribute("Water"),
				(string)Row.Attribute("Materials"), (string)Row.Attribute("Ticks"), out var value,
				out string failure), failure);
			return value;
		}

		private static KingdomSocketTransitionReceiptShape Receipt(KingdomSocketTransition Declaration)
		{
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TryDeclarationDigest(Declaration, out string digest));
			return new KingdomSocketTransitionReceiptShape {
				SchemaHasInt = true, Schema = 2, KeyHasString = true, Key = Declaration.Key,
				DeclarationHasString = true, DeclarationDigest = digest,
				BeforeHasString = true, BeforeHash = new string('a', 64),
				AfterHasString = true, AfterHash = new string('b', 64), JobHasString = true, JobId = "paid-job"
			};
		}

		[TestCase("tentrow-to-hutyard-s")]
		[TestCase("tentrow-to-mudhutcourt-s")]
		[TestCase("tentrow-to-blockyard-s")]
		public void RecordedOldPriceSurvivesCurrentCanvasChargeButCannotBecomeANewQuote(string Key)
		{
			var xml = XDocument.Parse(TestMain.ReadRepositoryText("Architecture/KingdomArchitectureTransitions.xml"));
			var current = Parse(xml.Root.Elements("transition").Single(e => (string)e.Attribute("Key") == Key));
			var retained = Parse(xml.Root.Elements("retained-transition").Single(e => (string)e.Attribute("Key") == Key));
			var receipt = Receipt(retained);
			ClassicAssert.IsTrue(KingdomMaterialRules.TryParseMaterial("canvas", out var canvas));
			ClassicAssert.AreEqual(0, retained.Materials.Get(canvas));
			ClassicAssert.AreEqual(1, current.Materials.Get(canvas));
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.MatchesRoute(retained, current));
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TryDeclarationDigest(current, out string newDigest));
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.ReceiptAuthorizes(receipt, Key, newDigest,
				receipt.BeforeHash, receipt.AfterHash, receipt.JobId, out _), "old completion path repriced the receipt");
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TrySelectPaidDeclaration(receipt, current, retained,
				out var selected));
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.SameDeclaration(retained, selected));
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TryDeclarationDigest(selected, out string digest));
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.ReceiptAuthorizes(receipt, Key, digest,
				receipt.BeforeHash, receipt.AfterHash, receipt.JobId, out bool legacy));
			ClassicAssert.IsFalse(legacy);
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.ReceiptAuthorizes(receipt, Key, digest,
				receipt.BeforeHash, new string('c', 64), receipt.JobId, out _));
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.ReceiptAuthorizes(receipt, Key, digest,
				receipt.BeforeHash, receipt.AfterHash, "another-job", out _));
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		public void UnknownOrTornHistoricalAuthorityCannotSelectARetainedPrice(int Fault)
		{
			var old = Sample(2); var current = Sample(3); var receipt = Receipt(old);
			switch (Fault)
			{
			case 0: old = null; break;
			case 1: receipt.SchemaHasString = true; break;
			case 2: receipt.DeclarationHasInt = true; break;
			case 3: receipt.DeclarationDigest = new string('c', 64); break;
			case 4: receipt.Schema = 3; break;
			}
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.TrySelectPaidDeclaration(receipt, current,
				old, out _));
		}

		[Test]
		public void CurrentReceiptAndLegacyReceiptKeepTheirExistingAuthority()
		{
			var current = Sample(3); var old = Sample(2); var receipt = Receipt(current);
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TrySelectPaidDeclaration(receipt, current,
				old, out var selected));
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.SameDeclaration(current, selected));
			receipt.Schema = 1; receipt.DeclarationHasString = false; receipt.DeclarationDigest = null;
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TrySelectPaidDeclaration(receipt, current,
				old, out selected));
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.SameDeclaration(current, selected));
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.TrySelectPaidDeclaration(receipt, null,
				old, out _), "schema-one receipt cannot adopt historical price authority");
		}

		private static KingdomSocketTransition Sample(int Canvas) => Parse(new XElement("transition",
			new XAttribute("Key", "test-route"), new XAttribute("From", "tent"), new XAttribute("To", "hut"),
			new XAttribute("Type", "housing"), new XAttribute("Size", "M"), new XAttribute("Mode", "renovate"),
			new XAttribute("Water", "4"), new XAttribute("Materials", "canvas:" + Canvas), new XAttribute("Ticks", "900")));
	}
}
