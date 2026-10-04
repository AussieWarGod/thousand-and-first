"""Strict host reader for the higher-heart witness and its retained physical fact rows."""
import base64
import hashlib
import re
import struct
import unicodedata

PREFIX = b"taf-camp-heart-chain-save-v1:"
FAMILY = b"taf-camp-heart-chain-save-"
FACT_PREFIX = b"taf-heart-chain-facts-v1:"
DOMAINS = ("jobs", "residents", "support", "custody")
MAX_SNAPSHOT = 32768
MAX_FACT_BYTES = 4194304
MAX_FACT_WIRE = len(FACT_PREFIX) + 4 * ((MAX_FACT_BYTES + 2) // 3)
SHA = re.compile(r"[0-9a-f]{64}\Z")
GUID = re.compile(r"[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}\Z")


def require(value, message):
    if not value:
        raise ValueError(message)


def valid_text(value, limit):
    require(len(value.encode("utf-16-le")) // 2 <= limit, "field exceeds UTF-16 character bound")
    require(not any(unicodedata.category(c) == "Cc" for c in value), "field contains control characters")
    return value


def payload(wire, prefix, limit):
    require(isinstance(wire, bytes) and len(wire) <= limit and wire.startswith(prefix), "unknown or oversized witness")
    try:
        body = base64.b64decode(wire[len(prefix):], validate=True)
    except ValueError as error:
        raise ValueError("invalid witness base64") from error
    require(prefix + base64.b64encode(body) == wire, "noncanonical witness base64")
    return body


class Reader:
    def __init__(self, body):
        self.body, self.at = body, 0

    def take(self, count):
        require(0 <= count <= len(self.body) - self.at, "truncated witness")
        value = self.body[self.at:self.at + count]
        self.at += count
        return value

    def number(self, wide=False):
        return struct.unpack("<q" if wide else "<i", self.take(8 if wide else 4))[0]

    def text(self, max_chars=1024, max_bytes=4096, nullable=False, empty=False):
        count = self.number()
        if count == -1 and nullable:
            return None
        require((0 if empty else 1) <= count <= max_bytes, "invalid field byte length")
        try:
            value = self.take(count).decode("utf-8", errors="strict")
        except UnicodeError as error:
            raise ValueError("invalid witness UTF-8") from error
        return valid_text(value, max_chars)

    def finished(self):
        require(self.at == len(self.body), "trailing witness bytes")


def decode_snapshot(wire):
    reader = Reader(payload(wire, PREFIX, MAX_SNAPSHOT))
    require(reader.number() == 0x31434354 and reader.number() == 1, "unknown higher-heart snapshot version")
    names = ("game_id", "realm_id", "city_id", "zone_id", "resident_id", "job_id",
             "jobs_digest", "residents_digest", "support_digest", "custody_digest")
    result = {name: reader.text() for name in names}
    require(GUID.fullmatch(result["game_id"]), "noncanonical saved game identity")
    for domain in DOMAINS:
        require(SHA.fullmatch(result[domain + "_digest"]), "malformed fact digest")
    ids = {result["resident_id"]}
    for name in ("heart", "basin", "store", "track"):
        anchor = dict(id=reader.text(), x=reader.number(), y=reader.number())
        require(anchor["id"] not in ids and 0 <= anchor["x"] < 4096 and 0 <= anchor["y"] < 4096,
                "anchor aliases another identity or is out of bounds")
        ids.add(anchor["id"])
        result[name] = anchor
    for name in ("rung", "population", "water", "food"):
        result[name] = reader.number()
    result["turns"], result["time_ticks"] = reader.number(True), reader.number(True)
    reader.finished()
    require(result["rung"] in (3, 4) and result["population"] > 0
            and all(result[name] >= 0 for name in ("water", "food", "turns", "time_ticks")),
            "invalid higher-heart counters or rung")
    return result


def decode_facts(wire, domain, digest):
    require(domain in DOMAINS and SHA.fullmatch(digest), "unknown fact domain or digest")
    require(hashlib.sha256(wire).hexdigest() == digest, "retained facts differ from saved digest: " + domain)
    body = payload(wire, FACT_PREFIX, MAX_FACT_WIRE)
    require(len(body) <= MAX_FACT_BYTES, "fact payload exceeds aggregate bound")
    reader = Reader(body)
    require(reader.number() == 1 and reader.text(max_chars=64, max_bytes=256) == domain,
            "fact domain/version differs")
    count = reader.number()
    require(0 <= count <= 4096, "invalid fact row count")
    rows, prior = {}, None
    for _ in range(count):
        fields = reader.number()
        require(1 <= fields <= 64, "invalid fact field count")
        row = [reader.text(max_chars=524288, max_bytes=MAX_FACT_BYTES, nullable=True, empty=True)
               for _ in range(fields)]
        require(row[0] is not None and row[0] != "", "fact row identity absent")
        valid_text(row[0], 1024)
        ordinal = row[0].encode("utf-16-be")
        require(prior is None or ordinal > prior, "fact identities duplicated or not in canonical order")
        prior = ordinal
        rows[row[0]] = row[1:]
    reader.finished()
    return rows


def fact_name(domain, phase=None):
    require(domain in DOMAINS and phase in (None, "preactivation", "activated", "completed"), "unknown fact filename")
    return "camp-heart-chain-" + ((phase + "-") if phase else "") + domain + "-facts.txt"
