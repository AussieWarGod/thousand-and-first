#!/usr/bin/env python3
"""Local draft editor and room review over the shipped architecture XML."""

import argparse
import json
import secrets
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

from layout_studio_data import Studio
from layout_studio_view import svg


def make_server(studio, port=0):
    token = secrets.token_urlsafe(24)
    page = (Path(__file__).parent / "layout-studio.html").read_text()
    page = page.replace("__SESSION_TOKEN__", token)

    class Handler(BaseHTTPRequestHandler):
        def setup(self):
            super().setup()
            self.connection.settimeout(10)

        def log_message(self, *_args):
            pass

        def send(self, status, data, mime="application/json"):
            raw = data.encode() if isinstance(data, str) else json.dumps(data).encode()
            self.send_response(status)
            self.send_header("Content-Type", mime + "; charset=utf-8")
            self.send_header("Content-Length", str(len(raw)))
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.end_headers()
            self.wfile.write(raw)

        def local(self):
            expected = f"127.0.0.1:{self.server.server_port}"
            return self.headers.get("Host") == expected

        def do_GET(self):
            if not self.local():
                return self.send(403, {"error": "loopback host required"})
            if self.path == "/":
                return self.send(200, page, "text/html")
            if self.path == "/api/cases":
                return self.send(200, studio.cases)
            return self.send(404, {"error": "not found"})

        def do_POST(self):
            if not self.local() or self.headers.get("X-Layout-Session") != token:
                return self.send(403, {"error": "layout session required"})
            if self.path != "/api/review":
                return self.send(404, {"error": "not found"})
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if not 0 < length <= 131072:
                    raise ValueError("request size must be 1..131072 bytes")
                payload = json.loads(self.rfile.read(length))
                if not isinstance(payload, dict):
                    raise ValueError("request must be an object")
                result = studio.review(payload.get("case"), payload.get("xml"), payload.get("pose", "north"))
                result["svg"] = svg(result)
                return self.send(200, result)
            except (ValueError, TypeError) as error:
                return self.send(400, {"error": str(error)})

    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    server.daemon_threads = True
    return server


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--qud-base", type=Path, help="installed Base XML for physical wall/door proof")
    parser.add_argument("--port", type=int, default=0, help="local editor port; 0 chooses a free port")
    parser.add_argument("--map", help="map key for a command-line review (requires --output-dir)")
    parser.add_argument("--palette", help="optional palette to disambiguate a map review")
    parser.add_argument("--draft", type=Path, help="one draft <map> XML, instead of the source layout")
    parser.add_argument("--pose", choices=("north", "east", "south", "west"), default="north")
    parser.add_argument("--output-dir", type=Path, help="fresh directory for SVG, JSON and draft XML")
    parser.add_argument("--census-output", type=Path, help="new JSON file with room facts for every case and pose")
    args = parser.parse_args()
    if bool(args.map) != bool(args.output_dir) or args.draft and not args.map:
        parser.error("--map and --output-dir go together; --draft requires both")
    if args.census_output and (args.map or args.draft or args.palette):
        parser.error("--census-output cannot be combined with a selected draft")
    studio = Studio(args.repo_root, args.qud_base)
    if args.census_output:
        if args.census_output.exists():
            parser.error("census output already exists")
        rows = []
        for case in studio.cases:
            for pose in studio.checker.POSES:
                reading = studio.review(case["id"], pose=pose)
                fields = ['rooms', 'beds', 'enclosed_beds', 'exposed_beds', 'doors',
                          'inaccessible_fixtures', 'unknown_blueprints', 'fits_selected_lot', 'issues']
                rows.append({**case, 'pose': pose, **{key: reading[key] for key in fields}})
        result = {'scope': 'Design-time room facts; not building quality or native acceptance.',
                  'cases': len(studio.cases), 'poses': len(rows),
                  'poses_with_exposed_sleep': sum(bool(row['exposed_beds']) for row in rows),
                  'poses_with_unproved_shapes': sum(bool(row['unknown_blueprints']) for row in rows),
                  'rows': rows}
        with args.census_output.open('x') as output:
            json.dump(result, output, indent=2)
            output.write('\n')
        print(f'ROOM_CENSUS cases={result["cases"]} poses={result["poses"]} '
              f'exposed_sleep={result["poses_with_exposed_sleep"]} output={args.census_output}')
        return 0
    if args.map:
        cases = [item for item in studio.cases if item["map"] == args.map
                 and (not args.palette or item["palette"] == args.palette)]
        if not cases or len({(item["palette"], item["building"]) for item in cases}) != 1:
            parser.error("map is absent or ambiguous; specify its palette")
        result = studio.review(cases[0]["id"], args.draft.read_text() if args.draft else None, args.pose)
        args.output_dir.mkdir(parents=True, exist_ok=False)
        (args.output_dir / "plan.svg").write_text(svg(result))
        (args.output_dir / "review.json").write_text(json.dumps(result, indent=2) + "\n")
        (args.output_dir / "draft.xml").write_text(result["xml"] + "\n")
        print(f'REVIEW rooms={result["rooms"]} beds={result["beds"]} '
              f'enclosed_beds={result["enclosed_beds"]} output={args.output_dir}')
        return 0
    server = make_server(studio, args.port)
    print(f"Layout studio: http://127.0.0.1:{server.server_port}/", flush=True)
    print("Drafts stay in the browser. Export XML before closing. Source files are never written.", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
