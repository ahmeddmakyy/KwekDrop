using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KwekDrop;

internal sealed record Field(string Name, string Label, string Default = "", string Kind = "text", string[]? Choices = null, double Min = 0, double Max = 0, double Step = 0, string Unit = "", string Group = "");
// Parts: the tools a combined entry opens together. Chain: each part works on the previous parts' result.
// Recent: the option used last for this kind of file, shown first in the wheel with a ↻ mark.
internal sealed record Operation(string Id, string Label, bool Tool = false, Field[]? Fields = null, bool Ordered = false, string[]? Parts = null, bool Chain = false, bool Recent = false);

internal sealed record ToolGroup(string Label, List<Operation> Items);

internal static class Catalog
{
    // Files with ten or more tools show categories first; this is how each kind of file is divided.
    private static readonly Dictionary<string, (string Label, string[] Ids)[]> Categories = new()
    {
        ["image"] = [(L.Arabic ? "الأبعاد والاتجاه" : "Size & Orientation", ["cropImage", "rotateImage", "resizeImage"]), (L.Arabic ? "المظهر والتعديل" : "Appearance & Edit", ["editImage", "frameImage", "watermark", "redactImage"]),
            (L.Arabic ? "الإخراج والتحسين" : "Export & Compress", ["compress", "removeMetadata", "location", "ocrImage", "makeIcon", "createPDF", "createCollage", "createAnimation", "packArchive"])],
        ["video"] = [(L.Arabic ? "المونتاج والقص" : "Trim & Split", ["trimVideo", "splitVideo", "joinVideos", "changeVideoSpeed", "videoSnapshots", "videoEffects"]), (L.Arabic ? "الإطار والتدوير" : "Frame & Crop", ["cropVideo", "rotateVideo", "videoSettings", "watermark", "redactVideo"]),
            (L.Arabic ? "الصوت والترجمة" : "Audio & Subtitles", ["muteVideo", "subtitlesAudio"]), (L.Arabic ? "الإخراج والضغط" : "Export & Compress", ["compress", "removeMetadata", "location", "videoToGif", "packArchive"])],
        ["audio"] = [(L.Arabic ? "القص والدمج" : "Trim & Join", ["trimAudio", "redactAudio", "joinAudio"]), (L.Arabic ? "الصوت والقنوات" : "Volume & Channels", ["normalizeAudio", "audioChannels", "audioEffects"]),
            (L.Arabic ? "الإخراج والضغط" : "Export & Compress", ["compress", "removeMetadata", "audioToVideo", "ringtone", "packArchive"])],
        ["document"] = [(L.Arabic ? "صفحات PDF" : "PDF Pages", ["splitPDF", "mergePDF", "organizePDF", "extractPdfImages", "pdfNumbers"]), (L.Arabic ? "الحماية والتعرف" : "Security & OCR", ["pdfPassword", "ocrPDF", "watermark", "redactPDF", "removeMetadata"]),
            (L.Arabic ? "الإخراج والضغط" : "Export & Compress", ["compress", "packArchive"])],
    };

    // When a single file has ten or more tools, related tools share one window. The older two-wheel layout stays
    // available (tray menu) and is used instead when chosen.
    public const string LayoutKey = "wheelLayout";
    public static bool TwoWheels => Settings.GetString(LayoutKey) == "twoWheels";
    private static readonly Dictionary<string, (string Id, string Label, string[] Parts, bool Chain)[]> Merged = new()
    {
        ["image"] = [("merge:cropRotate", L.Arabic ? "قص وتدوير" : "Crop & Rotate", ["cropImage", "rotateImage"], true), ("merge:sizeCompress", L.Arabic ? "تغيير الحجم وضغط" : "Resize & Compress", ["resizeImage", "compress"], true),
            ("merge:frameWatermark", L.Arabic ? "إطار وعلامة مائية" : "Frame & Watermark", ["frameImage", "watermark"], true)],
        ["video"] = [("merge:trimSplit", L.Arabic ? "قص وتقسيم" : "Trim & Split", ["trimVideo", "splitVideo"], false), ("merge:speedEffects", L.Arabic ? "السرعة والتأثيرات" : "Speed & Effects", ["changeVideoSpeed", "videoEffects"], true),
            ("merge:cropRotateVideo", L.Arabic ? "قص وتدوير الفيديو" : "Crop & Rotate Video", ["cropVideo", "rotateVideo"], true), ("merge:resolutionCompress", L.Arabic ? "الدقة والضغط" : "Resolution & Compress", ["videoSettings", "compress"], true),
            ("merge:soundSubtitles", L.Arabic ? "الصوت والترجمة" : "Audio & Subtitles", ["muteVideo", "subtitlesAudio"], true), ("merge:watermarkRedact", L.Arabic ? "علامة مائية وتعتيم" : "Watermark & Redact", ["watermark", "redactVideo"], true),
            ("merge:framesGif", L.Arabic ? "حفظ كـ GIF" : "Frames & GIF", ["videoSnapshots", "videoToGif"], false)],
        ["document"] = [("merge:watermarkRedactPdf", L.Arabic ? "علامة مائية وتعتيم" : "Watermark & Redact", ["watermark", "redactPDF"], true)],
    };

