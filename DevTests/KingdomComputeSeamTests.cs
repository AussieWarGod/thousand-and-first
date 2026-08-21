#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

// A forged engine namespace, declared here and nowhere else.
//
// The seam's whole job is to refuse a type from a Qud assembly, and the test assembly references
// no Qud assembly at all -- so a sweep of the real boundary types would pass whether the ban
// worked or not. A ban that cannot be seen to fire is not a ban. This is the negative fixture the
// enforcement test proves itself against, in the shape STANDARDS demands of a checker: verified
// the only way such a check can be, by confirming it fails against the defect it exists to catch.
namespace XRL.World
{
	internal sealed class ForgedGameObject
	{
		internal readonly int Id;

		internal ForgedGameObject(int id)
		{
			Id = id;
		}
	}
}

namespace ThousandAndFirst.Tests
{
	/// <summary>Marks a computation that exists to be refused. The assembly sweep skips these;
	/// every other implementation in the assembly must pass.</summary>
	internal interface ISeamNegativeFixture
	{
	}

	internal readonly struct SeamCargo
	{
		internal readonly long Amount;

		internal SeamCargo(long amount)
		{
			Amount = amount;
		}
	}

	internal struct SeamMutableCargo
	{
		internal long Amount;
	}

	internal readonly struct SeamStaticCargo
	{
		internal static long Tally;

		internal readonly long Amount;

		internal SeamStaticCargo(long amount)
		{
			Amount = amount;
		}
	}

	/// <summary>The shape every real computation will take from W1 on.</summary>
	internal sealed class DoublingComputation : IKingdomComputation<SeamCargo, SeamCargo>
	{
		private readonly int draws;

		private readonly bool succeeds;

		private readonly bool throws;

		internal DoublingComputation(int draws, bool succeeds, bool throws)
		{
			this.draws = draws;
			this.succeeds = succeeds;
			this.throws = throws;
		}

		public string Label
		{
			get { return "taf:test:doubling"; }
		}

		public KingdomBudgetLane Lane
		{
			get { return KingdomBudgetLane.Reckon; }
		}

		public bool TryRun(SeamCargo input, out SeamCargo output, out KingdomComputeCounters counters, out KingdomCityFault fault)
		{
			if (throws)
			{
				throw new InvalidOperationException("a third-party job misbehaving");
			}
			counters = new KingdomComputeCounters(3, 232L, draws, 0, 0L);
			if (!succeeds)
			{
				output = default(SeamCargo);
				fault = KingdomCityFault.ClockRegression;
				return false;
			}
			output = new SeamCargo(input.Amount * 2L);
			fault = KingdomCityFault.None;
			return true;
		}
	}

	internal sealed class EngineBoundComputation : IKingdomComputation<XRL.World.ForgedGameObject, SeamCargo>, ISeamNegativeFixture
	{
		public string Label
		{
			get { return "taf:test:engine-bound"; }
		}

		public KingdomBudgetLane Lane
		{
			get { return KingdomBudgetLane.Reckon; }
		}

		public bool TryRun(XRL.World.ForgedGameObject input, out SeamCargo output, out KingdomComputeCounters counters, out KingdomCityFault fault)
		{
			output = default(SeamCargo);
			counters = KingdomComputeCounters.None;
			fault = KingdomCityFault.None;
			return true;
		}
	}

	internal sealed class MutableBoundaryComputation : IKingdomComputation<SeamMutableCargo, SeamCargo>, ISeamNegativeFixture
	{
		public string Label
		{
			get { return "taf:test:mutable"; }
		}

		public KingdomBudgetLane Lane
		{
			get { return KingdomBudgetLane.Reckon; }
		}

		public bool TryRun(SeamMutableCargo input, out SeamCargo output, out KingdomComputeCounters counters, out KingdomCityFault fault)
		{
			output = default(SeamCargo);
			counters = KingdomComputeCounters.None;
			fault = KingdomCityFault.None;
			return true;
		}
	}

	internal sealed class MutableStaticComputation : IKingdomComputation<SeamStaticCargo, SeamCargo>, ISeamNegativeFixture
	{
		public string Label
		{
			get { return "taf:test:mutable-static"; }
		}

		public KingdomBudgetLane Lane
		{
			get { return KingdomBudgetLane.Reckon; }
		}

		public bool TryRun(SeamStaticCargo input, out SeamCargo output, out KingdomComputeCounters counters, out KingdomCityFault fault)
		{
			output = default(SeamCargo);
			counters = KingdomComputeCounters.None;
			fault = KingdomCityFault.None;
			return true;
		}
	}

