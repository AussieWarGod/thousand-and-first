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
				|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(Intent.EncodedSnapshot)
				|| !ValidLotId(LotId))
			{
				if (Failure == null) Failure = "layout owner, current snapshot, or lot identity is malformed";
				return false;
			}
			try
			{
				Owner.RemoveIntProperty(SchemaProperty);
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
				try { Owner.RemoveIntProperty(SchemaProperty); } catch { }
				return Fail("layout owner receipt write failed: " + exception.Message, out Failure);
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
			if (Owner == null || !Owner.HasIntProperty(SchemaProperty)
				|| Owner.HasStringProperty(SchemaProperty)
				|| Owner.GetIntProperty(SchemaProperty) != LayoutSchema)
				return Fail("layout owner receipt is absent, partial, or unknown", out Failure);
			string fault = Owner.GetStringProperty(FaultProperty);
			if (!string.IsNullOrEmpty(fault))
				return Fail("layout owner is quarantined: " + Bounded(fault), out Failure);
			string lot = Owner.GetStringProperty(LotIdProperty);
			string hash = Owner.GetStringProperty(HashProperty);
			if (!ValidLotId(lot) || hash == null || hash.Length != 64
				|| !KingdomArchitectureRuntime.TryRead(Owner, out Intent, out Failure)
				|| !KingdomArchitectureRuntime.TryDecode(Intent, out Snapshot, out Failure)
				|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(Intent.EncodedSnapshot)
				|| hash != Intent.SnapshotHash)
				return Failure != null ? false : Fail("layout owner scalars disagree with its snapshot",
					out Failure);
			int next = Owner.GetIntProperty(NextLayerProperty);
			if (!Owner.HasIntProperty(NextLayerProperty) || next < 0 || next > 3)
				return Fail("layout owner stage is absent or malformed", out Failure);
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				int state = Owner.GetIntProperty(OutputState(placement));
				string id = Owner.GetStringProperty(OutputId(placement));
				if (state < 0 || state > 2 || (state == 0 && !string.IsNullOrEmpty(id))
					|| (state > 0 && (string.IsNullOrEmpty(id)
						|| id.Length > KingdomConstructionRules.MaxSubjectChars))
					|| ((int)placement.Layer < next && state != 2))
					return Fail("layout slot receipt " + placement.Slot + " is malformed", out Failure);
			}
			LotId = lot;
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
			if (!KingdomArchitectureRuntime.TryCopyFrozen(Source, Target, out Failure)) return false;
			try
			{
				Target.RemoveIntProperty(SchemaProperty);
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
				try { Target.RemoveIntProperty(SchemaProperty); } catch { }
				return Fail("layout owner copy failed: " + exception.Message, out Failure);
			}
			KingdomArchitectureIntent ignoredIntent;
			ArchitectureLayoutSnapshot ignoredSnapshot;
			string checkedLot;
			return TryReadOwner(Target, out ignoredIntent, out ignoredSnapshot, out checkedLot,
				out Failure) && checkedLot == lot;
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
				if (!cell.Claim) continue;
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
