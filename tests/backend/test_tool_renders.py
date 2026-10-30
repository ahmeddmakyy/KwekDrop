import os, sys, time, subprocess, json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOTNET = r"D:\INSTALLED PROGRAMS\dotnet\dotnet.exe"
APP_DLL = ROOT / "src/KwekDrop/bin/Release/net8.0-windows10.0.19041.0/win-x64/KwekDrop.dll"
BACKDROP = ROOT / "docs/media/_work/wallpaper.png"
OUT_DIR = ROOT / "docs/media/_work/tool_renders"
OUT_DIR.mkdir(parents=True, exist_ok=True)

# Sample fixtures
IMG = ROOT / "docs/media/_work/sample-photo.jpg"
VID = ROOT / "docs/media/_work/sample-video.mp4"
AUD = ROOT / "docs/media/_work/sample-audio.mp3"
PDF = ROOT / "tests/_generated/full-fixtures/document.pdf"
DOCX = ROOT / "tests/_generated/full-fixtures/document-converted.docx"
ZIP = ROOT / "tests/_generated/full-fixtures/bundle.zip"

tools = [
    # Image
    ("cropImage", IMG), ("rotateImage", IMG), ("resizeImage", IMG), ("editImage", IMG),
    ("frameImage", IMG), ("watermark", IMG), ("redactImage", IMG), ("compress", IMG),
    ("removeMetadata", IMG), ("makeIcon", IMG), ("location", IMG),
    # Video
    ("trimVideo", VID), ("splitVideo", VID), ("joinVideos", VID), ("changeVideoSpeed", VID),
    ("videoSnapshots", VID), ("videoEffects", VID), ("cropVideo", VID), ("rotateVideo", VID),
    ("videoSettings", VID), ("redactVideo", VID), ("muteVideo", VID), ("subtitlesAudio", VID),
    ("videoToGif", VID),
    # Audio
    ("trimAudio", AUD), ("redactAudio", AUD), ("joinAudio", AUD), ("normalizeAudio", AUD),
    ("audioChannels", AUD), ("audioEffects", AUD), ("audioToVideo", AUD), ("ringtone", AUD),
    # PDF / Doc
    ("splitPDF", PDF), ("mergePDF", PDF), ("organizePDF", PDF), ("extractPdfImages", PDF),
    ("pdfNumbers", PDF), ("pdfPassword", PDF), ("redactPDF", PDF),
    # Office
    ("officePdf", DOCX),
    # Archive
    ("extractArchive", ZIP), ("packArchive", IMG),
    # Merged / Combined
    ("merge:cropRotate", IMG), ("merge:sizeCompress", IMG), ("merge:frameWatermark", IMG),
    ("merge:trimSplit", VID), ("merge:speedEffects", VID), ("merge:cropRotateVideo", VID),
    ("merge:resolutionCompress", VID), ("merge:soundSubtitles", VID), ("merge:watermarkRedact", VID),
    ("merge:framesGif", VID), ("merge:watermarkRedactPdf", PDF)
]

failed = []
passed = 0
start_all = time.time()

for op, sample in tools:
    for lang in ("en", "ar"):
        out_png = OUT_DIR / f"{op.replace(':', '_')}_{lang}.png"
        err_file = OUT_DIR / f"{op.replace(':', '_')}_{lang}.error.txt"
        err_file.unlink(missing_ok=True)
        
        cmd = [
            DOTNET, str(APP_DLL),
            "--debug-tool-render", op, str(sample), str(out_png),
            "--debug-theme", "light",
            "--debug-lang", lang,
            "--debug-backdrop", str(BACKDROP)
        ]
        
        env = os.environ.copy()
        env["DOTNET_ROOT"] = r"D:\INSTALLED PROGRAMS\dotnet"
        env["DOTNET_ROLL_FORWARD"] = "Major"
        
        t0 = time.time()
        res = subprocess.run(cmd, env=env, cwd=str(APP_DLL.parent), capture_output=True, text=True)
        dt = time.time() - t0
        
        has_png = out_png.exists() and out_png.stat().st_size > 1000
        has_err = err_file.exists()
        
        if res.returncode == 0 and has_png and not has_err:
            passed += 1
            print(f"[PASS] {op:25s} [{lang}] in {dt:.2f}s ({out_png.stat().st_size} bytes)")
        else:
            failed.append((op, lang, res.returncode, res.stderr[:200]))
            print(f"[FAIL] {op:25s} [{lang}] code={res.returncode} err={res.stderr[:100]}")

print(f"\nRender Summary: {passed} passed, {len(failed)} failed in {time.time()-start_all:.1f}s")
if failed:
    sys.exit(1)
