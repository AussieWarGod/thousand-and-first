#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NUnit.Framework;
using ThousandAndFirst.Integrations.Hearthpyre223;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomHearthpyreCollectionViewsTests
	{
		private sealed class Row { }
		private sealed class Box
		{
			internal readonly object Body;
			internal Box(object Body) { this.Body = Body; }
		}
		private sealed class ReadOnlyMap : IReadOnlyDictionary<string, Row>
		{
			internal readonly Dictionary<string, Row> Rows = new Dictionary<string, Row>();
			internal int Enumerations;
			internal int CountAdjustment;
			public int Count => Rows.Count + CountAdjustment;
			public IEnumerable<string> Keys => Rows.Keys;
			public IEnumerable<Row> Values => Rows.Values;
			public Row this[string Key] => Rows[Key];
			public bool ContainsKey(string Key) => Rows.ContainsKey(Key);
			public bool TryGetValue(string Key, out Row Value) => Rows.TryGetValue(Key, out Value);
			public IEnumerator<KeyValuePair<string, Row>> GetEnumerator() { Enumerations++; return Rows.GetEnumerator(); }
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
		private sealed class ReadOnlyList : IReadOnlyList<Row>
		{
			internal readonly List<Row> Rows = new List<Row>();
			public int Count => Rows.Count;
			public Row this[int Index] => Rows[Index];
			public IEnumerator<Row> GetEnumerator() => Rows.GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}

		[Test]
		public void ReadOnlyMapStaysLazyAndReproofSeesReplacedAndRemovedRows()
		{
			var source = new ReadOnlyMap(); var first = new Row(); var replacement = new Row();
			source.Rows.Add("home", first); source.Rows.Add("null", null);
			int wraps = 0;
			var view = KingdomHearthpyreCollectionViews.Map<string, Box>(source, typeof(Row), value => {
				wraps++; return value == null ? null : new Box(value);
			});
			Assert.That(wraps, Is.Zero); Assert.That(source.Enumerations, Is.Zero);
			Assert.That(view.Count, Is.EqualTo(2)); Assert.That(view.ContainsKey("home"), Is.True);
			Assert.That(wraps, Is.Zero); Assert.That(source.Enumerations, Is.Zero);
			Assert.That(view["home"].Body, Is.SameAs(first)); Assert.That(view["null"], Is.Null);
			Assert.That(view.TryGetValue("missing", out Box absent), Is.False); Assert.That(absent, Is.Null);
			Assert.That(wraps, Is.EqualTo(2));
			source.Rows["home"] = replacement;
			Assert.That(view.TryGetValue("home", out Box changed), Is.True);
			Assert.That(changed.Body, Is.SameAs(replacement));
			Assert.That(view.Single(pair => pair.Key == "home").Value.Body, Is.SameAs(replacement));
			Assert.That(view.Values.Single(value => value != null).Body, Is.SameAs(replacement));
			source.Rows.Remove("home");
			Assert.That(view.Count, Is.EqualTo(1)); Assert.That(view.ContainsKey("home"), Is.False);
			Assert.Throws<KeyNotFoundException>(() => { var missing = view["home"]; });
			Assert.That(view, Is.Not.InstanceOf<IDictionary<string, Box>>());
		}

		[TestCase(-1)]
		[TestCase(1)]
		public void RegistryCannotHideExtraOrMissingRowsBehindItsCount(int Adjustment)
		{
			var source = new ReadOnlyMap { CountAdjustment = Adjustment };
			source.Rows.Add("home", new Row());
			var view = KingdomHearthpyreCollectionViews.Map<string, object>(source, typeof(Row), value => value);
			Assert.Throws<InvalidOperationException>(() => view.ToArray());
			Assert.Throws<InvalidOperationException>(() => view.Values.ToArray());
			Assert.Throws<InvalidOperationException>(() => view.Keys.ToArray());
			source.CountAdjustment = 0;
			Assert.That(view.Single().Value, Is.SameAs(source.Rows["home"]));
			Assert.That(view.Keys, Is.EqualTo(new[] { "home" }));
		}

		[Test]
		public void MutableOnlyMapRetainsIdentityWithoutGrantingWrites()
		{
			// ConcurrentDictionary and Dictionary also implement IReadOnlyDictionary; use the
			// explicit IDictionary-only wrapper to exercise the other public contract.
			var source = new MutableMap(); var row = new Row(); source.Add("home", row);
			var view = KingdomHearthpyreCollectionViews.Map<string, object>(source, typeof(Row), value => value);
			Assert.That(view["home"], Is.SameAs(row)); Assert.That(view.Single().Value, Is.SameAs(row));
			source.Clear(); Assert.That(view.Count, Is.Zero);
			Assert.That(view, Is.Not.InstanceOf<IDictionary<string, object>>());
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ListReproofSeesReplacementsAndRefusesConcurrentCountChange(bool ReadOnly)
		{
			var source = new ReadOnlyList(); var first = new Row(); var replacement = new Row();
			source.Rows.Add(first); source.Rows.Add(null);
			object input = ReadOnly ? (object)source : new Collection<Row>(source.Rows);
			int wraps = 0;
			var view = KingdomHearthpyreCollectionViews.List<object>(input, typeof(Row), value => { wraps++; return value; });
			Assert.That(wraps, Is.Zero); Assert.That(view.Count, Is.EqualTo(2));
			Assert.That(view[0], Is.SameAs(first)); Assert.That(view[1], Is.Null);
			source.Rows[0] = replacement; Assert.That(view[0], Is.SameAs(replacement));
			using (var iterator = view.GetEnumerator())
			{
				Assert.That(iterator.MoveNext(), Is.True); Assert.That(iterator.Current, Is.SameAs(replacement));
				source.Rows.RemoveAt(1);
				Assert.Throws<InvalidOperationException>(() => iterator.MoveNext());
			}
			Assert.That(view.Single(), Is.SameAs(replacement));
			Assert.That(view, Is.Not.InstanceOf<IList<object>>());
		}

		[Test]
		public void AbsentCollectionsStayAbsentAndMalformedCollectionsRefuse()
		{
			Assert.That(KingdomHearthpyreCollectionViews.Map<string, object>(null, typeof(Row), v => v), Is.Null);
			Assert.That(KingdomHearthpyreCollectionViews.List<object>(null, typeof(Row), v => v), Is.Null);
			Assert.Catch(() => KingdomHearthpyreCollectionViews.Map<string, object>(new object(), typeof(Row), v => v));
			Assert.Catch(() => KingdomHearthpyreCollectionViews.List<object>(new object(), typeof(Row), v => v));
		}

		private sealed class MutableMap : IDictionary<string, Row>
		{
			private readonly Dictionary<string, Row> Rows = new Dictionary<string, Row>();
			public Row this[string Key] { get => Rows[Key]; set => Rows[Key] = value; }
			public ICollection<string> Keys => Rows.Keys;
			public ICollection<Row> Values => Rows.Values;
			public int Count => Rows.Count;
			public bool IsReadOnly => false;
			public void Add(string Key, Row Value) => Rows.Add(Key, Value);
			public bool ContainsKey(string Key) => Rows.ContainsKey(Key);
			public bool Remove(string Key) => Rows.Remove(Key);
			public bool TryGetValue(string Key, out Row Value) => Rows.TryGetValue(Key, out Value);
			public void Clear() => Rows.Clear();
			public void Add(KeyValuePair<string, Row> Item) => ((ICollection<KeyValuePair<string, Row>>)Rows).Add(Item);
			public bool Contains(KeyValuePair<string, Row> Item) => ((ICollection<KeyValuePair<string, Row>>)Rows).Contains(Item);
			public void CopyTo(KeyValuePair<string, Row>[] Array, int Index) => ((ICollection<KeyValuePair<string, Row>>)Rows).CopyTo(Array, Index);
			public bool Remove(KeyValuePair<string, Row> Item) => ((ICollection<KeyValuePair<string, Row>>)Rows).Remove(Item);
			public IEnumerator<KeyValuePair<string, Row>> GetEnumerator() => Rows.GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
	}
}
#endif
