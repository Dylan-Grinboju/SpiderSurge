import argparse
import pathlib
import re


def main():
    p = argparse.ArgumentParser(description='Read one QA reference section instead of the complete file.')
    p.add_argument('file', choices=['recipes.md', 'checklist.md', 'full-check.md', 'report.md'])
    p.add_argument('heading', nargs='?')
    a = p.parse_args()
    lines = (pathlib.Path(__file__).resolve().parent / a.file).read_text(encoding='utf-8').splitlines()
    headings = [(i, len(m[1]), m[2]) for i, line in enumerate(lines) if (m := re.match(r'^(#+) (.+)$', line))]
    if not a.heading:
        print('\n'.join(f'{text}' for _, _, text in headings))
        return
    found = [h for h in headings if h[2].casefold() == a.heading.casefold()]
    if len(found) != 1:
        p.error('Use one exact heading; omit the heading to list sections')
    start, level, _ = found[0]
    end = next((i for i, depth, _ in headings if i > start and depth <= level), len(lines))
    print('\n'.join(lines[start:end]))


if __name__ == '__main__':
    main()
