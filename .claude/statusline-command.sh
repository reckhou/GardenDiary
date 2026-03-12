#!/bin/bash
input=$(cat)

IFS='|' read -r model used cost_raw <<< $(echo "$input" | python -c "
import sys, json
d = json.load(sys.stdin)
model = d.get('model', {}).get('display_name', 'Claude')
cw = d.get('context_window', {})
used = cw.get('used_percentage')
cost = d.get('cost', {}).get('total_cost_usd')
used_str = '' if used is None else str(int(round(used)))
cost_str = '' if cost is None else f'{cost:.3f}'
print(f'{model}|{used_str}|{cost_str}')
" 2>/dev/null)

CYAN=$'\033[0;36m'
YELLOW=$'\033[0;33m'
GREEN=$'\033[0;32m'
DIM=$'\033[0;90m'
RESET=$'\033[0m'

if [ -n "$used" ]; then
  ctx_part="${YELLOW}Ctx: ${used}%${RESET}"
else
  ctx_part="${DIM}Ctx: -${RESET}"
fi

if [ -n "$cost_raw" ]; then
  cost_part="${GREEN}Cost: \$${cost_raw}${RESET}"
else
  cost_part="${DIM}Cost: \$0.000${RESET}"
fi

printf "%s" "${CYAN}${model}${RESET}  ${ctx_part}  ${cost_part}"
