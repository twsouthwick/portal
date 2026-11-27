#!/usr/bin/env bash
set -euo pipefail

package=app.swick.portal.client
activity="$package/.MainActivity"
apk_dir=Portal.Client/src-tauri/gen/android/app/build/outputs/apk
artifacts_dir=Portal.Client/artifacts
mkdir -p "$artifacts_dir"

diagnostics() {
  status=$?
  {
    echo "Android smoke test exit status: $status"
    echo "--- Activity state ---"
    adb shell dumpsys activity activities || true
    echo "--- Crash log ---"
    adb logcat -b crash -d || true
    echo "--- Recent logcat ---"
    adb logcat -d -t 500 || true
  } > "$artifacts_dir/android-diagnostics.log" 2>&1
  if (( status != 0 )); then
    cat "$artifacts_dir/android-diagnostics.log" >&2
    adb exec-out screencap -p > "$artifacts_dir/android-failure.png" || rm -f "$artifacts_dir/android-failure.png"
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
adb logcat -b all -c
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

crashes=$(adb logcat -b crash -d)
if grep -F "$package" <<< "$crashes"; then
  echo "$package crashed during the smoke test" >&2
  exit 1
fi
adb exec-out screencap -p > "$artifacts_dir/android-client.png"
test -s "$artifacts_dir/android-client.png"
echo "$activity remained in the foreground for 10 seconds (pid $pid)"
