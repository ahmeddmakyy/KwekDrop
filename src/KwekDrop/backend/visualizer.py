import os, sys, math, time, subprocess, json
from pathlib import Path
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFont

# Import common utilities
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import FFMPEG, FFPROBE, truth, number, duration, probe

COLOR_PRESETS = {
    'orange': ((0, 101, 255), (0, 165, 255)),       # BGR: #FF6500
    'white': ((240, 240, 240), (255, 255, 255)),
    'cyan': ((238, 211, 34), (255, 235, 120)),      # BGR: #22D3EE
    'violet': ((246, 92, 139), (220, 150, 255)),    # BGR: #8B5CF6
    'purple': ((246, 92, 139), (220, 150, 255)),
    'green': ((94, 197, 34), (160, 235, 80)),       # BGR: #22C55E
    'pink': ((153, 72, 236), (220, 140, 255)),      # BGR: #EC4899
    'sunset': ((0, 101, 255), (153, 72, 236)),      # Orange to Pink
    'ocean': ((212, 182, 6), (241, 102, 99)),       # Cyan to Indigo
    'rainbow': None                                  # Special cyclic
}

def parse_color_hex(hex_str, default=(0, 101, 255)):
    """Parses hex string or preset name to BGR tuple."""
    if not hex_str:
        return default
    s = hex_str.strip().lower()
    if s in COLOR_PRESETS and COLOR_PRESETS[s] is not None:
        return COLOR_PRESETS[s][0]
    s = s.lstrip('#')
    if len(s) == 6:
        try:
            r = int(s[0:2], 16)
            g = int(s[2:4], 16)
            b = int(s[4:6], 16)
            return (b, g, r) # BGR
        except ValueError:
            pass
    return default

class ColorPalette:
    def __init__(self, color_name_or_hex, color2_hex=None):
        raw = (color_name_or_hex or 'orange').strip().lower()
        self.is_rainbow = (raw == 'rainbow')
        self.is_sunset = (raw == 'sunset')
        self.is_ocean = (raw == 'ocean')

        if self.is_sunset:
            self.c1 = (0, 101, 255)   # #FF6500 BGR
            self.c2 = (153, 72, 236)  # #EC4899 BGR
        elif self.is_ocean:
            self.c1 = (212, 182, 6)   # #06B6D4 BGR
            self.c2 = (241, 102, 99)  # #6366F1 BGR
        elif raw in COLOR_PRESETS and COLOR_PRESETS[raw] is not None:
            self.c1, self.c2 = COLOR_PRESETS[raw]
        else:
            self.c1 = parse_color_hex(raw, (0, 101, 255))
            # If no second color, generate a harmonious brightened tint
            cb, cg, cr = self.c1
            self.c2 = (min(255, int(cb * 1.25 + 20)), min(255, int(cg * 1.2 + 20)), min(255, int(cr * 1.35 + 20)))

        if color2_hex:
            c2_parsed = parse_color_hex(color2_hex, None)
            if c2_parsed:
                self.c2 = c2_parsed

    def get_bgr(self, t_ratio, y_ratio=0.5):
        """Returns BGR color for position ratio (0..1)."""
        if self.is_rainbow:
            hue = int(((t_ratio * 0.8 + y_ratio * 0.2) % 1.0) * 180)
            hsv = np.uint8([[[hue, 225, 255]]])
            bgr = cv2.cvtColor(hsv, cv2.COLOR_HSV2BGR)[0, 0]
            return (int(bgr[0]), int(bgr[1]), int(bgr[2]))

        ratio = max(0.0, min(1.0, t_ratio * 0.7 + y_ratio * 0.3))
        b = int(self.c1[0] * (1.0 - ratio) + self.c2[0] * ratio)
        g = int(self.c1[1] * (1.0 - ratio) + self.c2[1] * ratio)
        r = int(self.c1[2] * (1.0 - ratio) + self.c2[2] * ratio)
        return (b, g, r)

    def get_spectrogram_lut(self):
        """Builds a 256-entry BGR lookup table (dark -> palette colour(s) -> near white for peaks)."""
        lut = np.zeros((256, 3), dtype=np.uint8)
        bg_dark = np.array([11, 13, 17], dtype=np.float32)
        c1 = np.array(self.c1, dtype=np.float32)
        c2 = np.array(self.c2, dtype=np.float32)
        peak_white = np.array([252, 252, 255], dtype=np.float32)

        for i in range(256):
            r = i / 255.0
            if self.is_rainbow:
                if r < 0.08:
                    t = r / 0.08
                    bgr = bg_dark * (1.0 - t) + np.array([140, 40, 15], dtype=np.float32) * t
                elif r < 0.86:
                    t = (r - 0.08) / 0.78
                    hue = int((t * 165) % 180)
                    sat = int(240 - t * 30)
                    val = int(80 + t * 175)
                    hsv = np.uint8([[[hue, sat, val]]])
                    bgr = cv2.cvtColor(hsv, cv2.COLOR_HSV2BGR)[0, 0].astype(np.float32)
                else:
                    t = (r - 0.86) / 0.14
                    hsv = np.uint8([[[165, int(210 * (1.0 - t)), 255]]])
                    bgr_h = cv2.cvtColor(hsv, cv2.COLOR_HSV2BGR)[0, 0].astype(np.float32)
                    bgr = bgr_h * (1.0 - t) + peak_white * t
            else:
                if r < 0.12:
                    t = r / 0.12
                    bgr = bg_dark * (1.0 - t) + (c1 * 0.30) * t
                elif r < 0.55:
                    t = (r - 0.12) / 0.43
                    bgr = (c1 * 0.30) * (1.0 - t) + c1 * t
                elif r < 0.85:
                    t = (r - 0.55) / 0.30
                    bgr = c1 * (1.0 - t) + c2 * t
                else:
                    t = (r - 0.85) / 0.15
                    bright_c = np.clip(c2 * 1.25, 0, 255)
                    bgr = bright_c * (1.0 - t) + peak_white * t

            lut[i] = np.clip(bgr, 0, 255).astype(np.uint8)
        return lut