	internal sealed class ScriptedClock : IKingdomComputeClock
	{
		private readonly long[] readings;

		private int index;

		internal ScriptedClock(long[] readings)
		{
			this.readings = readings;
			index = 0;
		}

		public long NowMicroseconds()
		{
			long reading = readings[(index < readings.Length) ? index : (readings.Length - 1)];
			index++;
			return reading;
		}
	}

	/// <summary>
	/// The executor seam. LIVING-CITY-ARCHITECTURE §2.5: one choke point, immutable in and out, no
	/// engine type across it, budget and timeout owned by the seam, and a job that may not read the
	/// clock. The reflection test at the bottom is the enforcement §2.5 asks for.
	/// </summary>
	public class KingdomComputeSeamTests
	{
		private static KingdomExecutor Executor(long[] readings, out KingdomComputeJournalRing journal)
		{
			journal = new KingdomComputeJournalRing();
			return new KingdomExecutor(new ScriptedClock(readings), journal);
		}

		[Test]
		public void SubmitPublishesTheNewValueAndRecordsOneReceipt()
		{
			KingdomComputeJournalRing journal;
			KingdomExecutor executor = Executor(new long[2] { 1000L, 1400L }, out journal);
			KingdomComputeResult<SeamCargo> result = executor.Submit(new SeamCargo(21L), new DoublingComputation(4, succeeds: true, throws: false));
			Assert.AreEqual(KingdomComputeStatus.Ok, result.Status);
			Assert.IsTrue(result.Published);
			Assert.AreEqual(42L, result.Value.Amount);
			Assert.AreEqual(400L, result.Receipt.Microseconds);
			Assert.AreEqual(232L, result.Receipt.Counters.RowVisits);
			Assert.AreEqual(1, journal.Count);
		}

		[Test]
		public void AFaultedJobPublishesNothingAndStillLeavesAReceipt()
		{
			KingdomComputeJournalRing journal;
			KingdomExecutor executor = Executor(new long[2] { 0L, 500L }, out journal);
			KingdomComputeResult<SeamCargo> result = executor.Submit(new SeamCargo(21L), new DoublingComputation(0, succeeds: false, throws: false));
			Assert.AreEqual(KingdomComputeStatus.Faulted, result.Status);
			Assert.IsFalse(result.Published);
			Assert.AreEqual(0L, result.Value.Amount, "a faulted job published a value");
			Assert.AreEqual(KingdomCityFault.ClockRegression, result.Fault);
			Assert.AreEqual(1, journal.Count, "a fault must still be measured");
		}

		/// <summary>A misbehaving job stalls itself, never the city and never the turn. That is the
		/// property §2.5 says no amount of documentation could give a direct call.</summary>
		[Test]
		public void AThrowingJobIsCaughtAtTheSeamAndNamed()
		{
			KingdomComputeJournalRing journal;
			KingdomExecutor executor = Executor(new long[2] { 0L, 100L }, out journal);
			KingdomComputeResult<SeamCargo> result = executor.Submit(new SeamCargo(3L), new DoublingComputation(0, succeeds: true, throws: true));
			Assert.AreEqual(KingdomComputeStatus.Faulted, result.Status);
			Assert.AreEqual(KingdomComputeRefusal.Threw, result.Refusal);
			Assert.AreEqual(0L, result.Value.Amount);
		}

		[Test]
		public void ANullJobIsRefusedWithoutInvokingAnything()
		{
			KingdomComputeJournalRing journal;
			KingdomExecutor executor = Executor(new long[1] { 0L }, out journal);
			KingdomComputeResult<SeamCargo> result = executor.Submit(new SeamCargo(1L), (IKingdomComputation<SeamCargo, SeamCargo>)null);
			Assert.AreEqual(KingdomComputeStatus.Refused, result.Status);
			Assert.AreEqual(KingdomComputeRefusal.NullJob, result.Refusal);
			Assert.AreEqual(0, journal.Count, "a refusal ran nothing, so it measured nothing");
		}

		/// <summary>Over the reckon lane's 8 ms fail rung (LIVING-CITY-ARCHITECTURE §0.0). The job
		/// succeeded; the seam still publishes nothing, so the caller's state is byte-identical.</summary>
		[Test]
		public void AJobOverItsTimeBudgetIsAbandonedAndPublishesNothing()
		{
			KingdomComputeJournalRing journal;
			KingdomExecutor executor = Executor(new long[2] { 0L, 8001L }, out journal);
			KingdomComputeResult<SeamCargo> result = executor.Submit(new SeamCargo(21L), new DoublingComputation(0, succeeds: true, throws: false));
			Assert.AreEqual(KingdomComputeStatus.OverBudget, result.Status);
			Assert.AreEqual(0L, result.Value.Amount, "an abandoned job published a value");
			Assert.AreEqual(KingdomBudgetVerdict.Over, result.Receipt.Verdict);
		}