    // Replaces each tool that belongs to a combined entry by that entry (in the place of its first tool).
    // Several files can share a combined window as well; each of its tools then saves on its own.
    private static List<string> Merge(string kind, List<string> ids)
    {
        if (TwoWheels || ids.Count < 10 || !Merged.TryGetValue(kind, out var plan))
            return ids;
        var result = new List<string>();
        foreach (string id in ids)
        {
            var entry = plan.FirstOrDefault(m => m.Parts.Contains(id) && m.Parts.All(ids.Contains));
            string chosen = entry.Id ?? id;
            if (!result.Contains(chosen))
                result.Add(chosen);
        }
        return result;
    }

    private static Operation? MergedDefinition(string id) =>
        Merged.Values.SelectMany(x => x).Where(m => m.Id == id).Select(m => new Operation(m.Id, L.T(m.Label), true, null, false, m.Parts, m.Chain)).FirstOrDefault();

    public static List<ToolGroup>? Grouped(List<Operation> operations, string kind)
    {
        if (operations.Count < 10 || !Categories.TryGetValue(kind, out var plan))
            return null;
        var groups = new List<ToolGroup>();
        var placed = new HashSet<string>();
        void Add(string label, IEnumerable<Operation> items)
        {
            var list = items.ToList();
            if (list.Count == 0)
                return;
            groups.Add(new ToolGroup(L.T(label), list));
            foreach (var item in list)
                placed.Add(item.Id);
        }
        foreach (var (label, ids) in plan)
            Add(label, ids.Select(id => operations.FirstOrDefault(o => o.Id == id)).Where(o => o != null).Select(o => o!));
        Add(L.Arabic ? "سير العمل" : "Workflows", operations.Where(o => o.Id.StartsWith(Flows.Prefix)));
        Add(L.Arabic ? "أخرى" : "Other", operations.Where(o => !placed.Contains(o.Id)));
        return groups.Count >= 2 ? groups : null;
    }

    public static string Extension(string path)
    {
        string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return ext switch { "jpeg" => "jpg", "tif" => "tiff", "heif" => "heic", "aif" => "aiff", "tgz" => "gz", _ => ext };
    }
    public static readonly string[] Images = ["jpg", "png", "webp", "heic", "tiff", "svg", "avif", "bmp"];
    public static readonly string[] Audio = ["mp3", "m4a", "wav", "flac", "ogg", "opus", "aiff", "wma"];
    public static readonly string[] Video = ["mp4", "mov", "mkv", "webm", "avi", "wmv"];
    public static readonly string[] Archives = ["tar", "gz", "zip", "rar", "7z"];
    public static readonly string[] OfficeSlides = ["ppt", "pptx", "pptm", "pps", "ppsx", "odp"];
    public static readonly string[] OfficeWords = ["doc", "docx", "docm", "rtf", "odt"];
    public static readonly string[] OfficeSheets = ["xls", "xlsx", "xlsm", "xlsb", "ods", "csv", "tsv"];
    private static readonly string[] ImageTargets = ["png", "jpg", "webp", "heic", "tiff", "avif", "bmp", "pdf"];
    public static string Category(string path)
    {
        string ext = Extension(path);
        return Images.Contains(ext) ? "image" : Audio.Contains(ext) ? "audio" : Video.Contains(ext) || ext == "gif" ? "video" : OfficeSlides.Contains(ext) || OfficeWords.Contains(ext) || OfficeSheets.Contains(ext) ? "office" : new[] { "pdf", "txt", "srt", "vtt" }.Contains(ext) ? "document" : Archives.Contains(ext) ? "archive" : "file";
    }
    public static bool PackingOnly(string[] paths) => paths.Length > 0 && paths.All(path => Directory.Exists(path) || Category(path) == "file");
    public static bool HasPages(string[] paths) => paths.Any(path => Extension(path) == "pages");
    private static string[] Targets(string ext)
    {
        if (OfficeSlides.Contains(ext))
            return ["pdf", "pptx", "jpg", "png", "txt", "html"];
        if (OfficeWords.Contains(ext))
            return ["pdf", "docx", "txt", "md", "html", "epub", "png", "jpg"];
        if (OfficeSheets.Contains(ext))
            return ["pdf", "xlsx", "ods", "csv", "tsv", "txt"];
        if (ext == "pdf")
            return ["docx", "pptx", "xlsx", "jpg", "png", "epub", "txt", "md"];
        if (ext == "epub")
            return ["pdf", "txt", "html", "md", "docx"];
        if (ext is "srt" or "vtt" or "ass")
            return ["srt", "vtt", "ass", "txt"];
        if (Images.Contains(ext))
            return ["png", "jpg", "webp", "heic", "tiff", "avif", "bmp", "pdf"];
        if (Audio.Contains(ext))
            return ["mp3", "m4a", "wav", "flac", "ogg", "opus", "aiff", "wma"];
        if (Video.Contains(ext))
            return ["mp4", "mkv", "webm", "avi", "mov", "wmv", "gif", "mp3"];
        if (ext == "gif")
            return ["mp4", "webm", "png", "jpg"];
        if (ext == "txt")
            return ["pdf", "docx", "jpg", "png", "md"];
        return Archives;
    }
    
    public static readonly Operation[] FileActions =
    [
        new("action:share", L.Arabic ? "مشاركة" : "Share", true),
        new("action:float", L.Arabic ? "تثبيت عائم" : "Float", true),
        new("action:copy", L.Arabic ? "نسخ الملف" : "Copy", true),
        new("action:move", L.Arabic ? "نقل إلى..." : "Move", true),
        new("action:copyPath", L.Arabic ? "نسخ المسار" : "Copy Path", true),
        new("action:terminal", L.Arabic ? "موجه الأوامر" : "Terminal", true)
    ];
public static List<Operation> Options(string[] paths, bool tools)
    {
        if (paths.Length == 0 || paths.Any(p => !File.Exists(p) && !Directory.Exists(p)))
            return [];
        return Arrange(Build(paths, tools), Category(paths[0]), tools, true);
    }

