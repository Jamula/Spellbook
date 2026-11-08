"""Report actual VSTest outcomes and reject empty or unsuccessful test runs."""

import os
import sys
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path

report = ET.parse(sys.argv[1]).getroot()
counters = report.find("./{*}ResultSummary/{*}Counters")
if counters is None:
    sys.exit("TRX report is missing test counters.")

total, executed, passed, failed, skipped = (
    int(counters.attrib[name])
    for name in ("total", "executed", "passed", "failed", "notExecuted")
)
if not (0 <= passed <= executed <= total and 0 <= failed <= executed
        and passed + failed <= executed and 0 <= skipped <= total - executed):
    sys.exit("TRX report contains inconsistent test counters.")

summary = (
    f"Tests: {total} total, {executed} executed, {passed} passed, "
    f"{failed} failed, {skipped} skipped.\n"
)
times = report.find("./{*}Times")
if times is not None and times.get("start") and times.get("finish"):
    start, finish = (datetime.fromisoformat(times.attrib[k].replace("Z", "+00:00")) for k in ("start", "finish"))
    summary += f"Test run duration: {(finish - start).total_seconds():.2f} seconds.\n"
print(summary, end="")
if os.environ.get("GITHUB_STEP_SUMMARY"):
    with Path(os.environ["GITHUB_STEP_SUMMARY"]).open("a") as output:
        output.write("### Test results\n\n" + summary + "\n")

if total == 0 or executed == 0:
    sys.exit("No tests executed; this does not validate the application.")
if failed or passed != executed or any(int(counters.get(k, "0")) for k in ("error", "timeout", "aborted")):
    sys.exit("The test run did not complete successfully; inspect the TRX artifact.")
