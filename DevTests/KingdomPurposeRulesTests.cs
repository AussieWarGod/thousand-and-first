#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPurposeRulesTests
	{
		[Test]
		public void PublicPurposeTypeMetadataIsFrozen()
		{
			Assert.AreEqual("ThousandAndFirst.KingdomPurposeKind", typeof(KingdomPurposeKind).FullName);
			Assert.AreEqual(typeof(byte), System.Enum.GetUnderlyingType(typeof(KingdomPurposeKind)));
			Assert.AreEqual(0, (byte)KingdomPurposeKind.None);
			Assert.AreEqual(1, (byte)KingdomPurposeKind.Flesh);
			Assert.AreEqual(2, (byte)KingdomPurposeKind.Chrome);
			Assert.AreEqual("ThousandAndFirst.KingdomPurposeSite", typeof(KingdomPurposeSite).FullName);
			Assert.AreEqual(typeof(byte), System.Enum.GetUnderlyingType(typeof(KingdomPurposeSite)));
			Assert.AreEqual(0, (byte)KingdomPurposeSite.None);
			Assert.AreEqual(1, (byte)KingdomPurposeSite.LivingSurgery);
			Assert.AreEqual(2, (byte)KingdomPurposeSite.RuinEnrollment);
			Assert.AreEqual("ThousandAndFirst.KingdomPurposeDefinition",
				typeof(KingdomPurposeDefinition).FullName);
			Assert.AreEqual("ThousandAndFirst.KingdomPurposeManifest",
				typeof(KingdomPurposeManifest).FullName);
			Assert.AreEqual("ThousandAndFirst.KingdomPurposeCommitment",
				typeof(KingdomPurposeCommitment).FullName);
		}

		private static KingdomPurposeManifest Manifest()
		{
			KingdomMaterialTally materials = new KingdomMaterialTally();
			materials.Set(KingdomMaterial.Brush, 4);
			materials.Set(KingdomMaterial.WorkedMetal, 1);
			return new KingdomPurposeManifest
			{
				BuildKey = "chimerictheatre", Kind = KingdomPurposeKind.Flesh,
				Site = KingdomPurposeSite.LivingSurgery,
				CargoKey = "graft-stock-casket",
				CargoName = "sealed graft-stock; casket: exact|one",
				CargoMaterial = KingdomMaterial.WorkedMetal, CargoWater = 12,
				CargoCostClaim = new KingdomMaterialDebitCost(materials, null, null).ToClaimString(),
				OriginSettlementId = "origin-identity", OriginCity = "Far; City: One",
				OriginZoneId = "JoppaWorld.1.2.1.1.10", SourceGateKey = "gate-source-key",
				DestinationSettlementId = "destination-identity", DestinationCity = "Near City",
				DestinationZoneId = "JoppaWorld.2.2.1.1.10",
				DestinationGateKey = "gate-destination-key",
				ProducerProof = "vathouse|graftinghall",
				Effect = "performs authored procedures; no token proxy"
			};
		}

		[Test]
		public void DeclarationFreezesDistinctSiteAndTypedPhysicalCost()
		{
			Assert.IsTrue(KingdomPurposeRules.TryCreateDefinition("chimerictheatre", "flesh",
				"living-surgery", "graft-stock-casket", "sealed graft-stock casket",
				"workedmetal", "12", "brush:4,workedmetal:1", "vathouse|graftinghall",
				"performs authored procedures", out KingdomPurposeDefinition definition,
				out string error), error);
			Assert.AreEqual(KingdomPurposeKind.Flesh, definition.Kind);
			Assert.AreEqual(KingdomPurposeSite.LivingSurgery, definition.Site);
			Assert.AreEqual(1, definition.CargoCost.Get(KingdomMaterial.WorkedMetal));

			Assert.IsFalse(KingdomPurposeRules.TryCreateDefinition("becomingannexe", "chrome",
				"living-surgery", "roll", "roll", "workedmetal", "16", "scrap:6",
				"smelter,chargingpost", "enrols", out _, out _),
				"a cargo cannot be minted beside a cost which omits its own typed material");
		}

		[Test]
		public void ProducerGrammarMeansCommaAllAndPipeEither()
		{
			HashSet<string> standing = new HashSet<string> { "graftinghall", "smelter" };
			Assert.IsTrue(KingdomPurposeRules.ProducersSatisfied("vathouse|graftinghall",
				standing, out _));
			Assert.IsFalse(KingdomPurposeRules.ProducersSatisfied("smelter,chargingpost",
				standing, out string missing));
			Assert.AreEqual("chargingpost", missing);
			standing.Add("chargingpost");
			Assert.IsTrue(KingdomPurposeRules.ProducersSatisfied("smelter,chargingpost",
				standing, out _));
		}

		[Test]
		public void ManifestAndCommitmentAreCanonicalDelimiterSafeAndIdentityBound()
		{
			KingdomPurposeManifest manifest = Manifest();
			string encoded = KingdomPurposeRules.EncodeManifest(manifest);
			Assert.IsNotNull(encoded);
			Assert.IsTrue(KingdomPurposeRules.TryDecodeManifest(encoded,
				out KingdomPurposeManifest decoded));
			Assert.AreEqual(encoded, KingdomPurposeRules.EncodeManifest(decoded));
			Assert.AreEqual(manifest.CargoName, decoded.CargoName);

			KingdomPurposeCommitment commitment = new KingdomPurposeCommitment
			{
				Manifest = encoded, ConsignmentId = "consignment-identity",
				CargoItemId = "cargo-object-identity", SiteProof = "site; proof: exact|fresh",
				SpecialistId = "specialist-identity", SpecialistName = "Ari; the sawbones"
			};
			string receipt = KingdomPurposeRules.EncodeCommitment(commitment);
			Assert.IsNotNull(receipt);
			Assert.IsTrue(KingdomPurposeRules.TryDecodeCommitment(receipt,
				out KingdomPurposeCommitment decodedCommitment));
			Assert.AreEqual(receipt, KingdomPurposeRules.EncodeCommitment(decodedCommitment));
			decodedCommitment.CargoItemId = "substitute-object";
			Assert.AreNotEqual(receipt, KingdomPurposeRules.EncodeCommitment(decodedCommitment));
		}

		[Test]
		public void WrongPurposeOrRouteIdentityCannotReuseManifest()
		{
			KingdomPurposeManifest manifest = Manifest();
			string original = KingdomPurposeRules.EncodeManifest(manifest);
			Assert.IsTrue(KingdomPurposeRules.TryCreateDefinition("chimerictheatre", "flesh",
				"living-surgery", "graft-stock-casket", manifest.CargoName, "workedmetal",
				"12", "brush:4,workedmetal:1", "vathouse|graftinghall", manifest.Effect,
				out KingdomPurposeDefinition definition, out string error), error);
			Assert.IsTrue(KingdomPurposeRules.ManifestMatchesDefinition(manifest, definition));
			definition.CargoWater++;
			Assert.IsFalse(KingdomPurposeRules.ManifestMatchesDefinition(manifest, definition),
				"a changed producer recipe cannot reinterpret an old physical output");
			manifest.BuildKey = "becomingannexe";
			manifest.Kind = KingdomPurposeKind.Chrome;
			manifest.Site = KingdomPurposeSite.RuinEnrollment;
			string changed = KingdomPurposeRules.EncodeManifest(manifest);
			Assert.IsNotNull(changed);
			Assert.AreNotEqual(original, changed);
			manifest.DestinationGateKey = manifest.SourceGateKey;
			Assert.IsNull(KingdomPurposeRules.EncodeManifest(manifest));
		}
	}
}
#endif
