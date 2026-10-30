using KwekDrop;
using System.Text.Json;

L.UseForSession("zh"); // assertions use Chinese messages; never touch the saved choice
var checks=new List<object>();int failed=0;
void Check(string name,Action body)
{
    try{body();checks.Add(new{test=name,passed=true});}
    catch(Exception ex){failed++;checks.Add(new{test=name,passed=false,error=ex.Message});}
}
void Require(bool value){if(!value)throw new Exception("Unexpected result");}
ConversionJob Job(string id,string ext,params (string,string)[] fields)
{
    var p=Catalog.Definition(id,Catalog.Category("sample."+ext)).Fields!.ToDictionary(f=>f.Name,f=>f.Default);
    foreach(var (key,value) in fields)p[key]=value;
    return new(["sample."+ext],id,p);
}
void Reject(ConversionJob job,int w=160,int h=120,double seconds=10,int pages=3)
{
    try{JobValidation.ResolveRatio(job.Parameters!);JobValidation.Check(job,w,h,seconds,pages);}
    catch(ArgumentException){return;}
    throw new Exception("Invalid parameters were accepted");
}
foreach(string tool in new[]{"cropImage","cropVideo"})
{
    Check(tool+" offers custom aspect ratio",()=>Require(Catalog.Definition(tool,"image").Fields!.First(f=>f.Name=="ratio").Choices!.Contains("custom")));
    Check(tool+" resolves decimal custom ratio for the real worker",()=>{var j=Job(tool,tool=="cropImage"?"png":"mp4",("width","150"),("height","100"),("ratio","custom"),("ratioWidth","2.35"),("ratioHeight","1"));JobValidation.ResolveRatio(j.Parameters!);JobValidation.Check(j,160,120);Require(j.Parameters!["ratio"]=="2.35:1");});
    foreach(string invalid in new[]{"0","-1","NaN","Infinity","abc",""})Check(tool+" rejects custom ratio width "+invalid,()=>Reject(Job(tool,"png",("ratio","custom"),("ratioWidth",invalid),("width","150"),("height","100"))));
}
foreach(string tool in new[]{"rotateImage","rotateVideo"})
{
    Check(tool+" offers quarter turns and flips, starting unchanged",()=>
    {
        var fields=Catalog.Definition(tool,tool=="rotateImage"?"image":"video").Fields!;
        Require(fields.Single(f=>f.Name=="angle").Kind=="number"&&fields.Single(f=>f.Name=="angle").Default=="0");
        Require(fields.Single(f=>f.Name=="expand").Choices!.SequenceEqual(tool=="rotateImage" ? ["crop","expand","keep"] : ["expand","keep"]));
        Require(fields.Single(f=>f.Name=="expand").Default == (tool=="rotateImage" ? "crop" : "expand"));
        Require(fields.Single(f=>f.Name=="flip").Choices!.SequenceEqual(["none","horizontal","vertical"])&&fields.Single(f=>f.Name=="flip").Default=="none");
    });
    Check(tool+" is available for pictures and videos of any size",()=>{var j=Job(tool,tool=="rotateImage"?"png":"mp4",("angle","90"));JobValidation.Check(j,160,120,10,0);});
}
foreach(string angle in new[]{"-1","361","abc"})
    Check("a rotation of "+angle+" degrees is refused",()=>Reject(Job("rotateImage","png",("angle",angle))));
foreach(string angle in new[]{"0","0.5","45","89.9","123.45","270","360"})
    Check("a rotation of "+angle+" degrees is accepted",()=>JobValidation.Check(Job("rotateVideo","mp4",("angle",angle)),160,120,10,0));
