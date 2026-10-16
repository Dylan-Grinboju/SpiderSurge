from __future__ import annotations

import argparse
import json
import random
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


@dataclass(frozen=True)
class TitleDef:
    name: str
    requirements: frozenset[str]


@dataclass(frozen=True)
class AwardedTitle:
    title: TitleDef
    player: int


TITLE_BLOCK_RE = re.compile(
    r"titles\.Add\(new TitleBuilder\(leaders\)(?P<body>.*?)\.Build\(\)\);",
    re.DOTALL,
)
NAME_RE = re.compile(r'\.WithName\("(?P<name>(?:[^"\\]|\\.)*)"\)')
REQ_RE = re.compile(r"Req\.(?P<req>[A-Za-z_][A-Za-z0-9_]*)")


def parse_titles_manager(path: Path) -> list[TitleDef]:
    content = path.read_text(encoding="utf-8")
    titles: list[TitleDef] = []

    for match in TITLE_BLOCK_RE.finditer(content):
        body = match.group("body")
        name_match = NAME_RE.search(body)
        if not name_match:
            continue

        title_name = bytes(name_match.group("name"), "utf-8").decode("unicode_escape")
        requirements = frozenset(REQ_RE.findall(body))
        titles.append(TitleDef(name=title_name, requirements=requirements))

    unique_by_name: dict[str, TitleDef] = {}
    for title in titles:
        unique_by_name[title.name] = title

    return sorted(unique_by_name.values(), key=lambda t: (len(t.requirements), t.name))


def random_requirements_assignment(
    requirements: Iterable[str],
    player_count: int,
    rng: random.Random,
) -> dict[str, int]:
    if player_count <= 1:
        raise ValueError("--players must be greater than 1")
    return {req: rng.randint(1, player_count) for req in sorted(set(requirements))}


def award_titles_from_assignment(
    titles: Iterable[TitleDef],
    assignment: dict[str, int],
) -> list[AwardedTitle]:
    awarded: list[AwardedTitle] = []
    for title in titles:
        if not title.requirements:
            continue
        req_players = {assignment[req] for req in title.requirements}
        if len(req_players) == 1:
            awarded.append(AwardedTitle(title=title, player=req_players.pop()))
    return awarded


def apply_dominance_filter_awarded(
    titles: Iterable[AwardedTitle],
) -> tuple[list[AwardedTitle], list[AwardedTitle]]:
    title_list = list(titles)
    survivors: list[AwardedTitle] = []
    removed: list[AwardedTitle] = []

    for candidate in title_list:
        dominated = False
        for other in title_list:
            if other.title.name == candidate.title.name and other.player == candidate.player:
                continue
            if other.player != candidate.player:
                continue
            if len(other.title.requirements) <= len(candidate.title.requirements):
                continue
            if candidate.title.requirements.issubset(other.title.requirements):
                dominated = True
                break

        if dominated:
            removed.append(candidate)
        else:
            survivors.append(candidate)

    return survivors, removed


def analyze_simple_titles(
    all_titles: list[TitleDef],
    players: int,
    runs: int,
    seed: int | None,
    max_reqs: int,
    min_survival_percent: float,
) -> list[dict[str, float | int | str]]:
    if runs <= 0:
        raise ValueError("--runs must be greater than 0")
    if max_reqs <= 0:
        raise ValueError("--max-reqs must be greater than 0")
    if min_survival_percent < 0 or min_survival_percent > 100:
        raise ValueError("--min-survival-percent must be in [0, 100]")

    rng = random.Random(seed)
    all_requirements = {req for title in all_titles for req in title.requirements}

    awarded_count_by_title: dict[str, int] = {title.name: 0 for title in all_titles}
    survived_count_by_title: dict[str, int] = {title.name: 0 for title in all_titles}
    req_count_by_title: dict[str, int] = {title.name: len(title.requirements) for title in all_titles}

    for _ in range(runs):
        assignment = random_requirements_assignment(all_requirements, players, rng)
        awarded = award_titles_from_assignment(all_titles, assignment)
        survivors, _ = apply_dominance_filter_awarded(awarded)

        for award in awarded:
            awarded_count_by_title[award.title.name] += 1
        for survivor in survivors:
            survived_count_by_title[survivor.title.name] += 1

    results: list[dict[str, float | int | str]] = []
    for title in all_titles:
        req_count = req_count_by_title[title.name]
        if req_count > max_reqs:
            continue

        awarded_count = awarded_count_by_title[title.name]
        survived_count = survived_count_by_title[title.name]

        survive_percent_of_runs = (survived_count / runs) * 100
        if survive_percent_of_runs < min_survival_percent:
            continue

        survive_when_awarded_percent = 0.0
        if awarded_count > 0:
            survive_when_awarded_percent = (survived_count / awarded_count) * 100

        results.append(
            {
                "title": title.name,
                "req_count": req_count,
                "awarded_runs": awarded_count,
                "survived_runs": survived_count,
                "survive_percent_of_runs": survive_percent_of_runs,
                "awarded_percent_of_runs": (awarded_count / runs) * 100,
                "survive_when_awarded_percent": survive_when_awarded_percent,
            }
        )

    results.sort(
        key=lambda row: (
            -float(row["survive_percent_of_runs"]),
            int(row["req_count"]),
            str(row["title"]),
        )
    )
    return results


def get_all_requirements(all_titles: list[TitleDef]) -> list[str]:
    return sorted({req for title in all_titles for req in title.requirements})


