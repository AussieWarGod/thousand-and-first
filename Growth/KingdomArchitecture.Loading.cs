using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitecture
	{
		/// <summary>
		/// Rebuilds from the exact KingdomBuildings entries already merged by the caller. Calling
		/// KingdomData from here would recurse into its in-progress load, so this enumerable is the
		/// only building authority accepted.
		/// </summary>
		public static void Reload(IEnumerable<KingdomRules.BuildEntry> Buildings)
		{
			if (reloading)
			{
				MetricsManager.LogError("ThousandAndFirst KingdomArchitectures: recursive reload refused");
				return;
			}
			reloading = true;
			LoadState next = new LoadState();
			try
			{
				FreezeBuildings(next, Buildings);
				LoadXml(next);
				Materialise(next);
				next.Loaded = true;
			}
			catch (Exception exception)
			{
				AddFault(next, "catalogue", "load failed: " + exception.Message);
				// A catalogue-wide exception has no exact entry boundary. Publish no partial index.
				next.Records.Clear();
				next.RecordsByBuild.Clear();
				next.RecordsByBinding.Clear();
				next.Loaded = true;
			}
			finally
			{
				state = next;
				reloading = false;
			}
			ReportFaults(next);
		}

		/// <summary>Writes the current bounded named report to Qud's error log.</summary>
		public static void ReportFaults()
		{
			ReportFaults(state);
		}

		private static void ReportFaults(LoadState State)
		{
			for (int i = 0; i < State.Faults.Count; i++)
				MetricsManager.LogError("ThousandAndFirst KingdomArchitectures: " + State.Faults[i]);
		}

		private static void FreezeBuildings(LoadState State,
			IEnumerable<KingdomRules.BuildEntry> Buildings)
		{
			if (Buildings == null)
			{
				AddFault(State, "buildings", "the merged KingdomBuildings view is absent");
				return;
			}
			foreach (KingdomRules.BuildEntry entry in Buildings)
			{
				if (entry == null || !ValidKey(entry.Key))
				{
					AddFault(State, "building", "an unnamed or malformed merged building was supplied");
					continue;
				}
				if (State.Buildings.ContainsKey(entry.Key))
				{
					AddFault(State, "building " + entry.Key, "the merged view contains the key twice");
					continue;
				}
				FrozenBuilding frozen = new FrozenBuilding
				{
					Key = entry.Key,
					Blueprint = entry.Blueprint,
					Category = Fold(entry.Category)
				};
				KingdomPlotRules.PlotSpec spec;
				if (KingdomPlots.TryGetSpec(entry.Key, out spec) && spec != null
					&& TryLotSize(spec.Size, out ArchitectureLotSize size))
				{
					frozen.HasPlot = true;
					frozen.LotSize = size;
					frozen.FootprintWidth = spec.FootprintWidth;
					frozen.FootprintHeight = spec.FootprintHeight;
					frozen.Roof = spec.Roof;
				}
				State.Buildings.Add(entry.Key, frozen);
			}
		}

		private static void LoadXml(LoadState State)
		{
			int streams = 0;
			foreach (XmlDataHelper xml in DataManager.YieldXMLStreamsWithRoot("KingdomArchitectures"))
			{
				streams++;
				if (streams > MaxStreams)
				{
					AddFault(State, "catalogue", "more than " + MaxStreams
						+ " KingdomArchitectures streams were supplied");
					break;
				}
				try { ParseStream(State, xml); }
				catch (Exception exception)
				{
					AddFault(State, "stream " + streams.ToString(CultureInfo.InvariantCulture),
						"XML parse failed: " + exception.Message);
				}
			}
			if (streams == 0) AddFault(State, "catalogue", "no KingdomArchitectures schema-1 stream was found");
		}

		private static void ParseStream(LoadState State, XmlDataHelper Xml)
		{
			bool foundRoot = false;
			Dictionary<string, Action<XmlDataHelper>> roots =
				new Dictionary<string, Action<XmlDataHelper>>(StringComparer.Ordinal)
				{
					{ "KingdomArchitectures", delegate(XmlDataHelper root)
						{
							foundRoot = true;
							HandleRoot(State, root);
						} }
				};
			Xml.HandleNodes(roots, delegate(XmlDataHelper unknown)
			{
				AddFault(State, "root", "expected uppercase KingdomArchitectures at " + Source(unknown));
				Skip(unknown);
			});
			if (!foundRoot) AddFault(State, "root", "stream did not contain uppercase KingdomArchitectures");
		}

		private static void HandleRoot(LoadState State, XmlDataHelper Xml)
		{
			string schema = Xml.GetAttribute("Schema");
			if (schema != Schema.ToString(CultureInfo.InvariantCulture))
			{
				AddFault(State, "root", "unsupported or absent Schema at " + Source(Xml));
				Skip(Xml);
				return;
			}
			Dictionary<string, Action<XmlDataHelper>> nodes =
				new Dictionary<string, Action<XmlDataHelper>>(StringComparer.Ordinal)
				{
					{ "palette", delegate(XmlDataHelper child) { HandlePalette(State, child); } },
					{ "pose", delegate(XmlDataHelper child) { HandlePose(State, child); } },
					{ "map", delegate(XmlDataHelper child) { HandleMap(State, child); } },
					{ "plan", delegate(XmlDataHelper child) { HandlePlan(State, child); } }
				};
			Xml.HandleNodes(nodes, delegate(XmlDataHelper unknown) { Unknown(State, unknown); });
		}

	}
}
