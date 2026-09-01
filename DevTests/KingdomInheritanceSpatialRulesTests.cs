#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomInheritanceSpatialRulesTests
	{
		[Test]
		public void NativeSpatialAdapterKeepsExactInternalAndNestedAbi()
		{
			string source = KingdomInheritanceSpatialLogicalSource.Read();
			StringAssert.Contains("internal enum KingdomInheritanceSpatialCaptureResult", source);
			StringAssert.Contains("Captured = 0", source);
			StringAssert.Contains("Unavailable = 1", source);
			StringAssert.Contains("Malformed = 2", source);
			StringAssert.Contains("internal static partial class KingdomInheritanceSpatial", source);
			StringAssert.Contains("private sealed class SourceWork", source);
			string[] fields =
			{
				"internal int WorkId;", "internal string Blueprint;", "internal int X;",
				"internal int Y;"
			};
			int prior = -1;
			for (int i = 0; i < fields.Length; i++)
			{
				int at = source.IndexOf(fields[i], StringComparison.Ordinal);
				Assert.Greater(at, prior, "source-work field order " + i);
				prior = at;
			}
		}

		private static ArchitectureLayoutSnapshot House()
		{
			MethodInfo compile = typeof(KingdomArchitectureRulesTests).GetMethod("Compile",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull(compile);
			return (ArchitectureLayoutSnapshot)compile.Invoke(null, null);
		}

		private static ArchitectureLayoutSnapshot Heart()
		{
			MethodInfo compile = typeof(KingdomArchitectureRulesTests).GetMethod("HeartSnapshot",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull(compile);
			return (ArchitectureLayoutSnapshot)compile.Invoke(null, new object[]
				{ 1, ArchitectureLotSize.Small, 6, 4, 2, 0 });
		}

		private static void Encode(ArchitectureLayoutSnapshot Snapshot, out string Encoded,
			out string Hash)
		{
			Assert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(Snapshot, out Encoded,
				out string failure), failure);
			Assert.IsTrue(KingdomArchitectureRules.TryEncodedSnapshotHash(Encoded, out Hash,
				out failure), failure);
		}

		private static KingdomSealRecord HouseRecord()
		{
			ArchitectureLayoutSnapshot snapshot = House();
			Encode(snapshot, out string encoded, out string hash);
			KingdomSealRecord record = new KingdomSealRecord
			{
				SpatialVersion = KingdomInheritanceSpatialRules.SpatialVersion,
				SpatialWidth = KingdomInheritanceSpatialRules.Width,
				SpatialHeight = KingdomInheritanceSpatialRules.Height,
				SpatialEntrySide = KingdomInheritanceSpatialRules.North,
				SpatialEntryX = 22,
				SpatialEntryY = 0
			};
			record.WorkKeys.Add("tent");
			record.WorkX.Add(22);
			record.WorkY.Add(11);
			record.WorkConditions.Add(90);
			record.WorkSnapshots.Add(encoded);
			record.WorkSnapshotHashes.Add(hash);
			for (int y = 0; y <= 9; y++)
			{
				record.StreetX.Add(22);
				record.StreetY.Add(y);
			}
			return record;
		}

		private static KingdomSealBody WithoutSpatialKeys(KingdomSealBody Source)
		{
			HashSet<string> spatial = new HashSet<string>(StringComparer.Ordinal)
			{
				"spatial_version", "spatial_width", "spatial_height", "spatial_entry_side",
				"spatial_entry_x", "spatial_entry_y", "work_snapshot", "work_snapshot_hash",
				"street_x", "street_y", "profile_schema", "technology_band", "canonical_body",
				"source_profile_digest", "profile_provenance_digest"
			};
			KingdomSealBody copy = new KingdomSealBody();
			for (int i = 0; i < Source.Keys.Count; i++)
			{
				string key = Source.Keys[i];
				if (spatial.Contains(key)) continue;
				switch (Source.KindOf(key))
				{
					case KingdomSealKind.Text: copy.Put(key, Source.Text(key)); break;
					case KingdomSealKind.Number: copy.Put(key, Source.Number(key)); break;
					case KingdomSealKind.TextList: copy.PutList(key, Source.TextList(key)); break;
					case KingdomSealKind.NumberList: copy.PutList(key, Source.NumberList(key)); break;
					default: copy.PutList(key, new string[0]); break;
				}
			}
			return copy;
		}

		private static KingdomSealBody WithoutProfileKeys(KingdomSealBody Source)
		{
			HashSet<string> profile = new HashSet<string>(StringComparer.Ordinal)
			{
				"profile_schema", "technology_band", "canonical_body",
				"source_profile_digest", "profile_provenance_digest"
			};
			KingdomSealBody copy = new KingdomSealBody();
			for (int i = 0; i < Source.Keys.Count; i++)
			{
				string key = Source.Keys[i];
				if (profile.Contains(key)) continue;
				switch (Source.KindOf(key))
				{
					case KingdomSealKind.Text: copy.Put(key, Source.Text(key)); break;
					case KingdomSealKind.Number: copy.Put(key, Source.Number(key)); break;
					case KingdomSealKind.TextList: copy.PutList(key, Source.TextList(key)); break;
					case KingdomSealKind.NumberList: copy.PutList(key, Source.NumberList(key)); break;
					default: copy.PutList(key, new string[0]); break;
				}
			}
			return copy;
		}

		[Test]
		public void CanonicalExactSnapshotAndBoundaryStreetValidateTogether()
		{
			Assert.AreEqual(KingdomArchitectureRules.MaxSnapshotChars,
				KingdomInheritanceSpatialRules.MaxSnapshotChars);
			KingdomSealRecord record = HouseRecord();
			Assert.IsTrue(KingdomInheritanceSpatialRules.TryValidate(record.WorkKeys,
				record.WorkX, record.WorkY, record.WorkConditions, record.WorkSnapshots,
				record.WorkSnapshotHashes, record.SpatialWidth, record.SpatialHeight,
				record.SpatialEntrySide, record.SpatialEntryX, record.SpatialEntryY,
				record.StreetX, record.StreetY, out KingdomInheritanceSpatialFault fault),
				fault.ToString());
		}

		[Test]
		public void SnapshotHashTamperAndRaggedOrDisconnectedStreetFailClosed()
		{
			KingdomSealRecord record = HouseRecord();
			record.WorkSnapshotHashes[0] = new string('0', 64);
			Assert.IsFalse(KingdomInheritanceSpatialRules.TryValidate(record.WorkKeys,
				record.WorkX, record.WorkY, record.WorkConditions, record.WorkSnapshots,
				record.WorkSnapshotHashes, record.SpatialWidth, record.SpatialHeight,
				record.SpatialEntrySide, record.SpatialEntryX, record.SpatialEntryY,
				record.StreetX, record.StreetY, out KingdomInheritanceSpatialFault fault));
			Assert.AreEqual(KingdomInheritanceSpatialFault.SnapshotHash, fault);

			record = HouseRecord();
			record.StreetX.Add(50);
			Assert.IsFalse(KingdomInheritanceSpatialRules.TryValidate(record.WorkKeys,
				record.WorkX, record.WorkY, record.WorkConditions, record.WorkSnapshots,
				record.WorkSnapshotHashes, record.SpatialWidth, record.SpatialHeight,
				record.SpatialEntrySide, record.SpatialEntryX, record.SpatialEntryY,
				record.StreetX, record.StreetY, out fault));
			Assert.AreEqual(KingdomInheritanceSpatialFault.RaggedStreets, fault);

			record = HouseRecord();
			record.StreetX.Add(50);
			record.StreetY.Add(20);
			Assert.IsFalse(KingdomInheritanceSpatialRules.TryValidate(record.WorkKeys,
				record.WorkX, record.WorkY, record.WorkConditions, record.WorkSnapshots,
				record.WorkSnapshotHashes, record.SpatialWidth, record.SpatialHeight,
				record.SpatialEntrySide, record.SpatialEntryX, record.SpatialEntryY,
				record.StreetX, record.StreetY, out fault));
			Assert.AreEqual(KingdomInheritanceSpatialFault.DisconnectedStreet, fault);
		}

		[Test]
		public void CurrentSpatialStateKeepsExactPoseSnapshotAndStreetGraph()
		{
			KingdomSealRecord record = HouseRecord();
			Assert.IsTrue(KingdomInheritRules.TryPrepare(record,
				KingdomRules.InheritedState.Abandoned, 50,
				out KingdomInheritPlacement placement, out KingdomInheritFault fault),
				fault.ToString());
			KingdomInheritWork work = placement.WorkAt(0);
			Assert.AreEqual(22, work.X);
			Assert.AreEqual(11, work.Y);
			Assert.AreEqual(KingdomInheritWorkState.Derelict, work.State);
			Assert.AreEqual(record.WorkSnapshots[0], work.ArchitectureSnapshot);
			Assert.AreEqual(record.WorkSnapshotHashes[0], work.ArchitectureHash);
			Assert.AreEqual(record.StreetX.Count, placement.StreetCount);
			for (int i = 0; i < placement.StreetCount; i++)
			{
				Assert.AreEqual(record.StreetX[i], placement.StreetXAt(i));
				Assert.AreEqual(record.StreetY[i], placement.StreetYAt(i));
			}
		}

		[Test]
		public void InheritedConditionDrivesDeterministicVisibleFabricWithoutMarkingFloors()
		{
			Assert.AreEqual(20, KingdomInheritanceFabricRules.WearFor(
				KingdomInheritWorkState.Standing, 80));
			Assert.AreEqual(KingdomVisualStateKind.HalfRuined,
				KingdomInheritanceFabricRules.VisualStateFor(20));
			Assert.AreEqual(KingdomMaterialRules.MaxWearPercent,
				KingdomInheritanceFabricRules.WearFor(KingdomInheritWorkState.Derelict, 20));
			Assert.AreEqual(0, KingdomInheritanceFabricRules.WearFor(
				KingdomInheritWorkState.Memory, 0));
			Assert.IsFalse(KingdomInheritanceFabricRules.MarksComponent(
				KingdomInheritWorkState.Derelict, 20, ArchitectureLayer.Ground,
				new string('a', 64), "floor:1"));
			Assert.IsTrue(KingdomInheritanceFabricRules.MarksComponent(
				KingdomInheritWorkState.Derelict, 20, ArchitectureLayer.Structure,
				new string('a', 64), "wall:1"));
			bool first = KingdomInheritanceFabricRules.MarksComponent(
				KingdomInheritWorkState.Standing, 80, ArchitectureLayer.Object,
				new string('b', 64), "fixture:1");
			Assert.AreEqual(first, KingdomInheritanceFabricRules.MarksComponent(
				KingdomInheritWorkState.Standing, 80, ArchitectureLayer.Object,
				new string('b', 64), "fixture:1"));
		}

		[Test]
		public void ExistingAuthorityDegradesWholeWorkToMemoryAndNeverDuplicatesBasin()
		{
			ArchitectureLayoutSnapshot snapshot = Heart();
			Encode(snapshot, out string encoded, out string hash);
			KingdomSealRecord record = new KingdomSealRecord
			{
				SpatialVersion = 1, SpatialWidth = 80, SpatialHeight = 25,
				SpatialEntrySide = KingdomInheritanceSpatialRules.North,
				SpatialEntryX = 20, SpatialEntryY = 0
			};
			record.WorkKeys.Add("heartbasin");
			record.WorkX.Add(22);
			record.WorkY.Add(1);
			record.WorkConditions.Add(100);
			record.WorkSnapshots.Add(encoded);
			record.WorkSnapshotHashes.Add(hash);
			record.StreetX.Add(20);
			record.StreetY.Add(0);
			Assert.IsTrue(KingdomInheritRules.TryPrepare(record,
				KingdomRules.InheritedState.Held, 50, out KingdomInheritPlacement placement,
				out KingdomInheritFault fault), fault.ToString());
			Assert.AreEqual(KingdomInheritRules.MemoryKey, placement.WorkAt(0).Key);
			Assert.AreEqual("", placement.WorkAt(0).ArchitectureSnapshot);
		}

		[Test]
		public void SpatialVersionZeroStillUsesLegacyProxyPreparation()
		{
			KingdomSealRecord record = new KingdomSealRecord();
			record.WorkKeys.Add("palisade");
			record.WorkX.Add(10);
			record.WorkY.Add(10);
			record.WorkConditions.Add(80);
			Assert.IsTrue(KingdomInheritRules.TryPrepare(record,
				KingdomRules.InheritedState.Held, 50, out KingdomInheritPlacement placement,
				out KingdomInheritFault fault), fault.ToString());
			Assert.AreEqual(0, placement.SpatialVersion);
			Assert.AreEqual(0, placement.StreetCount);
			Assert.AreEqual(KingdomInheritEngine.LegacyReconstructionVersion,
				KingdomInheritEngine.ReconstructionVersionFor(record));
		}

		[Test]
		public void LegacyProxyShapeHasAnExplicitVersionAndRefusesDimensionDrift()
		{
			Assert.AreEqual(1, KingdomInheritRules.LegacyProxyShapeVersion);
			Assert.IsTrue(KingdomInheritRules.LegacyProxyShapeMatches(
				"heartbasin", 3, 3, KingdomInheritRules.LegacyProxyShapeVersion));
			Assert.IsFalse(KingdomInheritRules.LegacyProxyShapeMatches(
				"heartbasin", 4, 4, KingdomInheritRules.LegacyProxyShapeVersion));
			Assert.IsFalse(KingdomInheritRules.LegacyProxyShapeMatches(
				"heartbasin", 3, 3, 2));
			Assert.IsTrue(KingdomInheritRules.TryValidateLegacyProxyShape(
				new List<string> { "tent", "palisade", "unknown-memory-token" },
				KingdomInheritRules.LegacyProxyShapeVersion, out string failure), failure);
			Assert.IsFalse(KingdomInheritRules.TryValidateLegacyProxyShape(
				new List<string> { "heartbasin" },
				KingdomInheritRules.LegacyProxyShapeVersion, out failure));
			StringAssert.Contains("legacy proxy shape changed for heartbasin", failure);
			Assert.IsFalse(KingdomInheritRules.TryValidateLegacyProxyShape(
				new List<string> { "tent" }, 2, out failure));
		}

		[Test]
		public void ChangedLegacyHeartProxyCannotBypassShapeMigrationGate()
		{
			KingdomSealRecord record = new KingdomSealRecord();
			record.WorkKeys.Add("heartwaterstone");
			record.WorkX.Add(10);
			record.WorkY.Add(10);
			record.WorkConditions.Add(100);
			Assert.IsFalse(KingdomInheritRules.TryPrepare(record,
				KingdomRules.InheritedState.Held, 50, out KingdomInheritPlacement placement,
				out KingdomInheritFault fault));
			Assert.IsNull(placement);
			Assert.AreEqual(KingdomInheritFault.Malformed, fault);
		}

		[Test]
		public void SchemaFourExternalRecordStillReadsAsExplicitLegacyProxy()
		{
			MethodInfo sample = typeof(KingdomSealRulesTests).GetMethod("SampleCapturedRecord",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull(sample);
			KingdomSealRecord current = (KingdomSealRecord)sample.Invoke(null, new object[]
				{ "lineage-four", "legacy-four", "origin-four", 0, 1 });
			string oldText = KingdomSealFormat.Compose(4,
				WithoutSpatialKeys(current.WriteBody()));
			Assert.IsTrue(KingdomSealRecord.TryParse(oldText, out KingdomSealRecord old,
				out KingdomSealFault fault, out string detail), fault + ": " + detail);
			Assert.AreEqual(oldText, old.Compose(),
				"parsed schema-four authority must retain its exact canonical envelope");
			StringAssert.StartsWith("taf-seal 6\n", KingdomSealRules.Copy(old).Compose(),
				"a new transition copy must use the current schema");
			Assert.AreEqual(0, old.SpatialVersion);
			Assert.AreEqual(0, old.WorkSnapshots.Count);
			Assert.AreEqual(0, old.StreetX.Count);
		}

		[Test]
		public void SchemaFiveRoundTripRetainsSpatialGraphButPinsProfileUnresolved()
		{
			MethodInfo sample = typeof(KingdomSealRulesTests).GetMethod("SampleCapturedRecord",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull(sample);
			KingdomSealRecord current = (KingdomSealRecord)sample.Invoke(null, new object[]
				{ "lineage-five", "legacy-five", "origin-five", 0, 1 });
			KingdomSealRecord spatial = HouseRecord();
			current.WorkKeys = new List<string>(spatial.WorkKeys);
			current.WorkX = new List<int>(spatial.WorkX);
			current.WorkY = new List<int>(spatial.WorkY);
			current.WorkConditions = new List<int>(spatial.WorkConditions);
			current.SpatialVersion = spatial.SpatialVersion;
			current.SpatialWidth = spatial.SpatialWidth;
			current.SpatialHeight = spatial.SpatialHeight;
			current.SpatialEntrySide = spatial.SpatialEntrySide;
			current.SpatialEntryX = spatial.SpatialEntryX;
			current.SpatialEntryY = spatial.SpatialEntryY;
			current.WorkSnapshots = new List<string>(spatial.WorkSnapshots);
			current.WorkSnapshotHashes = new List<string>(spatial.WorkSnapshotHashes);
			current.StreetX = new List<int>(spatial.StreetX);
			current.StreetY = new List<int>(spatial.StreetY);
			string schemaFive = KingdomSealFormat.Compose(5,
				WithoutProfileKeys(current.WriteBody()));
			Assert.IsTrue(KingdomSealRecord.TryParse(schemaFive,
				out KingdomSealRecord read, out KingdomSealFault fault, out string detail),
				fault + ": " + detail);
			Assert.AreEqual(schemaFive, read.Compose(),
				"parsed schema-five authority must retain its exact canonical envelope");
			CollectionAssert.AreEqual(current.WorkSnapshots, read.WorkSnapshots);
			CollectionAssert.AreEqual(current.WorkSnapshotHashes, read.WorkSnapshotHashes);
			CollectionAssert.AreEqual(current.StreetX, read.StreetX);
			CollectionAssert.AreEqual(current.StreetY, read.StreetY);
			Assert.AreEqual(KingdomPolityProfileRules.UnresolvedLegacyProfileSchema,
				read.ProfileSchema);
			Assert.AreEqual(0, read.TechnologyBand);
			CollectionAssert.IsEmpty(read.CanonicalBodyKeys);
		}

		[Test]
		public void CairnCarriesNamedRollAsHistoryWithoutReplayingPeople()
		{
			KingdomSealRecord record = new KingdomSealRecord();
			record.RollNames.Add("Aster");
			record.RollOrigins.Add("the salt marshes");
			record.RollArrived.Add("came at the first rain");
			string text = KingdomInheritEngine.ComposeCairnText(record);
			StringAssert.Contains("Remembered settlers", text);
			StringAssert.Contains("Aster, from the salt marshes", text);
			StringAssert.Contains("came at the first rain", text);
		}
	}
}
#endif
