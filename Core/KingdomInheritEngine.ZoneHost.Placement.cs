using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#if !TAF_TESTS
using XRL;
using XRL.World;
using XRL.World.Parts;
#endif

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritEngine
	{
#if !TAF_TESTS
		private sealed partial class ZoneHost
		{
			public bool IsFreshEmpty(object Handle)
			{
				GameObject obj = Handle as GameObject;
				if (!IsEmptyObject(obj)) return false;
				return obj == null || !obj.HasIntProperty(KingdomArchitectureStamper.SchemaProperty)
					|| obj.CurrentCell == null || EmptyArchitecture(obj);
			}

			public bool TryPlace(object Handle, int X, int Y, out string Failure)
			{
				Failure = "";
				GameObject obj = Handle as GameObject;
				Cell cell = Zone.GetCell(X, Y);
				if (obj == null || cell == null)
				{
					Failure = "the inherited object or its prepared cell is missing";
					return false;
				}
				cell.AddObject(obj, Forced: false, System: true, IgnoreGravity: true, NoStack: true,
					Silent: true, Repaint: false);
				if (obj.CurrentCell != cell)
				{
					Failure = "the inherited object was rejected by its prepared cell";
					return false;
				}
				if (obj.HasIntProperty(KingdomArchitectureStamper.SchemaProperty))
				{
					if (!KingdomArchitectureStamper.TryStageLayer(obj, Zone,
						ArchitectureLayer.Ground, out Failure)
						|| !KingdomArchitectureStamper.TryStageLayer(obj, Zone,
							ArchitectureLayer.Structure, out Failure)
						|| !KingdomArchitectureStamper.TryStageLayer(obj, Zone,
							ArchitectureLayer.Object, out Failure)
						|| !ScrubArchitecture(obj, out Failure)
						|| !KingdomArchitectureStamper.TryVerifyComplete(obj, Zone, out Failure))
						return false;
				}
				return true;
			}

			public bool Discard(object Handle)
			{
				GameObject obj = Handle as GameObject;
				if (obj == null)
				{
					return true;
				}
				bool architectureClean = DiscardArchitecture(obj);
				obj.Obliterate(null, Silent: true);
				return architectureClean && obj.CurrentCell == null;
			}

			public bool TryWriteApplicationMarker(string Marker, out string Failure)
			{
				Failure = "";
				string existing = ReadApplicationMarker();
				if (!string.IsNullOrEmpty(existing) && existing != Marker)
				{
					Failure = "the zone acquired a different inherited-site marker";
					return false;
				}
				Zone.SetZoneProperty(ZoneMarkerProperty, Marker);
				return ReadApplicationMarker() == Marker;
			}

			public bool TryRemoveApplicationMarker(string Marker)
			{
				if (ReadApplicationMarker() == Marker)
				{
					Zone.RemoveZoneProperty(ZoneMarkerProperty);
				}
				return ReadApplicationMarker().Length == 0;
			}

			private void MarkConnection(ZoneConnection Connection)
			{
				if (Connection != null && Connection.X >= 0 && Connection.Y >= 0
					&& Connection.X < Zone.Width && Connection.Y < Zone.Height)
				{
					Connections[Connection.X, Connection.Y] = true;
				}
			}

			private static bool IsOccupied(Cell Cell)
			{
				for (int i = 0; i < Cell.Objects.Count; i++)
				{
					GameObject obj = Cell.Objects[i];
					if ((obj.Render != null && obj.Render.RenderLayer > 5)
						|| obj.IsCombatObject())
					{
						return true;
					}
				}
				return false;
			}

			private static bool IsEmptyObject(GameObject Object)
			{
				if (Object == null || Object.GetContents(new List<GameObject>()).Count != 0)
				{
					return false;
				}
				LiquidVolume liquid = Object.GetPart<LiquidVolume>();
				Capacitor capacitor = Object.GetPart<Capacitor>();
				Clockwork clockwork = Object.GetPart<Clockwork>();
				Circuitry circuitry = Object.GetPart<Circuitry>();
				return (liquid == null || liquid.IsEmpty())
					&& (capacitor == null || capacitor.Charge == 0)
					&& (clockwork == null || clockwork.Charge == 0)
					&& (circuitry == null || (circuitry.Charge == 0 && circuitry.IncomingCharge == 0));
			}

			private static void Scrub(GameObject Object)
			{
				if (Object == null) return;
				Object.StripContents(KeepNatural: false, Silent: true);
				LiquidVolume liquid = Object.GetPart<LiquidVolume>();
				if (liquid != null && !liquid.IsEmpty()) liquid.Empty();
				Capacitor capacitor = Object.GetPart<Capacitor>();
				if (capacitor != null) capacitor.Charge = 0;
				Clockwork clockwork = Object.GetPart<Clockwork>();
				if (clockwork != null) clockwork.Charge = 0;
				Circuitry circuitry = Object.GetPart<Circuitry>();
				if (circuitry != null)
				{
					circuitry.Charge = 0;
					circuitry.IncomingCharge = 0;
				}
			}

		}
#endif

	}
}