    // --- the order of the wheel: the user's own order, and the option used last at the top --------------------
    public const string LastFirstKey = "wheelLastFirst";
    public static bool LastFirst => Settings.GetBool(LastFirstKey, true);
    private static string Mode(bool tools) => tools ? "tools" : "formats";
    public static bool IsToolId(string id) => !id.StartsWith("convert:") && !id.StartsWith("pack:");

    public static List<string> SavedOrder(string kind, bool tools)
    {
        try
        { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(Settings.GetString($"wheelOrder:{kind}:{Mode(tools)}") ?? "[]") ?? []; }
        catch (System.Text.Json.JsonException) { return []; }
    }

    public static void SaveOrder(string kind, bool tools, List<string>? ids) =>
        Settings.Set($"wheelOrder:{kind}:{Mode(tools)}", ids == null ? "" : System.Text.Json.JsonSerializer.Serialize(ids));

    public static void RememberLast(string[] paths, Operation operation)
    {
        if (paths.Length > 0 && operation.Id != Flows.MenuId)
            Settings.Set($"wheelLast:{Category(paths[0])}:{Mode(IsToolId(operation.Id))}", operation.Id);
    }

    private static List<Operation> Arrange(List<Operation> list, string kind, bool tools, bool markRecent)
    {
        var order = SavedOrder(kind, tools);
        var result = list.Select((operation, index) => (operation, index))
            .OrderBy(x => order.IndexOf(x.operation.Id) is int at and >= 0 ? at : int.MaxValue).ThenBy(x => x.index)
            .Select(x => x.operation).ToList();
        if (markRecent && LastFirst && Settings.GetString($"wheelLast:{kind}:{Mode(tools)}") is { Length: > 0 } last)
        {
            // A tool used on its own may now be part of a combined entry; that entry moves up instead.
            int at = result.FindIndex(o => o.Id == last);
            if (at < 0)
                at = result.FindIndex(o => o.Parts?.Contains(last) == true);
            if (at >= 0)
            {
                var recent = result[at] with { Recent = true };
                result.RemoveAt(at);
                result.Insert(0, recent);
            }
        }
        return result;
    }

    // What the wheel offers for a kind of file, in its current order, for arranging it (one file and several files together).
    public static List<Operation> Sample(string kind, bool tools)
    {
        string[] extensions = kind switch { "image" => Images, "video" => Video, "audio" => Audio, "document" => ["pdf", "txt", "srt", "vtt"], _ => Archives };
        var all = new List<Operation>();
        foreach (string extension in tools ? extensions.Take(1) : extensions)
            foreach (var paths in new[] { new[] { "x." + extension }, new[] { "x." + extension, "y." + extension } })
                foreach (var operation in Build(paths, tools))
                    if (!all.Any(o => o.Id == operation.Id))
                        all.Add(operation);
        return Arrange(all, kind, tools, false);
    }

