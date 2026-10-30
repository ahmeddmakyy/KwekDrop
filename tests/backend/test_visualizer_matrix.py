import os, sys, time, subprocess, json, wave
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'src/KwekDrop/backend'))
import visualizer
from common import FFPROBE, FFMPEG

out_dir = ROOT / 'docs/media/_work/viz'
out_dir.mkdir(parents=True, exist_ok=True)

# Generate music-like test track: kick, bass, chord pad, hats, melody (5 s, 44.1 kHz stereo)
sr = 44100
n = sr * 5
t = np.arange(n) / sr
beat = (t * 2) % 1.0
kick = np.sin(2 * np.pi * (50 + 120 * np.exp(-beat * 18)) * t) * np.exp(-beat * 7)
bass = 0.35 * np.sin(2 * np.pi * 55 * t) * (0.6 + 0.4 * np.sin(2 * np.pi * 2 * t))
pad = sum(0.12 * np.sin(2 * np.pi * f * t) for f in (220, 277.2, 329.6, 440))
hat = 0.12 * np.random.default_rng(1).normal(size=n) * (((t * 8) % 1.0) < 0.08)
mel_f = np.array([523, 587, 659, 784])[(t * 4).astype(int) % 4]
mel = 0.2 * np.sin(2 * np.pi * mel_f * t) * np.exp(-((t * 4) % 1.0) * 3)

x = 0.8 * kick + bass + pad + hat + mel
x = x / np.max(np.abs(x)) * 0.8
pcm = (np.stack([x, np.roll(x, 200)], 1) * 32767).astype('<i2')

music_wav = ROOT / 'docs/media/_work/music.wav'
with wave.open(str(music_wav), 'wb') as w:
    w.setnchannels(2)
    w.setsampwidth(2)
    w.setframerate(sr)
    w.writeframes(pcm.tobytes())

print(f"Generated music track at {music_wav} ({music_wav.stat().st_size} bytes)")

styles = ['mirror', 'bars', 'line', 'circle_bars', 'circle_wave', 'spectrum', 'dots', 'pulse', 'wave_fill', 'particles']
aspects = ['landscape', 'portrait', 'square']
colors = ['orange', 'sunset', '#10B981', 'rainbow', 'ocean']
backgrounds = ['gradient', 'black']

results = []

for idx, style in enumerate(styles):
    asp = aspects[idx % len(aspects)]
    col = colors[idx % len(colors)]
    bg = backgrounds[idx % len(backgrounds)]

    vid_path = out_dir / f'test_{style}_{asp}.mp4'
    t0 = time.time()
    visualizer.generate_visualizer(
        music_wav,
        {
            'aspect': asp,
            'waveStyle': style,
            'color': col,
            'background': bg,
            'title': style.replace('_', ' ').title() if idx % 2 == 0 else ''
        },
        vid_path,
        duration_limit=3.5,
        start_time=0.0
    )
    dt = time.time() - t0
    fps_render = 105 / dt

    # probe
    probe_cmd = [str(FFPROBE), '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(vid_path)]
    pinfo = json.loads(subprocess.check_output(probe_cmd))
    v_st = next(s for s in pinfo['streams'] if s['codec_type'] == 'video')
    a_st = next(s for s in pinfo['streams'] if s['codec_type'] == 'audio')
    fmt = pinfo['format']
    dur = float(fmt['duration'])

    # extract frame at 2.3 s
    png_path = out_dir / f'{style}.png'
    ext_cmd = [str(FFMPEG), '-y', '-ss', '2.3', '-i', str(vid_path), '-frames:v', '1', str(png_path)]
    subprocess.run(ext_cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    # Validate extracted frame
    assert png_path.exists(), f"PNG missing for {style}"
    im = Image.open(png_path)
    arr = np.array(im)
    assert im.size == (v_st['width'], v_st['height']), f"Dimension mismatch in {style}"
    # Verify non-blank (contrast/variance)
    assert np.std(arr) > 5.0, f"Frame for {style} is completely blank/flat"

    results.append({
        'style': style,
        'aspect': asp,
        'size': f'{v_st["width"]}x{v_st["height"]}',
        'color': col,
        'background': bg,
        'video_codec': v_st['codec_name'],
        'pix_fmt': v_st.get('pix_fmt'),
        'audio_codec': a_st['codec_name'],
        'duration': dur,
        'render_sec': dt,
        'render_fps': fps_render,
        'png_exists': png_path.exists(),
        'png_size_bytes': png_path.stat().st_size
    })
    print(f'[{style:12s}] aspect={asp:9s} size={v_st["width"]}x{v_st["height"]} col={col:10s} bg={bg:8s} dur={dur:.2f}s render={dt:.2f}s ({fps_render:.1f} fps) png={png_path.stat().st_size}B')

report_path = out_dir / 'matrix_report.json'
report_path.write_text(json.dumps(results, indent=2), encoding='utf-8')
print("\nVisualizer matrix test complete! Report saved to", report_path)
