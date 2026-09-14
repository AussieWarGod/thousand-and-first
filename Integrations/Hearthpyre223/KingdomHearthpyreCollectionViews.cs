using System;
using System.Collections;
using System.Collections.Generic;

namespace ThousandAndFirst.Integrations.Hearthpyre223
{
	/// <summary>Lazy read-only views retain foreign object identity without copying or exposing
	/// mutation APIs. Callers must charge their existing scan budget before enumerating a view.</summary>
	internal static class KingdomHearthpyreCollectionViews
	{
		internal static IReadOnlyDictionary<TKey, TValue> Map<TKey, TValue>(object Source, Type Foreign,
			Func<object, TValue> Wrap)
		{
			if (Source == null) return null;
			return (IReadOnlyDictionary<TKey, TValue>)Activator.CreateInstance(
				typeof(MapView<,,>).MakeGenericType(typeof(TKey), Foreign, typeof(TValue)), Source, Wrap);
		}

		internal static IReadOnlyList<TValue> List<TValue>(object Source, Type Foreign, Func<object, TValue> Wrap)
		{
			if (Source == null) return null;
			return (IReadOnlyList<TValue>)Activator.CreateInstance(
				typeof(ListView<,>).MakeGenericType(Foreign, typeof(TValue)), Source, Wrap);
		}

		private sealed class MapView<TKey, TForeign, TValue> : IReadOnlyDictionary<TKey, TValue>
		{
			private readonly IReadOnlyDictionary<TKey, TForeign> ReadOnly;
			private readonly IDictionary<TKey, TForeign> Mutable;
			private readonly IEnumerable<KeyValuePair<TKey, TForeign>> Entries;
			private readonly Func<object, TValue> Wrap;

			public MapView(object Source, Func<object, TValue> Wrap)
			{
				ReadOnly = Source as IReadOnlyDictionary<TKey, TForeign>;
				Mutable = Source as IDictionary<TKey, TForeign>;
				if (ReadOnly == null && Mutable == null) throw new InvalidOperationException("realm registry type changed");
				Entries = (IEnumerable<KeyValuePair<TKey, TForeign>>)Source;
				this.Wrap = Wrap ?? throw new ArgumentNullException(nameof(Wrap));
			}
			public int Count => ReadOnly != null ? ReadOnly.Count : Mutable.Count;
			public IEnumerable<TKey> Keys { get { foreach (var row in this) yield return row.Key; } }
			public IEnumerable<TValue> Values { get { foreach (var row in this) yield return row.Value; } }
			public TValue this[TKey Key] => Wrap(ReadOnly != null ? ReadOnly[Key] : Mutable[Key]);
			public bool ContainsKey(TKey Key) => ReadOnly != null ? ReadOnly.ContainsKey(Key) : Mutable.ContainsKey(Key);
			public bool TryGetValue(TKey Key, out TValue Value)
			{
				TForeign found;
				bool present = ReadOnly != null ? ReadOnly.TryGetValue(Key, out found) : Mutable.TryGetValue(Key, out found);
				Value = present ? Wrap(found) : default(TValue);
				return present;
			}
			public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
			{
				int count = Count, seen = 0;
				if (count < 0) throw new InvalidOperationException("realm registry count is negative");
				foreach (var row in Entries)
				{
					if (Count != count || seen >= count)
						throw new InvalidOperationException("realm registry changed or exceeded its declared count");
					seen++;
					yield return new KeyValuePair<TKey, TValue>(row.Key, Wrap(row.Value));
				}
				if (seen != count || Count != count)
					throw new InvalidOperationException("realm registry enumeration does not match its count");
			}
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}

		private sealed class ListView<TForeign, TValue> : IReadOnlyList<TValue>
		{
			private readonly IReadOnlyList<TForeign> ReadOnly;
			private readonly IList<TForeign> Mutable;
			private readonly Func<object, TValue> Wrap;
			public ListView(object Source, Func<object, TValue> Wrap)
			{
				ReadOnly = Source as IReadOnlyList<TForeign>; Mutable = Source as IList<TForeign>;
				if (ReadOnly == null && Mutable == null) throw new InvalidOperationException("realm Home roster type changed");
				this.Wrap = Wrap ?? throw new ArgumentNullException(nameof(Wrap));
			}
			public int Count => ReadOnly != null ? ReadOnly.Count : Mutable.Count;
			public TValue this[int Index] => Wrap(ReadOnly != null ? ReadOnly[Index] : Mutable[Index]);
			public IEnumerator<TValue> GetEnumerator()
			{
				int count = Count;
				for (int i = 0; i < count; i++)
				{
					if (Count != count) throw new InvalidOperationException("realm Home roster changed while enumerated");
					yield return this[i];
				}
				if (Count != count) throw new InvalidOperationException("realm Home roster changed while enumerated");
			}
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
	}
}
