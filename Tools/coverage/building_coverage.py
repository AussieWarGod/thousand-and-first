"""Link exact building configurations to the existing behavioral matrix.

Mapping is an audit aid, not proof that a scenario exercises a building. No state in
this report grants native acceptance or Beta readiness; retained evidence needs its
existing byte/source/scope audit and the actual behavioral assertions.
"""

from collections import Counter

from building_catalogue import OBLIGATIONS


def report(catalogue, mapping, matrix):
    if (not isinstance(mapping, dict) or set(mapping) != {"schemaVersion", "bindings"}
            or type(mapping["schemaVersion"]) is not int or mapping["schemaVersion"] != 1):
        raise ValueError("building mapping requires schemaVersion 1 and bindings")
    if not isinstance(mapping["bindings"], list):
        raise ValueError("building bindings must be a list")
    configurations = {case["id"]: case for case in catalogue["configurations"]}
    rows = {row["id"]: row for row in matrix["rows"]}
    bindings = {}
    for binding in mapping["bindings"]:
        required = {"configuration", "definitionDigest", "obligation", "rows", "reason"}
        if not isinstance(binding, dict) or set(binding) != required:
            raise ValueError("building binding has missing or unknown fields")
        config = binding["configuration"]
        obligation = binding["obligation"]
        if not isinstance(config, str) or config not in configurations:
            raise ValueError("binding names an unknown configuration")
        if not isinstance(obligation, str) or obligation not in OBLIGATIONS:
            raise ValueError("binding names an unknown obligation")
        if not isinstance(binding["reason"], str) or not binding["reason"].strip():
            raise ValueError("binding needs a concrete scenario/scope explanation")
        digest = binding["definitionDigest"]
        if not isinstance(digest, str) or len(digest) != 64 or any(c not in "0123456789abcdef" for c in digest):
            raise ValueError("binding requires a definition SHA-256")
        refs = binding["rows"]
        if (not isinstance(refs, list) or not refs or any(type(rid) is not int or rid not in rows for rid in refs)
                or len(refs) != len(set(refs))):
            raise ValueError("binding requires distinct existing behavioral row IDs")
        key = (config, obligation)
        if key in bindings:
            raise ValueError("duplicate configuration/obligation binding")
        bindings[key] = binding
    cases, totals = [], Counter()
    for config, case in sorted(configurations.items()):
        obligations = []
        for obligation in OBLIGATIONS:
            binding = bindings.get((config, obligation))
            status, refs = "UNMAPPED", []
            if binding:
                refs = binding["rows"]
                status = "MAPPED"
                if binding["definitionDigest"] != case["definitionDigest"]:
                    status = "STALE_MAPPING"
            totals[status] += 1
            obligations.append({"obligation": obligation, "mappingStatus": status,
                                "rows": [{"id": rid, "status": rows[rid]["status"]} for rid in refs],
                                "reason": binding["reason"] if binding else None})
        cases.append({**case, "obligations": obligations})
    return {"schemaVersion": 1, "scope": catalogue["scope"],
            "nativeAcceptance": "not established by this inventory",
            "sourceDigest": catalogue["sourceDigest"], "sources": catalogue["sources"],
            "summary": {"buildings": catalogue["buildings"], "yardworks": catalogue["yardworks"],
                        "configurations": len(cases), "obligations": sum(totals.values()),
                        "mappingStatuses": dict(sorted(totals.items())),
                        "unboundPlots": sum(case["kind"] == "unbound-plot" for case in cases)},
            "configurations": cases}
