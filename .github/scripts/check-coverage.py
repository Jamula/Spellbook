"""Validate Cobertura totals and make filenames relative to the checkout."""

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

tree = ET.parse(sys.argv[1])
report = tree.getroot()
valid = int(report.attrib["lines-valid"])
covered = int(report.attrib["lines-covered"])
if valid <= 0 or not 0 <= covered <= valid:
    sys.exit("Coverage report must contain measured application lines.")

percentage = 100 * covered / valid
print(f"Application line coverage: {percentage:.2f}% ({covered}/{valid} lines)")
if percentage < 97:
    sys.exit("Application line coverage is below the CI minimum of 97%.")

# Coverlet uses the application project as its source root. GitHub needs
# filenames relative to the repository, which also contains the test project.
root = Path(__file__).resolve().parents[2]
sources = report.find("sources")
source_paths = [root / source.text for source in sources or [] if source.text]
for entry in report.findall(".//class"):
    filename = Path(entry.attrib["filename"])
    candidates = [filename] if filename.is_absolute() else [root / filename]
    candidates.extend(source / filename for source in source_paths)
    resolved = next(
        (path.resolve() for path in candidates if path.is_file() and path.resolve().is_relative_to(root)),
        None,
    )
    if resolved is None:
        sys.exit(f"Coverage source does not exist inside the repository: {filename}")
    entry.set("filename", resolved.relative_to(root).as_posix())
if sources is None:
    sources = ET.SubElement(report, "sources")
sources.clear()
ET.SubElement(sources, "source").text = "."
tree.write(sys.argv[1], encoding="utf-8", xml_declaration=True)
