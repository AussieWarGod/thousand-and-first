#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomInheritEngineTests
	{
		[Test]
		public void DeclarationsKeepExactInternalAbiOrdinalsAndFieldOrder()
		{
			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomInheritApplyStatus)));
			Assert.AreEqual("0:Applied,1:AlreadyApplied,2:Refused,3:Failed",
				string.Join(",", Array.ConvertAll((KingdomInheritApplyStatus[])Enum.GetValues(
					typeof(KingdomInheritApplyStatus)), value => ((int)value) + ":" + value)));
			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomInheritApplyFault)));
			Assert.AreEqual("0:None,1:NullInput,2:LegacyNotPromoted,3:ReceiptNotReserved,"
				+ "4:ReceiptMismatch,5:TargetGameMismatch,6:TargetZoneMismatch,7:PlanInvalid,"
				+ "8:WrongZoneSize,9:ApplicationConflict,10:PartialApplication,11:BlueprintMissing,"
				+ "12:InvalidCell,13:ConnectionCell,14:Terrain,15:Occupied,16:Stairs,"
				+ "17:EntryToHeartPath,18:ObjectCreation,19:ObjectNotEmpty,20:ObjectPlacement,"
				+ "21:MarkerWrite",
				string.Join(",", Array.ConvertAll((KingdomInheritApplyFault[])Enum.GetValues(
					typeof(KingdomInheritApplyFault)), value => ((int)value) + ":" + value)));

			System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.NonPublic |
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;
			Type result = typeof(KingdomInheritApplyResult);
			Assert.IsTrue(result.IsNotPublic && result.IsSealed);
			System.Reflection.FieldInfo[] resultFields = result.GetFields(fields);
			Assert.AreEqual("Status,Fault,Detail,ApplicationMarker,PlacedCount,FreshEmptyVerified",
				string.Join(",", Array.ConvertAll(resultFields, field => field.Name)));
			for (int i = 0; i < resultFields.Length; i++) Assert.IsTrue(resultFields[i].IsInitOnly);
			KingdomInheritApplyResult empty = new KingdomInheritApplyResult(
				KingdomInheritApplyStatus.Applied, KingdomInheritApplyFault.None, null, null, 0, false);
			Assert.AreEqual("", empty.Detail);
			Assert.AreEqual("", empty.ApplicationMarker);

			Type facts = typeof(KingdomInheritCellFacts);
			Assert.IsTrue(facts.IsValueType && !facts.IsEnum);
			Assert.AreEqual("Exists,Occupied,Terrain,Stairs,Connection,Walkable",
				string.Join(",", Array.ConvertAll(facts.GetFields(fields), field => field.Name)));
			Type spec = typeof(KingdomInheritBuildSpec);
			Assert.IsTrue(spec.IsNotPublic && spec.IsSealed);
			Assert.AreEqual("Index,Key,Blueprint,X,Y,Condition,State,FootprintWidth,FootprintHeight,"
				+ "FootprintX,FootprintY,IsArchitecture,IsStreet,ArchitectureSnapshot,ArchitectureHash",
				string.Join(",", Array.ConvertAll(spec.GetFields(fields), field => field.Name)));

			Type host = typeof(IKingdomInheritEngineHost);
			Assert.IsTrue(host.IsNotPublic && host.IsInterface);
			Assert.AreEqual("Width,Height,ZoneId,TargetGameId",
				string.Join(",", Array.ConvertAll(host.GetProperties(), property => property.Name)));
			System.Reflection.MethodInfo[] hostMethods = Array.FindAll(host.GetMethods(),
				method => !method.IsSpecialName);
			Assert.AreEqual("ReadApplicationMarker,CountApplicationObjects,HasAnyApplicationObjects,"
				+ "HasExactApplicationObject,HasBlueprint,TryReadCell,TryCreateFresh,IsFreshEmpty,"
				+ "TryPlace,Discard,TryWriteApplicationMarker,TryRemoveApplicationMarker",
				string.Join(",", Array.ConvertAll(hostMethods, method => method.Name)));
			Type engine = typeof(KingdomInheritEngine);
			Assert.IsTrue(engine.IsNotPublic && engine.IsAbstract && engine.IsSealed);
		}

		private sealed class FakeObject
		{
			internal KingdomInheritBuildSpec Spec;

			internal string Marker;

			internal string CairnText;

			internal bool Empty;

			internal bool Placed;
		}

		private sealed class FakeHost : IKingdomInheritEngineHost
		{
			private readonly KingdomInheritCellFacts[,] Cells;

			internal readonly List<FakeObject> Objects = new List<FakeObject>();

			internal readonly List<string> CreatedBlueprints = new List<string>();

			internal string Marker = "";

			internal int MutationCalls;

			internal bool CreateEmpty = true;

			internal bool FailMarkerWrite;

			internal bool DiscardFails;

			internal int FailPlaceIndex = -1;

			internal int DirtyAfterPlaceIndex = -1;

			internal string MissingBlueprint = "";

			internal FakeHost(string ZoneId, string TargetGameId)
			{
				this.ZoneId = ZoneId;
				this.TargetGameId = TargetGameId;
				Cells = new KingdomInheritCellFacts[Width, Height];
				for (int y = 0; y < Height; y++)
				{
					for (int x = 0; x < Width; x++)
					{
						Cells[x, y] = new KingdomInheritCellFacts
						{
							Exists = true,
							Walkable = true
						};
					}
				}
			}

			public int Width { get { return KingdomInheritRules.TargetWidth; } }

			public int Height { get { return KingdomInheritRules.TargetHeight; } }

			public string ZoneId { get; private set; }

			public string TargetGameId { get; private set; }

			internal KingdomInheritCellFacts Facts(int X, int Y)
			{
				return Cells[X, Y];
			}

			internal void SetFacts(int X, int Y, KingdomInheritCellFacts Facts)
			{
				Cells[X, Y] = Facts;
			}

			public string ReadApplicationMarker()
			{
				return Marker;
			}

			public int CountApplicationObjects(string Marker)
			{
				int count = 0;
				for (int i = 0; i < Objects.Count; i++)
				{
					if (Objects[i].Placed && Objects[i].Marker == Marker)
					{
						count++;
					}
				}
				return count;
			}

			public bool HasAnyApplicationObjects()
			{
				for (int i = 0; i < Objects.Count; i++)
				{
					if (Objects[i].Placed && !string.IsNullOrEmpty(Objects[i].Marker))
					{
						return true;
					}
				}
				return false;
			}

			public bool HasExactApplicationObject(string Marker, KingdomInheritBuildSpec Spec,
				string CairnText)
			{
				for (int i = 0; i < Objects.Count; i++)
				{
					FakeObject obj = Objects[i];
					if (obj.Placed && obj.Marker == Marker
						&& obj.Spec.Index == Spec.Index && obj.Spec.Key == Spec.Key
						&& obj.Spec.Blueprint == Spec.Blueprint && obj.Spec.X == Spec.X
						&& obj.Spec.Y == Spec.Y && obj.Spec.Condition == Spec.Condition
						&& obj.Spec.State == Spec.State
						&& (Spec.Key != KingdomInheritRules.FounderCairnKey
							|| obj.CairnText == CairnText))
					{
						return true;
					}
				}
				return false;
			}

			public bool HasBlueprint(string Blueprint)
			{
				return Blueprint != MissingBlueprint;
			}

			public bool TryReadCell(int X, int Y, out KingdomInheritCellFacts Facts)
			{
				if (X < 0 || Y < 0 || X >= Width || Y >= Height)
				{
					Facts = new KingdomInheritCellFacts();
					return false;
				}
				Facts = Cells[X, Y];
				return Facts.Exists;
			}

			public bool TryCreateFresh(KingdomInheritBuildSpec Spec, string Marker,
				string CairnText, out object Handle, out string Failure)
			{
				MutationCalls++;
				Failure = "";
				FakeObject obj = new FakeObject
				{
					Spec = Spec,
					Marker = Marker,
					CairnText = CairnText,
					Empty = CreateEmpty
				};
				CreatedBlueprints.Add(Spec.Blueprint);
				Objects.Add(obj);
				Handle = obj;
				return true;
			}

			public bool IsFreshEmpty(object Handle)
			{
				FakeObject obj = Handle as FakeObject;
				return obj != null && obj.Empty;
			}

			public bool TryPlace(object Handle, int X, int Y, out string Failure)
			{
				MutationCalls++;
				FakeObject obj = Handle as FakeObject;
				if (obj == null || obj.Spec.Index == FailPlaceIndex)
				{
					Failure = "synthetic placement rejection";
					return false;
				}
				obj.Placed = true;
				if (obj.Spec.Index == DirtyAfterPlaceIndex)
				{
					obj.Empty = false;
				}
				Failure = "";
				return true;
			}

			public bool Discard(object Handle)
			{
				MutationCalls++;
				FakeObject obj = Handle as FakeObject;
				if (obj != null && !DiscardFails)
				{
					obj.Placed = false;
					Objects.Remove(obj);
				}
				return !DiscardFails;
			}

			public bool TryWriteApplicationMarker(string Marker, out string Failure)
			{
				MutationCalls++;
				if (FailMarkerWrite)
				{
					Failure = "synthetic marker failure";
					return false;
				}
				this.Marker = Marker;
				Failure = "";
				return true;
			}

			public bool TryRemoveApplicationMarker(string Marker)
			{
				MutationCalls++;
				if (this.Marker == Marker)
				{
					this.Marker = "";
				}
				return this.Marker.Length == 0;
			}
		}

		private static KingdomSealRecord StageRecord(params string[] Keys)
		{
			KingdomSealRecord record = new KingdomSealRecord
			{
				WriterVersion = "test",
				EngineVersion = "test",
				Status = KingdomSealStatus.Living,
				LineageId = "lineage",
				LegacyId = "legacy-one",
				OriginGameId = "origin.game",
				Generation = 1,
				Revision = 7,
				WrittenTick = 100L,
				FounderName = "Abram",
				RealmName = "Old Realm",
				SettlementName = "Old Seat",
				SettlementId = "old-seat",
				Vocation = "holding",
				Style = "common",
				FoundedTick = 10L,
				GroundZoneId = "JoppaWorld.1.1.1.1.10",
				RegionName = "Salt",
				TerrainBlueprint = "TerrainSaltMarsh",
				Depth = 10,
				Stage = (int)GrowthStage.Camp,
				Population = 2,
				Defence = 1,
				StoredWater = 5
			};
			record.Vigour = KingdomRules.SealedVigour((GrowthStage)record.Stage,
				record.Population, record.Defence, record.StoredWater, record.Withered);
			for (int i = 0; i < Keys.Length; i++)
			{
				record.WorkKeys.Add(Keys[i]);
				record.WorkX.Add(10 + i * 3);
				record.WorkY.Add(10 + (i % 2) * 3);
				record.WorkConditions.Add(90 - i * 5);
			}
			record.Chronicle.Add("The first wall went up before the first rain.");
			return KingdomSealTestIdentity.Bind(record);
		}

		private static KingdomSealRecord Promoted(params string[] Keys)
		{
			return KingdomSealRules.PromoteRetirement(
				KingdomSealRules.WithRetirement(StageRecord(Keys)));
		}

		private static KingdomSealRecord PromotedForState(int DesiredState, params string[] Keys)
		{
			KingdomSealRecord stage = StageRecord(Keys);
			if (DesiredState == (int)KingdomRules.InheritedState.Held)
			{
				stage.Stage = (int)GrowthStage.City;
				stage.Population = 25;
				stage.Defence = 10;
				stage.StoredWater = 120;
			}
			else if (DesiredState == (int)KingdomRules.InheritedState.Faded)
			{
				stage.Stage = (int)GrowthStage.Town;
				stage.Population = 10;
				stage.Defence = 0;
				stage.StoredWater = 80;
			}
			else if (DesiredState == (int)KingdomRules.InheritedState.Abandoned)
			{
				stage.Stage = (int)GrowthStage.Village;
				stage.Population = 5;
				stage.Defence = 2;
				stage.StoredWater = 8;
			}
			stage.Vigour = KingdomRules.SealedVigour((GrowthStage)stage.Stage,
				stage.Population, stage.Defence, stage.StoredWater, stage.Withered);
			for (int revision = 0; revision < 1000; revision++)
			{
				stage.Revision = revision;
				stage.WrittenTick = 100L + revision;
				KingdomSealRecord promoted = PromotedCopy(stage);
				if (promoted.InheritedState == DesiredState)
				{
					return promoted;
				}
			}
			throw new InvalidOperationException("No deterministic fixture produced inherited state " + DesiredState + ".");
		}

		private static KingdomSealRecord PromotedCopy(KingdomSealRecord Stage)
		{
			return KingdomSealRules.PromoteRetirement(KingdomSealRules.WithRetirement(Stage));
		}

		private static KingdomSealReceipt Receipt(KingdomSealRecord Record)
		{
			return new KingdomSealReceipt
			{
				LineageId = Record.LineageId,
				LegacyId = Record.LegacyId,
				TargetGameId = "target-game",
				State = KingdomSealReceiptState.Reserved,
				WrittenTick = 200L
			};
		}

		private static FakeHost Host(KingdomSealRecord Record)
		{
			return new FakeHost(Record.GroundZoneId, "target-game");
		}

		private static KingdomInheritPlacement Placement(KingdomSealRecord Record)
		{
			KingdomInheritPlacement placement;
			KingdomInheritFault fault;
			Assert.IsTrue(KingdomInheritRules.TryPrepare(Record.WorkKeys, Record.WorkX, Record.WorkY,
				Record.WorkConditions, (KingdomRules.InheritedState)Record.InheritedState,
				Record.InterregnumRoll, out placement, out fault), fault.ToString());
			return placement;
		}

		[Test]
		public void ExactPromotedReservationBuildsFreshEmptyAllowlistedObjectsAndCairn()
		{
			KingdomSealRecord record = Promoted("tent", "palisade", "heartbasin");
			FakeHost host = Host(record);
			KingdomInheritApplyResult result = KingdomInheritEngine.Apply(record, Receipt(record), host.ZoneId, host);

			Assert.AreEqual(KingdomInheritApplyStatus.Applied, result.Status, result.Detail);
			Assert.AreEqual(KingdomInheritApplyFault.None, result.Fault);
			Assert.IsTrue(result.ShouldCommit);
			Assert.IsFalse(result.ShouldRelease);
			Assert.IsTrue(result.FreshEmptyVerified);
			Assert.AreEqual(record.WorkKeys.Count + 1, result.PlacedCount, "founder cairn is unconditional");
			Assert.AreEqual(result.ApplicationMarker, host.Marker);
			StringAssert.Contains("|reserved|200|", result.ApplicationMarker,
				"the idempotence key binds the exact reserved receipt, including its written tick");
			Assert.AreEqual(result.PlacedCount, host.Objects.Count);
			for (int i = 0; i < host.Objects.Count; i++)
			{
				Assert.IsTrue(host.Objects[i].Empty, host.Objects[i].Spec.Key);
				StringAssert.StartsWith("r_Kingdom", host.Objects[i].Spec.Blueprint);
			}
			FakeObject cairn = host.Objects.Find(delegate(FakeObject o)
			{
				return o.Spec.Key == KingdomInheritRules.FounderCairnKey;
			});
			Assert.IsNotNull(cairn);
			StringAssert.Contains("Abram", cairn.CairnText);
			StringAssert.Contains("Chronicle of the old kingdom", cairn.CairnText);
			StringAssert.Contains(record.Chronicle[0], cairn.CairnText);
		}

		[Test]
		public void ExactMarkerAndRowsAreIdempotentWithoutFurtherMutation()
		{
			KingdomSealRecord record = Promoted("palisade", "heartbasin");
			KingdomSealReceipt receipt = Receipt(record);
			FakeHost host = Host(record);
			KingdomInheritApplyResult first = KingdomInheritEngine.Apply(record, receipt, host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Applied, first.Status, first.Detail);
			int mutations = host.MutationCalls;

			KingdomInheritApplyResult second = KingdomInheritEngine.Apply(record, receipt, host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.AlreadyApplied, second.Status, second.Detail);
			Assert.AreEqual(first.ApplicationMarker, second.ApplicationMarker);
			Assert.IsTrue(second.ShouldCommit);
			Assert.AreEqual(mutations, host.MutationCalls);
			Assert.AreEqual(first.PlacedCount, host.Objects.Count);
		}

		[Test]
		public void PlacementSideEffectThatAddsStateIsRejectedAndRolledBack()
		{
			KingdomSealRecord record = Promoted("palisade", "heartbasin");
			FakeHost host = Host(record);
			host.DirtyAfterPlaceIndex = 1;

			KingdomInheritApplyResult result = KingdomInheritEngine.Apply(record, Receipt(record),
				host.ZoneId, host);

			Assert.AreEqual(KingdomInheritApplyStatus.Failed, result.Status, result.Detail);
			Assert.AreEqual(KingdomInheritApplyFault.ObjectNotEmpty, result.Fault);
			Assert.IsFalse(result.ShouldCommit);
			Assert.IsFalse(result.FreshEmptyVerified);
			Assert.AreEqual(0, host.Objects.Count);
			Assert.AreEqual("", host.Marker);
		}

		[Test]
		public void ExactRetryDoesNotDemandEmptinessAfterPlayerInteraction()
		{
			KingdomSealRecord record = Promoted("palisade");
			KingdomSealReceipt receipt = Receipt(record);
			FakeHost host = Host(record);
			KingdomInheritApplyResult first = KingdomInheritEngine.Apply(record, receipt,
				host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Applied, first.Status, first.Detail);
			host.Objects[0].Empty = false;
			int mutations = host.MutationCalls;

			KingdomInheritApplyResult retry = KingdomInheritEngine.Apply(record, receipt,
				host.ZoneId, host);

			Assert.AreEqual(KingdomInheritApplyStatus.AlreadyApplied, retry.Status, retry.Detail);
			Assert.IsTrue(retry.ShouldCommit);
			Assert.AreEqual(mutations, host.MutationCalls);
		}

		[Test]
		public void EmptyOldPlanStillPlacesOneExactFounderCairnAndChronicle()
		{
			KingdomSealRecord record = Promoted();
			FakeHost host = Host(record);
			KingdomInheritApplyResult result = KingdomInheritEngine.Apply(record, Receipt(record),
				host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Applied, result.Status, result.Detail);
			Assert.AreEqual(1, result.PlacedCount);
			Assert.AreEqual(KingdomInheritRules.FounderCairnKey, host.Objects[0].Spec.Key);
			StringAssert.Contains(record.Chronicle[0], host.Objects[0].CairnText);
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		public void AllFourSealedStatesPlaceTheExactRuleTransform(int State)
		{
			KingdomSealRecord record = PromotedForState(State,
				"palisade", "rampart", "heartbasin", "tent");
			KingdomInheritPlacement expected = Placement(record);
			FakeHost host = Host(record);

			KingdomInheritApplyResult result = KingdomInheritEngine.Apply(record, Receipt(record), host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Applied, result.Status, result.Detail);
			Assert.AreEqual(expected.Count, host.Objects.Count);
			for (int i = 0; i < expected.Count; i++)
			{
				KingdomInheritWork work = expected.WorkAt(i);
				FakeObject actual = host.Objects.Find(delegate(FakeObject o) { return o.Spec.Index == i; });
				Assert.IsNotNull(actual, i.ToString());
				Assert.AreEqual(work.Key, actual.Spec.Key, i.ToString());
				Assert.AreEqual(work.X, actual.Spec.X, work.Key);
				Assert.AreEqual(work.Y, actual.Spec.Y, work.Key);
				Assert.AreEqual(work.Condition, actual.Spec.Condition, work.Key);
				Assert.AreEqual(work.State, actual.Spec.State, work.Key);
			}
		}

		[TestCase(13)]
		[TestCase(14)]
		[TestCase(15)]
		[TestCase(16)]
		public void UnsafeFootprintRefusesBeforeAnyMutation(int FaultValue)
		{
			KingdomInheritApplyFault Fault = (KingdomInheritApplyFault)FaultValue;
			KingdomSealRecord record = Promoted("palisade");
			KingdomInheritPlacement placement = Placement(record);
			KingdomInheritWork work = placement.WorkAt(0);
			FakeHost host = Host(record);
			KingdomInheritCellFacts facts = host.Facts(work.X, work.Y);
			facts.Connection = Fault == KingdomInheritApplyFault.ConnectionCell;
			facts.Terrain = Fault == KingdomInheritApplyFault.Terrain;
			facts.Occupied = Fault == KingdomInheritApplyFault.Occupied;
			facts.Stairs = Fault == KingdomInheritApplyFault.Stairs;
			host.SetFacts(work.X, work.Y, facts);

			KingdomInheritApplyResult result = KingdomInheritEngine.Apply(record, Receipt(record), host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Refused, result.Status, result.Detail);
			Assert.AreEqual(Fault, result.Fault);
			Assert.IsTrue(result.ShouldRelease);
			Assert.AreEqual(0, host.MutationCalls);
			Assert.AreEqual(0, host.Objects.Count);
			Assert.AreEqual("", host.Marker);
		}

		[Test]
		public void BlockedEntryToHeartPathRefusesBeforeAnyMutation()
		{
			KingdomSealRecord record = Promoted("palisade");
			KingdomInheritPlacement placement = Placement(record);
			FakeHost host = Host(record);
			if (placement.EntryX == 0 || placement.EntryX == host.Width - 1)
			{
				int barrier = placement.EntryX == 0 ? 1 : host.Width - 2;
				for (int y = 0; y < host.Height; y++)
				{
					KingdomInheritCellFacts facts = host.Facts(barrier, y);
					facts.Walkable = false;
					host.SetFacts(barrier, y, facts);
				}
			}
			else
			{
				int barrier = placement.EntryY == 0 ? 1 : host.Height - 2;
				for (int x = 0; x < host.Width; x++)
				{
					KingdomInheritCellFacts facts = host.Facts(x, barrier);
					facts.Walkable = false;
					host.SetFacts(x, barrier, facts);
				}
			}

			KingdomInheritApplyResult result = KingdomInheritEngine.Apply(record, Receipt(record), host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Refused, result.Status, result.Detail);
			Assert.AreEqual(KingdomInheritApplyFault.EntryToHeartPath, result.Fault);
			Assert.AreEqual(0, host.MutationCalls);
		}

		[Test]
		public void WrongSelectedZoneAndWrongTargetGameFailBeforeMutation()
		{
			KingdomSealRecord record = Promoted("palisade");
			KingdomSealReceipt receipt = Receipt(record);
			FakeHost wrongSeat = new FakeHost("JoppaWorld.9.9.9.9.10", receipt.TargetGameId);
			KingdomInheritApplyResult seat = KingdomInheritEngine.Apply(record, receipt, record.GroundZoneId, wrongSeat);
			Assert.AreEqual(KingdomInheritApplyStatus.Failed, seat.Status);
			Assert.AreEqual(KingdomInheritApplyFault.TargetZoneMismatch, seat.Fault);
			Assert.IsFalse(seat.ShouldRelease, "a caller binding error must not spend the reservation");
			Assert.AreEqual(0, wrongSeat.MutationCalls);

			FakeHost wrongGame = new FakeHost(record.GroundZoneId, "other-game");
			KingdomInheritApplyResult game = KingdomInheritEngine.Apply(record, receipt, wrongGame.ZoneId, wrongGame);
			Assert.AreEqual(KingdomInheritApplyStatus.Failed, game.Status);
			Assert.AreEqual(KingdomInheritApplyFault.TargetGameMismatch, game.Fault);
			Assert.AreEqual(0, wrongGame.MutationCalls);
		}

		[Test]
		public void ExplicitSafeTargetMayDifferFromOldGroundAndKeysTheApplication()
		{
			KingdomSealRecord record = Promoted("palisade");
			string targetZone = "JoppaWorld.4.5.1.1.10";
			Assert.AreNotEqual(record.GroundZoneId, targetZone);
			FakeHost host = new FakeHost(targetZone, "target-game");
			KingdomInheritApplyResult result = KingdomInheritEngine.Apply(record, Receipt(record),
				targetZone, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Applied, result.Status, result.Detail);
			StringAssert.EndsWith("|" + targetZone, result.ApplicationMarker);
			StringAssert.DoesNotContain("|" + record.GroundZoneId, result.ApplicationMarker);
		}

		[Test]
		public void UnknownSavedSemanticTokenBecomesMemoryAndNeverARequestedBlueprint()
		{
			KingdomSealRecord record = Promoted("campfire");
			FakeHost host = Host(record);
			KingdomInheritApplyResult result = KingdomInheritEngine.Apply(record, Receipt(record), host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Applied, result.Status, result.Detail);
			Assert.AreEqual(2, host.CreatedBlueprints.Count, "memory marker plus founder cairn");
			Assert.AreEqual("r_KingdomCairn", host.CreatedBlueprints[0]);
			Assert.AreEqual("r_KingdomCairn", host.CreatedBlueprints[1]);
			CollectionAssert.DoesNotContain(host.CreatedBlueprints, "campfire");
		}

		[Test]
		public void MissingBlueprintAndNonemptyFactoryFailBeforeLivePlacement()
		{
			KingdomSealRecord record = Promoted("palisade");
			FakeHost missing = Host(record);
			missing.MissingBlueprint = "r_KingdomPalisade";
			KingdomInheritApplyResult missingResult = KingdomInheritEngine.Apply(record, Receipt(record), missing.ZoneId, missing);
			Assert.AreEqual(KingdomInheritApplyStatus.Failed, missingResult.Status);
			Assert.AreEqual(KingdomInheritApplyFault.BlueprintMissing, missingResult.Fault);
			Assert.AreEqual(0, missing.MutationCalls);

			FakeHost nonempty = Host(record);
			nonempty.CreateEmpty = false;
			KingdomInheritApplyResult nonemptyResult = KingdomInheritEngine.Apply(record, Receipt(record), nonempty.ZoneId, nonempty);
			Assert.AreEqual(KingdomInheritApplyStatus.Failed, nonemptyResult.Status);
			Assert.AreEqual(KingdomInheritApplyFault.ObjectNotEmpty, nonemptyResult.Fault);
			Assert.AreEqual(0, nonempty.Objects.Count, "off-zone object was discarded");
			Assert.AreEqual("", nonempty.Marker);
		}

		[TestCase(true, false, 20)]
		[TestCase(false, true, 21)]
		public void TransactionFailureRollsBackObjectsAndLeavesNoMarker(bool FailPlacement,
			bool FailMarker, int FaultValue)
		{
			KingdomInheritApplyFault Fault = (KingdomInheritApplyFault)FaultValue;
			KingdomSealRecord record = Promoted("palisade", "heartbasin");
			FakeHost host = Host(record);
			host.FailPlaceIndex = FailPlacement ? 1 : -1;
			host.FailMarkerWrite = FailMarker;
			KingdomInheritApplyResult result = KingdomInheritEngine.Apply(record, Receipt(record), host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Failed, result.Status, result.Detail);
			Assert.AreEqual(Fault, result.Fault);
			Assert.IsFalse(result.ShouldCommit);
			Assert.IsFalse(result.ShouldRelease);
			Assert.AreEqual(0, host.Objects.Count);
			Assert.AreEqual("", host.Marker);
		}

		[Test]
		public void FailedRollbackIsReportedAsPartialAndKeepsReservationUnresolved()
		{
			KingdomSealRecord record = Promoted("palisade", "heartbasin");
			FakeHost host = Host(record);
			host.FailPlaceIndex = 1;
			host.DiscardFails = true;
			KingdomInheritApplyResult result = KingdomInheritEngine.Apply(record, Receipt(record),
				host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Failed, result.Status);
			Assert.AreEqual(KingdomInheritApplyFault.PartialApplication, result.Fault);
			Assert.IsFalse(result.ShouldCommit);
			Assert.IsFalse(result.ShouldRelease);
			Assert.Greater(host.Objects.Count, 0, "failed cleanup remains visible for repair");
		}

		[Test]
		public void TornExactApplicationFailsWithoutRebuilding()
		{
			KingdomSealRecord record = Promoted("palisade", "heartbasin");
			KingdomSealReceipt receipt = Receipt(record);
			FakeHost host = Host(record);
			KingdomInheritApplyResult first = KingdomInheritEngine.Apply(record, receipt, host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Applied, first.Status, first.Detail);
			host.Objects.RemoveAt(0);
			int mutations = host.MutationCalls;

			KingdomInheritApplyResult torn = KingdomInheritEngine.Apply(record, receipt, host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Failed, torn.Status);
			Assert.AreEqual(KingdomInheritApplyFault.PartialApplication, torn.Fault);
			Assert.AreEqual(mutations, host.MutationCalls);
			Assert.IsFalse(torn.ShouldCommit);
			Assert.IsFalse(torn.ShouldRelease);
		}

		[Test]
		public void MissingExactCairnPayloadIsPartialAndNeverSilentlyAccepted()
		{
			KingdomSealRecord record = Promoted("palisade");
			KingdomSealReceipt receipt = Receipt(record);
			FakeHost host = Host(record);
			KingdomInheritApplyResult first = KingdomInheritEngine.Apply(record, receipt,
				host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Applied, first.Status, first.Detail);
			FakeObject cairn = host.Objects.Find(delegate(FakeObject o)
			{
				return o.Spec.Key == KingdomInheritRules.FounderCairnKey;
			});
			cairn.CairnText = "chronicle missing";
			int mutations = host.MutationCalls;
			KingdomInheritApplyResult retry = KingdomInheritEngine.Apply(record, receipt,
				host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Failed, retry.Status);
			Assert.AreEqual(KingdomInheritApplyFault.PartialApplication, retry.Fault);
			Assert.AreEqual(mutations, host.MutationCalls);
		}

		[Test]
		public void ConflictingMarkerRefusesAndOrphanRowsFailWithoutMutation()
		{
			KingdomSealRecord record = Promoted("palisade");
			KingdomSealReceipt receipt = Receipt(record);
			FakeHost conflict = Host(record);
			conflict.Marker = "taf-inherit-v1|other";
			KingdomInheritApplyResult conflictResult = KingdomInheritEngine.Apply(record, receipt, conflict.ZoneId, conflict);
			Assert.AreEqual(KingdomInheritApplyStatus.Refused, conflictResult.Status);
			Assert.AreEqual(KingdomInheritApplyFault.ApplicationConflict, conflictResult.Fault);
			Assert.AreEqual(0, conflict.MutationCalls);

			FakeHost orphan = Host(record);
			KingdomInheritApplyResult applied = KingdomInheritEngine.Apply(record, receipt, orphan.ZoneId, orphan);
			Assert.AreEqual(KingdomInheritApplyStatus.Applied, applied.Status, applied.Detail);
			orphan.Marker = "";
			int mutations = orphan.MutationCalls;
			KingdomInheritApplyResult orphanResult = KingdomInheritEngine.Apply(record, receipt, orphan.ZoneId, orphan);
			Assert.AreEqual(KingdomInheritApplyStatus.Failed, orphanResult.Status);
			Assert.AreEqual(KingdomInheritApplyFault.PartialApplication, orphanResult.Fault);
			Assert.AreEqual(mutations, orphan.MutationCalls);
		}

		[Test]
		public void UnsafeRecordAndNonreservedOrMismatchedReceiptNeverMutate()
		{
			KingdomSealRecord record = Promoted("palisade");
			FakeHost host = Host(record);
			record.FounderName = "{{R|unsafe}}";
			KingdomInheritApplyResult unsafeResult = KingdomInheritEngine.Apply(record, Receipt(record), host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyStatus.Failed, unsafeResult.Status);
			Assert.AreEqual(KingdomInheritApplyFault.LegacyNotPromoted, unsafeResult.Fault);
			Assert.AreEqual(0, host.MutationCalls);

			record = Promoted("palisade");
			KingdomSealReceipt receipt = Receipt(record);
			receipt.State = KingdomSealReceiptState.Committed;
			KingdomInheritApplyResult committed = KingdomInheritEngine.Apply(record, receipt,
				record.GroundZoneId, Host(record));
			Assert.AreEqual(KingdomInheritApplyFault.ReceiptNotReserved, committed.Fault);

			receipt = Receipt(record);
			receipt.LegacyId = "legacy-other";
			KingdomInheritApplyResult mismatch = KingdomInheritEngine.Apply(record, receipt,
				record.GroundZoneId, Host(record));
			Assert.AreEqual(KingdomInheritApplyFault.ReceiptMismatch, mismatch.Fault);
		}

		[Test]
		public void TamperedInterregnumRollOrStateNeverReconstructs()
		{
			KingdomSealRecord record = Promoted("palisade");
			FakeHost host = Host(record);
			record.InterregnumRoll = (record.InterregnumRoll + 1) % 100;
			KingdomInheritApplyResult roll = KingdomInheritEngine.Apply(record, Receipt(record),
				host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyFault.LegacyNotPromoted, roll.Fault);
			Assert.AreEqual(0, host.MutationCalls);

			record = Promoted("palisade");
			host = Host(record);
			record.InheritedState = (record.InheritedState + 1) % 4;
			KingdomInheritApplyResult state = KingdomInheritEngine.Apply(record, Receipt(record),
				host.ZoneId, host);
			Assert.AreEqual(KingdomInheritApplyFault.LegacyNotPromoted, state.Fault);
			Assert.AreEqual(0, host.MutationCalls);
		}

		[Test]
		public void CairnPayloadSanitizesMarkupControlsAndDescriptionTilde()
		{
			KingdomSealRecord record = Promoted("palisade");
			record.FounderName = "{{R|Aster}} &Y~the elder";
			record.Chronicle.Clear();
			record.Chronicle.Add("{{G|Raised}} &ya wall~at dawn.\nThen rested.");
			string text = KingdomInheritEngine.ComposeCairnText(record);
			StringAssert.DoesNotContain("{{", text);
			StringAssert.DoesNotContain("}}", text);
			StringAssert.DoesNotContain("&Y", text);
			StringAssert.DoesNotContain("&y", text);
			StringAssert.DoesNotContain("~", text);
			StringAssert.Contains("Chronicle of the old kingdom", text);
		}
	}
}
#endif
