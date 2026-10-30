# Validation Results

Date: 2026-10-04.

- 188 official conversion directions passed; 25 advanced tools all cover real file processing.
- 387 automated checks passed: 220 conversion/tool/original file verifications, 45 advanced branches and previews, 5 dispatch checks from C# main program to processing engine, 1 long-GOP video frame-by-frame regression check, 45 Office checks, 23 F9 image/audio/video processing regression checks, 30 parameter validation checks, and 18 progress publishing/reading/component lifecycle checks.
- Added PPT/PPTX, Word, Excel document conversions. Core testing covers PDF, page-by-page images, text, legacy formats, ODF, HTML, CSV/TSV/JSON, page-selected PDF, mixed Office merging and metadata cleanup; native local Microsoft Office export is operational.
- Crop window selection dragging updates pixel parameters; user confirms and clicks save to generate a new file, followed by verification that actual output dimensions match parameters submitted by the interface.
- F9 simplified interface: video visual crop removes audio channels, start/end timestamps, and playback controls; other tools retain only relevant operations. Video crop window inspection captured. Automated operations were halted by user Esc prior to save verification, without claiming that the new save and cancel interactions have passed full manual testing.
- Added checks confirm: mosaic uses averaged pixel blocks while preserving pixels outside the selection; primary and selected frames for HEIC, MPO/JPEG, and multi-page TIFF are editable; custom aspect ratio preview matches export, and video cropping preserves duration and audio tracks.

- Added progress indicators cover reading files, generating previews, preparing playback, analysis, saving, and background conversion. Batch progress is calculated from actual completed tasks; single tasks display animations and elapsed time without using estimated percentages. Component tests cover old task termination, concurrent tasks, late-arriving callbacks, and invalid progress files.
- Real 4K test images were used to complete processing, with separate style preview captures of progress cards (sample data 2/5), without treating style previews as interactive evidence of real batch conversions.

Detailed test results are in the package folder validation/. Test inputs are all generated images, audio/video, PDF, text, and archives; results are re-read or unpacked for validation, verifying against original file hashes.

Multi-monitor setups, all DPI scaling factors, encrypted files, all specialized encodings, and manual operations for each individual tool window have not yet been individually verified. PDF → Word does not guarantee identical layout preservation, and RAR output uses storage mode.
