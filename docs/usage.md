# KwekDrop

Version 1.0.0, Date: 2026-10-08. Local Windows x64 file utility running quietly in the system tray, with no main window.

## How to Use

1. Select files on the desktop or in File Explorer, and hold down the left mouse button to begin dragging.
2. While dragging, hold **Shift** to show the **Convert** wheel (formats); move to the target format and release the mouse.
3. While dragging, hold **Alt** to show the **Tools** wheel, or hold **Ctrl** to show the **Actions** wheel (Share, Float, Copy, Move, Copy Path, Terminal). Priority when multiple keys are held: Alt, then Ctrl, then Shift.
4. Operations requiring parameters open a compact tool window; adjust settings and click “Save new file”. The window displays processing results, and allows cancelling the task or opening the output location. When reading large files, generating previews, or saving, a thin progress line appears along the top of the preview area (operations that finish quickly do not show it) without obstructing the view.

Once the wheel appears, you can release the keyboard and simply keep the left mouse button held down. The keys can be pressed before or during the drag. Pressing **F8** opens the Convert wheel and **F9** the Tools wheel for the current drag; this is useful when dragging files started from other applications. When not dragging files, F8 and F9 pass through to the active application as normal (such as recalculating in Excel). Press **Esc** to close the wheel. The drag shortcuts are designed for file drags started from the desktop or File Explorer, so normal typing is never intercepted.

Format conversions and tools that do not open parameter windows display processing cards in the bottom-right corner of the desktop. Batch jobs show the actual count of completed items; single tasks that cannot measure an exact percentage use an animated indicator. Once completed, cards dismiss automatically without opening any main window.

Each task has its own card, arranged from top to bottom in start order. Card buttons:

- **Pause / Resume**: Freezes the entire task (including external programs such as FFmpeg) and pauses the timer; resuming continues processing right from where it paused.
- **Cancel**: Stops the task and cleans up unfinished temporary files; paused tasks can also be cancelled directly.
- **Hide**: Hides only the card while the task continues running in the background; a notification is sent from the tray upon completion.

Up to 2 tasks run concurrently; any remaining tasks display “Waiting”. A paused task yields its slot, and the next queued task starts immediately. When two or more tasks exist, a summary bar appears at the top, allowing you to “Collapse” into a stack (showing only active jobs), “Expand” to list all tasks, or “Hide all”. The tray menu can show progress again, pause or resume all jobs, or cancel all jobs.

The interface is available in English and Arabic. The light theme is active by default with real frosted glass (the desktop wallpaper softly shows through, blurred, with a thin white veil); Dark and Match Windows appearances are also available. Arabic uses text-only RTL (layouts are never mirrored), with typography automatically adapting (Thmanyah Sans for Arabic, Segoe UI Variable for English). On first launch, the language follows the Windows display language and can be switched at any time from the tray menu or Settings; your choices are saved.

