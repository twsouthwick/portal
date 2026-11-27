#!/usr/bin/env bash
set -euo pipefail

package=app.swick.portal.client
activity="$package/.MainActivity"
apk_dir=Portal.Client/src-tauri/gen/android/app/build/outputs/apk

diagnostics() {
  status=$?
  if (( status != 0 )); then
    echo "Android smoke test failed; collecting diagnostics" >&2
    adb shell dumpsys activity activities || true
    adb logcat -b crash -d || true
    adb logcat -d -t 500 || true
  fi
}
trap diagnostics EXIT

mapfile -t apks < <(find "$apk_dir" -type f -name '*.apk')
if (( ${#apks[@]} != 1 )); then
  echo "Expected exactly one debug APK in $apk_dir, found ${#apks[@]}" >&2
  exit 1
fi

adb install -r "${apks[0]}"
adb shell am force-stop "$package"
adb logcat -c
launch=$(adb shell am start -W -n "$activity")
echo "$launch"
grep -q 'Status: ok' <<< "$launch"

pid=
for ((second = 0; second < 30; second++)); do
  current_pid=$(adb shell pidof "$package" | tr -d '\r' || true)
  if [[ -n "$current_pid" ]] &&
    adb shell dumpsys activity activities | grep -E 'mResumedActivity:|topResumedActivity=' | grep -Fq "$activity"; then
    pid=$current_pid
    break
  fi
  sleep 1
done

if [[ -z "$pid" ]]; then
  echo "$activity did not reach the foreground" >&2
  exit 1
fi

for ((second = 0; second < 10; second++)); do
  sleep 1
  [[ $(adb shell pidof "$package" | tr -d '\r') == "$pid" ]]
  adb shell dumpsys activity activities | grep -E 'mResumedActivity:|topResumedActivity=' | grep -Fq "$activity"
done

if adb logcat -b crash -d | grep -F "$package"; then
  echo "$package crashed during the smoke test" >&2
  exit 1
fi
echo "$activity remained in the foreground for 10 seconds (pid $pid)"