Check("crop coordinates cannot extend outside original image",()=>Reject(Job("cropImage","png",("x","120"),("width","50"),("height","30"))));
Check("zero crop dimensions cannot be saved",()=>Reject(Job("cropVideo","mp4",("width","0"),("height","30"))));
Check("fractional pixel dimensions rejected",()=>Reject(Job("cropImage","png",("width","30.5"),("height","30"))));
Check("non-finite edits rejected",()=>Reject(Job("editImage","png",("exposure","NaN"))));
Check("invalid JSON regions rejected",()=>Reject(Job("redactImage","png",("width","30"),("height","30"),("regions","{bad}"))));
Check("backwards time range rejected",()=>Reject(Job("trimVideo","mp4",("start","8"),("end","2"))));
Check("time outside source duration rejected",()=>Reject(Job("trimAudio","wav",("start","1"),("end","20"))));
Check("invalid clock seconds rejected",()=>Reject(Job("trimVideo","mp4",("start","0:99"),("end","0"))));
Check("clock format and end-zero supported",()=>JobValidation.Check(Job("trimAudio","wav",("start","0:01.5"),("end","0")),0,0,10));
Check("out-of-range mosaic block size rejected",()=>Reject(Job("redactImage","png",("width","40"),("height","30"),("blockSize","1"))));
Check("mosaic is the default for image and video redaction",()=>Require(new[]{"redactImage","redactVideo"}.All(id=>Catalog.Definition(id,"image").Fields!.First(f=>f.Name=="style").Default=="pixelate")));
Check("PDF page numbers beyond source rejected",()=>Reject(Job("organizePDF","pdf",("pageOrder","1,4"))));
Check("PDF repeated/reordered pages allowed",()=>JobValidation.Check(Job("organizePDF","pdf",("pageOrder","3,1,1")),0,0,0,3));
Check("negative snapshot frame offset retained",()=>JobValidation.Check(Job("videoSnapshots","mp4",("time","1"),("frame","-1")),160,120,10));
Check("ImageAdjust identity at default parameters", () =>
{
    byte[] src = [50, 100, 150, 255, 20, 40, 60, 255];
    byte[] dst = new byte[src.Length];
    ImageAdjust.Apply(src, dst, 2, 1, exposure: 0, contrast: 1, highlights: 0, shadows: 0, saturation: 1, temperature: 0);
    for (int i = 0; i < src.Length; i++)
        Require(Math.Abs(src[i] - dst[i]) <= 1);
});
Check("ImageAdjust exposure +1 EV increases mid-grey", () =>
{
    byte[] src = [128, 128, 128, 255];
    byte[] dst = new byte[4];
    ImageAdjust.Apply(src, dst, 1, 1, exposure: 1, contrast: 1, highlights: 0, shadows: 0, saturation: 1, temperature: 0);
    Require(dst[0] > 180 && dst[0] <= 255 && dst[0] == dst[1] && dst[1] == dst[2]);
});
Check("ImageAdjust saturation 0 gives neutral grey", () =>
{
    byte[] src = [200, 100, 50, 255];
    byte[] dst = new byte[4];
    ImageAdjust.Apply(src, dst, 1, 1, exposure: 0, contrast: 1, highlights: 0, shadows: 0, saturation: 0, temperature: 0);
    Require(dst[0] == dst[1] && dst[1] == dst[2]);
});
Check("ImageAdjust extreme parameters stay within 0..255", () =>
{
    byte[] src = [10, 50, 200, 255, 240, 240, 240, 255];
    byte[] dst = new byte[src.Length];
    ImageAdjust.Apply(src, dst, 2, 1, exposure: 3, contrast: 2, highlights: 100, shadows: 100, saturation: 2, temperature: 100);
    for (int i = 0; i < dst.Length; i++)
        Require(dst[i] >= 0 && dst[i] <= 255);
});
Check("ImageAdjust processes 1000x667 in under 100 ms in Release", () =>
{
    int w = 1000, h = 667;
    byte[] src = new byte[w * h * 4];
    byte[] dst = new byte[src.Length];
    new Random(42).NextBytes(src);
    ImageAdjust.Apply(src, dst, w, h, 0.5, 1.2, 20, -10, 1.1, 15);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    ImageAdjust.Apply(src, dst, w, h, 0.5, 1.2, 20, -10, 1.1, 15);
    sw.Stop();
    Console.WriteLine($"ImageAdjust 1000x667: {sw.ElapsedMilliseconds} ms");
    Require(sw.ElapsedMilliseconds < 100);
});
Check("QR encoder HELLO WORLD at level Q produces version 1 and format bits match spec", () =>
{
    var m = QrMatrix.Encode("HELLO WORLD", QrCorrection.Q);
    Require(m != null && m.Version == 1 && m.Size == 21);
    int expectedBits = QrMatrix.GetFormatBits(m.Correction, m.Mask);
    for (int i = 0; i <= 5; i++) Require(m.IsDark(8, i) == (((expectedBits >> i) & 1) != 0));
    Require(m.IsDark(8, 7) == (((expectedBits >> 6) & 1) != 0));
    Require(m.IsDark(8, 8) == (((expectedBits >> 7) & 1) != 0));
    Require(m.IsDark(7, 8) == (((expectedBits >> 8) & 1) != 0));
    for (int i = 9; i < 15; i++) Require(m.IsDark(14 - i, 8) == (((expectedBits >> i) & 1) != 0));
});
Check("QR encoder https://example.com fits version 2 at level M", () =>
{
    var m = QrMatrix.Encode("https://example.com", QrCorrection.M);
    Require(m != null && m.Version == 2 && m.Size == 25);
});
Check("QR encoder 1,000-byte payload at level L selects version 22", () =>
{
    string payload = new string('A', 1000);
    var m = QrMatrix.Encode(payload, QrCorrection.L);
    Require(m != null && m.Version == 22 && m.Size == 17 + 4 * 22);
});
Check("Reed-Solomon codeword stream evaluates to zero at generator roots", () =>
{
    byte[] data = System.Text.Encoding.UTF8.GetBytes("HELLO WORLD");
    byte[] divisor = QrMatrix.ComputeDivisor(13);
    byte[] remainder = QrMatrix.ComputeRemainder(data, divisor);
    byte[] codewords = new byte[data.Length + remainder.Length];
    Array.Copy(data, 0, codewords, 0, data.Length);
    Array.Copy(remainder, 0, codewords, data.Length, remainder.Length);
    byte root = 1;
    for (int i = 0; i < 13; i++)
    {
        Require(QrMatrix.EvaluatePolynomial(codewords, root) == 0);
        root = QrMatrix.GfMultiply(root, 0x02);
    }
});
Check("QR matrix invariants: finder patterns, timing patterns and dark module", () =>
{
    var m = QrMatrix.Encode("KwekDrop Invariants", QrCorrection.M)!;
    Require(m != null);
    // Dark module at (8, Size - 8)
    Require(m.IsDark(8, m.Size - 8) == true);
    // Timing patterns at row 6 and col 6
    for (int i = 8; i < m.Size - 8; i++)
    {
        Require(m.IsDark(i, 6) == (i % 2 == 0));
        Require(m.IsDark(6, i) == (i % 2 == 0));
    }
    // Finder checks
    Require(m.IsFinder(0, 0) == true);
    Require(m.IsFinder(6, 6) == true);
    Require(m.IsFinder(m.Size - 1, 0) == true);
    Require(m.IsFinder(0, m.Size - 1) == true);
    Require(m.IsFinder(7, 7) == false);
    Require(m.IsFinder(10, 10) == false);
    // Top-left finder core
    for (int y = 2; y <= 4; y++)
        for (int x = 2; x <= 4; x++)
            Require(m.IsDark(x, y) == true);
});
Check("QR encoder returns null when text exceeds Version 40 capacity", () =>
{
    string tooLong = new string('X', 4000);
    var m = QrMatrix.Encode(tooLong, QrCorrection.L);
    Require(m == null);
});
Check("Wi-Fi payload builder escapes special characters properly", () =>
{
    string wifi = QrContent.Wifi("My;Net,Work\\:\"Name", "p;a,s\\:\"s", "WPA", true);
    Require(wifi == "WIFI:T:WPA;S:My\\;Net\\,Work\\\\\\:\\\"Name;P:p\\;a\\,s\\\\\\:\\\"s;H:true;;");
    string wifiNone = QrContent.Wifi("OpenNet", "", "None");
    Require(wifiNone == "WIFI:T:nopass;S:OpenNet;;");
});
var report=new{passed=checks.Count-failed,failed,checks};if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"passed={checks.Count-failed}, failed={failed}");return failed==0?0:1;
