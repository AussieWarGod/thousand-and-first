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
		/// <summary>Rejects any mutable reference shared anywhere between two bounded realm
		/// graphs. Pairwise value comparison misses cross-root aliases (for example an archived
		/// seat list installed into the live away city); this scan treats the complete roots as
		/// one graph and uses reference identity, never value equality.</summary>
		public static bool DisjointMutableGraphs(object[] ArchivedRoots, object[] LiveRoots,
			out string Failure)
		{
			Failure = null;
			if (ArchivedRoots == null || LiveRoots == null)
			{
				Failure = "Realm graph roots are absent.";
				return false;
			}
			HashSet<object> archived = new HashSet<object>(new ReferenceComparer());
			Budget budget = new Budget();
			for (int i = 0; i < ArchivedRoots.Length; i++)
			{
				object root = ArchivedRoots[i];
				if (root == null) continue;
				HashSet<object> seen = new HashSet<object>(new ReferenceComparer());
				HashSet<object> collected = new HashSet<object>(new ReferenceComparer());
				if (!ScanMutable(root, root.GetType(), 0, budget, seen, collected,
					archived, out Failure)) return false;
				archived.UnionWith(collected);
			}
			budget.Objects = 0;
			HashSet<object> forbidden = new HashSet<object>(archived,
				new ReferenceComparer());
			for (int i = 0; i < LiveRoots.Length; i++)
			{
				object root = LiveRoots[i];
				if (root == null) continue;
				HashSet<object> seen = new HashSet<object>(new ReferenceComparer());
				HashSet<object> collected = new HashSet<object>(new ReferenceComparer());
				if (!ScanMutable(root, root.GetType(), 0, budget, seen, collected,
					forbidden, out Failure)) return false;
				forbidden.UnionWith(collected);
			}
			return true;
		}

		internal static bool EmptyRegistries(
			Simulation.City.KingdomBindingRegistry Bindings,
			Simulation.City.KingdomJobRegistry Jobs)
		{
			return Bindings != null && Bindings.Keys != null && Bindings.Keys.Count == 0 &&
				Bindings.Kinds != null && Bindings.Kinds.Count == 0 &&
				Bindings.ZoneIds != null && Bindings.ZoneIds.Count == 0 &&
				Bindings.ObjectIds != null && Bindings.ObjectIds.Count == 0 &&
				Bindings.MintedTicks != null && Bindings.MintedTicks.Count == 0 && Jobs != null &&
				Jobs.JobCounter == 0 && Jobs.JobIds != null && Jobs.JobIds.Count == 0 &&
				Jobs.Kinds != null && Jobs.Kinds.Count == 0 && Jobs.Cargos != null &&
				Jobs.Cargos.Count == 0 && Jobs.CargoAmounts != null &&
				Jobs.CargoAmounts.Count == 0 && Jobs.SourceZoneIds != null &&
				Jobs.SourceZoneIds.Count == 0 && Jobs.DestZoneIds != null &&
				Jobs.DestZoneIds.Count == 0 && Jobs.StartTicks != null &&
				Jobs.StartTicks.Count == 0 && Jobs.WalkTicksPerCell != null &&
				Jobs.WalkTicksPerCell.Count == 0 && Jobs.Statuses != null &&
				Jobs.Statuses.Count == 0 && Jobs.OriginCodes != null &&
				Jobs.OriginCodes.Count == 0 && Jobs.DepositLegIndexes != null &&
				Jobs.DepositLegIndexes.Count == 0 && Jobs.SubjectIds != null &&
				Jobs.SubjectIds.Count == 0 && Jobs.SubjectNames != null &&
				Jobs.SubjectNames.Count == 0 && Jobs.TargetNames != null &&
				Jobs.TargetNames.Count == 0 && Jobs.DueTicks != null &&
				Jobs.DueTicks.Count == 0 && Jobs.WaterCosts != null &&
				Jobs.WaterCosts.Count == 0 && Jobs.ProvisionCosts != null &&
				Jobs.ProvisionCosts.Count == 0 && Jobs.OutcomeCodes != null &&
				Jobs.OutcomeCodes.Count == 0 && Jobs.DeliverySourceEndpointIds != null &&
				Jobs.DeliverySourceEndpointIds.Count == 0 &&
				Jobs.DeliverySourceObjectIds != null && Jobs.DeliverySourceObjectIds.Count == 0 &&
				Jobs.DeliverySourceXs != null && Jobs.DeliverySourceXs.Count == 0 &&
				Jobs.DeliverySourceYs != null && Jobs.DeliverySourceYs.Count == 0 &&
				Jobs.DeliveryTargetEndpointIds != null &&
				Jobs.DeliveryTargetEndpointIds.Count == 0 &&
				Jobs.DeliveryTargetObjectIds != null && Jobs.DeliveryTargetObjectIds.Count == 0 &&
				Jobs.DeliveryTargetXs != null && Jobs.DeliveryTargetXs.Count == 0 &&
				Jobs.DeliveryTargetYs != null && Jobs.DeliveryTargetYs.Count == 0 &&
				Jobs.DeliverySourceBeforeAmounts != null &&
				Jobs.DeliverySourceBeforeAmounts.Count == 0 && Jobs.DeliveryTripIds != null &&
				Jobs.DeliveryTripIds.Count == 0 && Jobs.DeliveryStopOrdinals != null &&
				Jobs.DeliveryStopOrdinals.Count == 0 && Jobs.DeliveryPhases != null &&
				Jobs.DeliveryPhases.Count == 0 && Jobs.DeliveryCargoAuthorityKinds != null &&
				Jobs.DeliveryCargoAuthorityKinds.Count == 0 &&
				Jobs.DeliveryOwnerOperationIds != null && Jobs.DeliveryOwnerOperationIds.Count == 0 &&
				Jobs.DeliveryOwnerManifestVersions != null && Jobs.DeliveryOwnerManifestVersions.Count == 0 &&
				Jobs.DeliveryOwnerManifestDigests != null && Jobs.DeliveryOwnerManifestDigests.Count == 0 &&
				Jobs.DeliveryOwnerManifestRevisions != null && Jobs.DeliveryOwnerManifestRevisions.Count == 0 &&
				Jobs.DeliveryManifestSourceStarts != null && Jobs.DeliveryManifestSourceStarts.Count == 0 &&
				Jobs.DeliveryManifestSourceCounts != null && Jobs.DeliveryManifestSourceCounts.Count == 0 &&
				Jobs.DeliveryTargetBeforeAmounts != null && Jobs.DeliveryTargetBeforeAmounts.Count == 0 &&
				Jobs.DeliveryTargetReceiptStates != null && Jobs.DeliveryTargetReceiptStates.Count == 0 &&
				Jobs.LegCounts != null &&
				Jobs.LegCounts.Count == 0 && Jobs.LegZoneIds != null &&
				Jobs.LegZoneIds.Count == 0 && Jobs.LegEnterX != null &&
				Jobs.LegEnterX.Count == 0 && Jobs.LegEnterY != null &&
				Jobs.LegEnterY.Count == 0 && Jobs.LegExitX != null &&
				Jobs.LegExitX.Count == 0 && Jobs.LegExitY != null &&
				Jobs.LegExitY.Count == 0 && Jobs.LegLengths != null &&
				Jobs.LegLengths.Count == 0 && Jobs.LegDepartTicks != null &&
				Jobs.LegDepartTicks.Count == 0 && Jobs.LegArriveTicks != null &&
				Jobs.LegArriveTicks.Count == 0;
		}

		internal static bool EmptyCarry(KingdomCarryBook Value)
		{
			return TryCarryBytes(new KingdomCarryBook(), out byte[] expected) &&
				TryCarryBytes(Value, out byte[] actual) && ExactBytes(expected, actual);
		}

		private static bool TryCarryBytes(KingdomCarryBook Value, out byte[] Bytes)
		{
			Bytes = null;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					KingdomLifecycleWireCodec.WriteCarry(writer, Value);
					writer.Flush();
					if (stream.Length > MaxPayloadBytes) return false;
					Bytes = stream.ToArray();
					return true;
				}
			}
			catch
			{
				return false;
			}
		}

		private static bool ExactBytes(byte[] Left, byte[] Right)
		{
			if (Left == null || Right == null || Left.Length != Right.Length) return false;
			int difference = 0;
			for (int i = 0; i < Left.Length; i++) difference |= Left[i] ^ Right[i];
			return difference == 0;
		}

		private static bool ScanMutable(object Value, Type Type, int Depth, Budget Budget,
			HashSet<object> Seen, HashSet<object> Collected, HashSet<object> Forbidden,
			out string Failure)
		{
			Failure = null;
			if (Value == null || Type == typeof(string) || Type.IsPrimitive || Type.IsEnum)
				return true;
			if (Value.GetType() != Type)
			{
				Failure = "Realm reference runtime type is not exact: " + Type.FullName + ".";
				return false;
			}
			if (Forbidden != null && Forbidden.Contains(Value))
			{
				Failure = "Archived and live realm graphs share mutable " + Type.FullName + ".";
				return false;
			}
			if (!Seen.Add(Value))
			{
				Failure = "Realm graph repeats mutable " + Type.FullName + ".";
				return false;
			}
			if (++Budget.Objects > MaxObjects || Depth > MaxDepth)
			{
				Failure = "Realm reference graph exceeds proof bounds.";
				return false;
			}
			Collected?.Add(Value);
			if (Type == typeof(byte[]))
			{
				if (((byte[])Value).Length > MaxByteArrayBytes)
				{
					Failure = "Realm byte array exceeds proof cap.";
					return false;
				}
				return true;
			}
			if (IsList(Type))
			{
				IList list = (IList)Value;
				if (list.Count > MaxCollectionCount)
				{
					Failure = "Realm reference list exceeds proof cap.";
					return false;
				}
				Type item = Type.GetGenericArguments()[0];
				for (int i = 0; i < list.Count; i++)
					if (!ScanMutable(list[i], item, Depth + 1, Budget, Seen, Collected,
						Forbidden, out Failure)) return false;
				return true;
			}
			if (IsDictionary(Type))
			{
				IDictionary dictionary = (IDictionary)Value;
				if (!CanonicalDictionaryComparer(Type, dictionary))
				{
					Failure = "Realm dictionary comparer is noncanonical.";
					return false;
				}
				if (dictionary.Count > MaxCollectionCount)
				{
					Failure = "Realm reference dictionary exceeds proof cap.";
					return false;
				}
				Type[] arguments = Type.GetGenericArguments();
				foreach (DictionaryEntry row in dictionary)
				{
					if (!ScanMutable(row.Key, arguments[0], Depth + 1, Budget, Seen,
						Collected, Forbidden, out Failure) ||
						!ScanMutable(row.Value, arguments[1], Depth + 1, Budget, Seen,
							Collected, Forbidden, out Failure)) return false;
				}
				return true;
			}
			if (!Approved(Type))
			{
				Failure = "Realm reference field type is unsupported: " + Type.FullName + ".";
				return false;
			}
			FieldInfo[] fields = Fields(Type);
			for (int i = 0; i < fields.Length; i++)
				if (!ScanMutable(fields[i].GetValue(Value), fields[i].FieldType, Depth + 1,
					Budget, Seen, Collected, Forbidden, out Failure)) return false;
			return true;
		}

		private static bool StrictMutableRoot(object Value, Type Type, out string Failure)
		{
			Failure = null;
			if (Value == null) return true;
			return ScanMutable(Value, Type, 0, new Budget(),
				new HashSet<object>(new ReferenceComparer()),
				new HashSet<object>(new ReferenceComparer()), null, out Failure);
		}

	}
}