		/// <summary>Draws are per happening, never per day: 512 a city pass is the ceiling, so 513
		/// is a failure even in nought milliseconds. LIVING-CITY-ARCHITECTURE §0.0(a).</summary>
		[Test]
		public void AJobOverItsCountBudgetIsAbandonedEvenWhenItWasFast()
		{
			KingdomComputeJournalRing journal;
			KingdomExecutor executor = Executor(new long[2] { 0L, 10L }, out journal);
			KingdomComputeResult<SeamCargo> result = executor.Submit(new SeamCargo(21L), new DoublingComputation(KingdomBudgetRules.MaxDrawsPerCityPass + 1, succeeds: true, throws: false));
			Assert.AreEqual(KingdomComputeStatus.OverBudget, result.Status);
			Assert.AreEqual(KingdomBudgetVerdict.Over, result.Receipt.CountVerdict);
			Assert.AreEqual(KingdomBudgetVerdict.Within, result.Receipt.TimeVerdict);
		}

		[Test]
		public void AWarnIsRecordedAndStillPublishes()
		{
			KingdomComputeJournalRing journal;
			KingdomExecutor executor = Executor(new long[2] { 0L, 3000L }, out journal);
			KingdomComputeResult<SeamCargo> result = executor.Submit(new SeamCargo(21L), new DoublingComputation(0, succeeds: true, throws: false));
			Assert.AreEqual(KingdomComputeStatus.Ok, result.Status);
			Assert.AreEqual(KingdomBudgetVerdict.Warn, result.Receipt.Verdict);
			Assert.AreEqual(42L, result.Value.Amount);
		}

		[Test]
		public void TheJournalKeepsTheLastReceiptsAndTheWorstPerLane()
		{
			KingdomComputeJournalRing journal = new KingdomComputeJournalRing();
			for (int i = 0; i < KingdomComputeJournalRing.Capacity + 3; i++)
			{
				journal.Record(new KingdomPerfReceipt(KingdomBudgetLane.Reckon, "taf:test", i, KingdomComputeCounters.None, 0L,
					KingdomBudgetVerdict.Within, KingdomBudgetVerdict.Within));
			}
			Assert.AreEqual(KingdomComputeJournalRing.Capacity, journal.Count, "the ring grew");
			KingdomPerfReceipt oldest;
			Assert.IsTrue(journal.TryGet(0, out oldest));
			Assert.AreEqual(3L, oldest.Microseconds, "the ring did not forget its oldest three");
			KingdomPerfReceipt worst;
			Assert.IsTrue(journal.TryWorst(KingdomBudgetLane.Reckon, out worst));
			Assert.AreEqual(KingdomComputeJournalRing.Capacity + 2L, worst.Microseconds);
			Assert.IsFalse(journal.TryWorst(KingdomBudgetLane.Reify, out worst), "a lane nothing ran on has no worst");
		}

		// =================================================================================
		// The enforcement test. LIVING-CITY-ARCHITECTURE §2.5.
		// =================================================================================

		/// <summary>
		/// Walks the type closure of every <c>IKingdomComputation</c>'s input and output in this
		/// assembly and fails the build on a type from a Qud assembly, a mutable field, or a
		/// non-readonly static. This is the one thing standing between the design and a second
		/// computation path growing quietly beside the first.
		/// </summary>
		[Test]
		public void NoEngineTypeCrossesTheExecutorSeam()
		{
			int checkedBoundaries = 0;
			foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
			{
				if (type.IsAbstract || type.IsInterface)
				{
					continue;
				}
				if (typeof(ISeamNegativeFixture).IsAssignableFrom(type))
				{
					continue;
				}
				foreach (Type contract in type.GetInterfaces())
				{
					if (!contract.IsGenericType || contract.GetGenericTypeDefinition() != typeof(IKingdomComputation<,>))
					{
						continue;
					}
					Type[] boundary = contract.GetGenericArguments();
					KingdomComputeRefusal refusal;
					string offender;
					bool clean = KingdomComputeSeam.TryValidateBoundary(boundary[0], boundary[1], out refusal, out offender);
					Assert.IsTrue(clean, type.FullName + " crosses the seam with " + offender + " (" + refusal + ")");
					checkedBoundaries++;
				}
			}
			// The sweep has to have actually looked at something: a rename that made every
			// computation unfindable would otherwise pass silently.
			Assert.Greater(checkedBoundaries, 0, "the sweep found no computation to check");
		}

