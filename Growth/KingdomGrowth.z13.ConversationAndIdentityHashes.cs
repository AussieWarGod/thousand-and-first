using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{

		private static void WriteConversationBlueprint(BinaryWriter writer,
			ConversationXMLBlueprint blueprint, int depth, ref int remaining)
		{
			if (blueprint == null) { writer.Write((byte)0); return; }
			remaining--;
			writer.Write((byte)1);
			WriteString(writer, blueprint.ID); WriteString(writer, blueprint.Name);
			WriteString(writer, blueprint.Text); WriteString(writer, blueprint.Inherits);
			writer.Write(blueprint.Cardinal); writer.Write(blueprint.References);
			WriteString(writer, blueprint.Distribute);
			writer.Write(blueprint.Qualifier); writer.Write(blueprint.Load);
			if (blueprint.Attributes == null) writer.Write(-1);
			else
			{
				List<string> keys = new List<string>(blueprint.Attributes.Keys);
				keys.Sort(StringComparer.Ordinal);
				writer.Write(keys.Count);
				for (int i = 0; i < keys.Count; i++)
				{
					WriteString(writer, keys[i]);
					WriteString(writer, blueprint.Attributes[keys[i]]);
				}
			}
			if (blueprint.Children == null) writer.Write(-1);
			else
			{
				writer.Write(blueprint.Children.Count);
				for (int i = 0; i < blueprint.Children.Count; i++)
					WriteConversationBlueprint(writer, blueprint.Children[i], depth + 1,
						ref remaining);
			}
		}

		private static string PersonDomainMapHash(KingdomSystem system, GameObject settler,
			KingdomGrowthDomainStepKind kind, bool projectedAfter, string operationId)
		{
			return Hash(delegate(BinaryWriter writer)
			{
				WriteString(writer, "arrival-domain-map"); writer.Write((byte)kind);
				switch (kind)
				{
				case KingdomGrowthDomainStepKind.Enrollment:
					WriteDictionary(writer, system.OriginCounts,
						projectedAfter ? settler.GetStringProperty(ArrivalOriginPlanProperty) : null,
						projectedAfter ? 1 : 0);
					break;
				case KingdomGrowthDomainStepKind.Roster:
					// Roster callback owns only body naming plus its receipt. Resident rows publish
					// once all person-domain callbacks prove, before the arrival clock advances.
					WriteString(writer, projectedAfter ? operationId
						: settler.GetStringProperty(ArrivalRosterReceiptProperty));
					break;
				case KingdomGrowthDomainStepKind.Creed:
					WriteDictionary(writer, system.CreedCounts,
						projectedAfter && !string.IsNullOrEmpty(PlannedCreed(settler))
							? PlannedCreed(settler) : null,
						projectedAfter && !string.IsNullOrEmpty(PlannedCreed(settler)) ? 1 : 0);
					WriteString(writer, projectedAfter ? operationId
						: settler.GetStringProperty(ArrivalCreedReceiptProperty));
					break;
				case KingdomGrowthDomainStepKind.Population:
					writer.Write(projectedAfter ? system.Population + 1 : system.Population);
					break;
				}
			});
		}

		private static KingdomGrowthAccountingSnapshot AccountingSnapshot(KingdomSystem system)
		{
			KingdomLedger ledger = system.Ledger;
			return new KingdomGrowthAccountingSnapshot
			{
				Fetched = ledger.Fetched, UpkeepDrawn = ledger.UpkeepDrawn,
				ArrivalCost = ledger.ArrivalCost, Delivered = ledger.Delivered,
				Harvested = ledger.Harvested, Foraged = ledger.Foraged,
				RationsDrawn = ledger.RationsDrawn, Milled = ledger.Milled,
				HarvestLost = ledger.HarvestLost, Plundered = ledger.Plundered,
				Arrivals = ledger.Arrivals, Departures = ledger.Departures
			};
		}

		private static string AccountingHash(KingdomSystem system, bool projectedAfter)
		{
			KingdomGrowthAccountingSnapshot x = AccountingSnapshot(system);
			if (projectedAfter)
			{
				x.ArrivalCost += KingdomRules.DramsPerArrival;
				x.Arrivals++;
			}
			return Hash(delegate(BinaryWriter writer)
			{
				WriteString(writer, "arrival-accounting-graph");
				writer.Write(x.Fetched); writer.Write(x.UpkeepDrawn); writer.Write(x.ArrivalCost);
				writer.Write(x.Delivered); writer.Write(x.Harvested); writer.Write(x.Foraged);
				writer.Write(x.RationsDrawn); writer.Write(x.Milled); writer.Write(x.HarvestLost);
				writer.Write(x.Plundered); writer.Write(x.Arrivals); writer.Write(x.Departures);
			});
		}

		private static string AccountingMapHash(KingdomSystem system, bool projectedAfter)
		{
			return HashText("arrival-accounting-map", AccountingHash(system, projectedAfter),
				system.Ledger.Notes.Count.ToString(CultureInfo.InvariantCulture));
		}

		private static string PlannedCreed(GameObject settler)
		{
			string value = settler?.GetStringProperty(ArrivalCreedPlanProperty);
			return value == "-" || value == null ? "" : value;
		}

		private static string ArrivalPersonHash(GameObject settler)
		{
			return Hash(delegate(BinaryWriter writer)
			{
				WriteString(writer, "arrival-person"); WriteString(writer, settler?.ID);
				WriteString(writer, settler?.Blueprint); writer.Write(settler?.Count ?? -1);
				WriteString(writer, settler?.GetStringProperty(ArrivalMarkerProperty));
				WriteString(writer, settler?.GetStringProperty(ArrivalOriginPlanProperty));
				WriteString(writer, settler?.GetStringProperty(ArrivalCreedPlanProperty));
				WriteString(writer, settler?.GetStringProperty(ArrivalNamePlanProperty));
				WriteString(writer, settler?.GetStringProperty(ArrivalDatePlanProperty));
				string citizenshipPlan =
					settler?.GetStringProperty(ArrivalCitizenshipPlanProperty);
				if (!string.IsNullOrEmpty(citizenshipPlan))
				{
					WriteString(writer, ArrivalCitizenshipPlanProperty);
					WriteString(writer, citizenshipPlan);
				}
			});
		}

		private static string ArrivalObjectHash(KingdomGrowthArrivalCandidate candidate,
			GameObject settler, KingdomGrowthLocationKind location, int x, int y)
		{
			return HashText("arrival-object-location", settler?.ID ?? candidate?.ObjectId,
				candidate?.Marker,
				candidate?.Blueprint, location.ToString(),
				location == KingdomGrowthLocationKind.Cell ? candidate?.LodgingZoneId : null,
				x.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture),
				ArrivalPersonHash(settler));
		}

		private static string ArrivalZoneIdentityHash(Zone zone, GameObject settler,
			string marker, string escrow, KingdomGrowthLocationKind location, int x, int y)
		{
			return ArrivalTopologyHash(zone, settler?.ID, marker, escrow, location, x, y);
		}

		private static string ArrivalTopologyHash(Zone zone, string objectId,
			string marker, string escrow, KingdomGrowthLocationKind location, int x, int y)
		{
			return HashText("arrival-topology", zone?.ZoneID, objectId, marker, escrow,
				location.ToString(), x.ToString(CultureInfo.InvariantCulture),
				y.ToString(CultureInfo.InvariantCulture));
		}
	}
}
