#!/usr/bin/env bash
# Checks a test plan against its PRD: every requirement is in the matrix with
# a P1 case, and the matrix and the case tables list the same test IDs.
# Usage: bash check-traceability.sh <prd.md> <test-plan.md>
set -euo pipefail

prd=$1
plan=$2
id='[A-Z]{2,6}-[0-9]{2,4}'
fail=0

reqs=$(grep -oE '^\| *[A-Z]+ *- *[0-9]{4} *\|' "$prd" | tr -d '| ' | sort -u)
matrix=$(sed -n '/^## Traceability matrix/,/^### Success metrics/p' "$plan")
matrix_reqs=$(echo "$matrix" | grep -oE '^\| [A-Z]+-[0-9]{4} ' | tr -d '| ' | sort -u)
cases=$(sed -n '/^## Test cases/,/^## Traceability matrix/p' "$plan" | grep -oE "^\| $id \|" | tr -d '| ' | sort -u)
matrix_cases=$(echo "$matrix" | grep -E '^\| [A-Z]+-[0-9]{4} ' | cut -d'|' -f4 | grep -oE "$id" | sort -u)
p1_cases=$(grep -E "^\| $id \| P1 \|" "$plan" | cut -d'|' -f2 | tr -d ' ' | sort -u)

report() { echo "$1:"; echo "$2" | sed 's/^/  /'; fail=1; }

missing=$(comm -23 <(echo "$reqs") <(echo "$matrix_reqs"))
[ -n "$missing" ] && report "Requirements missing from the matrix" "$missing"

unmapped=$(comm -23 <(echo "$cases") <(echo "$matrix_cases"))
[ -n "$unmapped" ] && report "Cases not in the matrix" "$unmapped"

undefined=$(comm -13 <(echo "$cases") <(echo "$matrix_cases"))
[ -n "$undefined" ] && report "Matrix IDs with no case" "$undefined"

no_p1=""
while IFS= read -r row; do
  req=$(echo "$row" | cut -d'|' -f2 | tr -d ' ')
  ids=$(echo "$row" | cut -d'|' -f4 | grep -oE "$id" | sort -u)
  [ -z "$(comm -12 <(echo "$ids") <(echo "$p1_cases"))" ] && no_p1+="$req"$'\n'
done < <(echo "$matrix" | grep -E '^\| [A-Z]+-[0-9]{4} ')
[ -n "$no_p1" ] && report "Requirements with no P1 case" "${no_p1%$'\n'}"

if [ "$fail" -eq 0 ]; then
  echo "OK: $(echo "$reqs" | wc -l | tr -d ' ') requirements, $(echo "$cases" | wc -l | tr -d ' ') cases"
fi
exit "$fail"
