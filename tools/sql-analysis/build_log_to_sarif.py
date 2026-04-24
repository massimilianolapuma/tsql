"""
build_log_to_sarif.py
---------------------
Converte i diagnostici MSBuild (output di `dotnet build`) in un file SARIF 2.1.0
caricabile su GitHub code scanning.

Riconosce le righe nel formato canonico di MSBuild:

    <path>(<line>,<col>): <error|warning> <RuleId>: <message> [<projectPath>]

Funzionalità:
  * deduplica diagnostici identici (stesso file/riga/regola/messaggio);
  * normalizza i path in URI relativi alla repo root;
  * popola `rules[]` con metadata (helpUri verso docs Microsoft per le SR*,
    verso docs interne per le regole custom Contoso.*);
  * mappa severity MSBuild -> SARIF level con override per regole "*!" elevate.

Uso:
    python build_log_to_sarif.py --input build.log --repo-root . --output out.sarif
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable

# es: /repo/src/Database/Procs/Foo.sql(12,3): warning SR0001: SELECT * ... [/repo/src/Database/Database.sqlproj]
DIAG_RE = re.compile(
    r"""^
    (?P<file>[^()]+?)                # path
    \((?P<line>\d+),(?P<col>\d+)\)   # (line,col)
    \s*:\s*
    (?P<severity>error|warning|info) # severity msbuild
    \s+
    (?P<rule>[A-Za-z][\w.]*)         # rule id (SR0001, Contoso.Security.CT0001, ...)
    \s*:\s*
    (?P<message>.+?)                 # messaggio
    (?:\s+\[[^\]]+\])?               # progetto (opzionale)
    \s*$
    """,
    re.VERBOSE,
)

SEVERITY_TO_LEVEL = {"error": "error", "warning": "warning", "info": "note"}

RULE_HELP_URIS = {
    # built-in
    "SR": "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/{rule_lower}",
    # custom
    "Contoso.Security": "https://internal.contoso.local/docs/sql-rules/{rule}",
}


@dataclass(frozen=True)
class Diagnostic:
    file: str
    line: int
    col: int
    rule: str
    level: str
    message: str

    def fingerprint(self) -> str:
        h = hashlib.sha1(
            f"{self.file}|{self.line}|{self.col}|{self.rule}|{self.message}".encode()
        )
        return h.hexdigest()


@dataclass
class RuleMeta:
    id: str
    name: str = ""
    help_uri: str = ""
    occurrences: int = 0


def parse_log(lines: Iterable[str]) -> list[Diagnostic]:
    seen: set[str] = set()
    diags: list[Diagnostic] = []
    for raw in lines:
        m = DIAG_RE.match(raw.rstrip("\n"))
        if not m:
            continue
        # filtra rumore: vogliamo solo regole SR* e custom (Contoso.*)
        rule = m.group("rule")
        if not (rule.startswith("SR") or "." in rule):
            continue
        d = Diagnostic(
            file=m.group("file"),
            line=int(m.group("line")),
            col=int(m.group("col")),
            rule=rule,
            level=SEVERITY_TO_LEVEL.get(m.group("severity"), "warning"),
            message=m.group("message").strip(),
        )
        fp = d.fingerprint()
        if fp in seen:
            continue
        seen.add(fp)
        diags.append(d)
    return diags


def relativize(path_str: str, repo_root: Path) -> str:
    p = Path(path_str)
    try:
        rel = p.resolve().relative_to(repo_root.resolve())
        return rel.as_posix()
    except ValueError:
        return p.as_posix()


def help_uri_for(rule: str) -> str:
    if rule.startswith("SR"):
        return RULE_HELP_URIS["SR"].format(rule_lower=rule.lower())
    prefix = ".".join(rule.split(".")[:2])
    template = RULE_HELP_URIS.get(prefix)
    return template.format(rule=rule) if template else ""


def to_sarif(diags: list[Diagnostic], repo_root: Path, tool_name: str) -> dict:
    rules: dict[str, RuleMeta] = {}
    results: list[dict] = []

    for d in diags:
        meta = rules.setdefault(d.rule, RuleMeta(id=d.rule, help_uri=help_uri_for(d.rule)))
        meta.occurrences += 1
        results.append(
            {
                "ruleId": d.rule,
                "level": d.level,
                "message": {"text": d.message},
                "locations": [
                    {
                        "physicalLocation": {
                            "artifactLocation": {
                                "uri": relativize(d.file, repo_root),
                                "uriBaseId": "%SRCROOT%",
                            },
                            "region": {"startLine": d.line, "startColumn": d.col},
                        }
                    }
                ],
                "partialFingerprints": {"primaryLocationLineHash": d.fingerprint()},
            }
        )

    return {
        "$schema": "https://json.schemastore.org/sarif-2.1.0.json",
        "version": "2.1.0",
        "runs": [
            {
                "tool": {
                    "driver": {
                        "name": tool_name,
                        "informationUri": "https://learn.microsoft.com/sql/tools/sql-database-projects/concepts/sql-code-analysis/sql-code-analysis",
                        "rules": [
                            {
                                "id": r.id,
                                "name": r.id,
                                "helpUri": r.help_uri or None,
                                "shortDescription": {"text": r.id},
                            }
                            for r in rules.values()
                        ],
                    }
                },
                "originalUriBaseIds": {"%SRCROOT%": {"uri": repo_root.resolve().as_uri() + "/"}},
                "results": results,
            }
        ],
    }


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--input", required=True, type=Path)
    ap.add_argument("--output", required=True, type=Path)
    ap.add_argument("--repo-root", required=True, type=Path)
    ap.add_argument("--tool-name", default="SqlCodeAnalysis")
    args = ap.parse_args(argv)

    if not args.input.exists():
        print(f"input log not found: {args.input}", file=sys.stderr)
        return 2

    with args.input.open("r", encoding="utf-8", errors="replace") as f:
        diags = parse_log(f)

    sarif = to_sarif(diags, args.repo_root, args.tool_name)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(sarif, indent=2), encoding="utf-8")

    print(f"converted {len(diags)} diagnostics into {args.output}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