def resolve_requirement_name(input_name: str, all_requirements: list[str]) -> str:
    normalized_map = {req.lower(): req for req in all_requirements}
    key = input_name.strip().lower()
    if key in normalized_map:
        return normalized_map[key]

    key_no_prefix = key.removeprefix("req.")
    if key_no_prefix in normalized_map:
        return normalized_map[key_no_prefix]

    raise ValueError(
        f"Unknown requirement: {input_name}. "
        f"Use `list-reqs` to see valid names."
    )


def find_titles_using_requirement(all_titles: list[TitleDef], requirement: str) -> list[TitleDef]:
    matches = [title for title in all_titles if requirement in title.requirements]
    return sorted(matches, key=lambda t: (len(t.requirements), t.name))


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Title analysis tool.",
        epilog=(
            "Examples:\n"
            "  python Tools/title_dominance_analyzer.py sim --players 2\n"
            "  python Tools/title_dominance_analyzer.py list-reqs\n"
            "  python Tools/title_dominance_analyzer.py req MostLavaDeaths"
        ),
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument(
        "--titles-file",
        type=Path,
        default=Path("Tracking") / "TitlesManager.cs",
        help="Path to TitlesManager.cs",
    )

    subparsers = parser.add_subparsers(dest="command", required=True)

    sim_parser = subparsers.add_parser(
        "sim",
        help="Run simulation to find simple titles with high survival",
    )
    sim_parser.add_argument("--players", type=int, required=True, help="Number of players (>1)")
    sim_parser.add_argument("--runs", type=int, default=100000, help="Simulation runs (default: 100000)")
    sim_parser.add_argument("--seed", type=int, help="Optional RNG seed for reproducibility")
    sim_parser.add_argument("--max-reqs", type=int, default=2, help="Maximum requirement count to consider simple (default: 2)")
    sim_parser.add_argument(
        "--min-survival-percent",
        type=float,
        default=0.0,
        help="Minimum survive_percent_of_runs threshold (0-100, default: 0)",
    )
    sim_parser.add_argument("--top", type=int, default=30, help="Max rows to print (default: 30)")
    sim_parser.add_argument("--json", action="store_true", help="Output JSON")

    list_reqs_parser = subparsers.add_parser(
        "list-reqs",
        help="Print all available requirement names",
    )
    list_reqs_parser.add_argument("--json", action="store_true", help="Output JSON")

    req_parser = subparsers.add_parser(
        "req",
        help="Display all titles that use one requirement",
    )
    req_parser.add_argument("req_name", type=str, help="Requirement name (e.g. MostLavaDeaths)")
    req_parser.add_argument("--json", action="store_true", help="Output JSON")

    return parser


def main() -> None:
    parser = build_parser()
    args = parser.parse_args()

    if not args.titles_file.exists():
        raise FileNotFoundError(f"Could not find titles file: {args.titles_file}")

    all_titles = parse_titles_manager(args.titles_file)
    if not all_titles:
        raise RuntimeError("No titles could be parsed from the input file")

    if args.command == "list-reqs":
        requirements = get_all_requirements(all_titles)
        if args.json:
            print(json.dumps({"count": len(requirements), "requirements": requirements}, indent=2))
            return

        print(f"Requirements: {len(requirements)}")
        for requirement in requirements:
            print(f"- {requirement}")
        return

    if args.command == "req":
        requirements = get_all_requirements(all_titles)
        requirement_name = resolve_requirement_name(args.req_name, requirements)
        matches = find_titles_using_requirement(all_titles, requirement_name)

        if args.json:
            payload = {
                "requirement": requirement_name,
                "count": len(matches),
                "titles": [
                    {
                        "title": title.name,
                        "req_count": len(title.requirements),
                        "requirements": sorted(title.requirements),
                    }
                    for title in matches
                ],
            }
            print(json.dumps(payload, indent=2))
            return

        print(f"Requirement: {requirement_name}")
        print(f"Matching titles: {len(matches)}")
        for title in matches:
            req_text = ", ".join(sorted(title.requirements))
            print(f"- {title.name} [{len(title.requirements)} reqs]: [{req_text}]")
        return

    if args.command != "sim":
        raise RuntimeError(f"Unsupported command: {args.command}")

    rows = analyze_simple_titles(
        all_titles=all_titles,
        players=args.players,
        runs=args.runs,
        seed=args.seed,
        max_reqs=args.max_reqs,
        min_survival_percent=args.min_survival_percent,
    )

    if args.top > 0:
        rows = rows[: args.top]

    if args.json:
        payload = {
            "players": args.players,
            "runs": args.runs,
            "seed": args.seed,
            "max_reqs": args.max_reqs,
            "min_survival_percent": args.min_survival_percent,
            "result_count": len(rows),
            "results": rows,
        }
        print(json.dumps(payload, indent=2))
        return

    print(f"Players: {args.players}")
    print(f"Runs: {args.runs}")
    print(f"Seed: {args.seed if args.seed is not None else 'random'}")
    print(f"Simple threshold (max reqs): {args.max_reqs}")
    print(f"Minimum survival percent: {args.min_survival_percent:.1f}%")
    print(f"Results: {len(rows)}")

    if not rows:
        print("No titles matched the filters.")
        return

    print("\nSimple titles with high survival:")
    for row in rows:
        print(
            f"- {row['title']} [{row['req_count']} reqs] | "
            f"survive:{float(row['survive_percent_of_runs']):.1f}% | "
            f"awarded:{float(row['awarded_percent_of_runs']):.1f}% | "
            f"survive_when_awarded:{float(row['survive_when_awarded_percent']):.1f}%"
        )


if __name__ == "__main__":
    main()
