using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;
using XRL.World.Tinkering;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		private static bool TryReadHeirs(KingdomSystem System, out List<HeirRuntime> Result)
		{
			Result = new List<HeirRuntime>();
			if (!ReadHeirs(System.City, System.OfficeHolderResidentId,
				System.OfficeHolderName, Result))
			{
				return false;
			}
			if (System.Away != null)
			{
				if (!ReadHeirs(System.Away.City, System.Away.OfficeHolderResidentId,
					System.Away.OfficeHolderName, Result))
				{
					return false;
				}
			}
			return true;
		}

		private static bool ReadHeirs(KingdomCityBook Book, int OfficeHolderResidentId,
			string LegacyOfficeHolder, List<HeirRuntime> Result)
		{
			KingdomCityState state;
			KingdomCityFault fault = default(KingdomCityFault);
			if (Book == null || !Book.TryRead(out state, out fault))
			{
				KingdomLog.Log("succession: a city book could not be read while choosing the heir (" + fault + ")");
				return false;
			}
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row) || row.Standing != KingdomResidentStanding.Resident)
				{
					continue;
				}
				KingdomHeir rule = new KingdomHeir(row.Name, row.ArrivedTick, null, row.KeptCreeds,
					onTheRoll: true,
					holdsOffice: OfficeHolderResidentId > 0
						? row.ResidentId == OfficeHolderResidentId
						: !string.IsNullOrEmpty(LegacyOfficeHolder)
							&& string.Equals(row.Name, LegacyOfficeHolder,
								StringComparison.Ordinal),
					row.BoundZoneId, row.ResidentId);
				Result.Add(new HeirRuntime(rule));
			}
			return true;
		}

		private static void JudgeActualNews(KingdomSystem System, Zone DeathZone, out int Days, out NewsRoad Road)
		{
			string deathZoneId = DeathZone?.ZoneID;
			string seatZoneId = (System.ClaimedZones != null && System.ClaimedZones.Count > 0)
				? System.ClaimedZones[0] : null;
			string deathWorld = null;
			string seatWorld = null;
			int dwx = 0;
			int dwy = 0;
			int dzx = 0;
			int dzy = 0;
			int dz = 0;
			int swx = 0;
			int swy = 0;
			int szx = 0;
			int szy = 0;
			int sz = 0;
			bool deathParsed = TryParseZone(deathZoneId,
				out deathWorld, out dwx, out dwy, out dzx, out dzy, out dz);
			bool seatParsed = TryParseZone(seatZoneId,
				out seatWorld, out swx, out swy, out szx, out szy, out sz);
			bool onOwnedGround = !string.IsNullOrEmpty(deathZoneId)
				&& ((System.ClaimedZones != null && System.ClaimedZones.Contains(deathZoneId))
					|| (System.Away?.ClaimedZones != null
						&& System.Away.ClaimedZones.Contains(deathZoneId)));
			bool sameWorld = onOwnedGround || (deathParsed && seatParsed
				&& string.Equals(deathWorld, seatWorld, StringComparison.Ordinal));
			int dx = 0;
			int dy = 0;
			int depth = 0;
			if (sameWorld && !onOwnedGround)
			{
				dx = SaturatedDifference((long)dwx * 3L + dzx, (long)swx * 3L + szx);
				dy = SaturatedDifference((long)dwy * 3L + dzy, (long)swy * 3L + szy);
				depth = SaturatedDifference(dz, sz);
			}
			bool arch = ArchAnswersSeat(System, DeathZone);
			KingdomSuccessionRules.JudgeNews(arch, sameWorld, dx, dy, depth, out Days, out Road);
		}

		private static int SaturatedDifference(long A, long B)
		{
			long difference = A >= B ? A - B : B - A;
			return difference >= int.MaxValue ? int.MaxValue : (int)difference;
		}

		private static bool TryParseZone(string ZoneId, out string World, out int WorldX,
			out int WorldY, out int ZoneX, out int ZoneY, out int ZoneZ)
		{
			World = null;
			WorldX = 0;
			WorldY = 0;
			ZoneX = 0;
			ZoneY = 0;
			ZoneZ = 0;
			if (string.IsNullOrEmpty(ZoneId))
			{
				return false;
			}
			try
			{
				return ZoneID.Parse(ZoneId, out World, out WorldX, out WorldY,
					out ZoneX, out ZoneY, out ZoneZ);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("succession: zone id could not price news (" + ex.GetType().Name + ")");
				return false;
			}
		}

		private static bool ArchAnswersSeat(KingdomSystem System, Zone Zone)
		{
			try
			{
				if (Zone == null || The.Game == null || !KingdomPower.Enabled)
				{
					return false;
				}
				KingdomGateRow[] rows;
				int dropped;
				KingdomMirrorGateRules.TryParseRegister(
					The.Game.GetStringGameState(KingdomMirrorGateRules.RegisterStateKey, ""), out rows, out dropped);
				foreach (GameObject obj in Zone.GetObjects())
				{
					r_KingdomMirrorGate gate = obj?.GetPart<r_KingdomMirrorGate>();
					if (gate == null || gate.Dark)
					{
						continue;
					}
					KingdomMirrorGate.Anchor(gate);
					int here = KingdomMirrorGateRules.IndexOfKey(rows, gate.LocationKey);
					if (here < 0)
					{
						continue;
					}
					int there = KingdomMirrorGateRules.IndexOfKey(rows, rows[here].Partner);
					if (there >= 0 && string.Equals(rows[there].City, System.SeatName,
						StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
				return false;
			}
			catch (Exception ex)
			{
				KingdomLog.Log("succession: arch news fact unavailable (" + ex.GetType().Name + ")");
				return false;
			}
		}

	}
}
