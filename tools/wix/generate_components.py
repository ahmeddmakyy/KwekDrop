from pathlib import Path
import os
import sys
import xml.sax.saxutils as saxutils

ROOT = Path(__file__).resolve().parent.parent.parent
base_dir = str(Path(sys.argv[1]).resolve()) if len(sys.argv) > 1 else str(ROOT / "outputs" / "KwekDrop")
output_wxs = str(Path(__file__).resolve().parent / "components.wxs")

comp_refs = []
file_count = 0
dir_count = 0

lines = []
lines.append('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
lines.append('  <Fragment>')
lines.append('    <StandardDirectory Id="LocalAppDataFolder">')
lines.append('      <Directory Id="ProgramsFolder" Name="Programs">')
lines.append('        <Directory Id="INSTALLFOLDER" Name="KwekDrop">')

def walk_dir(current_dir, indent):
    global file_count, dir_count
    rel = os.path.relpath(current_dir, base_dir)
    files = [f for f in os.listdir(current_dir) if os.path.isfile(os.path.join(current_dir, f))]
    dirs = [d for d in os.listdir(current_dir) if os.path.isdir(os.path.join(current_dir, d))]

    if rel == ".":
        dir_id = "INSTALLFOLDER"
    else:
        dir_count += 1
        dir_id = f"dir_{dir_count}"
        dname = os.path.basename(current_dir)
        lines.append(f'{"  " * indent}<Directory Id="{dir_id}" Name="{saxutils.escape(dname)}">')

    # Add components for files in this directory
    for f in files:
        file_count += 1
        fpath = os.path.join(current_dir, f)
        frel = os.path.relpath(fpath, base_dir)
        cid = f"comp_{file_count}"
        fid = f"file_{file_count}"
        comp_refs.append(cid)
        lines.append(f'{"  " * (indent + 1)}<Component Id="{cid}" Directory="{dir_id}">')
        lines.append(f'{"  " * (indent + 2)}<File Id="{fid}" Source="{saxutils.escape(os.path.join(base_dir, frel))}" KeyPath="yes" />')
        lines.append(f'{"  " * (indent + 1)}</Component>')

    for d in dirs:
        walk_dir(os.path.join(current_dir, d), indent + 1)

    if rel != ".":
        lines.append(f'{"  " * indent}</Directory>')

walk_dir(base_dir, 5)

lines.append('        </Directory>')
lines.append('      </Directory>')
lines.append('    </StandardDirectory>')
lines.append('  </Fragment>')

lines.append('  <Fragment>')
lines.append('    <ComponentGroup Id="ProductFiles">')
for cid in comp_refs:
    lines.append(f'      <ComponentRef Id="{cid}" />')
lines.append('    </ComponentGroup>')
lines.append('  </Fragment>')
lines.append('</Wix>')

with open(output_wxs, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))

print(f"Generated components.wxs with {file_count} files and {dir_count} directories!")
