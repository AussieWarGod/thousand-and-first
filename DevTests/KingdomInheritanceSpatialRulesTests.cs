#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
			StringAssert.Contains("Pending = 3", source);
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
				ClassicAssert.Greater(at, prior, "source-work field order " + i);
				prior = at;
			}
		}

		private static ArchitectureLayoutSnapshot House()
		{
			MethodInfo compile = typeof(KingdomArchitectureRulesTests).GetMethod("Compile",
				BindingFlags.Static | BindingFlags.NonPublic);
			ClassicAssert.IsNotNull(compile);
			return (ArchitectureLayoutSnapshot)compile.Invoke(null, null);
		}

		private static ArchitectureLayoutSnapshot Heart()
		{
			MethodInfo compile = typeof(KingdomArchitectureRulesTests).GetMethod("HeartSnapshot",
				BindingFlags.Static | BindingFlags.NonPublic);
			ClassicAssert.IsNotNull(compile);
			return (ArchitectureLayoutSnapshot)compile.Invoke(null, new object[]
				{ 1, ArchitectureLotSize.Small, 6, 4, 2, 0 });
		}

		private static void Encode(ArchitectureLayoutSnapshot Snapshot, out string Encoded,
			out string Hash)
		{
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodeSnapshot(Snapshot, out Encoded,
				out string failure), failure);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryEncodedSnapshotHash(Encoded, out Hash,
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
			ClassicAssert.AreEqual(KingdomArchitectureRules.MaxSnapshotChars,
				KingdomInheritanceSpatialRules.MaxSnapshotChars);
			KingdomSealRecord record = HouseRecord();
			ClassicAssert.IsTrue(KingdomInheritanceSpatialRules.TryValidate(record.WorkKeys,
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
			ClassicAssert.IsFalse(KingdomInheritanceSpatialRules.TryValidate(record.WorkKeys,
				record.WorkX, record.WorkY, record.WorkConditions, record.WorkSnapshots,
				record.WorkSnapshotHashes, record.SpatialWidth, record.SpatialHeight,
				record.SpatialEntrySide, record.SpatialEntryX, record.SpatialEntryY,
				record.StreetX, record.StreetY, out KingdomInheritanceSpatialFault fault));
			ClassicAssert.AreEqual(KingdomInheritanceSpatialFault.SnapshotHash, fault);

			record = HouseRecord();
			record.StreetX.Add(50);
			ClassicAssert.IsFalse(KingdomInheritanceSpatialRules.TryValidate(record.WorkKeys,
				record.WorkX, record.WorkY, record.WorkConditions, record.WorkSnapshots,
				record.WorkSnapshotHashes, record.SpatialWidth, record.SpatialHeight,
				record.SpatialEntrySide, record.SpatialEntryX, record.SpatialEntryY,
				record.StreetX, record.StreetY, out fault));
			ClassicAssert.AreEqual(KingdomInheritanceSpatialFault.RaggedStreets, fault);

			record = HouseRecord();
			record.StreetX.Add(50);
			record.StreetY.Add(20);
			ClassicAssert.IsFalse(KingdomInheritanceSpatialRules.TryValidate(record.WorkKeys,
				record.WorkX, record.WorkY, record.WorkConditions, record.WorkSnapshots,
				record.WorkSnapshotHashes, record.SpatialWidth, record.SpatialHeight,
				record.SpatialEntrySide, record.SpatialEntryX, record.SpatialEntryY,
				record.StreetX, record.StreetY, out fault));
			ClassicAssert.AreEqual(KingdomInheritanceSpatialFault.DisconnectedStreet, fault);
		}

		[Test]
		public void CurrentSpatialStateKeepsExactPoseSnapshotAndStreetGraph()
		{
			KingdomSealRecord record = HouseRecord();
			ClassicAssert.IsTrue(KingdomInheritRules.TryPrepare(record,
				KingdomRules.InheritedState.Abandoned, 50,
				out KingdomInheritPlacement placement, out KingdomInheritFault fault),
				fault.ToString());
			KingdomInheritWork work = placement.WorkAt(0);
			ClassicAssert.AreEqual(22, work.X);
			ClassicAssert.AreEqual(11, work.Y);
			ClassicAssert.AreEqual(KingdomInheritWorkState.Derelict, work.State);
			ClassicAssert.AreEqual(record.WorkSnapshots[0], work.ArchitectureSnapshot);
			ClassicAssert.AreEqual(record.WorkSnapshotHashes[0], work.ArchitectureHash);
			ClassicAssert.AreEqual(record.StreetX.Count, placement.StreetCount);
			for (int i = 0; i < placement.StreetCount; i++)
			{
				ClassicAssert.AreEqual(record.StreetX[i], placement.StreetXAt(i));
				ClassicAssert.AreEqual(record.StreetY[i], placement.StreetYAt(i));
			}
		}

		[Test]
		public void InheritedConditionDrivesDeterministicVisibleFabricWithoutMarkingFloors()
		{
			ClassicAssert.AreEqual(20, KingdomInheritanceFabricRules.WearFor(
				KingdomInheritWorkState.Standing, 80));
			ClassicAssert.AreEqual(KingdomVisualStateKind.HalfRuined,
				KingdomInheritanceFabricRules.VisualStateFor(20));
			ClassicAssert.AreEqual(KingdomMaterialRules.MaxWearPercent,
				KingdomInheritanceFabricRules.WearFor(KingdomInheritWorkState.Derelict, 20));
			ClassicAssert.AreEqual(0, KingdomInheritanceFabricRules.WearFor(
				KingdomInheritWorkState.Memory, 0));
			ClassicAssert.IsFalse(KingdomInheritanceFabricRules.MarksComponent(
				KingdomInheritWorkState.Derelict, 20, ArchitectureLayer.Ground,
				new string('a', 64), "floor:1"));
			ClassicAssert.IsTrue(KingdomInheritanceFabricRules.MarksComponent(
				KingdomInheritWorkState.Derelict, 20, ArchitectureLayer.Structure,
				new string('a', 64), "wall:1"));
			bool first = KingdomInheritanceFabricRules.MarksComponent(
				KingdomInheritWorkState.Standing, 80, ArchitectureLayer.Object,
				new string('b', 64), "fixture:1");
			ClassicAssert.AreEqual(first, KingdomInheritanceFabricRules.MarksComponent(
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
			ClassicAssert.IsTrue(KingdomInheritRules.TryPrepare(record,
				KingdomRules.InheritedState.Held, 50, out KingdomInheritPlacement placement,
				out KingdomInheritFault fault), fault.ToString());
			ClassicAssert.AreEqual(KingdomInheritRules.MemoryKey, placement.WorkAt(0).Key);
			ClassicAssert.AreEqual("", placement.WorkAt(0).ArchitectureSnapshot);
		}

		[Test]
		public void SpatialVersionZeroStillUsesLegacyProxyPreparation()
		{
			KingdomSealRecord record = new KingdomSealRecord();
			record.WorkKeys.Add("palisade");
			record.WorkX.Add(10);
			record.WorkY.Add(10);
			record.WorkConditions.Add(80);
			ClassicAssert.IsTrue(KingdomInheritRules.TryPrepare(record,
				KingdomRules.InheritedState.Held, 50, out KingdomInheritPlacement placement,
				out KingdomInheritFault fault), fault.ToString());
			ClassicAssert.AreEqual(0, placement.SpatialVersion);
			ClassicAssert.AreEqual(0, placement.StreetCount);
			ClassicAssert.AreEqual(KingdomInheritEngine.LegacyReconstructionVersion,
				KingdomInheritEngine.ReconstructionVersionFor(record));
		}

		[Test]
		public void LegacyProxyShapeHasAnExplicitVersionAndRefusesDimensionDrift()
		{
			ClassicAssert.AreEqual(1, KingdomInheritRules.LegacyProxyShapeVersion);
			ClassicAssert.IsTrue(KingdomInheritRules.LegacyProxyShapeMatches(
				"heartbasin", 3, 3, KingdomInheritRules.LegacyProxyShapeVersion));
			ClassicAssert.IsFalse(KingdomInheritRules.LegacyProxyShapeMatches(
				"heartbasin", 4, 4, KingdomInheritRules.LegacyProxyShapeVersion));
			ClassicAssert.IsFalse(KingdomInheritRules.LegacyProxyShapeMatches(
				"heartbasin", 3, 3, 2));
			ClassicAssert.IsTrue(KingdomInheritRules.TryValidateLegacyProxyShape(
				new List<string> { "tent", "palisade", "unknown-memory-token" },
				KingdomInheritRules.LegacyProxyShapeVersion, out string failure), failure);
			ClassicAssert.IsFalse(KingdomInheritRules.TryValidateLegacyProxyShape(
				new List<string> { "heartbasin" },
				KingdomInheritRules.LegacyProxyShapeVersion, out failure));
			StringAssert.Contains("legacy proxy shape changed for heartbasin", failure);
			ClassicAssert.IsFalse(KingdomInheritRules.TryValidateLegacyProxyShape(
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
			ClassicAssert.IsFalse(KingdomInheritRules.TryPrepare(record,
				KingdomRules.InheritedState.Held, 50, out KingdomInheritPlacement placement,
				out KingdomInheritFault fault));
			ClassicAssert.IsNull(placement);
			ClassicAssert.AreEqual(KingdomInheritFault.Malformed, fault);
		}

		[Test]
		public void SchemaFourExternalRecordStillReadsAsExplicitLegacyProxy()
		{
			MethodInfo sample = typeof(KingdomSealRulesTests).GetMethod("SampleCapturedRecord",
				BindingFlags.Static | BindingFlags.NonPublic);
			ClassicAssert.IsNotNull(sample);
			KingdomSealRecord current = (KingdomSealRecord)sample.Invoke(null, new object[]
				{ "lineage-four", "legacy-four", "origin-four", 0, 1 });
			string oldText = KingdomSealFormat.Compose(4,
				WithoutSpatialKeys(current.WriteBody()));
			ClassicAssert.IsTrue(KingdomSealRecord.TryParse(oldText, out KingdomSealRecord old,
				out KingdomSealFault fault, out string detail), fault + ": " + detail);
			ClassicAssert.AreEqual(oldText, old.Compose(),
				"parsed schema-four authority must retain its exact canonical envelope");
			StringAssert.StartsWith("taf-seal 6\n", KingdomSealRules.Copy(old).Compose(),
				"a new transition copy must use the current schema");
			ClassicAssert.AreEqual(0, old.SpatialVersion);
			ClassicAssert.AreEqual(0, old.WorkSnapshots.Count);
			ClassicAssert.AreEqual(0, old.StreetX.Count);
		}

		[Test]
		public void SchemaFiveRoundTripRetainsSpatialGraphButPinsProfileUnresolved()
		{
			MethodInfo sample = typeof(KingdomSealRulesTests).GetMethod("SampleCapturedRecord",
				BindingFlags.Static | BindingFlags.NonPublic);
			ClassicAssert.IsNotNull(sample);
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
			ClassicAssert.IsTrue(KingdomSealRecord.TryParse(schemaFive,
				out KingdomSealRecord read, out KingdomSealFault fault, out string detail),
				fault + ": " + detail);
			ClassicAssert.AreEqual(schemaFive, read.Compose(),
				"parsed schema-five authority must retain its exact canonical envelope");
			CollectionAssert.AreEqual(current.WorkSnapshots, read.WorkSnapshots);
			CollectionAssert.AreEqual(current.WorkSnapshotHashes, read.WorkSnapshotHashes);
			CollectionAssert.AreEqual(current.StreetX, read.StreetX);
			CollectionAssert.AreEqual(current.StreetY, read.StreetY);
			ClassicAssert.AreEqual(KingdomPolityProfileRules.UnresolvedLegacyProfileSchema,
				read.ProfileSchema);
			ClassicAssert.AreEqual(0, read.TechnologyBand);
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
