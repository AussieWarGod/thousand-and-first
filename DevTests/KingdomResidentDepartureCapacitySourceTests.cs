#if TAF_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Boundary wiring only; executable archive and story-frontier coverage is separate.</summary>
	[TestFixture]
	public sealed class KingdomResidentDepartureCapacitySourceTests
	{
		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static void Ordered(string source, params string[] tokens)
		{
			int start = 0;
			foreach (string token in tokens)
			{
				int found = source.IndexOf(token, start, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(found, start, token); start = found + token.Length;
			}
		}

		[Test]
		public void NewAdmissionReservesWarningRoomBeforeJournalBodyAndRoleMutations()
		{
			string begin = Read("Growth/KingdomResidentDepartureRuntime.Begin.cs");
			Ordered(begin, "out FormerRow, out Failure)) return false;",
				"Chronicled && !KingdomResidentDepartureCapacityArchive.CanAdmit(",
				"KingdomSubsidenceStepRuntime.TryAssociate(", "System.ResidentDeparture = operation;",
				"Body.AddPart(marker)", "KingdomResidentDeparturePreparation.TryPrepare(", "KingdomCitizenship.TryRemove(");
			StringAssert.Contains("read retained departure warnings", begin);
		}

		[Test]
		public void OnlyTellingChangesWhileAccountingAndExactBodyRetirementKeepTheirGates()
		{
			string effects = Read("Growth/KingdomResidentDepartureRuntime.Effects.cs");
			Ordered(effects, "System.Ledger.Departures == Operation.DeparturesBefore", "System.Ledger.Departures++",
				"System.Ledger.Departures != Operation.DeparturesBefore + 1", "TrySettleChronicle(System, Operation)",
				"System.Ledger.Note(Operation.LedgerLine)");
			string recovery = Read("Growth/KingdomResidentDepartureRuntime.Recovery.cs");
			Ordered(recovery, "KingdomSubsidenceStepRuntime.TryCredit(", "KingdomResidentDeparturePhase.CarriersRemoved",
				"TryCloseRoles(", "KingdomResidentDeparturePhase.RolesClosed", "TryPublishEffects(",
				"KingdomResidentDeparturePhase.EffectsPublished", "TryDestroyBody(");
			Ordered(effects, "marker?.Matches(Operation, leaver) != true", "KingdomSubsidenceStepRuntime.CanRetire(",
				"leaver.Obliterate()", "GameObject.Validate(leaver)", "KingdomSubsidenceStepRuntime.TryRetireJournal(");
			StringAssert.DoesNotContain("Capacity", Read("Growth/KingdomResidentDepartureOperation.cs"));
			StringAssert.DoesNotContain("Capacity", Read("Growth/KingdomResidentDepartureRules.cs"));
		}

		[Test]
		public void CapacityPublicationPinsOwnerLedgerArchiveAndLegacyChronicleFingerprint()
		{
			string capacity = Read("Growth/KingdomResidentDepartureRuntime.Capacity.cs");
			foreach (string proof in new[] { "ReferenceEquals(The.Game, game)", "ReferenceEquals(system.ResidentDeparture, operation)",
				"operation.Revision == frozen.Revision", "operation.ChronicleLine == frozen.ChronicleLine",
				"realm == frozen.RealmId && settlement == frozen.SettlementId", "ReferenceEquals(system.Ledger, ledger)",
				"ledger.Departures == operation.DeparturesBefore + 1",
				"string.Equals(system.ResidentDepartureCapacityWarnings, archive, StringComparison.Ordinal)" })
				StringAssert.Contains(proof, capacity);
			Ordered(capacity, "KingdomResidentDepartureStoryRules.TrySettle(", "KingdomChronicle.TryObserveCapacityRefusal(",
				"KingdomChronicle.ReproveCapacityRefusal(capacity, exact)", "if (!exact()) return false;",
				"system.ResidentDepartureCapacityWarnings = next;", "archive = next;", "return exact();",
				"KingdomChronicle.RecordOnce(system, operation.OperationId + \":chronicle\", operation.ChronicleLine)");
			StringAssert.DoesNotContain("RecordOnceAt(", capacity);
			string chronicle = Read("Chronicle/KingdomChronicle.Capacity.cs");
			Ordered(chronicle, "internal static bool TryObserveCapacityRefusal(", "PublicationAllowed(ownerExact)",
				"KingdomChronicleReceiptRules.TryFingerprint(eventId, text, false, null, out string fingerprint)",
				"ReadCapacityTables(game,", "KingdomChronicleCapacityRules.TryObserve(", "ReproveCapacityRefusal(value, ownerExact)");
		}

		[Test]
		public void ArchiveUsesAdditiveNamedSystemFieldOutsideSeatTransferAndReset()
		{
			StringAssert.Contains("public string ResidentDepartureCapacityWarnings = KingdomResidentDepartureCapacityArchive.None;",
				Read("Core/KingdomSystem.z08a.ResidentDeparture.cs"));
			string serial = Read("Core/KingdomSystem.z19a.Serialization.cs");
			StringAssert.Contains("Writer.WriteNamedFields(this, typeof(KingdomSystem))", serial);
			StringAssert.Contains("Reader.ReadNamedFields(this, typeof(KingdomSystem))", serial);
			foreach (string path in new[] { "Core/KingdomSystem.z11.Return.Begin.cs", "Core/KingdomSystem.z23.Normalization.cs",
				"Core/KingdomSettlement.Transfer.cs", "Debug/KingdomWishes.ResetAndCitizenCommands.cs" })
				StringAssert.DoesNotContain("ResidentDepartureCapacityWarnings", Read(path));
			StringAssert.Contains("typeof(KingdomSettlement).GetFields", Read("Core/KingdomSettlement.Reflection.cs"));
			string archive = Read("Growth/KingdomResidentDepartureCapacityArchive.cs");
			StringAssert.Contains("row.Realm != realm || row.Settlement != settlement", archive);
			StringAssert.Contains("kept.Add(row)", archive);
			StringAssert.DoesNotContain("RemoveAt(", archive); StringAssert.DoesNotContain(".Clear(", archive);
			StringAssert.DoesNotContain("Lost", archive); StringAssert.DoesNotContain("Delivered", archive);
		}

		[Test]
		public void HomecomingPinsExactWarningArchiveAndAcknowledgesOnlyAfterSuccessfulDisplay()
		{
			string home = Read("Growth/KingdomSubsidenceStepRuntime.Homecoming.cs");
			Ordered(home, "DepartureWarnings = system.ResidentDepartureCapacityWarnings", "TryPrepareRead(frame.DepartureWarnings,",
				"owner.Realm, owner.Settlement", "departureWarnings.Length == 0", "digest += warnings + noticeWarning + departureWarnings",
				"show(digest)", "if (!HomecomingExact(frame)", "SaveOption(owner, next)",
				"if (!HomecomingExact(frame)) return false", "system.ResidentDepartureCapacityWarnings = acknowledgedDepartures", "ledger.Reset()");
			StringAssert.Contains("string.Equals(frame.Owner.System.ResidentDepartureCapacityWarnings, frame.DepartureWarnings, StringComparison.Ordinal)", home);
		}

		[Test]
		public void EveryMutableOperationFieldAndNestedReceiptFieldEntersFrozenPublicationProof()
		{
			string capacity = Read("Growth/KingdomResidentDepartureRuntime.Capacity.cs");
			Ordered(capacity, "KingdomResidentDepartureOperation frozen = operation.Copy();", "Func<bool> exact =",
				"SameCook(operation.PriorCook, frozen.PriorCook)", "SameOffice(operation.PriorOffice, frozen.PriorOffice)",
				"SamePolity(operation.PriorPolity, frozen.PriorPolity)", "KingdomResidentDepartureStoryRules.TrySettle(");
			string authority = Read("Growth/KingdomResidentDepartureRuntime.Authority.cs");
			foreach (FieldInfo field in typeof(KingdomResidentDepartureOperation).GetFields(BindingFlags.Instance | BindingFlags.Public))
			{
				if (field.FieldType == typeof(string) || field.FieldType.IsValueType)
				{
					StringAssert.Contains("operation." + field.Name + " == frozen." + field.Name, capacity, field.Name);
					continue;
				}
				string method = field.Name == "PriorCook" ? "SameCook" : field.Name == "PriorOffice" ? "SameOffice" : "SamePolity";
				int at = authority.IndexOf("private static bool " + method + "(", StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(at, 0, field.Name);
				string equality = authority.Substring(at, authority.IndexOf("\n\t\t}", at, StringComparison.Ordinal) - at);
				foreach (FieldInfo nested in field.FieldType.GetFields(BindingFlags.Instance | BindingFlags.Public))
					ClassicAssert.IsTrue(equality.Contains("A." + nested.Name + " == B." + nested.Name)
						|| capacity.Contains("operation." + field.Name + "." + nested.Name + " == frozen." + field.Name + "." + nested.Name),
						field.Name + "." + nested.Name + " is omitted from the exact frozen proof");
			}
			StringAssert.Contains("return exact() && record() && exact();", Read("Growth/KingdomResidentDepartureStoryRules.cs"));
		}
	}
}
#endif
