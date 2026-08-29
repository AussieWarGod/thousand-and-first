#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomLabBodyHistoryRegistryTests
	{
		private const string Realm =
			"taf:realm:v1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

		[Test]
		public void VersionTwoBindsRulerLifeAndCarriesLegacyRows()
		{
			KingdomLabRegistryEntry row = BoundRow();
			KingdomLabRegistryEntry legacy = row.Copy();
			legacy.JobId = "legacy";
			legacy.RulerSuccessionOrdinal = -1;
			legacy.RulerLifeId = "";
			string wire = KingdomLabRules.FormatRegistry(
				new List<KingdomLabRegistryEntry> { row, legacy });
			Assert.That(wire, Does.StartWith("v2\n"));
			List<KingdomLabRegistryEntry> loaded = KingdomLabRules.ParseRegistry(
				wire, out bool quarantined);
			Assert.IsFalse(quarantined);
			Assert.AreEqual(2, loaded.Count);
			Assert.AreEqual(row.RulerSuccessionOrdinal,
				loaded[0].RulerSuccessionOrdinal);
			Assert.AreEqual(row.RulerLifeId, loaded[0].RulerLifeId);
			Assert.IsTrue(KingdomLabRules.RegistryAuthority(loaded[0], row,
				RequireActive: true));
			Assert.AreEqual(-1, loaded[1].RulerSuccessionOrdinal);
			Assert.AreEqual("", loaded[1].RulerLifeId);
			Assert.AreEqual(wire, KingdomLabRules.FormatRegistry(loaded));
			string[] lines = wire.Split('\n');
			string[] fields = lines[1].Split('|');
			fields[6] = "1";
			lines[1] = string.Join("|", fields);
			List<KingdomLabRegistryEntry> tampered = KingdomLabRules.ParseRegistry(
				string.Join("\n", lines), out quarantined);
			Assert.IsTrue(quarantined);
			Assert.AreEqual(1, tampered.Count, "foreign life row must be discarded");

			KingdomLabRegistryEntry changed = row.Copy();
			changed.RulerSuccessionOrdinal++;
			Assert.IsFalse(KingdomLabRules.RegistryAuthority(row, changed, false));
			changed = row.Copy();
			changed.RulerLifeId = KingdomBodyHistoryRulerLifeRules.Identity(
				Realm, 1, "taf:object:" + row.PatientId);
			Assert.IsFalse(KingdomLabRules.RegistryAuthority(row, changed, false));
		}

		private static KingdomLabRegistryEntry BoundRow()
		{
			string detail = "stamp:" + KingdomLabRules.ExecutionStampFingerprint("stamp");
			string fingerprint = KingdomLabRules.EffectFingerprint(
				KingdomLabRules.EffectContractVersion, "grafted-hand", "GasImmunity",
				(int)LabSource.Part, (int)LabAttach.Body, "TAF::Lab::grafted-hand", detail);
			return new KingdomLabRegistryEntry
			{
				JobId = "job", BuildingId = "hall", PatientId = "body-one",
				GameId = "game", RealmId = Realm, RealmFoundedTick = 5,
				RulerSuccessionOrdinal = 0,
				RulerLifeId = KingdomBodyHistoryRulerLifeRules.Identity(
					Realm, 0, "taf:object:body-one"),
				ContractVersion = KingdomLabRules.EffectContractVersion,
				ProcedureKey = "grafted-hand", Grants = "GasImmunity",
				Source = (int)LabSource.Part, Attach = (int)LabAttach.Body,
				Manager = "TAF::Lab::grafted-hand", Detail = detail,
				Fingerprint = fingerprint, Status = KingdomLabRegistryStatus.Active,
				UpdatedTick = 9
			};
		}
	}
}
#endif
