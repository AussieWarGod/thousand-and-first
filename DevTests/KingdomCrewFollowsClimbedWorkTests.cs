#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// What happens to a work's crew when an improvement replaces the object the crew was posted
	/// to -- the question raised against the heart's rung climb (issue #162, separable finding).
	///
	/// <para>THE ANSWER, EXECUTED BELOW. Crew is an exact join between a resident row's posted job
	/// id and the work row's own id, and the work row's id is the standing object's identity
	/// folded. A replacement therefore changes the work's id, and a settler still posted to the
	/// retired identity does not join -- for exactly as long as that posting stands. The posting
	/// is re-stamped on EVERY settler on EVERY staffing pass from the works standing at that pass
	/// (KingdomGrowth.z15.WorkAssignment), and staffing runs before the check-in that rebuilds the
	/// rows, so the crew follows the successor by the ordinary route on the next pass.</para>
	///
	/// <para>WHY NOT RE-KEY THE ROW AT CHECK-IN. The row is derived from the person, and the
	/// person's stamped post is the authority; re-keying JobWorkId while rebuilding the work rows
	/// would invent a posting the staffing pass never made, and the row would then disagree with
	/// the settler it was read from. The exposure this leaves is ONE pass -- a heart replaced
	/// after that pass's staffing reads crew 0 until the next one -- and that is a one-pass
	/// manning dip, not a silent permanent loss.</para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomCrewFollowsClimbedWorkTests
	{
		private const string Here = "taf:zone:here";

		private static KingdomResidentRow Posted(int Id, int WorkId)
		{
			return new KingdomResidentRow(Id, "Ptoh-" + Id, 2, 3, 400L, 0, WorkId, 0,
				KingdomDayShape.Craft, KingdomResidentStanding.Resident,
				KingdomStandingCause.None, Here, KingdomBrinkWindow.None,
				KingdomBrinkWindow.None, null, 0);
		}

		private static KingdomCityState Book(params KingdomResidentRow[] Rows)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion,
				KingdomCityRules.RulesVersion, "taf:city:kavvat", 900L, default(KingdomStocks),
				null, null, Rows, null, out state, out fault), fault.ToString());
			return state;
		}

		/// <summary>
		/// VALUE. The identity fold moves when the object is replaced, the old posting stops
		/// joining, and the re-posting the next staffing pass makes brings the same crew back to
		/// the successor's own row. Three counts, over production's own join.
		/// </summary>
		[Test]
		public void CrewFollowsTheSuccessorOnceTheStaffingPassRepostsIt()
		{
			int retired = KingdomCityRules.StableId("r_TAF_FoundingHeart:final:9f2c");
			int successor = KingdomCityRules.StableId("r_TAF_FoundingHeart:final:9f2d");
			ClassicAssert.AreNotEqual(retired, successor,
				"a replaced work that folded to the same id would hide the question");

			// The pass in which the climb happened: two settlers still posted to the retired
			// identity, and the work row now carries the successor's.
			KingdomCityState during = Book(Posted(1, retired), Posted(2, retired));
			ClassicAssert.AreEqual(2, KingdomResidentRules.CrewAssigned(during, Here, retired));
			ClassicAssert.AreEqual(0, KingdomResidentRules.CrewAssigned(during, Here, successor),
				"the crew cannot join a work whose identity they were never posted to");

			// The next staffing pass re-posts every settler from the works standing then. The
			// rows are read from those postings, and the crew is on the successor.
			KingdomResidentRow first;
			KingdomResidentRow second;
			ClassicAssert.IsTrue(during.TryResident(0, out first));
			ClassicAssert.IsTrue(during.TryResident(1, out second));
			KingdomCityState after = Book(
				first.WithReading(first.Name, first.OriginCode, first.CreedCode, 0, successor, 0,
					KingdomDayShape.Craft),
				second.WithReading(second.Name, second.OriginCode, second.CreedCode, 0, successor,
					0, KingdomDayShape.Craft));
			ClassicAssert.AreEqual(2, KingdomResidentRules.CrewAssigned(after, Here, successor));
			ClassicAssert.AreEqual(0, KingdomResidentRules.CrewAssigned(after, Here, retired),
				"nothing is left crewing an identity that no longer stands");
		}

		/// <summary>
		/// The route that makes the paragraph above true: every available settler is re-posted on
		/// every staffing pass, from the works standing at that pass, and the check-in reads the
		/// roster before it rebuilds the work rows -- so a row's crew is never older than the
		/// postings it was counted from.
		/// </summary>
		[Test]
		public void StaffingRepostsEverySettlerAndCheckInReadsTheRosterFirst()
		{
			string assignment = TestMain.ReadRepositoryText(
				"Growth/KingdomGrowth.z15.WorkAssignment.cs");
			StringAssert.Contains("for (int i = 0; i < available.Count; i++)", assignment);
			StringAssert.Contains(
				"Simulation.City.KingdomStations.Post(available[i], postIds[i], postKinds[i]);",
				assignment);

			string checkIn = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomCity.z01.CheckIn.cs");
			int roster = checkIn.IndexOf("KingdomResidents.ReadRoster(System, Z, Survey, state",
				StringComparison.Ordinal);
			int works = checkIn.IndexOf("ReadWorks(state, Z, Survey)", StringComparison.Ordinal);
			ClassicAssert.IsTrue(roster > -1 && works > roster,
				"the roster must be read before the work rows that count it");

			// And the work row's crew is that join, taken on the row's own id and ground.
			string worksSource = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomCity.z09.WorksAndAudit.cs");
			StringAssert.Contains("int workId = KingdomCityRules.StableId(work.IDIfAssigned);",
				worksSource);
			StringAssert.Contains("KingdomResidentRules.CrewAssigned(state, Z.ZoneID, workId)",
				worksSource);

			// The posting the row is read from is the settler's own stamp, re-written each pass.
			string helpers = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomResidents.06.Helpers.cs");
			StringAssert.Contains("int jobWorkId = KingdomStations.PostOf(settler);", helpers);
		}
	}
}
#endif