KwekDrop has no permanent main window; it lives quietly in the Windows notification area (system tray). On first launch, an interactive 4-page Setup guide opens (Welcome, Try it now with a real drag and live keycaps, Make it yours for language, appearance and startup preferences, and You're all set). You can reopen the guide at any time from the tray menu under Help > Guide, or from the Settings window. Double-clicking the tray icon, choosing Settings from the tray menu, or launching KwekDrop a second time while it is already running opens the Settings window.

Esc cancels the floating wheel. Releasing the mouse outside any action sector cancels without submitting a task. Right-clicking the tray icon lets you show processing progress, pause or cancel all jobs, open the latest output folder, switch languages, or quit.

Converted results are saved in the original folder with an operation suffix appended to the filename; identical names receive an automatic numeric suffix without overwriting original files. Multi-page exports and video segmentations create new folders. Batch processing supports up to 200 inputs, and failure of a single file does not stop processing of the remaining files.

## Five Feature Categories

| Category | Conversions & Advanced Tools |
| --- | --- |
| Images | JPG, PNG, WebP, HEIC, TIFF, SVG input, AVIF, BMP; PDF export, DOCX export for JPG/PNG. Compression, metadata, location information (remove or modify), exposure/color/clarity/dehaze/denoise/grain, background/gradient/rounded corners/shadows, crop, rotate/flip, resizing, watermark, ICO/favicon creation, OCR text extraction, multi-image animated GIF/slideshow synthesis, solid/blur/pixelate redaction regions, PDF compilation, image collage. |
| Video | MP4, MOV, MKV, WebM, AVI, WMV, GIF, and MP3 extraction. Compression, metadata, location information (remove or modify), audio track removal, time segment or visual crop, rotate/flip, resolution and frame rate, convert to GIF, fade in/out/reverse/loop, subtitle burning and audio track replacement/mixing, watermark, thumbnail summary, pitch-preserving speed change, mixed-dimension and with/without audio track video concatenation, frame-by-frame navigation and capture, segmentation, time-ranged multi-region redaction. |
| Audio | MP3, M4A, WAV, FLAC, OGG, Opus, AIFF, WMA. Compression, metadata, loudness normalization and analysis, trim/remove silence at ends, mono/stereo channels and left/right volume balance, volume and fade in/out/noise reduction, concatenation of multiple audio files, ringtones (.m4r), video audio visualization with waveforms or image backgrounds, multiple beep mute segments. |
| Documents | PDF → DOCX/JPG/PNG/TXT; TXT → PDF/JPG/PNG/SRT/VTT; SRT/VTT/TXT bidirectional conversion. PDF compression, metadata, merge, split by page groups, page reorder/rotate/duplicate/remove, add or remove password protection, page numbering and header/footer, embedded image extraction, watermark, redaction (blacking out and truly deleting selected content or specific text), scanned document OCR (with searchable PDF output). |
| Archives | ZIP, TAR, GZIP, RAR mutual conversion and unpacking; packaging regular files or folders, with support for 7z, AES-256 password encryption, and multi-volume splitting. RAR uses non-compressed storage mode, consistent with the reference product's RAR output. |

Verified against published input-output rules, offering 188 conversion directions and 25 standalone advanced tools. When selecting multiple files, the wheel displays operations supported by all selected inputs; specific tools adapt according to file formats and selection counts.

## Additional Features

- **Combine related tools**: When tools for a single image or video reach 10 or more, related tools combine into a single wheel item opened in one window—such as “Crop & Rotate”, “Size & Compress”, and “Background & Watermark” for images, or “Trim & Segment”, “Audio & Subtitles”, and “Watermark & Redaction” for videos. Tabs at the top of the window switch between tools; after adjusting one tool, click “Apply this step” to continue adjusting on the result, use “Undo last step” to revert, and finally click “Save new file” when finished to write out once. Each tab in “Trim & Segment” and “Frames & GIF” saves independently. Metadata, icon creation, and OCR text extraction always remain standalone. Files with fewer than 10 tools are unaffected.
- **Two wheels (categories)** (optional): You can also switch to a two-level wheel: the first level displays categories; hovering files over a category expands a second outer wheel listing tools in that category. Select “Combine related tools” or “Two wheels (categories)” in the Settings window or under the tray menu “Wheel with many tools” (under “Wheel”).
- **Location information**: The tools menu for images and videos includes “Location”, displaying the recorded capture location. You can select “Remove location” (deleting GPS from EXIF/XMP and video location tags) or “Set a new location”: paste coordinates (latitude, longitude, such as `31.2304, 121.4737`, copied by right-clicking a location in map applications; notations such as `33.8688 S 151.2093 E` are also supported), or pick a photo taken there to use its location. Other metadata such as camera, date, and orientation are preserved; JPG image pixels are not recompressed, and video audio/video streams are copied directly. Since KwekDrop does not access the internet, no map is displayed. Multiple images and videos can be processed simultaneously.
- **Customize wheel**: Tray menu “Customize wheel…” allows adjusting the option order by file type and menu (formats / tools); the first item sits at the top of the wheel and the rest follow clockwise. By default, the most recently used option is placed first and marked with ↻, which can also be disabled here.
- **Settings**: The near-square 600×580 Settings window centralizes all preferences: Appearance (Light, Dark, Auto), Language (Arabic, English), Glass effects (blur strength and window opacity), Start with Windows, configurable Drag shortcuts, Wheel layout (Combined or Two wheels), a button to open the Setup guide, and Restore Defaults. It opens by double-clicking the tray icon, selecting Settings from the tray menu, or launching KwekDrop when it is already running.
- **Drag shortcuts**: While dragging files, holding modifier keys summons the wheels: Shift opens Convert, Alt opens Tools, and Ctrl opens Actions (priority: Alt, then Ctrl, then Shift). These three keys are fully configurable in the Settings window (each can be set to Shift, Ctrl, Alt, or None; defaults are Shift/Alt/Ctrl, stored in settings keys `convertKey`, `toolsKey`, and `actionsKey`). Alternative hotkeys F8 (Convert) and F9 (Tools) work during file drags, and Esc closes the wheel.
- **Appearance**: Windows feature real frosted glass. You can choose “Light” (default), “Dark”, or “Match Windows” (Auto) via the tray menu or Settings. Glass blur strength and window opacity are fully adjustable in Settings; windows opened thereafter use the new look, while the floating wheel and progress cards switch immediately.
- **Remember settings & presets**: Tool windows remember previously used settings. Under “Presets…”, you can click “Save as preset” to apply current settings in one click later. Selection regions, time segments, and passwords are not remembered.
- **Compression quick size buttons**: Quick preset buttons for 8 / 25 / 50 / 100 MB in the compression tool fill in target sizes, suitable for upload limits in emails and messaging apps.
- **Copy result**: After saving, clicking “Copy result” copies images as image data, OCR text extraction results as text, and other files as file objects.
- **Output filenames**: New files are named by purpose, such as `photo-rotated.jpg` or `clip-compressed.mp4`; the tray menu “Save location” allows switching to a unified output folder.
- **Tray menu**: “Recent results” lists recently processed files; “Process clipboard…” directly handles screenshots or files just copied to the clipboard.
- **Explorer right-click menu** (portable edition only): When “Explorer right-click menu” is enabled in the tray menu, right-click any file or folder and select “Process with KwekDrop…”. (This option is not supported in the Microsoft Store packaged edition.)
- **Watched folders**: Tray menu “Watched folders…” allows designating folders and processing actions (format conversion or compression using previous settings); newly dropped files are automatically processed and results saved into a “KwekDrop output” subfolder within them.
- **Flows**: Tray menu “Flows…” chains multiple tools into a pipeline (each step applying a preset saved for that tool); the flow appears in the tools menu for corresponding files, such as “Resize → Watermark → Compress”.
- **Text recognition (OCR)** uses the OCR engine built into Windows without increasing the installer package size; it requires the “Optical Character Recognition” feature for the corresponding language to be installed in Windows.

## Office Documents

- PPT/PPTX/PPTM/PPS/PPSX/ODP: PDF, page-by-page PNG/JPG, TXT, and PPT/PPTX/ODP mutual format conversion.
- DOC/DOCX/DOCM/RTF/ODT: PDF, page-by-page PNG/JPG, TXT, HTML, and DOC/DOCX/RTF/ODT mutual format conversion. HTML is saved as a folder containing resources.
- XLS/XLSX/XLSM/XLSB/ODS/CSV/TSV: PDF, paginated PNG/JPG, CSV, TSV, JSON, TXT, and XLS/XLSX/ODS mutual format conversion. Multiple worksheets export as separate text files; JSON preserves all worksheets.
- F9 allows exporting selected pages to PDF, merging multiple Office files sequentially into a PDF, and inspecting/cleaning/editing metadata in modern Office files.

Native layout export for PDF, images, legacy formats, and ODF requires desktop Microsoft PowerPoint, Word, or Excel. When the corresponding desktop application is missing, conversion options dependent on it are hidden. Text extraction from modern PPTX/DOCX runs independently, and XLSX tabular text data can read cached results without Excel.

Office files are opened in read-only mode; conversions do not execute VBA macros and do not update external workbook links. Closing finishes only the file opened for the conversion; existing Office documents and application windows remain open and unaffected. Saving macro-enabled formats to non-macro formats strips macros. Compatibility and pagination between Office formats follow the installed Office engine.

Other formats not yet supported clearly display labels such as “Packing only” and “Pack ZIP”; these operations do not convert file contents.

## Tool Windows

- Image/Video Crop: The preview features a crop box initially covering the entire frame. Drag inside the box to reposition, and drag the four corners or edges to resize; areas outside darken so what you see is what is preserved. During dragging, current dimensions are displayed inside the box. You can also directly enter left x, top y, width, and height, which synchronizes the box. Selecting an aspect ratio locks the crop box to that ratio, and saved dimensions match the displayed crop dimensions exactly. “Show result” previews the cropped result, and “Reset crop” restores the full frame; video can switch to “Result” in the player.
- PDF Redaction: Drag a selection box on the page to redact content (use “Previous page” / “Next page” or page numbers to navigate, and multiple regions can be added), or enter a text snippet to redact all occurrences across the document. Text, images, and vector graphics within the box are truly removed from the file rather than merely covered by black boxes; rotated pages are supported. Watermark and redaction for a single PDF are handled within the same window.
- Image/Video Redaction / Mosaic: Drag selection regions on the preview or enter pixel dimensions; multiple regions can be added. Images update automatically upon parameter changes; video can switch to “Result” in the player. Redaction defaults to pixelated mosaic with adjustable tile size, with blur or solid color cover options available. Multiple regions can be modified or removed individually.
- Image/Video “Rotate / Flip”: Enter or use the slider to choose any angle from 0–360° (clockwise), or flip horizontally or vertically; “Rotate left 90°” and “Rotate right 90°” buttons add or subtract a right angle from the current angle. Right-angle rotations are lossless; other angles can expand the canvas to preserve the entire frame or retain original dimensions while cropping the corners, with blank space filled with color (transparent for images supporting transparency, white for JPG). Previews update immediately with angle, flip, and color adjustments (video previews directly in the playback frame). Multiple images or videos can be processed at once; video is re-encoded while preserving full duration and audio tracks.
- Both image and video cropping support custom aspect ratios, including 3:2, 21:9, 2.35:1, etc. Select “Custom” and enter width and height ratio values.
- Video “Crop Frame” only modifies the visual frame area, preserving full duration and audio tracks; the timeline is used to locate preview frames, not trim time segments.
- All audio and video tool windows include a media player offering play/pause, seek scrubbing, current time display, volume control, and mute. Original files can be played completely; incompatible formats prepare a local compatible playback file. Multi-file operations can switch between inputs.
- Tools supporting effect playback provide an “Original / Result” toggle; result playback covers a preview clip of up to 30 seconds from the current position, and seeking outside the clip reprovides the playback. Saving processes the complete file or selection without preview clip length limits.
- Time segment tools for video and audio provide start/end controls; switch to “Original” before setting time segments or dragging redaction regions. Video frame export provides frame-by-frame navigation.
- The player features a dark control bar and a thin progress bar. Trimming time segments allows dragging orange start/end markers on the timeline, with the selected interval highlighted; markers synchronize bidirectionally with time parameters on the right. In result mode, trimming markers cannot be edited; switch back to original first. Trim Audio and Video features a single Play/Stop button; playback automatically stops at the End marker, Space toggles playback, and the minimum selection range is 0.1 s.
- Audio Beep: Select multiple time intervals on the waveform.
- Metadata: View and search by file, track, and chapter; uncheck “Remove all” to edit fields and values in the table.
- Merge, Collage, and Concatenation: Adjust file order via the list.
- PDF Page Management: Reorder page lists, duplicate or remove pages, or directly enter page sequences and rotation angles.

## Scope and Limitations

All processing is performed locally on your machine. Multi-image HEIC, MPO/JPEG, multi-page TIFF, and other multi-frame formats allow switching frames under F9; the primary image (or first frame) is read by default, exporting the currently selected single frame while preserving all frames in the original file. Animated GIFs use the video conversion pipeline. The image processing limit is 80 megapixels.

PDF to Word formatting layout may vary; scanned pages can be embedded as images into Word. Extracting TXT from PDF requires selectable text in the original; scanned documents can use OCR text extraction first. PDF page image exports render at 300 DPI. PNG, WebP, HEIC, AVIF, and TIFF preserve transparency where supported, while JPG and BMP use a white background.

Target sizes are subject to format, content, and encoder constraints; if unreachable, a failure is reported rather than treating an oversized file as success. General lossy compression does not guarantee every input will shrink. Encrypted, corrupted, or unsupported inputs report errors.

“Start with Windows” can be enabled from the Settings window or the tray menu. There is currently no auto-updater or installer in the portable build. Multi-monitor setups, all DPI scaling factors, and manual interactions across every tool window have not been individually verified.

## Running and Source Code

After extracting, run `KwekDrop.exe`, keeping `backend`, `runtime`, and all DLL files in the same directory. The portable package includes the .NET 8 desktop runtime, Python, FFmpeg, 7-Zip, and required Python libraries, so no additional dependencies need to be installed.

`source/` contains the C# source code and Python processing scripts; `validation/` contains test reports. Operational logs and temporary preview files are stored in `%LOCALAPPDATA%\KwekDrop\logs`; logs automatically rotate when exceeding 4 MB, do not record full paths of processed files, and are not included in release archives. Application settings are stored in `%LOCALAPPDATA%\KwekDrop\settings.json`.

Third-party licenses are retained in the runtime directories for .NET, Python, FFmpeg, and 7-Zip, as well as Python `dist-info` directories; see the dependency manifest for specific versions and sources.

---

# KwekDrop — دليل الاستخدام

الإصدار 1.0.0، التاريخ: 2026-10-08. أداة مكتبية محلية لنظام Windows x64، تعمل في شريط المهام (صينية النظام)، دون نافذة رئيسية.

## الاستخدام

1. حدد الملفات على سطح المكتب أو في مستكشف الملفات (File Explorer)، واضغط مع الاستمرار على زر الماوس الأيسر لبدء السحب.
2. أثناء السحب، اضغط على **Shift** لإظهار عجلة **التحويل** (الصيغ)؛ حرّك المؤشر فوق الصيغة المطلوبة ثم أفلت الماوس.
3. أثناء السحب، اضغط على **Alt** لإظهار عجلة **الأدوات**، أو اضغط على **Ctrl** لإظهار عجلة **الإجراءات** (مشاركة، تثبيت عائم، نسخ الملف، نقل الملف، نسخ المسار، موجه الأوامر). عند الضغط على أكثر من مفتاح معاً، تكون الأولوية لـ Alt ثم Ctrl ثم Shift.
4. العمليات التي تتطلب خيارات إضافية تفتح نافذة أداة مدمجة؛ بعد ضبط الإعدادات اضغط على "حفظ وتطبيق". تعرض النافذة نتيجة المعالجة مع إمكانية إلغاء المهمة أو فتح مجلد الحفظ. عند قراءة الملفات الكبيرة أو إنشاء المعاينات أو الحفظ، يظهر خط تقدم دقيق أعلى منطقة المعاينة (العمليات التي تكتمل بسرعة لا تظهره) دون حجب الصورة.

بمجرد ظهور العجلة، يمكنك إفلات لوحة المفاتيح مع الاستمرار في الضغط على زر الماوس الأيسر فقط. يمكن الضغط على المفاتيح قبل بدء السحب أو أثنائه. يؤدي الضغط على **F8** أو **F9** أثناء السحب إلى فتح العجلة المقابلة (**F8** للتحويل، و**F9** للأدوات)، وهو ما يفيد عند سحب ملفات بدأت من برامج أخرى؛ وعند عدم سحب الملفات يُمرر F8 وF9 للبرنامج النشط كالمعتاد (مثل إعادة الحساب في Excel). يغلق مفتاح **Esc** العجلة. اختصارات السحب مخصصة لسحب الملفات من سطح المكتب أو مستكشف الملفات، ولا تؤثر على الكتابة العادية إطلاقاً.

تحويل الصيغ والأدوات التي لا تفتح نوافذ خيارات تعرض بطاقات تقدم في الزاوية السفلية لسطح المكتب. تعرض المهام المتعددة العدد الفعلي للعناصر المكتملة، بينما تستخدم المهام الفردية التي يتعذر قياس نسبتها مؤشراً متحركاً. عند اكتمال المهمة تُغلق البطاقة تلقائياً دون فتح أي نافذة رئيسية.

تحصل كل مهمة على بطاقة خاصة بها، مرتبة من الأعلى إلى الأسفل حسب وقت البدء. الأزرار الموجودة على البطاقة:

- **إيقاف مؤقت / استئناف**: تجميد المهمة بالكامل (بما في ذلك البرامج الخارجية مثل FFmpeg) وإيقاف العداد الزمني، والاستئناف من نقطة التوقف.
- **إلغاء**: إيقاف المهمة وحذف الملفات المؤقتة غير المكتملة؛ كما يمكن إلغاء المهام المتوقفة مؤقتاً مباشرة.
- **إخفاء**: إخفاء البطاقة فقط مع استمرار تشغيل المهمة في الخلفية، ويُرسل إشعار من شريط المهام عند الانتهاء.

تعمل مهمتان كحد أقصى في وقت واحد، وتظهر باقي المهام بحالة "في الانتظار"؛ تتيح المهمة المتوقفة مؤقتاً مكانها للمهمة التالية لتبدأ فوراً. عند وجود مهمتين أو أكثر، يظهر شريط ملخص في الأعلى يتيح "طي" البطاقات في حزمة واحدة (تعرض المهام قيد التشغيل فقط)، أو "توسيع" لعرض كافة المهام، أو "إخفاء الكل". تتيح قائمة شريط المهام إعادة عرض شريط التقدم، وإيقاف أو استئناف كل المهام، أو إلغاء كل المهام.

الواجهة متاحة باللغتين العربية والإنجليزية. المظهر الفاتح (Light) مفعل افتراضياً بتأثير الزجاج المضبب الحقيقي (حيث يظهر سطح المكتب شفافاً مع ضبابية ناعمة وغشاء أبيض رقيق)؛ كما يتوفر المظهر الداكن (Dark) ومطابقة النظام (Auto). تعتمد اللغة العربية على اتجاه القراءة فقط من اليمين لليسار (Text-only RTL) دون عكس تخطيط النوافذ، ويتكيف الخط تلقائياً مع اللغة (خط ثمانية Thmanyah Sans للعربية وSegoe UI Variable للإنجليزية). تتبع الواجهة لغة عرض ويندوز عند أول تشغيل، ويمكن التبديل في أي وقت من قائمة صينية النظام أو نافذة الإعدادات؛ وتُحفظ تفضيلاتك.

لا توجد نافذة رئيسية دائمة لـ KwekDrop؛ بل يعمل بهدوء في منطقة الإشعارات (صينية النظام). عند التشغيل الأول، يفتح دليل إعداد تفاعلي من 4 صفحات (مرحباً، جرب الآن مع سحب حقيقي ومفاتيح حية، خصص تجربتك لاختيار اللغة والمظهر والبدء مع ويندوز، وكل شيء جاهز). يمكنك إعادة فتح دليل الإعداد في أي وقت من قائمة صينية النظام عبر المساعدة > دليل الاستخدام (Help > Guide)، أو من نافذة الإعدادات. يؤدي النقر المزدوج على أيقونة صينية النظام، أو اختيار "الإعدادات" من قائمتها، أو تشغيل KwekDrop مرة ثانية وهو قيد العمل، إلى فتح نافذة الإعدادات.

يُلغي مفتاح Esc العجلة العائمة. إفلات الماوس خارج قطاعات الإجراءات يُلغي العملية دون تنفيذ أي مهمة. بالنقر بزر الماوس الأيمن على أيقونة شريط المهام يمكنك عرض شريط التقدم، أو إيقاف كل المهام مؤقتاً أو إلغاؤها، أو فتح مجلد المخرجات الأخير، أو تبديل اللغة، أو الخروج من KwekDrop.

تُحفظ النتائج في نفس مجلد الملف الأصلي مع إضافة لاحقة للعملية على اسم الملف؛ وتُرقّم الملفات ذات الأسماء المتطابقة تلقائياً دون استبدال الملف الأصلي. يؤدي تصدير الصفحات المتعددة وتقسيم الفيديو إلى إنشاء مجلد جديد. تدعم المعالجة الجماعية ما يصل إلى 200 ملف إدخال، وفشل معالجة ملف واحد لا يوقف معالجة بقية الملفات.

## الفئات الخمس للوظائف

| الفئة | التحويل والأدوات المتقدمة |
| --- | --- |
| الصور | مدخلات JPG وPNG وWebP وHEIC وTIFF وSVG وAVIF وBMP؛ تصدير PDF، وتصدير DOCX لصور JPG/PNG. الضغط، البيانات الوصفية، بيانات الموقع (إزالة أو تعديل)، التعريض/الألوان/الوضوح/إزالة الضباب/تقليل التشويش/الحبيبات، الخلفية/التدرج/الزوايا الدائرية/الظلال، القص، التدوير/القلب، تعديل الحجم، العلامة المائية، إنشاء أيقونات ICO والمواقع، استخراج النص (OCR)، دمج صور متعددة في صور متحركة GIF أو شرائح، مناطق حجب متعددة (لون خالص، ضبابي، بكسلة)، تجميع PDF، ودمج الصور (كولاج). |
| الفيديو | MP4 وMOV وMKV وWebM وAVI وWMV وGIF مع استخراج MP3. الضغط، البيانات الوصفية، بيانات الموقع (إزالة أو تعديل)، إزالة المسار الصوتي، قص المقطع الزمني أو إطار العرض، التدوير/القلب، الدقة ومعدل الإطارات، التحويل إلى GIF، التلاشي/العكس/التكرار، دمج الترجمة واستبدال أو خلط المسار الصوتي، العلامة المائية، ملخص الصور المصغرة، تغيير السرعة مع الحفاظ على طبقة الصوت، دمج مقاطع فيديو مختلفة الأبعاد أو باحتواء صوتي مختلف، التنقل إطاراً بإطار والتقاط الصور، التقسيم، ومناطق حجب متعددة محددة زمنياً. |
| الصوت | MP3 وM4A وWAV وFLAC وOGG وOpus وAIFF وWMA. الضغط، البيانات الوصفية، موازنة شدة الصوت وتحليلها، القص وإزالة الصمت من الأطراف، قنوات أحادية/ستيريو وتوازن مستوى الصوت يميناً ويساراً، مستوى الصوت والتلاشي وتقليل الضوضاء، دمج ملفات صوتية متعددة، نغمات الرنين (.m4r)، التمثيل المرئي للصوت كفيديو مع أشكال موجية أو خلفيات صور، وفترات كتم صوت متعددة مع صوت تنبيه. |
| المستندات | تحويل PDF → DOCX/JPG/PNG/TXT؛ وتحويل TXT → PDF/JPG/PNG/SRT/VTT؛ والتحويل التبادلي بين SRT وVTT وTXT. ضغط PDF، البيانات الوصفية، الدمج، التقسيم حسب مجموعات الصفحات، إعادة ترتيب/تدوير/تكرار/إزالة الصفحات، إضافة أو إزالة كلمات المرور، ترقيم الصفحات والرأس والتذييل، استخراج الصور المدمجة، العلامة المائية، الحجب (تعتيم وحذف المحتوى المحدد أو نصوص معينة فعلياً)، واستخراج النص من المستندات الممسوحة ضوئياً (مع إمكانية إنشاء مستند PDF قابل للبحث). |
| الأرشيف | التحويل التبادلي وفك الضغط بين ZIP وTAR وGZIP وRAR؛ بالإضافة إلى ضغط الملفات العادية أو المجلدات، مع دعم صيغة 7z، والتشفير بكلمة مرور AES-256، والتجزئة لأحجام متعددة. تستخدم صيغة RAR نمط التخزين غير المضغوط، بما يتطابق مع مخرجات RAR للمنتج المرجعي. |

تم التحقق بموجب قواعد الإدخال والإخراج المعتمدة، بإجمالي 188 اتجاهاً للتحويل و25 أداة متقدمة مستقلة. عند اختيار ملفات متعددة، تعرض العجلة العمليات المشتركة المدعومة لجميع الملفات المحددة؛ وتتغير الأدوات المتاحة بحسب صيغة الملفات وعددها.

## ميزات إضافية

- **دمج الأدوات المتشابهة في عجلة واحدة**: عندما يصل عدد الأدوات لصورة واحدة أو فيديو واحد إلى 10 أدوات أو أكثر، تُدمج الأدوات المترابطة في عنصر واحد على العجلة ويُفتح في نافذة واحدة، مثل "القص والتدوير"، و"المقاس والضغط"، و"الخلفية والعلامة المائية" للصور؛ أو "قص المقطع والتقسيم"، و"الصوت والترجمة"، و"العلامة المائية والحجب" للفيديو. تتيح التبويبات في أعلى النافذة التبديل بين الأدوات؛ بعد تعديل أداة ما اضغط على "تطبيق هذه الخطوة" للمتابعة في تطبيق الأداة التالية على النتيجة، واستخدم "التراجع عن الخطوة السابقة" للرجوع، ثم اضغط على "حفظ وتطبيق" عند الانتهاء لحفظ التعديلات دفعة واحدة. كل تبويب في "قص المقطع والتقسيم" و"لقطات و GIF" يُحفظ بشكل مستقل. تبقى البيانات الوصفية وإنشاء الأيقونات واستخراج النص (OCR) دائماً كأدوات مستقلة. الملفات التي تملك أقل من 10 أدوات لا تتأثر.
- **عجلتان (تفرع حسب التصنيف)** (اختياري): يمكنك أيضاً استخدام نمط العجلتين: تعرض العجلة الأولى التصنيفات، وبتحريك الملف فوق أحد التصنيفات تتوسع عجلة خارجية ثانية تعرض أدوات ذلك التصنيف. يمكن اختيار "دمج الأدوات المتشابهة في عجلة واحدة" أو "عجلتان (تفرع حسب التصنيف)" من نافذة الإعدادات أو من قائمة شريط المهام تحت "نمط العجلة للأدوات المتعددة" (ضمن قائمة "العجلة").
- **بيانات الموقع GPS**: تشتمل قائمة أدوات الصور والفيديو على "بيانات الموقع GPS"، حيث تعرض النافذة موقع الالتقاط المسجل في الملف. يمكنك اختيار "حذف بيانات الموقع" (لحذف إحداثيات GPS من بيانات EXIF/XMP ووسوم الفيديو)، أو "تحديد إحداثيات جديدة": عبر لصق الإحداثيات (خط العرض، خط الطول، مثل `31.2304, 121.4737`، التي يمكن نسخها بالنقر بزر الماوس الأيمن على أي موقع في تطبيقات الخرائط؛ كما تُدعم صيغ مثل `33.8688 S 151.2093 E`)، أو اختيار صورة ملتقطة في ذلك المكان لاستعارة إحداثياتها. يتم الحفاظ على باقي البيانات مثل الكاميرا والتاريخ والاتجاه؛ ولا يُعاد ضغط صور JPG، كما تُنسخ مسارات الصوت والفيديو مباشرة دون إعادة ترميز. ونظراً لأن KwekDrop لا يتصل بالإنترنت، فإنه لا يعرض خرائط. يمكن معالجة صور ومقاطع فيديو متعددة في نفس الوقت.
- **تخصيص العجلة**: يتيح خيار "تخصيص العجلة…" في قائمة شريط المهام إعادة ترتيب الخيارات حسب نوع الملف ونوع القائمة (صيغ / أدوات)؛ يظهر العنصر الأول في أعلى العجلة وتتبعه باقي العناصر باتجاه عقارب الساعة. افتراضياً يُوضع آخر خيار تم استخدامه في المقدمة ويُميّز بالرمز ↻، ويمكن إلغاء هذه الميزة من هنا أيضاً.
- **نافذة الإعدادات (Settings)**: لوحة إعدادات شبه مربعة بمقاس 600×580 تجمع كافة خيارات التطبيق: المظهر (نهاري Light، ليلي Dark، مطابقة النظام Auto)، واللغة (العربية، الإنجليزية)، ومؤثرات الزجاج (قوة الضبابية ونسبة الشفافية)، وبدء التشغيل مع ويندوز، واختصارات السحب أثناء الإفلات، وتخطيط العجلة (المدمجة أو عجلتان)، وزر لفتح دليل الإعداد، وخيار استعادة الإعدادات الافتراضية. تُفتح بالنقر المزدوج على أيقونة صينية النظام، أو باختيار الإعدادات من قائمتها، أو عند تشغيل KwekDrop ثانية أثناء عمله.
- **اختصارات السحب (Drag shortcuts)**: أثناء سحب الملفات، يؤدي الضغط على المفاتيح إلى إظهار العجلات: Shift لعجلة التحويل، وAlt لعجلة الأدوات، وCtrl لعجلة الإجراءات (الأولوية: Alt ثم Ctrl ثم Shift). هذه المفاتيح الثلاثة قابلة للتخصيص بالكامل من نافذة الإعدادات (يمكن تعيين كل منها إلى Shift أو Ctrl أو Alt أو بدون مفتاح؛ الإعداد الافتراضي هو Shift/Alt/Ctrl، وتُخزن في مفاتيح الإعدادات `convertKey` و`toolsKey` و`actionsKey`). كما يعمل المفتاحان البديلان F8 (للتحويل) وF9 (للأدوات) أثناء سحب الملفات، ويُغلق مفتاح Esc العجلة.
- **المظهر (Appearance)**: تعتمد النوافذ زجاجاً مضبباً حقيقياً. تتيح قائمة صينية النظام ونافذة الإعدادات الاختيار بين "نهاري" (Light - الافتراضي)، أو "ليلي" (Dark)، أو "مطابقة النظام" (Auto)، مع إمكانية ضبط قوة الضبابية ونسبة الشفافية من نافذة الإعدادات؛ وتستخدم النوافذ التي تُفتح لاحقاً المظهر الجديد، بينما تتغير العجلة العائمة وبطاقات التقدم على الفور.
- **تذكر الإعدادات والقوالب**: تتذكر نوافذ الأدوات آخر إعدادات تم استخدامها؛ ومن خلال "القوالب…" يمكنك اختيار "حفظ كقالب جاهز" لتطبيق الإعدادات الحالية بضغطة زر مستقبلاً. لا يتم تذكر مناطق التحديد والمقاطع الزمنية وكلمات المرور.
- **أزرار الحجم السريع للضغط**: توفر أداة الضغط أزراراً سريعة لأحجام 8 / 25 / 50 / 100 ميجابايت لملء الحجم المستهدف مباشرة، بما يناسب حدود الرفع في البريد الإلكتروني وتطبيقات المراسلة.
- **نسخ النتيجة**: بعد الحفظ، يتيح النقر على "نسخ النتيجة" نسخ الصور كبيانات صور، ونسخ نصوص استخراج النص (OCR) كنص عادي، ونسخ الملفات الأخرى كملفات.
- **تسمية ملفات الإخراج**: تُسمى الملفات الجديدة بحسب وظيفتها، مثل `photo-rotated.jpg` أو `clip-compressed.mp4`؛ ويتيح خيار "موقع الحفظ" في قائمة شريط المهام توحيد مجلد المخرجات لجميع العمليات.
- **قائمة شريط المهام**: يعرض خيار "آخر الملفات المحفوظة" أحدث الملفات المعالجة؛ بينما يتيح خيار "معالجة الحافظة…" التعامل مباشرة مع لقطات الشاشة أو الملفات المنسوخة حديثاً في الحافظة.
- **قائمة سياق ويندوز (كليك يمين)** (للنسخة المحمولة فقط): عند تفعيل "قائمة سياق ويندوز (كليك يمين)" في قائمة شريط المهام، يمكنك النقر بزر الماوس الأيمن على أي ملف أو مجلد واختيار "معالجة باستخدام KwekDrop…". (هذا الخيار غير مدعوم في نسخة متجر مايكروسوفت).
- **مراقبة المجلدات**: يتيح خيار "مراقبة المجلدات…" في شريط المهام تحديد مجلدات معينة ونوع المعالجة المطلوبة (تحويل الصيغة أو الضغط بآخر إعدادات)؛ وستتم معالجة أي ملف جديد يُوضع فيها تلقائياً وحفظ النتيجة في مجلد فرعي باسم "مخرجات KwekDrop" داخله.
- **العمليات**: يتيح خيار "العمليات…" في شريط المهام ربط عدة أدوات في مسار معالجة تسلسلي (بحيث تستخدم كل خطوة القالب المحفوظ لتلك الأداة)، وتظهر العملية ضمن قائمة أدوات الملفات المطابقة، مثل "تغيير الحجم → إضافة علامة مائية → ضغط".
- **استخراج النص (OCR)**: يستخدم محرك التعرف الضوئي على الحروف المدمج في نظام Windows دون زيادة حجم حزمة التثبيت؛ ويتطلب ذلك تثبيت حزمة "التعرف الضوئي على الحروف" للغة المطلوبة في النظام.

## مستندات Office

- PPT/PPTX/PPTM/PPS/PPSX/ODP: تحويل تبادلي إلى PDF، وصور PNG/JPG لكل صفحة، وTXT، والتحويل التبادلي بين صيغ PPT وPPTX وODP.
- DOC/DOCX/DOCM/RTF/ODT: تحويل تبادلي إلى PDF، وصور PNG/JPG لكل صفحة، وTXT، وHTML، والتحويل التبادلي بين صيغ DOC وDOCX وRTF وODT. يُحفظ HTML كمجلد يحتوي على الموارد.
- XLS/XLSX/XLSM/XLSB/ODS/CSV/TSV: تحويل تبادلي إلى PDF، وصور PNG/JPG مقسمة، وCSV، وTSV، وJSON، وTXT، والتحويل التبادلي بين صيغ XLS وXLSX وODS. تُصدر أوراق العمل المتعددة كملفات نصية منفصلة، ويحتفظ ملف JSON بكافة أوراق العمل.
- يتيح مفتاح F9 تصدير صفحات محددة إلى PDF، ودمج ملفات Office متعددة بالتسلسل في ملف PDF واحد، وعرض/تنظيف/تعديل البيانات الوصفية لملفات Office الحديثة.

يتطلب التصدير بالتنسيق الأصلي لملفات PDF والصور والصيغ القديمة وODF وجود برنامج Microsoft PowerPoint أو Word أو Excel المكتبي. وفي حال عدم تثبيت التطبيق المكتبي المناسب، تُخفى خيارات التحويل المعتمدة عليه. يعمل استخراج النصوص من ملفات PPTX/DOCX الحديثة بشكل مستقل، بينما يمكن قراءة البيانات النصية لملفات XLSX دون الحاجة لبرنامج Excel عبر قراءة النتائج المخزنة مؤقتاً.

تُفتح ملفات Office بوضع القراءة فقط؛ ولا تؤدي عمليات التحويل إلى تشغيل وحدات ماكرو VBA أو تحديث ارتباطات المصنفات الخارجية. وعند انتهاء التحويل، يُغلق الملف الذي فُتح لهذه العملية فقط؛ مع بقاء مستندات ونوافذ Office المفتوحة سابقاً دون أي تأثير. يؤدي حفظ الملفات التي تحتوي على ماكرو بصيغ لا تدعم الماكرو إلى إزالة وحدات الماكرو. يتبع التوافق وتقسيم الصفحات بين صيغ Office المحرك المستخدم لبرامج Office المثبتة.

الملفات الأخرى غير المدعومة حالياً تعرض تصنيفات صريحة مثل "حزم فقط" و"حزم في ملف ZIP"؛ وهذه العمليات لا تغيّر أو تحول محتوى الملفات.

## نوافذ الأدوات

- قص الصور والفيديو: تحتوي المعاينة على إطار للقص يغطي كامل المشهد في البداية. اسحب داخل الإطار لتغيير موضعه، واسحب الزوايا الأربع أو الحواف الأربع لتعديل حجمه؛ تصبح المناطق خارج الإطار داكنة بحيث يكون ما تراه هو ما سيتم حفظه؛ وأثناء السحب يظهر المقاس الحالي داخل الإطار. يمكنك أيضاً إدخال إحداثيات x من اليسار، وy من الأعلى، والعرض، والارتفاع مباشرة لتعديل الإطار تلقائياً. يؤدي اختيار نسبة عرض إلى ارتفاع إلى قفل إطار القص على تلك النسبة، وتطابق أبعاد الملف المحفوظ المقاس الظاهر في الإطار تماماً. يتيح زر "عرض النتيجة" معاينة المظهر بعد القص، ويعيد زر "إعادة تعيين القص" المشهد إلى وضعه الكامل؛ وبالنسبة للفيديو يمكن اختيار "معاينة النتيجة" في المشغل.
- حجب محتوى PDF: اسحب لتحديد إطار حول المحتوى المطلوب حجبه على الصفحة (يمكنك التنقل باستخدام "الصفحة السابقة" / "الصفحة التالية" أو رقم الصفحة، مع إمكانية إضافة مناطق متعددة)، كما يمكنك كتابة نص ليتم حجبه في جميع مواضع ظهوره بالمستند. يتم حذف النصوص والصور والرسومات داخل الإطار من الملف فعلياً وليس مجرد تغطيتها بمربع أسود؛ ويسري ذلك أيضاً على الصفحات المدورة. يتم تطبيق العلامة المائية والحجب لملف PDF الفردي داخل نفس النافذة.
- حجب/بكسلة الصور والفيديو: اسحب منطقة التحديد على المعاينة أو أدخل أبعاد البكسل، مع إمكانية إضافة مناطق متعددة. تتحدث الصور تلقائياً بعد تعديل الخيارات؛ وبالنسبة للفيديو يمكن اختيار "معاينة النتيجة" في المشغل. يستخدم الحجب افتراضياً نمط الموزاييك (البكسلة) مع إمكانية ضبط حجم الحبيبات، أو اختيار التمويه أو التغطية بلون خالص. يمكن تعديل كل منطقة أو إزالتها على حدة.
- "تدوير / قلب" الصور والفيديو: يمكنك كتابة أي زاوية أو استخدام شريط التمرير لاختيار زاوية من 0 إلى 360 درجة (باتجاه عقارب الساعة)، أو قلب الصورة أفقياً أو رأسياً؛ ويضيف زرا التدوير 90 درجة لليسار أو لليمين زاوية قائمة إلى الزاوية الحالية في كل نقرة. التدوير بزوايا قائمة غير منقوص للجودة؛ بينما تتيح الزوايا الأخرى توسيع مساحة العمل للحفاظ على كامل المشهد، أو الحفاظ على الأبعاد الأصلية مع قص الزوايا، مع ملء المساحات الفارغة بلون محدد (شفاف للصور التي تدعم الشفافية، وأبيض لصور JPG). تتحدث المعاينة فوراً مع تعديل الزاوية والقلب واللون (تتم معاينة الفيديو مباشرة على شاشة التشغيل). يمكن معالجة صور أو مقاطع فيديو متعددة في المرة الواحدة؛ ويُعاد ترميز الفيديو مع الحفاظ على كامل المدة والمسارات الصوتية.
- يدعم قص الصور والفيديو تحديد نسب مخصصة للعرض إلى الارتفاع، بما في ذلك 3:2 و21:9 و2.35:1 وغيرها؛ اختر "مخصص" ثم أدخل قيم العرض والارتفاع.
- أداة "قص الإطار" للفيديو تغيّر مساحة الإطار المرئي فقط، مع الحفاظ على كامل المدة والمسار الصوتي؛ ويُستخدم شريط التمرير الزمني لتحديد إطار المعاينة دون اقتطاع فترات زمنية.
- تشتمل كافة نوافذ أدوات الصوت والفيديو على مشغل وسائط يوفر أزرار التشغيل/الإيقاف المؤقت، وسحب شريط التقدم، وعرض الوقت الحالي، والتحكم في مستوى الصوت وكتم الصوت. يمكن تشغيل الملف الأصلي بالكامل؛ بينما تُجهز الملفات ذات الصيغ غير المتوافقة محلياً لتشغيل متوافق. تتيح العمليات المتعددة التبديل بين مدخلات الملفات المختلفة للتشغيل.
- توفر الأدوات التي تدعم استعراض النتائج زر تبديل بين "الملف الأصلي / معاينة النتيجة"؛ وتشغل معاينة النتيجة مقطعاً يصل إلى 30 ثانية من الموضع الحالي، وتتم إعادة تجهيز المعاينة عند سحب شريط التقدم خارج هذا النطاق. تطبق عملية الحفظ التعديل على كامل الملف أو النطاق الزمني المحدد دون التقيد بطول مقطع المعاينة.
- توفر أدوات اقتطاع الفترات الزمنية للفيديو والصوت خيارات لتحديد نقطتي البدء والانتهاء؛ ويجب التبديل إلى "الملف الأصلي" قبل تحديد النطاق الزمني أو سحب مناطق الحجب. توفر أداة حفظ إطارات الفيديو إمكانية التنقل الدقيق إطاراً بإطار.
- يستخدم المشغل شريط تحكم داكناً وشريط تقدم رفيعاً. يتيح قص المقطع الزمني سحب مؤشري البدء والانتهاء البرتقاليين على شريط الوقت، مع إبراز الفترة المحددة؛ ويتزامن المؤشران في كلا الاتجاهين مع معلمات الوقت على اليمين. في وضع معاينة النتيجة لا يمكن تعديل مؤشرات القص، ويجب الرجوع للملف الأصلي أولاً. يتميز قص الصوت والفيديو بزر تشغيل/إيقاف واحد؛ يتوقف التشغيل تلقائياً عند مؤشر النهاية (End)، ويقوم زر المسافة (Space) بتبديل التشغيل والإيقاف، والحد الأدنى لنطاق التحديد هو 0.1 ثانية.
- التنبيه الصوتي (Beep): يتيح تحديد فترات زمنية متعددة على الشكل الموجي للصوت لكتمها واستبدالها بنغمة تنبيه.
- البيانات الوصفية: عرض وبحث بحسب الملف والمسار والفصل؛ قم بإلغاء تحديد خيار "حذف الكل" لتعديل الحقول والقيم في الجدول.
- الدمج وتجميع الصور والربط التسلسلي: ترتيب الملفات من خلال القائمة.
- إدارة صفحات PDF: إعادة ترتيب قائمة الصفحات، وتكرار أو استبعاد الصفحات، أو إدخال ترتيب الصفحات وزوايا التدوير مباشرة.

## النطاق والقيود

تتم كافة المعالجات محلياً على جهازك. تتيح صيغ الصور متعددة الإطارات مثل HEIC متعددة اللقطات، وMPO/JPEG، وTIFF متعدد الصفحات، التبديل بين إطارات الصور عبر مفتاح F9؛ ويُقرأ الإطار الرئيسي (أو الإطار الأول) افتراضياً لتصدير الإطار المحدد حالياً، مع الحفاظ على كافة إطارات الملف الأصلي. تستخدم صور GIF المتحركة مسار تحويل الفيديو. يبلغ الحد الأقصى لمعالجة الصور 80 ميجابكسل.

قد يتغير التخطيط عند تحويل PDF إلى Word؛ ويمكن إدراج الصفحات الممسوحة ضوئياً كصور داخل مستند Word. يتطلب استخراج نصوص TXT من PDF وجود نصوص قابلة للتحديد في الملف الأصلي؛ ويمكن استخدام "استخراج النص (OCR)" أولاً مع المستندات الممسوحة ضوئياً. تُصدر صور صفحات PDF بدقة 300 DPI. تحافظ صيغ PNG وWebP وHEIC وAVIF وTIFF على الشفافية المدعومة، بينما تستخدم صيغتا JPG وBMP خلفية بيضاء.

يخضع الحجم المستهدف لقيود الصيغة والمحتوى ومحرك الترميز؛ وإذا تعذر الوصول للحجم المطلوب يُسجل فشل العملية، ولا يُعتبر الملف الذي يتجاوز الحجم المستهدف نجاحاً. لا يضمن الضغط المنقوص التقليدي تقليل حجم كل ملف إدخال. وتُبلغ الأداة عن أخطاء عند إدخال ملفات مشفرة أو تالفة أو غير مدعومة.

يمكن تفعيل "بدء التشغيل مع الويندوز" من نافذة الإعدادات أو قائمة شريط المهام. لا يوجد حالياً تحديث تلقائي. ولم يتم التحقق الفردي بعد من الشاشات المتعددة في Windows، وكافة نسب التحجيم (DPI)، والتفاعل اليدوي مع كل نافذة أداة على حدة.

## التشغيل والمصدر

بعد فك الضغط بالكامل، شغّل `KwekDrop.exe` مع الإبقاء على مجلدات `backend` و`runtime` وكافة ملفات DLL في نفس المجلد. تم تضمين بيئة تشغيل .NET 8 المكتبية، وبايثون (Python)، وFFmpeg، و7-Zip، والمكتبات المطلوبة داخل الحزمة، دون الحاجة لتثبيت أي متطلبات إضافية.

يحتوي مجلد `source/` على كود C# المصدري وسكربتات المعالجة بلغة بايثون؛ ويحتوي مجلد `validation/` على تقارير الاختبار. تُحفظ سجلات التشغيل وملفات المعاينة المؤقتة في المسار `%LOCALAPPDATA%\KwekDrop\logs`؛ وتدور السجلات تلقائياً عند تجاوز 4 ميجابايت، ولا تسجل المسارات الكاملة للملفات المعالجة، كما لا تُضمّن في حزم الإصدار. تُحفظ إعدادات التطبيق في المسار `%LOCALAPPDATA%\KwekDrop\settings.json`.

تُحفظ تراخيص المكونات الخارجية في مجلدات تشغيل .NET وPython وFFmpeg و7-Zip، وفي مجلدات `dist-info` الخاصة ببايثون؛ وللاطلاع على الإصدارات والمصادر بالتفصيل راجع ملف قائمة التبعيات.
