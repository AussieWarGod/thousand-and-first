#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>Portable adversarial coverage for declaration and durable-receipt authority.</summary>
	public class KingdomSocketTransitionPortableTests
	{
		private const string ExpectedKey = "tent-to-hut-s";
		private const string ExpectedJob = "job-1";
		private static readonly string Declaration = new string('b', 64);
		private static readonly string Before = new string('c', 64);
		private static readonly string After = new string('d', 64);

		[Test]
		public void ImmutableDeclarationAndDigestCoverEveryField()
		{
			Type type = typeof(KingdomSocketTransition);
			Assert.AreEqual(0, type.GetFields(System.Reflection.BindingFlags.Public
				| System.Reflection.BindingFlags.Instance).Length);
			string[] properties = { "Key", "FromBuildKey", "ToBuildKey", "LotType", "LotSize",
				"WaterDrams", "Materials", "WorkTicks" };
			for (int i = 0; i < properties.Length; i++)
				Assert.IsNull(type.GetProperty(properties[i]).GetSetMethod(), properties[i]);

			KingdomSocketTransition current = Route();
			Assert.IsTrue(KingdomSocketTransitionRules.TryDeclarationDigest(current,
				out string digest));
			KingdomSocketTransition[] forged =
			{
				Route(Key: "other"), Route(From: "tentrow"), Route(To: "mudhut"),
				Route(Type: "craft"), Route(Size: "M"), Route(Water: "5"),
				Route(Materials: "timber:99,mud:2"), Route(Ticks: "1351")
			};
			for (int i = 0; i < forged.Length; i++)
			{
				Assert.IsFalse(KingdomSocketTransitionRules.MatchesRoute(forged[i], current),
					properties[i]);
				Assert.IsTrue(KingdomSocketTransitionRules.TryDeclarationDigest(forged[i],
					out string forgedDigest));
				Assert.AreNotEqual(digest, forgedDigest, properties[i]);
			}

			Assert.IsTrue(KingdomSocketTransitionRules.TrySnapshot(current,
				out KingdomSocketTransition snapshot));
			KingdomMaterialTally exposed = snapshot.Materials;
			exposed.Set(KingdomMaterial.Timber, 99);
			Assert.IsTrue(KingdomSocketTransitionRules.MatchesRoute(snapshot, current));
			Assert.AreEqual(4, snapshot.Materials.Get(KingdomMaterial.Timber));
		}

		[Test]
		public void ReceiptRefusesEveryPublicationCutAndEveryPropertyShapeFault()
		{
			KingdomSocketTransitionReceiptShape cut = Values();
			Assert.IsFalse(Authorizes(cut, out _), "schema invalidated");
			for (int i = 0; i < 5; i++)
			{
				Publish(ref cut, i);
				Assert.IsFalse(Authorizes(cut, out _), "payload cut " + i);
			}
			cut.SchemaHasInt = true;
			cut.Schema = KingdomSocketTransitionRules.ReceiptSchema;
			Assert.IsTrue(Authorizes(cut, out bool legacy));
			Assert.IsFalse(legacy);

			for (int i = 0; i < 18; i++)
			{
				KingdomSocketTransitionReceiptShape receipt = Current();
				Fault(ref receipt, i);
				Assert.IsFalse(Authorizes(receipt, out _), "shape fault " + i);
			}
		}

		[Test]
		public void ReceiptBindsEveryValueAndOnlyExactLegacyShapeCanAdopt()
		{
			for (int i = 0; i < 5; i++)
			{
				KingdomSocketTransitionReceiptShape forged = Current();
				Forge(ref forged, i);
				Assert.IsFalse(Authorizes(forged, out _), "value " + i);
			}
			KingdomSocketTransitionReceiptShape legacyReceipt = Current();
			legacyReceipt.Schema = KingdomSocketTransitionRules.LegacyReceiptSchema;
			legacyReceipt.DeclarationHasString = false;
			legacyReceipt.DeclarationDigest = null;
			Assert.IsTrue(Authorizes(legacyReceipt, out bool legacy));
			Assert.IsTrue(legacy);
			legacyReceipt.DeclarationHasString = true;
			legacyReceipt.DeclarationDigest = Declaration;
			Assert.IsFalse(Authorizes(legacyReceipt, out _));
		}

		private static KingdomSocketTransition Route(string Key = ExpectedKey, string From = "tent",
			string To = "hut", string Type = "housing", string Size = "S", string Mode = "renovate", string Water = "4",
			string Materials = "timber:4,mud:2", string Ticks = "1350")
		{
			Assert.IsTrue(KingdomSocketTransitionRules.TryParse(Key, From, To, Type, Size,
				Mode, Water, Materials, Ticks, out KingdomSocketTransition route, out string failure),
				failure);
			return route;
		}

		private static KingdomSocketTransitionReceiptShape Values()
		{
			return new KingdomSocketTransitionReceiptShape
			{
				Key = ExpectedKey, DeclarationDigest = Declaration, BeforeHash = Before,
				AfterHash = After, JobId = ExpectedJob
			};
		}

		private static KingdomSocketTransitionReceiptShape Current()
		{
			KingdomSocketTransitionReceiptShape receipt = Values();
			for (int i = 0; i < 5; i++) Publish(ref receipt, i);
			receipt.SchemaHasInt = true;
			receipt.Schema = KingdomSocketTransitionRules.ReceiptSchema;
			return receipt;
		}

		private static bool Authorizes(KingdomSocketTransitionReceiptShape Receipt,
			out bool Legacy)
		{
			return KingdomSocketTransitionRules.ReceiptAuthorizes(Receipt, ExpectedKey, Declaration,
				Before, After, ExpectedJob, out Legacy);
		}

		private static void Publish(ref KingdomSocketTransitionReceiptShape Receipt, int Field)
		{
			switch (Field)
			{
			case 0: Receipt.KeyHasString = true; break;
			case 1: Receipt.DeclarationHasString = true; break;
			case 2: Receipt.BeforeHasString = true; break;
			case 3: Receipt.AfterHasString = true; break;
			case 4: Receipt.JobHasString = true; break;
			}
		}

		private static void Fault(ref KingdomSocketTransitionReceiptShape Receipt, int Fault)
		{
			switch (Fault)
			{
			case 0: Receipt.SchemaHasInt = false; break;
			case 1: Receipt.SchemaHasString = true; break;
			case 2: Receipt.SchemaHasInt = false; Receipt.SchemaHasString = true; break;
			case 3: Receipt.KeyHasString = false; break;
			case 4: Receipt.KeyHasInt = true; break;
			case 5: Receipt.KeyHasString = false; Receipt.KeyHasInt = true; break;
			case 6: Receipt.DeclarationHasString = false; break;
			case 7: Receipt.DeclarationHasInt = true; break;
			case 8: Receipt.DeclarationHasString = false; Receipt.DeclarationHasInt = true; break;
			case 9: Receipt.BeforeHasString = false; break;
			case 10: Receipt.BeforeHasInt = true; break;
			case 11: Receipt.BeforeHasString = false; Receipt.BeforeHasInt = true; break;
			case 12: Receipt.AfterHasString = false; break;
			case 13: Receipt.AfterHasInt = true; break;
			case 14: Receipt.AfterHasString = false; Receipt.AfterHasInt = true; break;
			case 15: Receipt.JobHasString = false; break;
			case 16: Receipt.JobHasInt = true; break;
			case 17: Receipt.JobHasString = false; Receipt.JobHasInt = true; break;
			}
		}

		private static void Forge(ref KingdomSocketTransitionReceiptShape Receipt, int Field)
		{
			switch (Field)
			{
			case 0: Receipt.Key = "forged"; break;
			case 1: Receipt.DeclarationDigest = new string('e', 64); break;
			case 2: Receipt.BeforeHash = new string('e', 64); break;
			case 3: Receipt.AfterHash = new string('e', 64); break;
			case 4: Receipt.JobId = "forged"; break;
			}
		}
	}
}
#endif
