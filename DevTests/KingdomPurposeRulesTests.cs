#if TAF_TESTS
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPurposeRulesTests
	{
		[Test]
		public void PublicPurposeTypeMetadataIsFrozen()
		{
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomPurposeKind", typeof(KingdomPurposeKind).FullName);
			ClassicAssert.AreEqual(typeof(byte), System.Enum.GetUnderlyingType(typeof(KingdomPurposeKind)));
			ClassicAssert.AreEqual(0, (byte)KingdomPurposeKind.None);
			ClassicAssert.AreEqual(1, (byte)KingdomPurposeKind.Flesh);
			ClassicAssert.AreEqual(2, (byte)KingdomPurposeKind.Chrome);
			ClassicAssert.AreEqual(3, (byte)KingdomPurposeKind.Deep);
			ClassicAssert.AreEqual(4, (byte)KingdomPurposeKind.Forge);
			ClassicAssert.AreEqual(5, (byte)KingdomPurposeKind.Harvest);
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomPurposeSite", typeof(KingdomPurposeSite).FullName);
			ClassicAssert.AreEqual(typeof(byte), System.Enum.GetUnderlyingType(typeof(KingdomPurposeSite)));
			ClassicAssert.AreEqual(0, (byte)KingdomPurposeSite.None);
			ClassicAssert.AreEqual(1, (byte)KingdomPurposeSite.LivingSurgery);
			ClassicAssert.AreEqual(2, (byte)KingdomPurposeSite.RuinEnrollment);
			ClassicAssert.AreEqual(3, (byte)KingdomPurposeSite.DeepDelve);
			ClassicAssert.AreEqual(4, (byte)KingdomPurposeSite.ForgeQuench);
			ClassicAssert.AreEqual(5, (byte)KingdomPurposeSite.HarvestWater);
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomPurposeDefinition",
				typeof(KingdomPurposeDefinition).FullName);
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomPurposeManifest",
				typeof(KingdomPurposeManifest).FullName);
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomPurposeCommitment",
				typeof(KingdomPurposeCommitment).FullName);
		}

		[Test]
		public void PortfolioDeclarationsUseFrozenDirectedTableNotLegacyCargoMetadata()
		{
			ClassicAssert.IsTrue(KingdomPurposeRules.TryCreateDefinition("deepbore", "deep",
				"deep-delve", null, null, null, null, null, "deepcut|masonyard",
				"performs one bounded deep extraction", out var definition, out var error), error);
			ClassicAssert.IsTrue(definition.PortfolioOnly);
			ClassicAssert.AreEqual(KingdomPurposeKind.Deep, definition.Kind);
			ClassicAssert.IsTrue(definition.CargoCost.IsEmpty());
			ClassicAssert.IsFalse(KingdomPurposeRules.TryCreateDefinition("deepbore", "deep",
				"forge-quench", null, null, null, null, null, "deepcut|masonyard",
				"extracts", out _, out _));
			ClassicAssert.IsFalse(KingdomPurposeRules.TryCreateDefinition("deepbore", "deep",
				"deep-delve", "invented-row", "cargo", "scrap", "1", "scrap:1",
				"deepcut|masonyard", "extracts", out _, out _));
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
			ClassicAssert.IsTrue(KingdomPurposeRules.TryCreateDefinition("chimerictheatre", "flesh",
				"living-surgery", "graft-stock-casket", "sealed graft-stock casket",
				"workedmetal", "12", "brush:4,workedmetal:1", "vathouse|graftinghall",
				"performs authored procedures", out KingdomPurposeDefinition definition,
				out string error), error);
			ClassicAssert.AreEqual(KingdomPurposeKind.Flesh, definition.Kind);
			ClassicAssert.AreEqual(KingdomPurposeSite.LivingSurgery, definition.Site);
			ClassicAssert.AreEqual(1, definition.CargoCost.Get(KingdomMaterial.WorkedMetal));

			ClassicAssert.IsFalse(KingdomPurposeRules.TryCreateDefinition("becomingannexe", "chrome",
				"living-surgery", "roll", "roll", "workedmetal", "16", "scrap:6",
				"smelter,chargingpost", "enrols", out _, out _),
				"a cargo cannot be minted beside a cost which omits its own typed material");
		}

		[Test]
		public void ProducerGrammarMeansCommaAllAndPipeEither()
		{
			HashSet<string> standing = new HashSet<string> { "graftinghall", "smelter" };
			ClassicAssert.IsTrue(KingdomPurposeRules.ProducersSatisfied("vathouse|graftinghall",
				standing, out _));
			ClassicAssert.IsFalse(KingdomPurposeRules.ProducersSatisfied("smelter,chargingpost",
				standing, out string missing));
			ClassicAssert.AreEqual("chargingpost", missing);
			standing.Add("chargingpost");
			ClassicAssert.IsTrue(KingdomPurposeRules.ProducersSatisfied("smelter,chargingpost",
				standing, out _));
		}

		[Test]
		public void ManifestAndCommitmentAreCanonicalDelimiterSafeAndIdentityBound()
		{
			KingdomPurposeManifest manifest = Manifest();
			string encoded = KingdomPurposeRules.EncodeManifest(manifest);
			ClassicAssert.IsNotNull(encoded);
			ClassicAssert.IsTrue(KingdomPurposeRules.TryDecodeManifest(encoded,
				out KingdomPurposeManifest decoded));
			ClassicAssert.AreEqual(encoded, KingdomPurposeRules.EncodeManifest(decoded));
			ClassicAssert.AreEqual(manifest.CargoName, decoded.CargoName);

			KingdomPurposeCommitment commitment = new KingdomPurposeCommitment
			{
				Manifest = encoded, ConsignmentId = "consignment-identity",
				CargoItemId = "cargo-object-identity", SiteProof = "site; proof: exact|fresh",
				SpecialistId = "specialist-identity", SpecialistName = "Ari; the sawbones"
			};
			string receipt = KingdomPurposeRules.EncodeCommitment(commitment);
			ClassicAssert.IsNotNull(receipt);
			ClassicAssert.IsTrue(KingdomPurposeRules.TryDecodeCommitment(receipt,
				out KingdomPurposeCommitment decodedCommitment));
			ClassicAssert.AreEqual(receipt, KingdomPurposeRules.EncodeCommitment(decodedCommitment));
			decodedCommitment.CargoItemId = "substitute-object";
			ClassicAssert.AreNotEqual(receipt, KingdomPurposeRules.EncodeCommitment(decodedCommitment));
		}

		[Test]
		public void InitialPortfolioShellHasBuildBoundCargoFreeAuthority()
		{
			KingdomPurposeCommitment commitment = new KingdomPurposeCommitment
			{
				InitialBuildKey = "deepbore", SiteProof = "exact deep-delve site",
				SpecialistId = "specialist-identity", SpecialistName = "Ari"
			};
			string receipt = KingdomPurposeRules.EncodeCommitment(commitment);
			ClassicAssert.IsNotNull(receipt);
			ClassicAssert.IsTrue(KingdomPurposeRules.TryDecodeCommitment(receipt, out var decoded));
			ClassicAssert.AreEqual("deepbore", decoded.InitialBuildKey);
			ClassicAssert.AreEqual(receipt, KingdomPurposeRules.EncodeCommitment(decoded));
			decoded.CargoItemId = "invented-cargo";
			ClassicAssert.IsNull(KingdomPurposeRules.EncodeCommitment(decoded));
		}

		[Test]
		public void PortfolioCommitmentV2MigratesThroughItsFrozenFieldEnvelope()
		{
			string manifest = KingdomPurposeRules.EncodeManifest(Manifest());
			string prior = Frame(manifest, "consignment-identity", "cargo-object-identity",
				"site proof", Frame("specialist-identity", "Ari"), "", "", "", "", "");
			ClassicAssert.IsTrue(KingdomPurposeRules.TryDecodeCommitment(prior, out var decoded));
			ClassicAssert.IsNull(decoded.InitialBuildKey);
			string current = KingdomPurposeRules.EncodeCommitment(decoded);
			ClassicAssert.IsNotNull(current);
			ClassicAssert.AreNotEqual(prior, current);
			ClassicAssert.IsTrue(KingdomPurposeRules.TryDecodeCommitment(current, out var rewritten));
			ClassicAssert.AreEqual(current, KingdomPurposeRules.EncodeCommitment(rewritten));
		}

		[Test]
		public void WrongPurposeOrRouteIdentityCannotReuseManifest()
		{
			KingdomPurposeManifest manifest = Manifest();
			string original = KingdomPurposeRules.EncodeManifest(manifest);
			ClassicAssert.IsTrue(KingdomPurposeRules.TryCreateDefinition("chimerictheatre", "flesh",
				"living-surgery", "graft-stock-casket", manifest.CargoName, "workedmetal",
				"12", "brush:4,workedmetal:1", "vathouse|graftinghall", manifest.Effect,
				out KingdomPurposeDefinition definition, out string error), error);
			ClassicAssert.IsTrue(KingdomPurposeRules.ManifestMatchesDefinition(manifest, definition));
			definition.CargoWater++;
			ClassicAssert.IsFalse(KingdomPurposeRules.ManifestMatchesDefinition(manifest, definition),
				"a changed producer recipe cannot reinterpret an old physical output");
			manifest.BuildKey = "becomingannexe";
			manifest.Kind = KingdomPurposeKind.Chrome;
			manifest.Site = KingdomPurposeSite.RuinEnrollment;
			string changed = KingdomPurposeRules.EncodeManifest(manifest);
			ClassicAssert.IsNotNull(changed);
			ClassicAssert.AreNotEqual(original, changed);
			manifest.DestinationGateKey = manifest.SourceGateKey;
			ClassicAssert.IsNull(KingdomPurposeRules.EncodeManifest(manifest));
		}

		private static string Frame(params string[] fields)
		{
			StringBuilder text = new StringBuilder("v1");
			for (int i = 0; i < fields.Length; i++)
			{
				string value = fields[i] ?? "";
				text.Append(';').Append(value.Length.ToString(CultureInfo.InvariantCulture))
					.Append(':').Append(value);
			}
			return text.ToString();
		}
	}
}
#endif
