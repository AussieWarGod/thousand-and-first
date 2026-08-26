using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed class KingdomSealBody
	{
		private readonly List<string> _order = new List<string>();

		private readonly Dictionary<string, KingdomSealKind> _kinds = new Dictionary<string, KingdomSealKind>();

		private readonly Dictionary<string, string> _text = new Dictionary<string, string>();

		private readonly Dictionary<string, long> _number = new Dictionary<string, long>();

		private readonly Dictionary<string, List<string>> _textList = new Dictionary<string, List<string>>();

		private readonly Dictionary<string, List<long>> _numberList = new Dictionary<string, List<long>>();

		/// <summary>The keys, in the order they were written.</summary>
		public IList<string> Keys => _order;

		public int Count => _order.Count;

		public bool Has(string Key)
		{
			return Key != null && _kinds.ContainsKey(Key);
		}

		public KingdomSealKind KindOf(string Key)
		{
			KingdomSealKind kind;
			return (Key != null && _kinds.TryGetValue(Key, out kind)) ? kind : KingdomSealKind.Text;
		}

		/// <summary>
		/// Writes a text value. A null is written as empty rather than refused: an absent founder
		/// name is a fact about a seal, not a corruption of one.
		/// </summary>
		/// <exception cref="ArgumentException">The key is null, empty, or already written.</exception>
		public void Put(string Key, string Value)
		{
			Claim(Key, KingdomSealKind.Text);
			_text[Key] = Value ?? "";
		}

		/// <exception cref="ArgumentException">The key is null, empty, or already written.</exception>
		public void Put(string Key, long Value)
		{
			Claim(Key, KingdomSealKind.Number);
			_number[Key] = Value;
		}

		/// <exception cref="ArgumentException">The key is null, empty, or already written.</exception>
		public void PutList(string Key, IList<string> Values)
		{
			if (Values == null || Values.Count == 0)
			{
				Claim(Key, KingdomSealKind.EmptyList);
				return;
			}
			Claim(Key, KingdomSealKind.TextList);
			List<string> copy = new List<string>(Values.Count);
			for (int i = 0; i < Values.Count; i++)
			{
				copy.Add(Values[i] ?? "");
			}
			_textList[Key] = copy;
		}

		/// <exception cref="ArgumentException">The key is null, empty, or already written.</exception>
		public void PutList(string Key, IList<long> Values)
		{
			if (Values == null || Values.Count == 0)
			{
				Claim(Key, KingdomSealKind.EmptyList);
				return;
			}
			Claim(Key, KingdomSealKind.NumberList);
			_numberList[Key] = new List<long>(Values);
		}

		/// <summary>The text at <paramref name="Key"/>, or null when absent or another kind.</summary>
		public string Text(string Key)
		{
			string value;
			return (Key != null && _text.TryGetValue(Key, out value)) ? value : null;
		}

		/// <summary>The number at <paramref name="Key"/>, or <paramref name="Fallback"/>.</summary>
		public long Number(string Key, long Fallback = 0L)
		{
			long value;
			return (Key != null && _number.TryGetValue(Key, out value)) ? value : Fallback;
		}

		/// <summary>The text list at <paramref name="Key"/>; empty for an empty list; null when
		/// absent or of the other kind.</summary>
		public List<string> TextList(string Key)
		{
			if (Key == null)
			{
				return null;
			}
			List<string> value;
			if (_textList.TryGetValue(Key, out value))
			{
				return value;
			}
			return (KindOf(Key) == KingdomSealKind.EmptyList && _kinds.ContainsKey(Key)) ? new List<string>() : null;
		}

		/// <summary>The number list at <paramref name="Key"/>; empty for an empty list; null when
		/// absent or of the other kind.</summary>
		public List<long> NumberList(string Key)
		{
			if (Key == null)
			{
				return null;
			}
			List<long> value;
			if (_numberList.TryGetValue(Key, out value))
			{
				return value;
			}
			return (KindOf(Key) == KingdomSealKind.EmptyList && _kinds.ContainsKey(Key)) ? new List<long>() : null;
		}

		internal void Adopt(string Key, KingdomSealKind Kind, string Text, long Number, List<string> Texts, List<long> Numbers)
		{
			Claim(Key, Kind);
			switch (Kind)
			{
			case KingdomSealKind.Text:
				_text[Key] = Text;
				break;
			case KingdomSealKind.Number:
				_number[Key] = Number;
				break;
			case KingdomSealKind.TextList:
				_textList[Key] = Texts;
				break;
			case KingdomSealKind.NumberList:
				_numberList[Key] = Numbers;
				break;
			}
		}

		private void Claim(string Key, KingdomSealKind Kind)
		{
			if (string.IsNullOrEmpty(Key))
			{
				throw new ArgumentException("A seal key may not be empty.");
			}
			if (_kinds.ContainsKey(Key))
			{
				throw new ArgumentException("The seal key '" + Key + "' was written twice.");
			}
			_kinds[Key] = Kind;
			_order.Add(Key);
		}
	}
}
