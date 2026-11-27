#!/usr/bin/env bash
set -euo pipefail

client_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
artifacts_dir="${PORTAL_CLIENT_ARTIFACTS:-$client_dir/artifacts}"
mkdir -p "$artifacts_dir"
log="$artifacts_dir/window-smoke.log"
: >"$log"
pid=""

cleanup() {
  status=$?
  if (( status != 0 )); then
    {
      echo "Client smoke test failed (exit $status)."
      echo "--- X11 window tree ---"
      xwininfo -root -tree || true
      if [[ -n "$pid" ]]; then
        ps -f -p "$pid" || true
      fi
    } >>"$log" 2>&1
    cat "$log" >&2
  fi
  if [[ -n "$pid" ]]; then
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
  fi
}
trap cleanup EXIT

"$client_dir/src-tauri/target/debug/portalclient" >"$log" 2>&1 &
pid=$!

window_visible() {
  xdotool search --all --onlyvisible --pid "$pid" --name '^Portal$' >/dev/null 2>&1
}

for ((i = 0; i < 30; i++)); do
  if ! kill -0 "$pid" 2>/dev/null; then
    echo "Client exited before its window appeared." >&2
    exit 1
  fi
  if window_visible; then
    break
  fi
  sleep 1
done

if ! window_visible; then
  echo "Timed out waiting for the Portal window." >&2
  exit 1
fi

for ((i = 0; i < 5; i++)); do
  sleep 1
  if ! kill -0 "$pid" 2>/dev/null || ! window_visible; then
    echo "Client or Portal window disappeared during the stability check." >&2
    exit 1
  fi
done

echo "Portal window remained visible and its process stayed running for 5 seconds."
