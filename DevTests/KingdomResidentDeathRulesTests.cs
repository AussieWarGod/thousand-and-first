#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	// Pure receipt and accounting laws only; no engine death, body lookup, or native publication proof.
	public sealed class KingdomResidentDeathRulesTests
	{
		[TestCase(false, false, true)] [TestCase(true, true, true)]
		[TestCase(false, true, false)] [TestCase(true, false, false)]
		public void OneSidedRoleClaimsCannotBeTreatedAsNoRole(bool residentMatches, bool bodyMatches, bool expected)
		{
			var r = DeathFixture.Receipt();
			ClassicAssert.AreEqual(expected, KingdomResidentDeathRules.RoleClaimAgrees(r,
				residentMatches ? r.Before.ResidentId : r.Before.ResidentId + 1, bodyMatches ? r.Body : r.Body + "-other"));
		}
		[Test]
		public void AppendCopiesTheWitnessAndAnExactRetryRetainsAdvancedProgress()
		{
			var witness = DeathFixture.Receipt(); var empty = DeathFixture.Journal();
			ClassicAssert.IsTrue(KingdomResidentDeathRules.TryAppend(empty, witness, out var journal, out int index));
			ClassicAssert.AreEqual(0, index); ClassicAssert.AreEqual(0, empty.Entries.Count);
			string wire = DeathFixture.Wire(journal); witness.Culture = "changed";
			ClassicAssert.AreEqual(wire, DeathFixture.Wire(journal));
			var progressed = DeathFixture.Accounted(journal.Entries[0]);
			progressed.Telling = KingdomResidentDeathTelling.Attempting;
			journal = journal.With(0, progressed); wire = DeathFixture.Wire(journal);
			ClassicAssert.IsTrue(KingdomResidentDeathRules.TryAppend(journal, DeathFixture.Receipt(), out var repeated, out index));
			ClassicAssert.AreSame(journal, repeated); ClassicAssert.AreEqual(0, index);
			ClassicAssert.AreEqual(wire, DeathFixture.Wire(repeated));
			ClassicAssert.AreEqual(KingdomResidentDeathTelling.Attempting, repeated.Entries[0].Telling);
		}

		[TestCase("realm")] [TestCase("settlement")] [TestCase("body")] [TestCase("resident")]
		[TestCase("cause")] [TestCase("tick")] [TestCase("zone")] [TestCase("minted")]
		[TestCase("name")] [TestCase("origin")] [TestCase("arrived")] [TestCase("home")]
		[TestCase("roof")] [TestCase("place")] [TestCase("memory")]
		[TestCase("culture")] [TestCase("species")] [TestCase("identity")]
		[TestCase("creed")] [TestCase("past")] [TestCase("figure")] [TestCase("role-fault")]
		[TestCase("step")] [TestCase("remembrance")] [TestCase("remembrance-unavailable")]
		public void AConflictingFrozenWitnessNeverAliasesAnExistingDeath(string field)
		{
			var journal = DeathFixture.Journal(DeathFixture.Receipt()); var changed = DeathFixture.Receipt();
			switch (field)
			{
			case "realm": changed.Realm = KingdomIdentityRules.RealmPrefix + new string('c', 64); break;
			case "settlement": changed.Settlement = KingdomIdentityRules.SettlementPrefix + new string('d', 64); break;
			case "body": changed.Body += "-other"; break;
			case "resident": changed.Before = DeathFixture.Row(2); break;
			case "cause": changed.Cause = KingdomStandingCause.Founder; break;
			case "tick": changed.Tick++; break;
			case "zone": changed.Zone += ".other"; break;
			case "minted": changed.MintedTick++; break;
			case "name": changed.Before = DeathFixture.Row(name: "Other resident"); break;
			case "origin": changed.Before = DeathFixture.Row(origin: "Other origin"); break;
			case "arrived": changed.Before = DeathFixture.Row(arrived: "Other date"); break;
			case "home": changed.Before = DeathFixture.Row(home: 43); break;
			case "roof": changed.Before = DeathFixture.Row(roof: new KingdomBrinkWindow(true, 70, 80)); break;
			case "place": changed.SettlementName += " elsewhere"; break;
			case "memory": changed.Memory = false; changed.Telling = KingdomResidentDeathTelling.Disabled; break;
			case "culture": changed.Culture += "x"; break;
			case "species": changed.Species += "x"; break;
			case "identity": changed.IdentityKeys = "body:robot"; break;
			case "creed": changed.Creed += "x"; break;
			case "past": changed.PastCreeds += "x"; break;
			case "figure": changed.FigureId = "different-figure"; break;
			case "role-fault": changed.RoleFault = "Unreadable role authority"; break;
			case "step": changed.StepWire = KingdomSubsidenceStepCodec.LegacyWire; break;
			case "remembrance": changed.Remembrance = true; break;
			case "remembrance-unavailable": changed.RemembranceUnavailable = true; break;
			}
			ClassicAssert.IsTrue(KingdomResidentDeathRules.Valid(changed), "negative must remain a valid independent witness");
			string before = DeathFixture.Wire(journal);
			ClassicAssert.IsFalse(KingdomResidentDeathRules.TryAppend(journal, changed, out var rejected, out int index));
			ClassicAssert.IsNull(rejected); ClassicAssert.AreEqual(-1, index); ClassicAssert.AreEqual(before, DeathFixture.Wire(journal));
		}

		[Test]
		public void FullJournalRefusesNewDeathWithoutEvictionButStillRecognizesExactRetry()
		{
			var rows = new List<KingdomResidentDeathReceipt>();
			for (int i = 1; i <= 4096; i++) rows.Add(DeathFixture.Receipt(i));
			var full = new KingdomResidentDeathJournal(DeathFixture.Realm, DeathFixture.Settlement, rows);
			ClassicAssert.AreEqual(4096, KingdomResidentDeathRules.MaxEntries); ClassicAssert.IsTrue(KingdomResidentDeathRules.Valid(full));
			string before = DeathFixture.Wire(full);
			ClassicAssert.IsFalse(KingdomResidentDeathRules.TryAppend(full, DeathFixture.Receipt(4097), out var rejected, out _));
			ClassicAssert.IsNull(rejected); ClassicAssert.AreEqual(before, DeathFixture.Wire(full));
			ClassicAssert.IsTrue(KingdomResidentDeathRules.TryAppend(full, DeathFixture.Receipt(4096), out var repeated, out int index));
			ClassicAssert.AreSame(full, repeated); ClassicAssert.AreEqual(4095, index);
			rows.Add(DeathFixture.Receipt(4097));
			ClassicAssert.IsFalse(KingdomResidentDeathRules.Valid(new KingdomResidentDeathJournal(DeathFixture.Realm, DeathFixture.Settlement, rows)));
		}

		[Test]
		public void FrozenOpaqueRoleAndExpeditionPayloadsMustAlsoMatchOnDuplicateAdmission()
		{
			var r = DeathFixture.Receipt(); r.OfficeGeneration = 2; r.CookGeneration = 3;
			r.OfficeBefore = "opaque-office-before"; r.CookBefore = "opaque-cook-before";
			r.ExpeditionBefore = "opaque-job-before"; r.ExpeditionPrepared = "opaque-job-prepared";
			var journal = DeathFixture.Journal(r); string before = DeathFixture.Wire(journal);
			foreach (Action<KingdomResidentDeathReceipt> mutate in new Action<KingdomResidentDeathReceipt>[] {
				v => v.OfficeGeneration++, v => v.CookGeneration++, v => v.OfficeBefore += "x",
				v => v.CookBefore += "x", v => v.ExpeditionBefore += "x", v => v.ExpeditionPrepared += "x" })
			{
				var changed = r.Copy(); mutate(changed); ClassicAssert.IsTrue(KingdomResidentDeathRules.Valid(changed));
				ClassicAssert.IsFalse(KingdomResidentDeathRules.TryAppend(journal, changed, out var rejected, out _));
				ClassicAssert.IsNull(rejected); ClassicAssert.AreEqual(before, DeathFixture.Wire(journal));
			}
			ClassicAssert.IsTrue(KingdomResidentDeathRules.TryAppend(journal, r.Copy(), out var repeated, out _));
			ClassicAssert.AreSame(journal, repeated);
		}

		[TestCase(false)] [TestCase(true)]
		public void FrozenMemoryControlsHistoryNotIdentityRemovalAndPreparedInputsRemainImmutable(bool memory)
		{
			var r = DeathFixture.Receipt(memory: memory); r.Phase = KingdomResidentDeathPhase.RolesSettled;
			string original = DeathFixture.Wire(DeathFixture.Journal(r)); string[] before = DeathFixture.Accounts();
			string[] saved = (string[])before.Clone();
			ClassicAssert.IsTrue(KingdomResidentDeathRules.TryPrepareAccounts(r, before, out var prepared));
			ClassicAssert.AreEqual(original, DeathFixture.Wire(DeathFixture.Journal(r)));
			ClassicAssert.AreNotSame(before, prepared.BeforeAccounts); before[0] = "changed";
			CollectionAssert.AreEqual(saved, prepared.BeforeAccounts);
			for (int i = 0; i < 5; i++)
			{
				var map = KingdomResidentDeathCodec.ReadMap(prepared.AfterAccounts[i]);
				ClassicAssert.AreEqual(1, map[DeathFixture.CountedKeys[i]]); ClassicAssert.AreEqual(7, map["unrelated"]);
			}
			string[] history = KingdomResidentDeathCodec.ReadFields(prepared.AfterAccounts[5]);
			ClassicAssert.AreEqual(memory ? "2" : "1", history[0]); ClassicAssert.AreEqual(memory ? 9 : 5, history.Length);
			for (int i = 1; i <= 4; i++) ClassicAssert.AreEqual(KingdomResidentDeathCodec.ReadFields(saved[5])[i], history[i]);
			if (memory) { ClassicAssert.AreEqual(r.Before.Name, history[5]); ClassicAssert.AreEqual(r.Before.Origin, history[6]); ClassicAssert.AreEqual(r.Before.Arrived, history[7]); }
			ClassicAssert.AreEqual(memory, DeathFixture.RoundTrip(DeathFixture.Journal(prepared)).Entries[0].Memory);
		}

		[Test]
		public void PrefixAcceptsExactlyEverySequentialCutAndRejectsAllNonprefixCombinations()
		{
			string[] before = { "b0", "b1", "b2", "b3", "b4", "b5" }, after = { "a0", "a1", "a2", "a3", "a4", "a5" };
			for (int mask = 0; mask < 64; mask++)
			{
				var observed = new string[6]; for (int i = 0; i < 6; i++) observed[i] = (mask & (1 << i)) == 0 ? before[i] : after[i];
				int expected = -1; for (int cut = 0; cut <= 6; cut++) if (mask == (1 << cut) - 1) expected = cut;
				ClassicAssert.AreEqual(expected, KingdomResidentDeathRules.Prefix(before, after, observed), "mask=" + mask);
			}
			ClassicAssert.AreEqual(-1, KingdomResidentDeathRules.Prefix(before, after, null));
			ClassicAssert.AreEqual(-1, KingdomResidentDeathRules.Prefix(before, new string[5], before));
			ClassicAssert.AreEqual(-1, KingdomResidentDeathRules.Prefix(before, after, new[] { "foreign", "b1", "b2", "b3", "b4", "b5" }));
			ClassicAssert.AreEqual(1, KingdomResidentDeathRules.Prefix(new[] { "same", "before" }, new[] { "same", "after" }, new[] { "same", "before" }));
		}

		[Test]
		public void TwoPreparedAccountingOwnersRefuseButSettledHistoryDoesNotBlockTheNextReceipt()
		{
			var one = DeathFixture.Accounted(DeathFixture.Receipt()); var two = DeathFixture.Accounted(DeathFixture.Receipt(2));
			ClassicAssert.IsFalse(KingdomResidentDeathRules.Valid(DeathFixture.Journal(one, two)));
			one.Phase = KingdomResidentDeathPhase.Settled; one.Telling = KingdomResidentDeathTelling.Uncertain;
			one.BeforeAccounts = one.AfterAccounts = new string[0];
			ClassicAssert.IsTrue(KingdomResidentDeathRules.Valid(DeathFixture.Journal(one, two)));
			ClassicAssert.IsTrue(KingdomResidentDeathRules.Valid(DeathFixture.RoundTrip(DeathFixture.Journal(one, two))));
		}

		[TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)]
		public void CapturedRemembranceDecisionSurvivesRecoveryWithoutInventingLaterEligibility(bool eligible, bool unavailable)
		{
			var r = DeathFixture.Receipt(); r.Remembrance = eligible; r.RemembranceUnavailable = unavailable;
			var copy = DeathFixture.RoundTrip(DeathFixture.Journal(DeathFixture.Accounted(r))).Entries[0];
			ClassicAssert.AreEqual(eligible, copy.Remembrance); ClassicAssert.AreEqual(unavailable, copy.RemembranceUnavailable);
			ClassicAssert.AreEqual(r.Memory, copy.Memory);
		}

		[TestCase(false, false)] [TestCase(true, false)] [TestCase(true, true)]
		public void DeathPreservesEveryRoofAndRowFieldWithoutClaimingSelectedRoofCompletion(bool selected, bool proved)
		{
			var book = RungFixture.Settling(false); var roof = RungFixture.Roof(20, true); var work = RungFixture.Work(roofs: new[] { roof });
			var plan = new KingdomSubsidenceRungPlan(book.Active.Id, book.RealmId, book.SettlementId, RungFixture.Zone,
				GrowthStage.City, GrowthStage.Town, book.Active.DueTick, RungFixture.Prepared, 5, new[] { work });
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, plan, out book));
			if (proved)
			{
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungWear(book, 0, out book));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, true, true, work.AfterWear, out book));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungRoof(book, 0, 0, out book));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungRoof(book, 0, 0, true, true, roof.BeforeReached, roof.BeforeWarned, out book));
			}
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string step));
			var r = DeathFixture.Receipt(selected ? 20 : 21); r.Tick = RungFixture.Prepared + 1; r.StepWire = step;
			if (selected)
			{
				r.Body = roof.BodyObjectId; r.RoofWork = work.ObjectId; r.RoofIndex = 0; r.RoofBlocked = !proved;
				r.Before = DeathFixture.Row(20, home: work.WorkId,
					roof: new KingdomBrinkWindow(roof.BeforeStanding, roof.BeforeReached, roof.BeforeWarned));
			}
			ClassicAssert.IsTrue(KingdomResidentDeathRules.Valid(r)); var dead = r.Before.WithStanding(KingdomResidentStanding.Dead, r.Cause);
			ClassicAssert.AreEqual(0, KingdomResidentDeathRules.RowCut(r, r.Before)); ClassicAssert.AreEqual(1, KingdomResidentDeathRules.RowCut(r, dead));
			ClassicAssert.AreEqual(-1, KingdomResidentDeathRules.RowCut(r, dead.WithStanding(KingdomResidentStanding.Dead, KingdomStandingCause.Founder)));
			ClassicAssert.AreEqual(r.Before.RoofBrink, dead.RoofBrink); ClassicAssert.AreEqual(r.Before.CreedBrink, dead.CreedBrink);
			ClassicAssert.AreEqual(r.Before.HomeWorkId, dead.HomeWorkId); ClassicAssert.AreEqual(r.Before.JobWorkId, dead.JobWorkId);
			var restored = DeathFixture.RoundTrip(DeathFixture.Journal(r)).Entries[0];
			ClassicAssert.AreEqual(step, restored.StepWire); ClassicAssert.AreEqual(selected && !proved, restored.RoofBlocked);
			if (selected) { var forged = r.Copy(); forged.RoofBlocked = !r.RoofBlocked; ClassicAssert.IsFalse(KingdomResidentDeathRules.Valid(forged)); }
			ClassicAssert.AreEqual(step, r.StepWire); ClassicAssert.IsTrue(KingdomSubsidenceStepRules.Valid(book));
		}
	}

	internal static class DeathFixture
	{
		internal static readonly string Realm = RungFixture.Realm, Settlement = RungFixture.Settlement;
		internal static readonly string[] CountedKeys = { "testfolk", "testspecies", "body:wet-bodied", "current creed", "past creed" };
		internal static KingdomResidentRow Row(int id = 1, string name = "Resident \ud83c\udfe0", string origin = "Exact origin",
			string arrived = "3 Uulu Ut, 1001 AR", int home = 42, KingdomBrinkWindow? roof = null)
			=> new KingdomResidentRow(id, name, 3, 4, 10, home, 43, 2, KingdomDayShape.Craft,
				KingdomResidentStanding.Resident, KingdomStandingCause.None, RungFixture.Zone,
				roof ?? new KingdomBrinkWindow(true, 50, 60), new KingdomBrinkWindow(true, 70, 80), "other creed", 1, "past creed", origin, arrived);
		internal static KingdomResidentDeathReceipt Receipt(int id = 1, bool memory = true)
			=> new KingdomResidentDeathReceipt { Realm = Realm, Settlement = Settlement, SettlementName = "Exact settlement",
				Body = "death-body:" + id.ToString(CultureInfo.InvariantCulture), Zone = RungFixture.Zone, Tick = 100, MintedTick = 5,
				Before = Row(id), Cause = KingdomStandingCause.Raid, Memory = memory, StepWire = KingdomSubsidenceStepCodec.FreshWire,
				Culture = CountedKeys[0], Species = CountedKeys[1], IdentityKeys = CountedKeys[2], Creed = CountedKeys[3], PastCreeds = CountedKeys[4],
				Telling = memory ? KingdomResidentDeathTelling.Pending : KingdomResidentDeathTelling.Disabled };
		internal static KingdomResidentDeathJournal Journal(params KingdomResidentDeathReceipt[] rows)
			=> new KingdomResidentDeathJournal(Realm, Settlement, rows);
		internal static string[] Accounts()
		{
			var result = new string[6];
			for (int i = 0; i < 5; i++) result[i] = KingdomResidentDeathCodec.Map(new Dictionary<string, int>(StringComparer.Ordinal) { { CountedKeys[i], 2 }, { "unrelated", 7 } });
			result[5] = KingdomResidentDeathCodec.Fields("1", "Older resident", "Older origin", "Older date", "Older cause"); return result;
		}
		internal static KingdomResidentDeathReceipt Accounted(KingdomResidentDeathReceipt r)
		{
			r = r.Copy(); r.Phase = KingdomResidentDeathPhase.RolesSettled;
			ClassicAssert.IsTrue(KingdomResidentDeathRules.TryPrepareAccounts(r, Accounts(), out var next));
			next.Phase = KingdomResidentDeathPhase.Accounted; ClassicAssert.IsTrue(KingdomResidentDeathRules.Valid(next)); return next;
		}
		internal static string Wire(KingdomResidentDeathJournal journal)
		{ ClassicAssert.IsTrue(KingdomResidentDeathCodec.TryEncode(journal, out string wire)); return wire; }
		internal static KingdomResidentDeathJournal RoundTrip(KingdomResidentDeathJournal journal)
		{
			string wire = Wire(journal); ClassicAssert.IsTrue(KingdomResidentDeathCodec.TryDecode(wire, out var restored));
			ClassicAssert.AreEqual(wire, Wire(restored)); return restored;
		}
	}
}
#endif
