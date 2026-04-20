#!/usr/bin/env python3
"""
mediawiki_dump.py

Example:
    python mediawiki_dump.py \
        --base-url https://vic2.paradoxwikis.com \
        --out vic2_wiki.jsonl

What it does:
1. Tries MediaWiki API enumeration via api.php?action=query&list=allpages
2. Fetches raw wikitext in batches
3. Falls back to page HTML scraping if API is unavailable
4. Writes one JSON object per page to a JSONL file
"""

from __future__ import annotations

import argparse
import json
import sys
import time
from dataclasses import dataclass
from typing import Dict, Iterable, Iterator, List, Optional

import requests


DEFAULT_HEADERS = {
    "User-Agent": "Mozilla/5.0 (compatible; WikiExporter/1.0; +https://example.com/contact)"
}


@dataclass
class PageRecord:
    title: str
    pageid: Optional[int]
    ns: Optional[int]
    timestamp: Optional[str]
    source: str               # "api-wikitext" or "html-fallback"
    content: str
    url: Optional[str] = None


class MediaWikiExporter:
    def __init__(
        self,
        base_url: str,
        timeout: int = 30,
        sleep_seconds: float = 0.25,
        session: Optional[requests.Session] = None,
    ) -> None:
        self.base_url = base_url.rstrip("/")
        self.api_url = f"{self.base_url}/api.php"
        self.session = session or requests.Session()
        self.session.headers.update(DEFAULT_HEADERS)
        self.timeout = timeout
        self.sleep_seconds = sleep_seconds

    def _get(self, url: str, **params) -> requests.Response:
        resp = self.session.get(url, params=params, timeout=self.timeout)
        resp.raise_for_status()
        return resp

    def api_is_available(self) -> bool:
        """
        Checks whether api.php behaves like a MediaWiki Action API endpoint.
        """
        try:
            resp = self._get(
                self.api_url,
                action="query",
                meta="siteinfo",
                siprop="general",
                format="json",
                formatversion="2",
            )
            data = resp.json()
            return "query" in data and "general" in data["query"]
        except Exception:
            return False

    def iter_allpages(self, namespace: int = 0) -> Iterator[Dict]:
        """
        Yields page metadata from list=allpages.
        """
        apcontinue = None

        while True:
            params = {
                "action": "query",
                "list": "allpages",
                "aplimit": "max",
                "apnamespace": str(namespace),
                "format": "json",
                "formatversion": "2",
            }
            if apcontinue:
                params["apcontinue"] = apcontinue

            resp = self._get(self.api_url, **params)
            data = resp.json()

            for page in data.get("query", {}).get("allpages", []):
                yield page

            cont = data.get("continue")
            if not cont or "apcontinue" not in cont:
                break
            apcontinue = cont["apcontinue"]
            time.sleep(self.sleep_seconds)

    def fetch_pages_wikitext(self, titles: List[str]) -> List[PageRecord]:
        """
        Fetches raw wikitext via prop=revisions&rvprop=timestamp|content
        using batched title queries.
        """
        if not titles:
            return []

        resp = self._get(
            self.api_url,
            action="query",
            prop="revisions",
            rvprop="timestamp|content",
            rvslots="main",
            titles="|".join(titles),
            redirects="1",
            format="json",
            formatversion="2",
        )
        data = resp.json()

        results: List[PageRecord] = []
        for page in data.get("query", {}).get("pages", []):
            revisions = page.get("revisions", [])
            timestamp = None
            content = ""

            if revisions:
                rev0 = revisions[0]
                timestamp = rev0.get("timestamp")
                slots = rev0.get("slots", {})
                main = slots.get("main", {})
                content = main.get("content", "")

            results.append(
                PageRecord(
                    title=page.get("title", ""),
                    pageid=page.get("pageid"),
                    ns=page.get("ns"),
                    timestamp=timestamp,
                    source="api-wikitext",
                    content=content,
                    url=f"{self.base_url}/wiki/{page.get('title', '').replace(' ', '_')}",
                )
            )
        return results

    def html_fallback(self, titles: Iterable[str]) -> Iterator[PageRecord]:
        """
        Last-resort fallback: downloads rendered HTML pages.
        This is worse for LLM ingestion than raw wikitext, but still usable.
        """
        for title in titles:
            url = f"{self.base_url}/wiki/{title.replace(' ', '_')}"
            try:
                resp = self._get(url)
                yield PageRecord(
                    title=title,
                    pageid=None,
                    ns=None,
                    timestamp=None,
                    source="html-fallback",
                    content=resp.text,
                    url=url,
                )
            except Exception as e:
                print(f"[warn] failed HTML fetch for {title}: {e}", file=sys.stderr)
            time.sleep(self.sleep_seconds)

    def run(
        self,
        out_path: str,
        namespace: int = 0,
        batch_size: int = 20,
        max_pages: Optional[int] = None,
    ) -> None:
        with open(out_path, "w", encoding="utf-8") as f:
            if self.api_is_available():
                print("[info] MediaWiki API detected; using structured export.", file=sys.stderr)

                batch: List[str] = []
                count = 0

                for page in self.iter_allpages(namespace=namespace):
                    title = page["title"]
                    batch.append(title)

                    if len(batch) >= batch_size:
                        for rec in self.fetch_pages_wikitext(batch):
                            f.write(json.dumps(rec.__dict__, ensure_ascii=False) + "\n")
                            count += 1
                            if max_pages and count >= max_pages:
                                print(f"[info] reached max_pages={max_pages}", file=sys.stderr)
                                return
                        batch.clear()
                        time.sleep(self.sleep_seconds)

                if batch:
                    for rec in self.fetch_pages_wikitext(batch):
                        f.write(json.dumps(rec.__dict__, ensure_ascii=False) + "\n")
                        count += 1
                        if max_pages and count >= max_pages:
                            print(f"[info] reached max_pages={max_pages}", file=sys.stderr)
                            return

            else:
                print("[warn] API unavailable; falling back to HTML mode.", file=sys.stderr)

                titles: List[str] = []
                # crude fallback seed page discovery:
                # you can replace this with a manual page-title list if necessary.
                seed_pages = ["Victoria_2_Wiki", "Production", "Population", "Trade", "Technology"]
                titles.extend(seed_pages)

                count = 0
                for rec in self.html_fallback(titles):
                    f.write(json.dumps(rec.__dict__, ensure_ascii=False) + "\n")
                    count += 1
                    if max_pages and count >= max_pages:
                        print(f"[info] reached max_pages={max_pages}", file=sys.stderr)
                        return


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", required=True, help="Wiki base URL, e.g. https://vic2.paradoxwikis.com")
    parser.add_argument("--out", required=True, help="Output JSONL file path")
    parser.add_argument("--namespace", type=int, default=0, help="MediaWiki namespace (default: 0)")
    parser.add_argument("--batch-size", type=int, default=20, help="Titles per content fetch batch")
    parser.add_argument("--max-pages", type=int, default=None, help="Optional limit for testing")
    parser.add_argument("--timeout", type=int, default=30)
    parser.add_argument("--sleep-seconds", type=float, default=0.25)
    return parser.parse_args()


def main() -> int:
    args = parse_args()

    exporter = MediaWikiExporter(
        base_url=args.base_url,
        timeout=args.timeout,
        sleep_seconds=args.sleep_seconds,
    )
    exporter.run(
        out_path=args.out,
        namespace=args.namespace,
        batch_size=args.batch_size,
        max_pages=args.max_pages,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
