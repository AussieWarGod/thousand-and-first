using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		/// <summary>Freeze layout ownership on detached works. Schema is final commit marker.</summary>
		public static bool TryInitializeOwner(GameObject Owner, KingdomArchitectureIntent Intent,
			string LotId, out string Failure)
		{
			Failure = null;
			ArchitectureLayoutSnapshot snapshot;
			if (Owner == null || !KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out Failure)
				|| !KingdomArchitectureRules.IsManagedSnapshotEncoding(Intent.EncodedSnapshot)
				|| !ValidLotId(LotId))
			{
				if (Failure == null) Failure = "layout owner, current snapshot, or lot identity is malformed";
				return false;
			}
			KingdomArchitectureIntent frozen;
			if (!KingdomArchitectureRuntime.TryRead(Owner, out frozen, out Failure)
				|| !SameOwnerIntent(frozen, Intent))
				return Failure != null ? false : Fail(
					"layout owner carries another frozen architecture receipt", out Failure);
			if (Owner.HasIntProperty(SchemaProperty))
			{
				KingdomArchitectureIntent observed;
				ArchitectureLayoutSnapshot observedSnapshot;
				string observedLot;
				return TryReadOwner(Owner, out observed, out observedSnapshot, out observedLot,
					out Failure) && observedLot == LotId && SameOwnerIntent(observed, Intent);
			}
			if (!TryAcceptNewOwnerPrefix(Owner, Intent, snapshot, LotId, 0, false, null,
				out Failure)) return false;
			try
			{
				Owner.SetStringProperty(LotIdProperty, LotId);
				Owner.SetStringProperty(HashProperty, Intent.SnapshotHash);
				Owner.SetIntProperty(NextLayerProperty, 0);
				Owner.SetStringProperty(FaultProperty, null, RemoveIfNull: true);
				for (int i = 0; i < snapshot.Placements.Count; i++)
				{
					Owner.SetStringProperty(OutputId(snapshot.Placements[i]), null, RemoveIfNull: true);
					Owner.RemoveIntProperty(OutputState(snapshot.Placements[i]));
				}
				Owner.SetIntProperty(SchemaProperty, LayoutSchema);
			}
			catch (Exception exception)
			{
				KingdomArchitectureIntent caughtIntent;
				ArchitectureLayoutSnapshot caughtSnapshot;
				string caughtLot;
				if (TryReadOwner(Owner, out caughtIntent, out caughtSnapshot, out caughtLot,
					out _) && caughtLot == LotId && SameOwnerIntent(caughtIntent, Intent)) return true;
				return Fail("layout owner receipt publication remains retryable: "
					+ exception.Message, out Failure);
			}
			KingdomArchitectureIntent readIntent;
			ArchitectureLayoutSnapshot readSnapshot;
			string readLot;
			return TryReadOwner(Owner, out readIntent, out readSnapshot, out readLot, out Failure)
				&& readLot == LotId;
		}

		public static bool TryReadOwner(GameObject Owner, out KingdomArchitectureIntent Intent,
			out ArchitectureLayoutSnapshot Snapshot, out string LotId, out string Failure)
		{
			Intent = null;
			Snapshot = null;
			LotId = null;
			Failure = null;
			if (!TryReadOwnerHeader(Owner, out Intent, out Snapshot, out LotId, out Failure))
				return false;
			int next = Owner.GetIntProperty(NextLayerProperty);
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				ArchitectureOutputPrefix prefix = OwnerOutputPrefix(Owner, placement, null);
				int state = Owner.GetIntProperty(OutputState(placement));
				string id = Owner.GetStringProperty(OutputId(placement));
				if ((prefix != ArchitectureOutputPrefix.Empty
						&& prefix != ArchitectureOutputPrefix.Published
						&& prefix != ArchitectureOutputPrefix.Settled)
					|| (!string.IsNullOrEmpty(id)
						&& id.Length > KingdomConstructionRules.MaxSubjectChars)
					|| ((int)placement.Layer < next && state != 2))
					return Fail("layout slot receipt " + placement.Slot + " is malformed", out Failure);
			}
			return true;
		}

		/// <summary>Copy complete frozen authority from works to detached final root.</summary>
		public static bool TryCopyFrozenOwner(GameObject Source, GameObject Target, out string Failure)
		{
			Failure = null;
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (Target == null || !TryReadOwner(Source, out intent, out snapshot, out lot, out Failure)
				|| Source.GetIntProperty(NextLayerProperty) != 3)
			{
				if (Failure == null) Failure = "only a complete layout owner may become a final root";
				return false;
			}
			if (Target.HasIntProperty(SchemaProperty))
				return ExactCopiedOwner(Target, Source, intent, snapshot, lot, out Failure);
			if (!TryAcceptNewOwnerPrefix(Target, intent, snapshot, lot, 3, true, Source,
				out Failure)) return false;
			if (!KingdomArchitectureRuntime.TryCopyFrozen(Source, Target, out Failure)) return false;
			try
			{
				Target.SetStringProperty(LotIdProperty, lot);
				Target.SetStringProperty(HashProperty, intent.SnapshotHash);
				Target.SetIntProperty(NextLayerProperty, 3);
				Target.SetStringProperty(FaultProperty, null, RemoveIfNull: true);
				for (int i = 0; i < snapshot.Placements.Count; i++)
				{
					ArchitecturePlacement placement = snapshot.Placements[i];
					Target.SetStringProperty(OutputId(placement),
						Source.GetStringProperty(OutputId(placement)));
					Target.SetIntProperty(OutputState(placement), 2);
				}
				Target.SetIntProperty(SchemaProperty, LayoutSchema);
			}
			catch (Exception exception)
			{
				string ignored;
				if (ExactCopiedOwner(Target, Source, intent, snapshot, lot, out ignored)) return true;
				return Fail("layout owner copy remains retryable: " + exception.Message, out Failure);
			}
			return ExactCopiedOwner(Target, Source, intent, snapshot, lot, out Failure);
		}

		public static bool TryManagedCells(KingdomArchitectureIntent Intent, Zone Z,
			out HashSet<int> Cells, out string Failure)
		{
			Cells = null;
			Failure = null;
			ArchitectureLayoutSnapshot snapshot;
			if (Z == null || !KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out Failure))
				return false;
			HashSet<int> result = new HashSet<int>();
			for (int i = 0; i < snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = snapshot.Cells[i];
				if (!KingdomArchitectureRules.IsClaimed(cell.Claim)) continue;
				int x;
				int y;
				if (!KingdomArchitectureRuntime.TryWorldCell(snapshot, Intent.Rect, cell,
					out x, out y, out Failure)) return false;
				result.Add(y * Z.Width + x);
			}
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				int x;
				int y;
				if (!KingdomArchitectureRuntime.TryWorldPlacement(snapshot, Intent.Rect,
					snapshot.Placements[i], out x, out y, out Failure)) return false;
				result.Add(y * Z.Width + x);
			}
			Cells = result;
			return true;
		}

		/// <summary>Stamp one exact layer. Interruption after output-ID publication fails closed.</summary>
	}
}
