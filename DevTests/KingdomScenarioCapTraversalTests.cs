#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// A list that records how far it was walked.
	/// <para>
	/// The models declare their collections as <c>IList&lt;&gt;</c>, so a cap can be tested for what
	/// it actually does rather than for what it reports. A source contract can prove a `return;`
	/// sits after a finding; only this can prove the validator never touched element 9 of a hostile
	/// list of 9,000.
	/// </para>
	/// </summary>
	internal sealed class CountingList<T> : IList<T>
	{
		private readonly List<T> _items;

		internal CountingList(IEnumerable<T> items)
		{
			_items = new List<T>(items);
		}

		/// <summary>Highest index any caller read, or -1 when the list was never indexed.</summary>
		internal int HighestIndexRead = -1;

		/// <summary>True when anything enumerated the list rather than indexing it.</summary>
		internal bool Enumerated;

		public T this[int index]
		{
			get
			{
				if (index > HighestIndexRead) HighestIndexRead = index;
				return _items[index];
			}
			set { _items[index] = value; }
		}

		public int Count { get { return _items.Count; } }
		public bool IsReadOnly { get { return false; } }
		public void Add(T item) { _items.Add(item); }
		public void Clear() { _items.Clear(); }
		public bool Contains(T item) { return _items.Contains(item); }
		public void CopyTo(T[] array, int index) { _items.CopyTo(array, index); }
		public int IndexOf(T item) { return _items.IndexOf(item); }
		public void Insert(int index, T item) { _items.Insert(index, item); }
		public bool Remove(T item) { return _items.Remove(item); }
		public void RemoveAt(int index) { _items.RemoveAt(index); }

		/// <summary>
		/// Enumeration records indices too. A sentinel that only watched the indexer would die
		/// silently the day someone rewrote a `for` as a `foreach`, which is the sort of edit
		/// nobody would think to re-run a cap test for.
		/// </summary>
		public IEnumerator<T> GetEnumerator()
		{
			Enumerated = true;
			for (int i = 0; i < _items.Count; i++)
			{
				if (i > HighestIndexRead) HighestIndexRead = i;
				yield return _items[i];
			}
		}

		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

		/// <summary>Highest index reached by ANY access route, indexer or enumerator.</summary>
		internal int HighestReach { get { return HighestIndexRead; } }
	}

	/// <summary>
	/// The counted-list sentinel RED 19 item 3 demanded: caps must bound ACCESS, not only the
	/// verdict. Each test fails if the validator walks past the cap it just reported.
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioCapTraversalTests
	{
		private const int Huge = 9000;

		private static KingdomScenarioStep Step()
		{
			KingdomScenarioStep step = new KingdomScenarioStep
			{
				Verb = KingdomScenarioVerb.ProveCatalogue
			};
			step.Arguments["Catalogue"] = "architecture";
			return step;
		}

		private static KingdomScenarioDefinition Row()
		{
			KingdomScenarioDefinition row = new KingdomScenarioDefinition
			{
				Key = "arch-gallery-slice",
				Family = "architecture",
				AuthorityClass = "architecture-stamper",
				SyntheticRaw = "false"
			};
			row.Steps.Add(Step());
			return row;
		}

		/// <summary>A hostile list is refused at the cap, not counted through to the end.</summary>
		[Test]
		public void TheParameterCapBoundsTraversalNotOnlyTheVerdict()
		{
			List<KingdomScenarioParameter> hostile = new List<KingdomScenarioParameter>();
			for (int i = 0; i < Huge; i++)
			{
				KingdomScenarioParameter parameter = new KingdomScenarioParameter
				{
					Name = "p" + i
				};
				parameter.Domain = new List<string> { "north" };
				hostile.Add(parameter);
			}
			CountingList<KingdomScenarioParameter> counted =
				new CountingList<KingdomScenarioParameter>(hostile);
			KingdomScenarioDefinition row = Row();
			row.Parameters = counted;
			IList<string> findings = KingdomScenarioRowValidator.Findings(row);
			Assert.IsNotEmpty(findings);
			Assert.LessOrEqual(counted.HighestIndexRead,
				KingdomScenarioRowValidator.MaxParameters,
				"the over-cap parameter list was walked past its own cap");
			Assert.IsFalse(counted.Enumerated, "an over-cap parameter list was enumerated whole");
		}

		[Test]
		public void TheDomainCapBoundsTraversalNotOnlyTheVerdict()
		{
			List<string> values = new List<string>();
			for (int i = 0; i < Huge; i++) values.Add("v" + i);
			CountingList<string> counted = new CountingList<string>(values);
			KingdomScenarioParameter parameter = new KingdomScenarioParameter { Name = "facing" };
			parameter.Domain = counted;
			KingdomScenarioDefinition row = Row();
			row.Parameters.Add(parameter);
			IList<string> findings = KingdomScenarioRowValidator.Findings(row);
			Assert.IsNotEmpty(findings);
			Assert.LessOrEqual(counted.HighestIndexRead,
				KingdomScenarioRowValidator.MaxDomainValues,
				"the over-cap domain was walked past its own cap");
		}

		[Test]
		public void TheStepCapBoundsTraversalNotOnlyTheVerdict()
		{
			List<KingdomScenarioStep> hostile = new List<KingdomScenarioStep>();
			for (int i = 0; i < Huge; i++) hostile.Add(Step());
			CountingList<KingdomScenarioStep> counted =
				new CountingList<KingdomScenarioStep>(hostile);
			KingdomScenarioDefinition row = Row();
			row.Steps = counted;
			IList<string> findings = KingdomScenarioRowValidator.Findings(row);
			Assert.IsNotEmpty(findings);
			Assert.LessOrEqual(counted.HighestIndexRead, KingdomScenarioRowValidator.MaxSteps,
				"the over-cap step list was walked past its own cap");
		}

		/// <summary>The registry cap refuses before validating a single row.</summary>
		[Test]
		public void TheRegistryCapBoundsTraversalNotOnlyTheVerdict()
		{
			List<KingdomScenarioDefinition> hostile = new List<KingdomScenarioDefinition>();
			for (int i = 0; i < Huge; i++) hostile.Add(Row());
			CountingList<KingdomScenarioDefinition> counted =
				new CountingList<KingdomScenarioDefinition>(hostile);
			IList<string> findings = KingdomScenarioRules.Validate(counted);
			Assert.IsNotEmpty(findings);
			Assert.AreEqual(-1, counted.HighestIndexRead,
				"an over-cap registry was indexed at all");
		}

		/// <summary>A lawful row is still walked: the caps must not refuse everything.</summary>
		[Test]
		public void ALawfulRowIsStillFullyValidated()
		{
			KingdomScenarioParameter parameter = new KingdomScenarioParameter { Name = "facing" };
			parameter.Domain = new List<string> { "north", "east" };
			KingdomScenarioDefinition row = Row();
			row.Parameters.Add(parameter);
			CollectionAssert.IsEmpty(KingdomScenarioRowValidator.Findings(row));
		}
	}
}
#endif
