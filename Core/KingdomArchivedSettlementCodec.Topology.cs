using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	internal static partial class KingdomArchivedSettlementCodec
	{
		private static bool ExactReferenceTopology(object Left, object Right, Type Type,
			int Depth, Budget Budget, Dictionary<object, object> LeftToRight,
			Dictionary<object, object> RightToLeft, out string Failure)
		{
			Failure = null;
			if (Left == null || Right == null)
			{
				if (Left == null && Right == null) return true;
				Failure = "Settlement reference topology differs at " + Type.FullName + ".";
				return false;
			}
			if (Type == typeof(string) || Type.IsPrimitive || Type.IsEnum) return true;
			if (Left.GetType() != Type || Right.GetType() != Type)
			{
				Failure = "Settlement runtime type differs from its declared schema type.";
				return false;
			}
			if (ReferenceEquals(Left, Right))
			{
				Failure = "Archived and live settlement graphs share mutable " + Type.FullName + ".";
				return false;
			}
			if (Depth > MaxDepth || ++Budget.Objects > MaxObjects)
			{
				Failure = "Settlement reference topology exceeds proof bounds.";
				return false;
			}
			bool leftMapped = LeftToRight.TryGetValue(Left, out object mappedRight);
			bool rightMapped = RightToLeft.TryGetValue(Right, out object mappedLeft);
			if (leftMapped || rightMapped)
			{
				if (ReferenceEquals(mappedRight, Right) && ReferenceEquals(mappedLeft, Left))
					return true;
				Failure = "Settlement reference topology is not one-to-one.";
				return false;
			}
			LeftToRight.Add(Left, Right);
			RightToLeft.Add(Right, Left);
			if (Type == typeof(byte[]))
			{
				byte[] left = (byte[])Left;
				byte[] right = (byte[])Right;
				if (left.Length != right.Length || left.Length > MaxByteArrayBytes)
				{
					Failure = "Settlement byte-array topology or bound differs.";
					return false;
				}
				int difference = 0;
				for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
				if (difference != 0) Failure = "Settlement byte arrays differ.";
				return difference == 0;
			}
			if (IsList(Type))
			{
				IList left = (IList)Left;
				IList right = (IList)Right;
				if (left.Count != right.Count || left.Count > MaxCollectionCount)
				{
					Failure = "Settlement list topology or bound differs.";
					return false;
				}
				Type item = Type.GetGenericArguments()[0];
				for (int i = 0; i < left.Count; i++)
					if (!ExactReferenceTopology(left[i], right[i], item, Depth + 1,
						Budget, LeftToRight, RightToLeft, out Failure)) return false;
				return true;
			}
			if (IsDictionary(Type))
			{
				IDictionary left = (IDictionary)Left;
				IDictionary right = (IDictionary)Right;
				if (!CanonicalDictionaryComparer(Type, left)
					|| !CanonicalDictionaryComparer(Type, right))
				{
					Failure = "Settlement dictionary comparer is noncanonical.";
					return false;
				}
				if (left.Count != right.Count || left.Count > MaxCollectionCount)
				{
					Failure = "Settlement dictionary topology or bound differs.";
					return false;
				}
				Type[] arguments = Type.GetGenericArguments();
				if (arguments[0] != typeof(string))
				{
					Failure = "Settlement dictionary key topology is unsupported.";
					return false;
				}
				foreach (DictionaryEntry row in left)
				{
					if (!(row.Key is string key) || !right.Contains(key) ||
						!ExactReferenceTopology(row.Value, right[key], arguments[1],
							Depth + 1, Budget, LeftToRight, RightToLeft, out Failure))
					{
						Failure = Failure ?? "Settlement dictionary keys differ.";
						return false;
					}
				}
				return true;
			}
			if (!Approved(Type))
			{
				Failure = "Settlement reference field type is unsupported: " + Type.FullName + ".";
				return false;
			}
			FieldInfo[] fields = Fields(Type);
			for (int i = 0; i < fields.Length; i++)
				if (!ExactReferenceTopology(fields[i].GetValue(Left), fields[i].GetValue(Right),
					fields[i].FieldType, Depth + 1, Budget, LeftToRight, RightToLeft,
					out Failure)) return false;
			return true;
		}

	}
}