    private static List<Operation> Build(string[] paths, bool tools)
    {
        if (tools)
        {
            var ids = Tools(paths[0], paths.Length).ToList();
            foreach (string path in paths.Skip(1))
                ids = ids.Intersect(Tools(path, paths.Length)).ToList();
            ids = Merge(Category(paths[0]), ids);
            var chosen = ids.Select(id => Definition(id, Category(paths[0]))).Select(o => paths.Length > 1 && o.Chain ? o with { Chain = false } : o).ToList();
            var flows = Flows.For(paths);
            // Flows follow the tools; if they would make the wheel too crowded they share one entry.
            if (!TwoWheels && chosen.Count + flows.Count >= 10 && flows.Count > 1)
                chosen.Add(new Operation(Flows.MenuId, L.Arabic ? "سير العمل…" : "Workflows…", true));
            else
                chosen.AddRange(flows);
            return chosen;
        }
        if (PackingOnly(paths))
            return Archives.Select(target => new Operation("pack:" + target, (L.Arabic ? "أرشيف " : "Pack ") + (target == "gz" ? "GZIP" : target.ToUpperInvariant()))).ToList();
        string ext = Extension(paths[0]);
        var targets = Targets(ext).ToList();
        foreach (string path in paths.Skip(1))
            targets = targets.Intersect(Targets(Extension(path))).ToList();
        return targets.Where(target => !paths.All(p => Extension(p) == target)).Select(target => new Operation("convert:" + target, target == "gz" ? "GZIP" : target.ToUpperInvariant())).ToList();
    }
    public static string[] ToolIds(string kind) => Tools(kind switch { "image" => "x.jpg", "video" => "x.mp4", "audio" => "x.mp3", _ => "x.pdf" }, 1);
    private static string[] Tools(string path, int count)
    {
        string kind = Category(path), ext = Extension(path);
        if (kind == "image")
        {
            if (ext == "svg")
                return count > 1 ? ["createPDF", "createCollage", "createAnimation", "packArchive"] : ["resizeImage", "makeIcon"];
            if (ext == "bmp")
                return count > 1 ? ["compress", "createPDF", "createCollage", "createAnimation", "rotateImage", "resizeImage", "watermark", "packArchive"] : ["compress", "editImage", "frameImage", "cropImage", "redactImage", "createPDF", "makeIcon", "ocrImage", "rotateImage", "resizeImage", "watermark"];
            return count > 1 ? ["compress", "removeMetadata", "createPDF", "createCollage", "createAnimation", "rotateImage", "resizeImage", "watermark", "location", "packArchive"] : ["compress", "removeMetadata", "editImage", "frameImage", "cropImage", "redactImage", "createPDF", "makeIcon", "location", "ocrImage", "rotateImage", "resizeImage", "watermark"];
        }
        if (kind == "audio")
            return count > 1 ? ["compress", "normalizeAudio", "joinAudio", "audioEffects", "packArchive"] : ["compress", "removeMetadata", "normalizeAudio", "audioToVideo", "trimAudio", "audioChannels", "redactAudio", "audioEffects", "ringtone"];
        if (kind == "video")
            return ext == "gif" ? ["removeMetadata"] : count > 1 ? ["compress", "muteVideo", "rotateVideo", "videoSettings", "watermark", "joinVideos", "location", "packArchive"] : ["compress", "removeMetadata", "muteVideo", "videoSnapshots", "changeVideoSpeed", "cropVideo", "trimVideo", "splitVideo", "redactVideo", "location", "rotateVideo", "videoSettings", "videoEffects", "videoToGif", "watermark", "subtitlesAudio"];
        if (kind == "office")
        {
            string family = OfficeSlides.Contains(ext) ? "PowerPoint" : OfficeWords.Contains(ext) ? "Word" : "Excel";
            bool render = OfficeBridge.Available(family), modern = new[] { "pptx", "pptm", "ppsx", "docx", "docm", "xlsx", "xlsm", "xlsb" }.Contains(ext);
            if (count > 1)
                return render ? ["officeMergePDF"] : [];
            return render ? modern ? ["officePdf", "removeMetadata"] : ["officePdf"] : modern ? ["removeMetadata"] : [];
        }
        if (ext == "pdf")
            return count > 1 ? ["compress", "splitPDF", "mergePDF", "watermark", "packArchive"] : ["compress", "removeMetadata", "organizePDF", "splitPDF", "pdfPassword", "pdfNumbers", "extractPdfImages", "watermark", "redactPDF", "ocrPDF"];
        if (kind == "file")
            return ["packArchive"];
        return Archives.Contains(ext) ? ["extractArchive"] : [];
    }
    private static Field N(string name, string label, string value) => new(name, label, value, "number");
    private static Field T(string name, string label, string value = "", string kind = "text") => new(name, label, value, kind);
    private static Field C(string name, string label, string value, params string[] choices) => new(name, label, value, "choice", choices);
    private static Field S(string name, string label, string value, double min, double max, double step, string unit = "", string group = "") => new(name, label, value, "slider", null, min, max, step, unit, group);
    private static Field[] Crop => [N("x", L.Arabic ? "يسار x (بكسل)" : "Left x (px)", "0"), N("y", L.Arabic ? "أعلى y (بكسل)" : "Top y (px)", "0"), N("width", L.Arabic ? "العرض (بكسل)" : "Width (px)", "0"), N("height", L.Arabic ? "الارتفاع (بكسل)" : "Height (px)", "0")];
    private static Field[] Rotation(bool video) => [N("angle", L.Arabic ? "زاوية الدوران (0-360°)" : "Angle (0-360°)", "0"), C("flip", L.Arabic ? "عكس" : "Flip", "none", "none", "horizontal", "vertical"), C("expand", L.Arabic ? "ملاءمة الإطار" : "Canvas Expand", video ? "expand" : "crop", video ? ["expand", "keep"] : ["crop", "expand", "keep"]), T("background", L.Arabic ? "لون الخلفية" : "Background Color", video ? "#000000" : "")];
    private static Field[] Positions => [C("position", L.Arabic ? "الموضع" : "Position", "bottomRight", "bottomRight", "bottomCenter", "bottomLeft", "middleRight", "center", "middleLeft", "topRight", "topCenter", "topLeft")];
    private static Field[] Times => [N("start", L.Arabic ? "البداية (د:ث.xx / ث)" : "Start (m:ss.xx / sec)", "0"), N("end", L.Arabic ? "النهاية (د:ث.xx، 0 حتى النهاية)" : "End (m:ss.xx, 0 for end)", "0")];
    private static Field[] Ratio => [C("ratio", L.Arabic ? "النسبة" : "Aspect Ratio", "free", "free", "1:1", "16:9", "9:16", "4:3", "3:4", "custom"), N("ratioWidth", L.Arabic ? "نسبة العرض" : "Aspect Width", "3"), N("ratioHeight", L.Arabic ? "نسبة الارتفاع" : "Aspect Height", "2")];
    public static Operation Definition(string id, string kind) => MergedDefinition(id) ?? id switch
    {
        "officePdf" => new(id, L.Arabic ? "تصدير صفحات PDF" : "Export PDF Pages", true, [T("pageOrder", L.Arabic ? "أرقام الصفحات (مفصولة بفاصلة)" : "Pages (comma separated)")]),
        "officeMergePDF" => new(id, L.Arabic ? "دمج إلى PDF" : "Merge to PDF", true, [], true),
        "compress" => new(id, L.Arabic ? "ضغط" : "Compress", true, kind == "audio" ? [N("bitrate", L.Arabic ? "معدل البت (kbps)" : "Bitrate (kbps)", "96"), N("targetKB", L.Arabic ? "الحجم المستهدف (KB)" : "Target KB", "0")] : [N("quality", L.Arabic ? "الجودة (1-100)" : "Quality (1-100)", kind == "video" ? "28" : "75"), N("maxEdge", L.Arabic ? "أطول ضلع (بكسل)" : "Max Edge (px)", kind == "video" ? "1280" : "2000"), N("targetKB", L.Arabic ? "الحجم المستهدف (KB)" : "Target KB", "0")]),
        "removeMetadata" => new(id, L.Arabic ? "البيانات الوصفية" : "Metadata", true, [T("remove", L.Arabic ? "إزالة كافة البيانات الوصفية" : "Remove All Metadata", "true", "bool"), T("search", L.Arabic ? "بحث في الحقول" : "Search"), T("metadata", L.Arabic ? "حقول البيانات الوصفية" : "Metadata Fields", "{}", "metadata")]),
        "editImage" => new(id, L.Arabic ? "تعديل الصورة" : "Edit Image", true, [
            S("exposure", L.Arabic ? "التعريض (EV)" : "Exposure", "0", -3, 3, 0.05, "EV", L.Arabic ? "الضوء" : "Light"),
            S("contrast", L.Arabic ? "التباين" : "Contrast", "1", 0, 2, 0.01, "", L.Arabic ? "الضوء" : "Light"),
            S("highlights", L.Arabic ? "الإضاءات" : "Highlights", "0", -100, 100, 1, "", L.Arabic ? "الضوء" : "Light"),
            S("shadows", L.Arabic ? "الظلال" : "Shadows", "0", -100, 100, 1, "", L.Arabic ? "الضوء" : "Light"),
            S("saturation", L.Arabic ? "التشبع" : "Saturation", "1", 0, 2, 0.01, "", L.Arabic ? "اللون" : "Color"),
            S("temperature", L.Arabic ? "حرارة اللون" : "Temperature", "0", -100, 100, 1, "", L.Arabic ? "اللون" : "Color"),
            S("clarity", L.Arabic ? "الوضوح" : "Clarity", "0", 0, 100, 1, "", L.Arabic ? "التفاصيل" : "Detail"),
            S("dehaze", L.Arabic ? "إزالة الضباب" : "Dehaze", "0", 0, 100, 1, "", L.Arabic ? "التفاصيل" : "Detail"),
            S("denoise", L.Arabic ? "تقليل التشويش" : "Denoise", "0", 0, 30, 1, "", L.Arabic ? "التفاصيل" : "Detail"),
            S("grain", L.Arabic ? "حبيبات الفيلم" : "Grain", "0", 0, 100, 1, "", L.Arabic ? "التفاصيل" : "Detail"),
            T("annotations", L.Arabic ? "توضيحات (JSON)" : "Annotations", "", "json")
        ]),
        "frameImage" => new(id, L.Arabic ? "إضافة إطار وخلفية" : "Add Frame", true, [
            N("canvasWidth", L.Arabic ? "عرض اللوحة" : "Canvas Width", "1400"),
            N("canvasHeight", L.Arabic ? "ارتفاع اللوحة" : "Canvas Height", "1000"),
            S("padding", L.Arabic ? "الهامش" : "Padding", "80", 0, 400, 1, "px"),
            S("radius", L.Arabic ? "استدارة الحواف" : "Corner Radius", "24", 0, 120, 1, "px"),
            S("shadow", L.Arabic ? "الظل" : "Shadow", "20", 0, 100, 1, "px"),
            T("background", L.Arabic ? "لون الخلفية" : "Background Color", "#f0ebff"),
            T("gradient", L.Arabic ? "لون التدرج الثاني" : "Gradient End"),
            T("backgroundImage", L.Arabic ? "صورة الخلفية" : "Background Image", "", "file"),
            S("backgroundBlur", L.Arabic ? "تمويه الخلفية" : "Blur", "0", 0, 60, 1, "px")
        ]),
        "cropImage" => new(id, L.Arabic ? "قص الصورة" : "Crop Image", true, [.. Crop, .. Ratio]),
        "resizeImage" => new(id, L.Arabic ? "تغيير الحجم" : "Resize", true, [C("mode", L.Arabic ? "النمط" : "Mode", "percent", "percent", "edge", "size"), N("percent", L.Arabic ? "نسبة التصغير (%)" : "Scale (%)", "50"), N("edge", L.Arabic ? "أطول ضلع (بكسل)" : "Max Edge (px)", "1920"), N("width", L.Arabic ? "العرض (بكسل)" : "Width (px)", "0"), N("height", L.Arabic ? "الارتفاع (بكسل)" : "Height (px)", "0"), T("keepRatio", L.Arabic ? "الحفاظ على النسبة" : "Keep Ratio", "true", "bool")]),
        "watermark" => new(id, L.Arabic ? "علامة مائية" : "Watermark", true, [T("text", L.Arabic ? "نص العلامة المائية" : "Text"), T("logo", L.Arabic ? "صورة العلامة (اختياري)" : "Logo", "", "file:image"), .. Positions, C("layout", L.Arabic ? "التكرار" : "Layout", "single", "single", "tiled"), N("opacity", L.Arabic ? "الشفافية (0-100)" : "Opacity", "60"), N("textSize", L.Arabic ? "حجم النص (%)" : "Text Size (%)", "5"), N("scale", L.Arabic ? "عرض الصورة (%)" : "Image Scale (%)", "20"), N("rotation", L.Arabic ? "زاوية الدوران" : "Rotation", "0"), N("margin", L.Arabic ? "الهامش (%)" : "Margin (%)", "3"), T("color", L.Arabic ? "لون النص" : "Text Color", "#ffffff")]),
        "makeIcon" => new(id, L.Arabic ? "إنشاء أيقونة" : "Make Icon", true, [C("iconKind", L.Arabic ? "الصيغة" : "Format", "ico", "ico", "favicon")]),
        "ocrImage" => new(id, L.Arabic ? "استخراج النص (OCR)" : "OCR Text", true, [T("language", L.Arabic ? "لغة التعرف" : "Language", "auto"), C("output", L.Arabic ? "المخرج" : "Output", "txt", "txt", "pdf")]),
        "ocrPDF" => new(id, L.Arabic ? "استخراج النص من PDF" : "OCR PDF", true, [T("language", L.Arabic ? "لغة التعرف" : "Language", "auto"), C("output", L.Arabic ? "المخرج" : "Output", "txt", "txt", "pdf"), T("password", L.Arabic ? "كلمة المرور" : "Password", "", "password")]),
        "createAnimation" => new(id, L.Arabic ? "إنشاء GIF متحرك" : "Create Animation", true, [C("format", L.Arabic ? "الصيغة" : "Format", "gif", "gif", "mp4"), N("seconds", L.Arabic ? "مدة كل صورة (ث)" : "Seconds Per Frame", "1"), N("maxEdge", L.Arabic ? "أطول ضلع (بكسل)" : "Max Edge (px)", "720"), T("loop", L.Arabic ? "تكرار مستمر" : "Loop", "true", "bool")], true),
        "videoToGif" => new(id, L.Arabic ? "تحويل إلى GIF" : "Video to GIF", true, [.. Times, S("fps", L.Arabic ? "معدل الإطارات (FPS)" : "FPS", "12", 1, 30, 1, "fps"), S("width", L.Arabic ? "العرض (بكسل)" : "Width (px)", "480", 16, 1920, 16, "px"), T("loop", L.Arabic ? "تكرار مستمر" : "Loop", "true", "bool")]),
        "videoSettings" => new(id, L.Arabic ? "الدقة ومعدل الإطارات" : "Resolution & FPS", true, [C("height", L.Arabic ? "الدقة (الارتفاع)" : "Resolution", "original", "original", "2160", "1440", "1080", "720", "480", "360"), S("fps", L.Arabic ? "معدل الإطارات (0 للأصل)" : "FPS", "0", 0, 240, 1, "fps")]),
        "videoEffects" => new(id, L.Arabic ? "تأثيرات الفيديو" : "Video Effects", true, [S("fadeIn", L.Arabic ? "تلاشي للظهور (ث)" : "Fade In (s)", "0", 0, 30, 0.5, "s"), S("fadeOut", L.Arabic ? "تلاشي للاختفاء (ث)" : "Fade Out (s)", "0", 0, 30, 0.5, "s"), T("reverse", L.Arabic ? "عكس اتجاه التشغيل" : "Reverse", "false", "bool"), S("loops", L.Arabic ? "عدد مرات التكرار" : "Loops", "1", 1, 20, 1)]),
        "subtitlesAudio" => new(id, L.Arabic ? "الترجمة والصوت" : "Subtitles & Audio", true, [T("subtitleFile", L.Arabic ? "ملف الترجمة (.srt / .ass / .vtt)" : "Subtitle File", "", "file:subtitle"), S("subtitleSize", L.Arabic ? "حجم الخط" : "Font Size", "22", 10, 72, 1, "pt"), T("audioFile", L.Arabic ? "ملف الصوت" : "Audio File", "", "file:audio"), T("mix", L.Arabic ? "دمج مع الصوت الأصلي" : "Mix with Original", "false", "bool"), S("volume", L.Arabic ? "مستوى الصوت الجديد" : "New Audio Volume", "1", 0, 4, 0.05, "x")]),
        "audioEffects" => new(id, L.Arabic ? "تأثيرات الصوت" : "Audio Effects", true, [S("gainDb", L.Arabic ? "تعديل مستوى الصوت (dB)" : "Gain (dB)", "0", -30, 30, 1, "dB"), S("fadeIn", L.Arabic ? "تلاشي للظهور (ث)" : "Fade In (s)", "0", 0, 10, 0.5, "s"), S("fadeOut", L.Arabic ? "تلاشي للاختفاء (ث)" : "Fade Out (s)", "0", 0, 10, 0.5, "s"), T("noiseReduction", L.Arabic ? "تقليل ضوضاء الخلفية" : "Reduce Background Noise", "false", "bool")]),
        "joinAudio" => new(id, L.Arabic ? "دمج الصوت" : "Join Audio", true, [], true),
        "ringtone" => new(id, L.Arabic ? "نغمة رنين" : "Ringtone", true, [N("start", L.Arabic ? "البداية (ث)" : "Start (s)", "0"), S("length", L.Arabic ? "المدة (ث)" : "Length (s)", "30", 1, 40, 1, "s")]),
        "pdfPassword" => new(id, L.Arabic ? "كلمة مرور PDF" : "PDF Password", true, [C("mode", L.Arabic ? "العملية" : "Action", "add", "add", "remove"), T("password", L.Arabic ? "كلمة المرور الحالية" : "Current Password", "", "password"), T("newPassword", L.Arabic ? "كلمة المرور الجديدة" : "New Password", "", "password")]),
        "pdfNumbers" => new(id, L.Arabic ? "أرقام الصفحات" : "Page Numbers", true, [T("numberFormat", L.Arabic ? "صيغة الترقيم ({n} الحالية، {total} الإجمالي)" : "Format ({n} / {total})", "{n} / {total}"), C("position", L.Arabic ? "الموضع" : "Position", "bottomRight", "bottomRight", "bottomCenter", "bottomLeft", "topRight", "topCenter", "topLeft"), N("startAt", L.Arabic ? "البدء من رقم" : "Start Page", "1"), T("header", L.Arabic ? "نص الترويسة" : "Header Text"), T("footer", L.Arabic ? "نص التذييل" : "Footer Text"), N("fontSize", L.Arabic ? "حجم الخط" : "Font Size", "10"), T("password", L.Arabic ? "كلمة المرور" : "Password", "", "password")]),
        "extractPdfImages" => new(id, L.Arabic ? "استخراج صور PDF" : "Extract Images", true, [N("minSize", L.Arabic ? "تجاهل الصور الأصغر من (بكسل)" : "Min Size (px)", "64"), T("password", L.Arabic ? "كلمة المرور" : "Password", "", "password")]),
        "packArchive" => new(id, L.Arabic ? "أرشفة وضغط" : "Archive", true, [C("format", L.Arabic ? "الصيغة" : "Format", "zip", "zip", "7z"), T("password", L.Arabic ? "كلمة المرور (اختياري)" : "Password", "", "password"), N("level", L.Arabic ? "مستوى الضغط (1-9)" : "Level", "5"), N("splitMB", L.Arabic ? "حجم التقسيم (MB)" : "Split (MB)", "0")], false),
        "rotateImage" or "rotateVideo" => new(id, L.Arabic ? "تدوير وعكس" : "Rotate & Flip", true, Rotation(id == "rotateVideo")),
        "redactImage" => new(id, L.Arabic ? "تعتيم الصورة" : "Redact Image", true, [.. Crop, C("style", L.Arabic ? "النمط" : "Style", "pixelate", "pixelate", "blur", "solid"), N("blockSize", L.Arabic ? "حجم الموزاييك (بكسل)" : "Block Size (px)", "12"), T("color", L.Arabic ? "لون التغطية" : "Color", "#000000"), T("regions", L.Arabic ? "مناطق إضافية" : "Regions", "", "json")]),
        "createPDF" => new(id, L.Arabic ? "إنشاء PDF" : "Create PDF", true, [], true),
        "createCollage" => new(id, L.Arabic ? "تجميع صور (كولاج)" : "Photo Collage", true, [C("layout", L.Arabic ? "التخطيط" : "Layout", "grid", "grid", "row", "column", "featured"), N("canvasWidth", L.Arabic ? "عرض اللوحة" : "Canvas Width", "1600"), N("canvasHeight", L.Arabic ? "ارتفاع اللوحة" : "Canvas Height", "1200"), N("columns", L.Arabic ? "عدد الأعمدة" : "Columns", "0"), N("gap", L.Arabic ? "المسافة بين الصور" : "Gap", "16"), N("radius", L.Arabic ? "استدارة الحواف" : "Radius", "0"), T("background", L.Arabic ? "لون الخلفية" : "Background Color", "#ffffff")], true),
        "muteVideo" => new(id, L.Arabic ? "كتم الصوت" : "Mute Video", true),
        "location" => new(id, L.Arabic ? "الموقع الجغرافي" : "Location", true, [C("mode", L.Arabic ? "العملية" : "Action", "strip", "strip", "set"), T("coordinates", L.Arabic ? "الإحداثيات الجديدة (خط العرض، خط الطول)" : "Coordinates"), T("locationSource", L.Arabic ? "أو نسخ من صورة أخرى" : "Location Source", "", "file:image")]),
        "redactPDF" => new(id, L.Arabic ? "تعتيم PDF" : "Redact PDF", true, [N("pageNumber", L.Arabic ? "رقم الصفحة" : "Page", "1"), T("findText", L.Arabic ? "نص للبحث والتعتيم التلقائي" : "Find Text"), T("password", L.Arabic ? "كلمة المرور" : "Password", "", "password"), T("regions", L.Arabic ? "مناطق إضافية" : "Regions", "", "json")]),
        "trimVideo" => new(id, L.Arabic ? "قص الفيديو" : "Trim Video", true, Times),
        "cropVideo" => new(id, L.Arabic ? "قص إطار الفيديو" : "Crop Video", true, [.. Crop, .. Ratio]),
        "changeVideoSpeed" => new(id, L.Arabic ? "تغيير السرعة" : "Change Speed", true, [S("speed", L.Arabic ? "مضاعف السرعة (0.125-8)" : "Speed Multiplier", "2", 0.25, 4, 0.05, "x")]),
        "joinVideos" => new(id, L.Arabic ? "دمج الفيديوهات" : "Join Videos", true, [], true),
        "videoSnapshots" => new(id, L.Arabic ? "لقطات من الفيديو" : "Snapshots", true, [N("time", L.Arabic ? "التوقيت (ث)" : "Time (s)", "0"), N("frame", L.Arabic ? "رقم الإطار" : "Frame", "0"), T("times", L.Arabic ? "توقيتات متعددة (ثواني، مفصولة بفاصلة)" : "Times"), T("sheet", L.Arabic ? "تجميع في صورة مصغرات واحدة" : "Contact Sheet", "false", "bool"), N("sheetCount", L.Arabic ? "عدد المصغرات" : "Count", "12"), N("sheetColumns", L.Arabic ? "عدد الأعمدة" : "Columns", "4")]),
        "splitVideo" => new(id, L.Arabic ? "تقسيم الفيديو" : "Split Video", true, [N("parts", L.Arabic ? "عدد الأجزاء المتساوية" : "Parts", "2"), T("splitPoints", L.Arabic ? "نقاط التقسيم (ثواني، مفصولة بفاصلة)" : "Split Points")]),
        "redactVideo" => new(id, L.Arabic ? "تعتيم الفيديو" : "Redact Video", true, [.. Crop, .. Times, C("style", L.Arabic ? "النمط" : "Style", "pixelate", "pixelate", "blur", "solid"), N("blockSize", L.Arabic ? "حجم الموزاييك (بكسل)" : "Block Size (px)", "12"), T("regions", L.Arabic ? "المناطق والفترات الزمنية" : "Regions", "", "json")]),
        "normalizeAudio" => new(id, L.Arabic ? "موازنة الصوت" : "Normalize Audio", true, [S("loudness", L.Arabic ? "مستوى الصوت المستهدف (LUFS)" : "Target LUFS", "-16", -24, -5, 0.5, "LUFS"), S("range", L.Arabic ? "نطاق الديناميكية (LRA)" : "Dynamic Range", "11", 1, 20, 1, "LU"), S("peak", L.Arabic ? "أقصى قمة (dBTP)" : "True Peak", "-1.5", -9, 0, 0.5, "dBTP")]),
        "audioToVideo" => new(id, L.Arabic ? "تحويل الصوت لفيديو" : "Audio Visualizer", true, [
            C("aspect", L.Arabic ? "نسبة الأبعاد" : "Aspect", "landscape", "landscape", "portrait", "square"),
            C("style", L.Arabic ? "النمط" : "Style", "waveform", "waveform", "still"),
            C("waveStyle", L.Arabic ? "نمط الموجة" : "Wave style", "mirror", "mirror", "bars", "line", "circle_bars", "circle_wave", "spectrum", "dots", "pulse", "wave_fill", "particles"),
            C("color", L.Arabic ? "اللون" : "Color", "#FF6500", "#FF6500", "#FFFFFF", "#22D3EE", "#8B5CF6", "#22C55E", "#EC4899", "sunset", "ocean", "rainbow", "custom"),
            T("color2", L.Arabic ? "اللون الثاني للتدرج" : "Second Color", ""),
            C("background", L.Arabic ? "الخلفية" : "Background", "black", "black", "gradient", "blurred", "still", "custom"),
            S("intensity", L.Arabic ? "حساسية الحركة" : "Sensitivity", "1.0", 0.2, 3.0, 0.1, "x"),
            S("thickness", L.Arabic ? "سماكة الخط والأشرطة" : "Thickness", "1.0", 0.5, 3.0, 0.1, "x"),
            T("glow", L.Arabic ? "توهج ناعم" : "Soft glow", "true", "bool"),
            T("title", L.Arabic ? "نص العنوان" : "Title Text", ""),
            T("overlayWave", L.Arabic ? "إظهار شكل الموجة فوق الصورة" : "Overlay waveform on picture", "false", "bool"),
            T("backgroundImage", L.Arabic ? "صورة الخلفية" : "Background Image", "", "file")]),
        "trimAudio" => new(id, L.Arabic ? "قص الصوت" : "Trim Audio", true, [.. Times, T("removeSilence", L.Arabic ? "إزالة الصمت من البداية والنهاية" : "Trim Silence", "false", "bool")]),
        "audioChannels" => new(id, L.Arabic ? "قنوات الصوت" : "Audio Channels", true, [C("channels", L.Arabic ? "القناة" : "Channels", "mono", "mono", "stereo"), S("leftGain", L.Arabic ? "مستوى اليسار" : "Left Gain", "1", 0, 1.5, 0.05, "x"), S("rightGain", L.Arabic ? "مستوى اليمين" : "Right Gain", "1", 0, 1.5, 0.05, "x")]),
        "redactAudio" => new(id, L.Arabic ? "كتم مقاطع صوتية" : "Mute Audio Sections", true, [.. Times, T("ranges", L.Arabic ? "فترات متعددة" : "Ranges", "", "json")]),
        "splitPDF" => new(id, L.Arabic ? "تقسيم PDF" : "Split PDF", true, [N("pagesPerFile", L.Arabic ? "عدد الصفحات لكل ملف" : "Pages Per File", "1"), T("password", L.Arabic ? "كلمة المرور" : "Password", "", "password")]),
        "mergePDF" => new(id, L.Arabic ? "دمج PDF" : "Merge PDF", true, [T("password", L.Arabic ? "كلمة المرور" : "Password", "", "password")], true),
        "organizePDF" => new(id, L.Arabic ? "إدارة صفحات PDF" : "Organize PDF Pages", true, [T("pageOrder", L.Arabic ? "ترتيب الصفحات" : "Page Order"), N("rotation", L.Arabic ? "زاوية الدوران العامة (0/90/180/270)" : "Rotation", "0"), T("rotations", L.Arabic ? "تدوير صفحات مخصصة" : "Page Rotations", "{}", "json"), T("password", L.Arabic ? "كلمة المرور" : "Password", "", "password")]),
        "extractArchive" => new(id, L.Arabic ? "استخراج الأرشيف" : "Extract Archive", true, [T("password", L.Arabic ? "كلمة مرور الأرشيف" : "Password", "", "password")]),
        _ => throw new ArgumentException((L.Arabic ? "أداة غير معروفة: " : "Unknown tool: ") + id)
    };
}
