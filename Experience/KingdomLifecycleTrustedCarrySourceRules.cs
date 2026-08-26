using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		/// <summary>
		/// Only the runtime shell may call this seam. Receipt constructors stay private and all
		/// observations are re-derived from the same opaque world before and after its callback.
		/// Public Rules APIs cannot mint a physical or schedule receipt from literals.
		/// </summary>
		internal static partial class TrustedAdapter
		{
			private const string ScheduleBlueprint = "Schedule";

			private sealed class Snapshot
			{
				internal readonly object Reference;
				internal readonly string ObjectId;
				internal readonly string Marker;
				internal readonly string Blueprint;
				internal readonly string SettlementId;
				internal readonly string OwnerId;
				internal readonly string ZoneId;
				internal readonly KingdomLifecycleTopology Topology;
				internal readonly int X;
				internal readonly int Y;
				internal readonly int Count;
				internal readonly int Capacity;
				internal readonly string Composition;
				internal readonly long Value;
				internal readonly long Revision;
				internal readonly string LastOperationId;

				private Snapshot(IKingdomLifecycleTrustedObservation source)
				{
					Reference = source.Reference;
					ObjectId = source.ObjectId;
					Marker = source.Marker;
					Blueprint = source.Blueprint;
					SettlementId = source.SettlementId;
					OwnerId = source.OwnerId;
					ZoneId = source.ZoneId;
					Topology = source.Topology;
					X = source.X;
					Y = source.Y;
					Count = source.Count;
					Capacity = source.Capacity;
					Composition = source.Composition;
					Value = source.Value;
					Revision = source.Revision;
					LastOperationId = source.LastOperationId;
				}

				internal static Snapshot Capture(IKingdomLifecycleTrustedObservation source)
				{
					return source == null ? null : new Snapshot(source);
				}
			}

			private sealed class CallbackReceipt
			{
				internal readonly Snapshot Before;
				internal readonly Snapshot After;
				internal readonly object Returned;

				private CallbackReceipt(Snapshot before, Snapshot after, object returned)
				{
					Before = before;
					After = after;
					Returned = returned;
				}

				internal static CallbackReceipt Create(Snapshot before,
					Snapshot after, object returned)
				{
					return new CallbackReceipt(before, after, returned);
				}
			}

			internal static KingdomLifecycleResourceLease PreparePhysicalLease(
				KingdomLifecycleBook book, KingdomLifecycleOperation operation,
				KingdomLifecycleResourceKind kind, string scopeId, string subjectId,
				long before, long delta)
			{
				return IsPhysicalResourceKind(kind)
					? PrepareLeaseCore(book, operation, kind, scopeId, subjectId, before, delta)
					: null;
			}

			internal static bool ProveCarrySource(KingdomCarryBook book,
				KingdomCarryOperation operation, KingdomCarrySource source,
				IKingdomLifecycleTrustedWorld world)
			{
				int sourceIndex = IndexOfSource(operation, source);
				int beforeMatches;
				Snapshot before = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, source == null ? null : source.ObjectId,
						StringComparison.Ordinal);
				}, out beforeMatches);
				if (sourceIndex < 0 || beforeMatches != 1 || before == null
					|| !ExactCarrySourceFields(before, source, source.UnitBefore)
					|| !BeginCarryUnitCore(book, operation, source)) return false;
				source.LiveAuthority = before.Reference;
				object returned;
				try { returned = world.InvokeCarryRemoval(before.Reference, 1, source.UnitEventId); }
				catch (Exception) { return false; }
				if (returned == null) return false;
				int afterMatches;
				Snapshot after = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, source.ObjectId, StringComparison.Ordinal);
				}, out afterMatches);
				CallbackReceipt receipt = CallbackReceipt.Create(before, after, returned);
				if (afterMatches != 1 || receipt.After == null
					|| !ExactCarrySourceFields(receipt.After, source, source.UnitAfter)
					|| !ReferenceEquals(receipt.Before.Reference, receipt.Returned)
					|| !ReferenceEquals(receipt.After.Reference, receipt.Returned)) return false;
				source.ReceiptAfterIdMatches = 1;
				source.ReceiptAfterCount = receipt.After.Count;
				source.ReceiptSameReference = true;
				source.ReceiptProofId = CarrySourceReceiptProof(operation, source, sourceIndex);
				source.ReceiptState = KingdomLifecyclePhysicalState.Proved;
				return ConfirmCarryUnitCore(book, operation, source);
			}

			/// <summary>Consumes exactly one unit from the frozen sign object. Intent is durable
			/// before the callback; an interrupted callback is recovered only from the same unique
			/// object id at its exact before/after count.</summary>
			internal static bool ProveExactCarrySign(KingdomCarryBook book,
				KingdomCarryOperation operation, IKingdomLifecycleTrustedWorld world)
			{
				if (!ExactCarryAuthority(book, operation)
					|| operation.AuthorityKind != KingdomCarryAuthorityKind.ExactManifest
					|| operation.Phase != KingdomLifecyclePhase.Prepared) return false;
				int matches;
				Snapshot observed = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, operation.SignObjectId,
						StringComparison.Ordinal);
				}, out matches);
				if (operation.SignReceiptState == KingdomLifecyclePhysicalState.Proved)
					return ExactSignAfter(observed, matches, operation)
						&& ExactCarryAuthority(book, operation);
				if (operation.SignReceiptState == KingdomLifecyclePhysicalState.Prepared)
				{
					if (matches != 1 || !ExactSignBefore(observed, operation)
						|| operation.ManifestRevision == long.MaxValue) return false;
					operation.SignReceiptBeforeMatches = 1;
					operation.SignReceiptBeforeCount = observed.Count;
					operation.SignReceiptState = KingdomLifecyclePhysicalState.Intent;
					operation.LiveAuthority = observed.Reference;
					if (!ExactCarryAuthority(book, operation)) return false;
				}
				else if (operation.SignReceiptState != KingdomLifecyclePhysicalState.Intent)
					return false;

				int expectedAfter = operation.SignCount - 1;
				bool atBefore = matches == 1 && ExactSignBefore(observed, operation);
				bool atAfter = ExactSignAfter(observed, matches, operation);
				if (!atBefore && !atAfter) return false;
				if (atBefore)
				{
					object returned;
					try
					{
						returned = world.InvokeCarrySignRemoval(observed.Reference, 1,
							operation.SignReceiptId);
					}
					catch (Exception) { return false; }
					if (returned == null || !ReferenceEquals(observed.Reference, returned))
						return false;
					observed = ExactObservation(world, delegate(Snapshot value)
					{
						return string.Equals(value.ObjectId, operation.SignObjectId,
							StringComparison.Ordinal);
					}, out matches);
					if (!ExactSignAfter(observed, matches, operation)) return false;
				}
				operation.SignReceiptAfterMatches = expectedAfter == 0 ? 0 : 1;
				operation.SignReceiptAfterCount = expectedAfter;
				operation.SignReceiptSameReference = true;
				operation.SignReceiptProofId = ExactCarrySignProof(operation);
				operation.SignReceiptState = KingdomLifecyclePhysicalState.Proved;
				operation.ManifestRevision++;
				return ExactCarryAuthority(book, operation);
			}

		}
	}
}