		/// <summary>The ban fires. Without this the sweep above proves nothing, because no Qud
		/// assembly is referenced by the test project at all.</summary>
		[Test]
		public void TheEngineBanIsNotVacuous()
		{
			KingdomComputeRefusal refusal;
			string offender;
			Assert.IsFalse(KingdomComputeSeam.TryValidateBoundary(typeof(XRL.World.ForgedGameObject), typeof(SeamCargo), out refusal, out offender));
			Assert.AreEqual(KingdomComputeRefusal.EngineTypeAtBoundary, refusal);
			Assert.AreEqual("XRL.World.ForgedGameObject", offender);
		}

		[Test]
		public void AMutableBoundaryFieldIsRefused()
		{
			KingdomComputeRefusal refusal;
			string offender;
			Assert.IsFalse(KingdomComputeSeam.TryValidateBoundary(typeof(SeamMutableCargo), typeof(SeamCargo), out refusal, out offender));
			Assert.AreEqual(KingdomComputeRefusal.MutableField, refusal);
			Assert.IsTrue(offender.EndsWith("SeamMutableCargo.Amount"), offender);
		}

		[Test]
		public void ANonReadonlyStaticOnABoundaryTypeIsRefused()
		{
			KingdomComputeRefusal refusal;
			string offender;
			Assert.IsFalse(KingdomComputeSeam.TryValidateBoundary(typeof(SeamStaticCargo), typeof(SeamCargo), out refusal, out offender));
			Assert.AreEqual(KingdomComputeRefusal.MutableStatic, refusal);
			Assert.IsTrue(offender.EndsWith("SeamStaticCargo.Tally"), offender);
		}

		/// <summary>Engine-adjacent namespaces are named by prefix, and a prefix match is only a
		/// match on a namespace boundary: "XRLike" is not "XRL".</summary>
		[TestCase("XRL", true)]
		[TestCase("XRL.World.Parts", true)]
		[TestCase("XRLike.Things", false)]
		[TestCase("UnityEngine.UI", true)]
		[TestCase("ConsoleLib.Console", true)]
		[TestCase("ThousandAndFirst.Simulation.City", false)]
		[TestCase("System.Collections.Generic", false)]
		[TestCase("", false)]
		[TestCase(null, false)]
		public void EngineNamespacesAreNamedOnBoundaries(string space, bool expected)
		{
			Assert.AreEqual(expected, KingdomComputeSeam.IsEngineNamespace(space));
		}

		[TestCase("Assembly-CSharp", true)]
		[TestCase("UnityEngine.CoreModule", true)]
		[TestCase("UnityEngine.PhysicsModule", true)]
		[TestCase("TafTests", false)]
		[TestCase("mscorlib", false)]
		[TestCase(null, false)]
		public void EngineAssembliesAreNamed(string assembly, bool expected)
		{
			Assert.AreEqual(expected, KingdomComputeSeam.IsEngineAssembly(assembly));
		}

		/// <summary>The whole model is boundary-eligible, so every one of its types must survive
		/// the same walk a computation's input does. A row that grows a mutable field fails here
		/// before it can reach a save.</summary>
		[Test]
		public void EveryModelTypeSurvivesTheSeamWalk()
		{
			List<Type> model = new List<Type>();
			model.Add(typeof(KingdomCityState));
			model.Add(typeof(KingdomZoneRow));
			model.Add(typeof(KingdomWorkRow));
			model.Add(typeof(KingdomResidentRow));
			model.Add(typeof(KingdomClockRow));
			model.Add(typeof(KingdomToldRow));
			model.Add(typeof(KingdomStocks));
			model.Add(typeof(KingdomWorkRunState));
			model.Add(typeof(KingdomLeg));
			model.Add(typeof(KingdomVesselRow));
			model.Add(typeof(KingdomBreakpoint));
			model.Add(typeof(KingdomCatchUpCounter));
			model.Add(typeof(KingdomPerfReceipt));
			foreach (Type type in model)
			{
				KingdomComputeRefusal refusal;
				string offender;
				Assert.IsTrue(KingdomComputeSeam.TryValidateType(type, out refusal, out offender),
					type.Name + " failed the seam walk at " + offender + " (" + refusal + ")");
			}
		}
	}
}
#endif
