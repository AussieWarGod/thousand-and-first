using System;
using System.Collections.Generic;
#if TAF_TESTS
using System.IO;
using System.Text;
#endif

using ThousandAndFirst.Simulation.Kernel;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomJobRules
	{
		/// <summary>The key every draw about one delivery hangs off. <c>rulesVersion</c> frozen at
		/// creation, the settlement's id, the delivery lane, and the job id as the occurrence
		/// ordinal (&sect;2.4).</summary>
		internal static bool TryKey(string settlementId, int jobId, out SemanticEventKey key, out KingdomCityFault fault)
		{
			key = default(SemanticEventKey);
			if (jobId <= 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			KernelFaultCode kernelFault;
			if (!SemanticEventKey.TryCreate(KingdomCityRules.RulesVersion, settlementId, DeliveryStreamId, DeliveryKindCode, (ulong)jobId, out key, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Which cell along an edge the carrier walks in by, drawn on the delivery lane.
		/// <para>
		/// <b>The edge itself is not drawn</b> &mdash; it is the one facing the source, which is a
		/// fact rather than a choice, and a fact cannot disagree with where the founder comes out.
		/// What is drawn is where along that edge, which is flavour and is therefore allowed one.
		/// </para>
		/// </summary>
		internal static bool TryDrawEntryCell(KernelSeed128 seed, string settlementId, int jobId, KingdomZoneStep edge, int width, int height, out short x, out short y, out KingdomCityFault fault)
		{
			x = 0;
			y = 0;
			if (width <= 2 || height <= 2 || (edge != KingdomZoneStep.North
				&& edge != KingdomZoneStep.South && edge != KingdomZoneStep.East
				&& edge != KingdomZoneStep.West))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			SemanticEventKey key;
			if (!TryKey(settlementId, jobId, out key, out fault))
			{
				return false;
			}
			bool vertical = (edge == KingdomZoneStep.North || edge == KingdomZoneStep.South);
			int span = vertical ? width : height;
			ulong along;
			KernelFaultCode kernelFault;
			if (!CounterRandom.TryDrawBelow(seed, key, EntryCellDrawIndex, (ulong)(span - 2), out along, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			int offset = (int)along + 1;
			switch (edge)
			{
			case KingdomZoneStep.North:
				x = (short)offset;
				y = 0;
				break;
			case KingdomZoneStep.South:
				x = (short)offset;
				y = (short)(height - 1);
				break;
			case KingdomZoneStep.West:
				x = 0;
				y = (short)offset;
				break;
			default:
				// East. Invalid and vertical steps were refused before the draw.
				x = (short)(width - 1);
				y = (short)offset;
				break;
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>A vanilla zone's own dimensions, for the edge arithmetic. Read off the live
		/// zone wherever one is in hand; these are what a zone that will not answer is taken to be.
		/// </summary>
		internal const int ZoneWidth = 80;

		internal const int ZoneHeight = 25;

		/// <summary>
		/// The cell the engine's own zone connection maps an exit cell to. Not a choice, so it
		/// needs no draw and cannot disagree with where the founder comes out (&sect;3.7).
		/// </summary>
		internal static void Mirror(short x, short y, KingdomZoneStep edge, int width, int height, out short mirrorX, out short mirrorY)
		{
			if (!TryMirror(x, y, edge, width, height, out mirrorX, out mirrorY))
			{
				mirrorX = x;
				mirrorY = y;
			}
		}

		/// <summary>Total horizontal connection mapping. Vertical travel uses the exact paired
		/// shaft receipt and no wall fallback; an unknown step refuses rather than becoming east.</summary>
		internal static bool TryMirror(short x, short y, KingdomZoneStep edge, int width, int height,
			out short mirrorX, out short mirrorY)
		{
			int w = (width > 2) ? width : ZoneWidth;
			int h = (height > 2) ? height : ZoneHeight;
			switch (edge)
			{
			case KingdomZoneStep.North:
				mirrorX = x;
				mirrorY = (short)(h - 1);
				return true;
			case KingdomZoneStep.South:
				mirrorX = x;
				mirrorY = 0;
				return true;
			case KingdomZoneStep.West:
				mirrorX = (short)(w - 1);
				mirrorY = y;
				return true;
			case KingdomZoneStep.East:
				mirrorX = 0;
				mirrorY = y;
				return true;
			default:
				mirrorX = x;
				mirrorY = y;
				return false;
			}
		}

		/// <summary>Which horizontal edge joins two exact neighbouring zones. Non-neighbours,
		/// malformed ids, and vertical pairs return <see cref="KingdomZoneStep.None"/>. A shaft is
		/// an exact authored cell and must never be laundered into a west-wall fallback.</summary>
		internal static KingdomZoneStep EdgeToward(string here, string source)
		{
			string world;
			int hx;
			int hy;
			int hz;
			string otherWorld;
			int sx;
			int sy;
			int sz;
			if (string.IsNullOrEmpty(source)
				|| !KingdomRules.TryParseZoneID(here, out world, out hx, out hy, out hz)
				|| !KingdomRules.TryParseZoneID(source, out otherWorld, out sx, out sy, out sz)
				|| !string.Equals(world, otherWorld, StringComparison.Ordinal))
			{
				return KingdomZoneStep.None;
			}
			KingdomZoneStep step = KingdomDistanceRules.StepBetween(
				new KingdomZoneNode(here, hx, hy, hz),
				new KingdomZoneNode(source, sx, sy, sz));
			return (step == KingdomZoneStep.Up || step == KingdomZoneStep.Down)
				? KingdomZoneStep.None : step;
		}

		/// <summary>Where the carrier says they are from, drawn on the same key at its own draw
		/// index &mdash; so adding or removing this draw cannot perturb the entry cell.</summary>
		internal static bool TryDrawOrigin(KernelSeed128 seed, string settlementId, int jobId, int originCount, out int originCode, out KingdomCityFault fault)
		{
			originCode = KingdomResidentRules.NoOrigin;
			if (originCount <= 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			SemanticEventKey key;
			if (!TryKey(settlementId, jobId, out key, out fault))
			{
				return false;
			}
			ulong drawn;
			KernelFaultCode kernelFault;
			if (!CounterRandom.TryDrawBelow(seed, key, OriginDrawIndex, (ulong)originCount, out drawn, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			originCode = (int)drawn + 1;
			fault = KingdomCityFault.None;
			return true;
		}
	}
}