def load_audio(path, start_time, duration_limit):
    """Decodes audio to stereo float32 PCM using ffmpeg."""
    cmd = [str(FFMPEG), '-hide_banner', '-loglevel', 'error', '-nostdin']
    if start_time and start_time > 0:
        cmd += ['-ss', str(start_time)]
    cmd += ['-i', str(path)]
    if duration_limit and duration_limit > 0:
        cmd += ['-t', str(duration_limit)]
    cmd += ['-f', 'f32le', '-ac', '2', '-ar', '44100', '-']

    proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if proc.returncode != 0 or len(proc.stdout) == 0:
        return np.zeros((44100, 2), dtype=np.float32), 44100

    samples = np.frombuffer(proc.stdout, dtype=np.float32).reshape(-1, 2)
    return samples, 44100

def generate_visualizer(path, params, target, duration_limit=None, start_time=None):
    """Renders the audio visualizer video using NumPy, OpenCV and FFmpeg."""
    aspect = params.get('aspect', 'landscape')
    w, h = {'landscape': (1280, 720), 'portrait': (720, 1280), 'square': (1080, 1080)}.get(aspect, (1280, 720))

    # Style mapping & normalization
    raw_style = params.get('waveStyle', params.get('style', 'mirror')).lower().strip()
    style_map = {
        'mirror': 'mirror', 'mirror bars': 'mirror', 'cline': 'mirror',
        'line': 'line', 'waveform': 'mirror',
        'bars': 'bars',
        'circle_bars': 'circle_bars', 'circle bars': 'circle_bars', 'circle': 'circle_bars',
        'circle_wave': 'circle_wave', 'circle wave': 'circle_wave',
        'spectrum': 'spectrum',
        'dots': 'dots', 'point': 'dots', 'points': 'dots',
        'pulse': 'pulse',
        'wave_fill': 'wave_fill', 'wave fill': 'wave_fill', 'filled': 'wave_fill',
        'particles': 'particles',
        'still': 'mirror'
    }
    style = style_map.get(raw_style, 'mirror')
    is_still_mode = (params.get('style') == 'still')
    overlay_wave = truth(params.get('overlayWave', 'false'))

    # Load audio
    samples, sr = load_audio(path, start_time, duration_limit)
    total_samples = len(samples)
    total_seconds = total_samples / sr
    fps = 30
    total_frames = max(1, int(round(total_seconds * fps)))

    mono = (samples[:, 0] + samples[:, 1]) * 0.5

    # Visualizer parameters
    color_val = params.get('color', '#FF6500')
    color2_val = params.get('color2', '')
    palette = ColorPalette(color_val, color2_val)

    bg_choice = params.get('background', 'black').strip().lower()
    bg_image_path = params.get('backgroundImage', '').strip()
    sensitivity = max(0.1, min(5.0, float(number(params, 'intensity', number(params, 'sensitivity', 1.0)))))
    thickness = max(0.5, min(5.0, float(number(params, 'thickness', number(params, 'size', 1.0)))))
    glow_enabled = truth(params.get('glow', 'true'))
    title_text = params.get('title', '').strip()

    # Pre-render background image or base
    base_bg = np.zeros((h, w, 3), dtype=np.uint8)
    has_custom_bg = False
    still_fg_image = None

    if bg_image_path and Path(bg_image_path).exists():
        try:
            img = cv2.imread(bg_image_path)
            if img is not None:
                ih, iw = img.shape[:2]
                scale = max(w / iw, h / ih)
                nw, nh = int(round(iw * scale)), int(round(ih * scale))
                resized = cv2.resize(img, (nw, nh), interpolation=cv2.INTER_AREA)
                crop_x = (nw - w) // 2
                crop_y = (nh - h) // 2
                cropped = resized[crop_y:crop_y + h, crop_x:crop_x + w]

                if bg_choice == 'blurred' or (is_still_mode and overlay_wave):
                    blurred = cv2.GaussianBlur(cropped, (51, 51), 30)
                    base_bg = (blurred * 0.5).astype(np.uint8)
                    has_custom_bg = True
                elif bg_choice == 'still' or is_still_mode:
                    base_bg = cropped
                    has_custom_bg = True

                scale_fit = min(w / iw, h / ih) * 0.45
                cfw, cfh = max(10, int(iw * scale_fit)), max(10, int(ih * scale_fit))
                still_fg_image = cv2.resize(img, (cfw, cfh), interpolation=cv2.INTER_AREA)
        except Exception:
            pass

    if not has_custom_bg:
        if bg_choice == 'gradient':
            for y in range(h):
                ratio = y / max(1, h - 1)
                b = int(36 * (1.0 - ratio) + 10 * ratio)
                g = int(27 * (1.0 - ratio) + 8 * ratio)
                r = int(24 * (1.0 - ratio) + 7 * ratio)
                base_bg[y, :] = (b, g, r)
        elif bg_choice.startswith('#') or len(bg_choice) == 6:
            bg_bgr = parse_color_hex(bg_choice, (11, 13, 17))
            base_bg[:] = bg_bgr
        else: # 'black' or default
            base_bg[:] = (11, 13, 17)

    # Pre-load title font if title_text is present
    title_font = None
    if title_text:
        font_candidates = [
            'C:/Windows/Fonts/seguisb.ttf',
            'C:/Windows/Fonts/segoeui.ttf',
            'C:/Windows/Fonts/arial.ttf'
        ]
        for fpath in font_candidates:
            if os.path.exists(fpath):
                try:
                    title_font = ImageFont.truetype(fpath, max(18, int(h * 0.035)))
                    break
                except Exception:
                    pass

    # FFT setup
    n_fft = 2048
    hop_length = int(sr / fps)
    hanning = np.hanning(n_fft)

    # Frequency bands for bar/dot visualizers (log-spaced)
    num_bands = 48 if aspect != 'portrait' else 36
    freqs = np.geomspace(40, 14000, num_bands + 1)
    freq_bins = np.int32(freqs * n_fft / sr)
    freq_bins = np.clip(freq_bins, 0, n_fft // 2)

    # High-resolution frequency bands for Spectrogram (128 log-spaced bins)
    spec_bins = 128
    spec_freqs = np.geomspace(35, 16000, spec_bins + 1)
    spec_bin_indices = np.int32(np.clip(spec_freqs * n_fft / sr, 0, n_fft // 2))
    spec_cols = 160
    spec_buffer = np.zeros((spec_bins, spec_cols), dtype=np.float32)
    spec_lut = palette.get_spectrogram_lut()
    spec_initialized = False

    # Temporal feature arrays for smoothing and peaks
    smooth_bands = np.zeros(num_bands, dtype=np.float32)
    peak_bands = np.zeros(num_bands, dtype=np.float32)

    # Particle system initialization for 'particles' style
    num_particles = 140
    particles_x = np.random.uniform(w * 0.08, w * 0.92, num_particles)
    particles_y = np.random.uniform(0, h, num_particles)
    particles_speed = np.random.uniform(1.2, 3.8, num_particles)
    particles_rad = np.random.uniform(2.5, 6.0, num_particles) * thickness
    particles_phase = np.random.uniform(0, math.pi * 2, num_particles)

    # Pulse ripples state for 'pulse' style
    ripples = []
    prev_bass_energy = 0.0
    last_ripple_frame = -10

    # FFmpeg output pipe
    cmd = [
        str(FFMPEG), '-hide_banner', '-loglevel', 'error', '-nostdin', '-y',
        '-f', 'rawvideo', '-pix_fmt', 'bgr24', '-s', f'{w}x{h}', '-r', str(fps), '-i', '-',
    ]
    if start_time and start_time > 0:
        cmd += ['-ss', str(start_time)]
    cmd += ['-i', str(path)]
    if duration_limit and duration_limit > 0:
        cmd += ['-t', str(duration_limit)]
    cmd += [
        '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '22', '-pix_fmt', 'yuv420p',
        '-c:a', 'aac', '-b:a', '160k', '-shortest', str(target)
    ]

    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    try:
        for f in range(total_frames):
            frame_center = int(f * hop_length)
            start_idx = frame_center - n_fft // 2
            end_idx = frame_center + n_fft // 2

            if start_idx < 0:
                chunk = np.pad(mono[:max(0, end_idx)], (max(0, -start_idx), 0))
            elif end_idx > total_samples:
                chunk = np.pad(mono[start_idx:], (0, max(0, end_idx - total_samples)))
            else:
                chunk = mono[start_idx:end_idx]

            if len(chunk) < n_fft:
                chunk = np.pad(chunk, (0, n_fft - len(chunk)))

            # FFT magnitude
            fft_mag = np.abs(np.fft.rfft(chunk * hanning))

            # Band magnitudes
            cur_bands = np.zeros(num_bands, dtype=np.float32)
            for b in range(num_bands):
                b_start = freq_bins[b]
                b_end = max(b_start + 1, freq_bins[b + 1])
                cur_bands[b] = np.mean(fft_mag[b_start:b_end])

            # Frequency tilt compensation
            slope = np.linspace(1.0, 4.0, num_bands, dtype=np.float32)
            cur_bands = np.log1p(cur_bands * slope * 0.05 * sensitivity)

            # Attack / decay smoothing
            attack = 0.65
            decay = 0.20
            smooth_bands = np.where(cur_bands > smooth_bands,
                                    smooth_bands * (1.0 - attack) + cur_bands * attack,
                                    smooth_bands * (1.0 - decay) + cur_bands * decay)

            # Peak hold
            gravity = 0.02
            peak_bands = np.maximum(smooth_bands, peak_bands - gravity)

            # Bass energy & RMS
            bass_energy = float(np.mean(smooth_bands[:max(1, num_bands // 8)]))
            rms = float(np.sqrt(np.mean(chunk**2))) * sensitivity

            # If still mode without overlay wave, just write background
            if is_still_mode and not overlay_wave:
                proc.stdin.write(base_bg.tobytes())
                continue

            # Frame buffer
            frame = base_bg.copy()
            overlay = np.zeros_like(frame)

            # RENDER CHOSEN STYLE
            if style == 'mirror':
                # Centered rounded mirrored bars with peak caps
                bar_margin = int(w * 0.08)
                avail_w = w - bar_margin * 2
                slot_w = avail_w / num_bands
                bar_w = max(2, int(slot_w * 0.65 * thickness))
                cy = h // 2
                max_h = int(h * 0.40)

                for b in range(num_bands):
                    bx = int(bar_margin + b * slot_w + (slot_w - bar_w) / 2)
                    bh = int(np.clip(smooth_bands[b] * max_h, 4, max_h))
                    pk_h = int(np.clip(peak_bands[b] * max_h, 4, max_h))
                    col = palette.get_bgr(b / num_bands, 0.5)

                    cv2.rectangle(overlay, (bx, cy - bh), (bx + bar_w, cy + bh), col, -1, cv2.LINE_AA)
                    rad = bar_w // 2
                    if rad > 0:
                        cv2.circle(overlay, (bx + rad, cy - bh), rad, col, -1, cv2.LINE_AA)
                        cv2.circle(overlay, (bx + rad, cy + bh), rad, col, -1, cv2.LINE_AA)

                    # Peak caps
                    pk_y1 = cy - pk_h - 4
                    pk_y2 = cy + pk_h + 4
                    if pk_y1 >= 10:
                        cv2.rectangle(overlay, (bx, pk_y1 - 2), (bx + bar_w, pk_y1), (255, 255, 255), -1, cv2.LINE_AA)
                    if pk_y2 <= h - 10:
                        cv2.rectangle(overlay, (bx, pk_y2), (bx + bar_w, pk_y2 + 2), (255, 255, 255), -1, cv2.LINE_AA)

            elif style == 'bars':
                # Rising bars from bottom baseline
                baseline_y = int(h * 0.85)
                bar_margin = int(w * 0.08)
                avail_w = w - bar_margin * 2
                slot_w = avail_w / num_bands
                bar_w = max(2, int(slot_w * 0.70 * thickness))
                max_h = int(h * 0.68)

                for b in range(num_bands):
                    bx = int(bar_margin + b * slot_w + (slot_w - bar_w) / 2)
                    bh = int(np.clip(smooth_bands[b] * max_h, 4, max_h))
                    pk_h = int(np.clip(peak_bands[b] * max_h, 4, max_h))
                    col = palette.get_bgr(b / num_bands, (max_h - bh) / max(1, max_h))

                    top_y = baseline_y - bh
                    cv2.rectangle(overlay, (bx, top_y), (bx + bar_w, baseline_y), col, -1, cv2.LINE_AA)
                    rad = bar_w // 2
                    if rad > 0:
                        cv2.circle(overlay, (bx + rad, top_y), rad, col, -1, cv2.LINE_AA)

                    # Peak cap
                    pk_y = baseline_y - pk_h - 6
                    if pk_y >= 10:
                        cv2.rectangle(overlay, (bx, pk_y - 2), (bx + bar_w, pk_y), (255, 255, 255), -1, cv2.LINE_AA)

            elif style == 'line':
                # Anti-aliased waveform line with soft glow and energetic RMS scaling
                cy = h // 2
                num_points = min(360, w // 2)
                step = len(chunk) / num_points
                pts = []
                line_amp = (0.50 + rms * 3.5 + bass_energy * 1.5) * sensitivity * (h * 0.38)
                for p in range(num_points):
                    s_idx = int(p * step)
                    val = chunk[s_idx] * line_amp
                    px = int(p * w / (num_points - 1))
                    py = int(np.clip(cy + val, 20, h - 20))
                    pts.append([px, py])

                pts = np.int32(pts)
                line_thick = max(2, int(2.5 * thickness))
                for p in range(len(pts) - 1):
                    col = palette.get_bgr(p / len(pts), (pts[p][1]) / h)
                    cv2.line(overlay, tuple(pts[p]), tuple(pts[p+1]), col, line_thick, cv2.LINE_AA)

            elif style == 'circle_bars':
                # Radial spectrum around pulsing center circle
                cx, cy = w // 2, h // 2
                min_dim = min(w, h)
                base_r = int(min_dim * 0.18 + (rms * 1.5 + bass_energy * 0.7) * (min_dim * 0.08))
                max_bar_len = int(min_dim * 0.22)
                n_rad = num_bands

                for b in range(n_rad):
                    angle = (b / n_rad) * 2.0 * math.pi - math.pi / 2
                    ca, sa = math.cos(angle), math.sin(angle)
                    bh = int(np.clip(smooth_bands[b] * max_bar_len, 4, max_bar_len))
                    col = palette.get_bgr(b / n_rad, 0.5)

                    r1 = base_r + 6
                    r2 = r1 + bh
                    x1, y1 = int(cx + ca * r1), int(cy + sa * r1)
                    x2, y2 = int(cx + ca * r2), int(cy + sa * r2)
                    cv2.line(overlay, (x1, y1), (x2, y2), col, max(2, int(3 * thickness)), cv2.LINE_AA)

                    # Peak dot
                    r_pk = r1 + int(np.clip(peak_bands[b] * max_bar_len, 4, max_bar_len)) + 6
                    x_pk, y_pk = int(cx + ca * r_pk), int(cy + sa * r_pk)
                    cv2.circle(overlay, (x_pk, y_pk), max(2, int(2.5 * thickness)), (255, 255, 255), -1, cv2.LINE_AA)

                # Center circle / disc
                cv2.circle(frame, (cx, cy), base_r, (18, 20, 26), -1, cv2.LINE_AA)
                cv2.circle(overlay, (cx, cy), base_r, palette.get_bgr(0.5, 0.5), max(2, int(2.5 * thickness)), cv2.LINE_AA)

                if still_fg_image is not None:
                    mask = np.zeros((base_r * 2, base_r * 2), dtype=np.uint8)
                    cv2.circle(mask, (base_r, base_r), base_r - 2, 255, -1)
                    fg_fit = cv2.resize(still_fg_image, (base_r * 2, base_r * 2))
                    roi = frame[cy - base_r:cy + base_r, cx - base_r:cx + base_r]
                    if roi.shape == fg_fit.shape:
                        cv2.copyTo(fg_fit, mask, roi)

            elif style == 'circle_wave':
                # Dynamic circular waveform ring pulsing clearly to audio RMS and waveform
                cx, cy = w // 2, h // 2
                min_dim = min(w, h)
                pulse_offset = (rms * 1.8 + bass_energy * 0.9) * (min_dim * 0.08)
                base_r = int(min_dim * 0.24 + pulse_offset)
                inner_r = int(min_dim * 0.20)

                # Center disc and still image behavior
                cv2.circle(frame, (cx, cy), inner_r, (18, 20, 26), -1, cv2.LINE_AA)
                cv2.circle(overlay, (cx, cy), inner_r, (40, 44, 52), 1, cv2.LINE_AA)

                if still_fg_image is not None:
                    mask = np.zeros((inner_r * 2, inner_r * 2), dtype=np.uint8)
                    cv2.circle(mask, (inner_r, inner_r), inner_r - 2, 255, -1)
                    fg_fit = cv2.resize(still_fg_image, (inner_r * 2, inner_r * 2))
                    roi = frame[cy - inner_r:cy + inner_r, cx - inner_r:cx + inner_r]
                    if roi.shape == fg_fit.shape:
                        cv2.copyTo(fg_fit, mask, roi)

                # Waveform ring with 360 points
                n_ring_pts = 360
                indices = np.linspace(0, len(chunk) - 1, n_ring_pts)
                sampled_wave = np.interp(indices, np.arange(len(chunk)), chunk)

                # Smooth circular boundary wrapping
                blend_len = 24
                blend_weights = np.linspace(0, 1, blend_len)
                sampled_wave[:blend_len] = sampled_wave[:blend_len] * blend_weights + sampled_wave[-blend_len:] * (1.0 - blend_weights)

                wave_scale = (0.25 + rms * 2.4 + bass_energy * 1.1) * sensitivity * (min_dim * 0.22)
                r_pts = base_r + sampled_wave * wave_scale
                r_pts = np.clip(r_pts, min_dim * 0.08, min_dim * 0.46)

                angles = np.linspace(0, 2 * np.pi, n_ring_pts, endpoint=False)
                xs = cx + np.cos(angles) * r_pts
                ys = cy + np.sin(angles) * r_pts
                pts = np.stack([xs, ys], axis=1).astype(np.int32)

                line_thick = max(2, min(4, int(2.5 * thickness)))
                for p in range(n_ring_pts):
                    p_next = (p + 1) % n_ring_pts
                    col = palette.get_bgr(p / n_ring_pts, 0.5)
                    cv2.line(overlay, tuple(pts[p]), tuple(pts[p_next]), col, line_thick, cv2.LINE_AA)

            elif style == 'spectrum':
                # Smooth log-frequency spectrogram filling the frame (minus small margin) and scrolling right-to-left
                margin_x = int(w * 0.03)
                margin_y = int(h * 0.03)
                disp_w = w - 2 * margin_x
                disp_h = h - 2 * margin_y

                # Compute log-frequency column
                cur_spec_col = np.zeros(spec_bins, dtype=np.float32)
                for b in range(spec_bins):
                    b_start = spec_bin_indices[b]
                    b_end = max(b_start + 1, spec_bin_indices[b + 1])
                    cur_spec_col[b] = np.mean(fft_mag[b_start:b_end])

                spec_tilt = np.linspace(1.0, 3.8, spec_bins, dtype=np.float32)
                cur_spec_col = np.log1p(cur_spec_col * spec_tilt * 0.08 * sensitivity)

                # Shift buffer leftwards: columns scroll right-to-left
                spec_buffer[:, :-1] = spec_buffer[:, 1:]
                spec_buffer[:, -1] = cur_spec_col

                if not spec_initialized:
                    for c_idx in range(spec_cols - 1):
                        spec_buffer[:, c_idx] = cur_spec_col
                    spec_initialized = True

                # Dynamic range normalization with percentiles to prevent saturation
                active_vals = spec_buffer[spec_buffer > 0.002]
                if len(active_vals) > 40:
                    p_low = float(np.percentile(active_vals, 5))
                    p_high = float(np.percentile(active_vals, 96))
                    drange = max(0.20, p_high - p_low)
                    norm_spec = np.clip((spec_buffer - p_low) / drange, 0.0, 1.0)
                else:
                    norm_spec = np.clip(spec_buffer / max(0.1, float(np.max(spec_buffer))), 0.0, 1.0)

                # Flip vertically so lowest frequency (bass) is at the bottom
                norm_flipped = np.flipud(norm_spec)

                # Upscale smoothly with bicubic interpolation and light blur
                upscaled = cv2.resize(norm_flipped, (disp_w, disp_h), interpolation=cv2.INTER_CUBIC)
                upscaled = np.clip(upscaled, 0.0, 1.0)
                blurred = cv2.GaussianBlur(upscaled, (5, 5), 0.8)

                # Apply color lookup table derived from palette
                u8_spec = (blurred * 255.0).astype(np.uint8)
                spec_colored = spec_lut[u8_spec]

                # Place into overlay and draw subtle edge border
                overlay[margin_y:margin_y + disp_h, margin_x:margin_x + disp_w] = spec_colored
                cv2.rectangle(overlay, (margin_x, margin_y), (margin_x + disp_w, margin_y + disp_h), (40, 44, 52), 1, cv2.LINE_AA)

            elif style == 'dots':
                # Modern LED dot matrix equalizer with reactive lit dots and peak caps
                num_rows = 14
                margin_x = int(w * 0.08)
                margin_y = int(h * 0.18)
                disp_w = w - 2 * margin_x
                disp_h = h - 2 * margin_y

                slot_w = disp_w / num_bands
                slot_h = disp_h / num_rows
                dot_r = max(2, int(min(slot_w, slot_h) * 0.38 * thickness))

                for b in range(num_bands):
                    cx_col = int(margin_x + b * slot_w + slot_w / 2)
                    active_level = np.clip(smooth_bands[b], 0.0, 1.0)
                    num_lit = int(round(active_level * num_rows))
                    peak_lit = int(round(np.clip(peak_bands[b], 0.0, 1.0) * num_rows))

                    for r in range(num_rows):
                        cy_dot = int(margin_y + disp_h - (r + 0.5) * slot_h)
                        if r < num_lit:
                            t_ratio = r / max(1, num_rows - 1)
                            col = palette.get_bgr(b / num_bands, t_ratio)
                            cv2.circle(overlay, (cx_col, cy_dot), dot_r, col, -1, cv2.LINE_AA)
                        elif r == peak_lit and r > 0:
                            cv2.circle(overlay, (cx_col, cy_dot), dot_r, (252, 252, 255), -1, cv2.LINE_AA)
                        else:
                            cv2.circle(overlay, (cx_col, cy_dot), max(1, dot_r - 2), (25, 28, 35), -1, cv2.LINE_AA)

            elif style == 'pulse':
                # Beat-triggered concentric expanding rings with center pulsating orb
                cx, cy = w // 2, h // 2
                min_dim = min(w, h)
                max_r = math.sqrt(w*w + h*h) * 0.55

                # Detect beat transient on bass
                is_beat = (bass_energy > 0.55 and bass_energy > prev_bass_energy * 1.12 and (f - last_ripple_frame) >= 5)
                if is_beat:
                    ripples.append({'r': float(min_dim * 0.08), 'speed': 10.0 + bass_energy * 14.0, 'alpha': 1.0})
                    last_ripple_frame = f
                prev_bass_energy = bass_energy

                new_ripples = []
                for rip in ripples:
                    rip['r'] += rip['speed']
                    rip['alpha'] = max(0.0, 1.0 - (rip['r'] / max_r))
                    if rip['alpha'] > 0.02 and rip['r'] < max_r:
                        rad = int(rip['r'])
                        col = palette.get_bgr(rip['r'] / max_r, 0.5)
                        b_val = int(col[0] * rip['alpha'])
                        g_val = int(col[1] * rip['alpha'])
                        r_val = int(col[2] * rip['alpha'])
                        cv2.circle(overlay, (cx, cy), rad, (b_val, g_val, r_val), max(2, int(2.5 * thickness)), cv2.LINE_AA)
                        new_ripples.append(rip)
                ripples = new_ripples[-12:]

                # Center pulsating orb
                center_rad = int(min_dim * 0.10 + bass_energy * (min_dim * 0.08))
                col_c = palette.get_bgr(0.5, 0.5)
                cv2.circle(overlay, (cx, cy), center_rad, col_c, -1, cv2.LINE_AA)

            elif style == 'wave_fill':
                # Fluid filled wave rolling down to frame bottom with glowing crest line
                num_points = 240
                indices = np.linspace(0, len(chunk) - 1, num_points)
                sampled_wave = np.interp(indices, np.arange(len(chunk)), chunk)

                baseline_y = int(h * 0.72)
                wave_amp = (0.35 + rms * 3.5 + bass_energy * 1.5) * sensitivity * (h * 0.36)

                pts = []
                for p in range(num_points):
                    px = int(p * w / (num_points - 1))
                    val = abs(sampled_wave[p]) * wave_amp
                    py = int(np.clip(baseline_y - val, 20, baseline_y))
                    pts.append([px, py])

                # Fill polygon down to bottom edge (h)
                poly_pts = np.int32([[0, h], [0, baseline_y]] + pts + [[w, baseline_y], [w, h]])
                col_fill = palette.get_bgr(0.5, 0.5)
                fill_color = (int(col_fill[0] * 0.40), int(col_fill[1] * 0.40), int(col_fill[2] * 0.40))
                cv2.fillPoly(overlay, [poly_pts], fill_color, cv2.LINE_AA)

                # Crest line
                for p in range(len(pts) - 1):
                    col = palette.get_bgr(p / len(pts), 0.2)
                    cv2.line(overlay, tuple(pts[p]), tuple(pts[p+1]), col, max(2, int(2.5 * thickness)), cv2.LINE_AA)

            elif style == 'particles':
                # Dynamic particle cloud rising upward driven by music energy
                energy_factor = max(0.2, rms * 5.0 + bass_energy * 2.0)
                particles_y -= particles_speed * energy_factor * 2.2
                particles_x += np.sin(particles_phase + f * 0.08) * 1.5

                # Wrap around
                reset_mask = (particles_y < -20)
                particles_y[reset_mask] = h + np.random.uniform(0, 30, np.sum(reset_mask))
                particles_x[reset_mask] = np.random.uniform(w * 0.08, w * 0.92, np.sum(reset_mask))

                for i in range(num_particles):
                    px, py = int(particles_x[i]), int(particles_y[i])
                    if 0 <= px < w and 0 <= py < h:
                        rad = int(max(2, particles_rad[i] * (0.8 + energy_factor * 0.4)))
                        col = palette.get_bgr(px / w, py / h)
                        cv2.circle(overlay, (px, py), rad, col, -1, cv2.LINE_AA)

            # Apply Glow if enabled
            if glow_enabled:
                glow = cv2.GaussianBlur(overlay, (25, 25), 0)
                frame = cv2.add(frame, glow)
            frame = cv2.add(frame, overlay)

            # Draw optional Title Overlay
            if title_text and title_font is not None:
                pil_img = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
                draw = ImageDraw.Draw(pil_img)
                bbox = draw.textbbox((0, 0), title_text, font=title_font)
                tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
                tx = (w - tw) // 2
                ty = int(h - th - max(28, h * 0.05))

                # Soft shadow
                draw.text((tx + 1, ty + 2), title_text, font=title_font, fill=(0, 0, 0, 180))
                draw.text((tx, ty), title_text, font=title_font, fill=(255, 255, 255, 240))
                frame = cv2.cvtColor(np.array(pil_img), cv2.COLOR_RGB2BGR)

            proc.stdin.write(frame.tobytes())

    finally:
        if proc.stdin:
            proc.stdin.close()
        proc.wait()

    return target
