#!/usr/bin/env python3
"""Original R01 SFX, with no external recordings or generative-audio model.

Rebuild: python generate.py --output ./rebuild
Dependencies: numpy, scipy. PCM output uses Python's standard wave module.
No downloads, network calls, Unity modifications, or Git operations.
"""
from __future__ import annotations

import argparse
import json
import math
import platform
import wave
from pathlib import Path
from typing import Any

import numpy as np
import scipy
from numpy.typing import NDArray
from scipy import signal
from scipy.interpolate import PchipInterpolator

FloatArray = NDArray[np.float64]
SR = 48_000
SYNTH_SR = 96_000
SEED = 26091401
ORDER = ['hiss', 'laowu', 'enemy_warn', 'interrupt', 'player_hurt', 'hit']
DURATIONS = dict(zip(ORDER, [0.38, 0.32, 0.23, 0.18, 0.18, 0.09]))
LIMITS = dict(zip(ORDER, [(0.25, .5), (.2, .4), (.15, .3), (.1, .25), (.1, .25), (.05, .12)]))
DESCRIPTIONS = {
    'hiss': 'Q 哈气：气流嘶声、短促喷气起音，无人声台词',
    'laowu': '老吴叫：原创短喵呜拟声，aow→u 共振峰收拢；不是原梗录音',
    'enemy_warn': '对峙预警：两下略带不协和的中频脉冲，区别于哈气',
    'interrupt': '成功打断：硬质短扣击与干净上行谐音确认',
    'player_hurt': '玩家受伤：短下坠拟声叠柔和钝击',
    'hit': '普通命中：低中频轻拍击，短尾，无尖锐金属长鸣',
}


def db(x: float) -> float:
    return 20.0 * math.log10(max(float(x), 1e-12))


def timeline(seconds: float, sr: int = SYNTH_SR) -> FloatArray:
    return np.arange(round(seconds * sr), dtype=np.float64) / sr


def curve(t: FloatArray, times: list[float], values: list[float]) -> FloatArray:
    return PchipInterpolator(times, values)(np.clip(t, times[0], times[-1]))


def filter_audio(x: FloatArray, cutoff: float | list[float], kind: str,
                 sr: int = SYNTH_SR, order: int = 3) -> FloatArray:
    sos = signal.butter(order, cutoff, btype=kind, fs=sr, output='sos')
    return signal.sosfilt(sos, x)


def normalized_rms(x: FloatArray) -> FloatArray:
    return x / max(float(np.sqrt(np.mean(x * x))), 1e-12)


def shaped_noise(t: FloatArray, rng: np.random.Generator,
                 low: float, high: float) -> FloatArray:
    return normalized_rms(filter_audio(rng.standard_normal(len(t)), [low, high], 'bandpass'))


def boundary_window(n: int, attack: float, release: float, sr: int = SYNTH_SR) -> FloatArray:
    w = np.ones(n, dtype=np.float64)
    a = max(2, min(round(attack * sr), n // 2))
    r = max(2, min(round(release * sr), n // 2))
    w[:a] = np.sin(np.linspace(0., np.pi / 2, a)) ** 2
    w[-r:] = np.cos(np.linspace(0., np.pi / 2, r)) ** 2
    w[0] = w[-1] = 0.
    return w


def soft_decay(t: FloatArray, tau: float, attack: float = .0015,
               release: float = .012) -> FloatArray:
    return np.exp(-t / tau) * boundary_window(len(t), attack, release)


def add_at(buf: FloatArray, clip: FloatArray, offset: float,
           gain: float = 1., sr: int = SYNTH_SR) -> None:
    start = round(offset * sr)
    end = min(len(buf), start + len(clip))
    if start < 0 or start >= len(buf):
        raise ValueError('offset is outside the destination audio')
    buf[start:end] += gain * clip[:end-start]


def vocal(t: FloatArray, f0: FloatArray, f1: FloatArray, f2: FloatArray,
          f3: FloatArray, rng: np.random.Generator, roughness: float = .025) -> FloatArray:
    """Band-limited voiced excitation with time-varying formant envelopes.

    This is an original synthetic animal-like voice, not speech synthesis,
    voice cloning, or an imitation learned from an existing recording.
    """
    jitter = filter_audio(rng.standard_normal(len(t)), 70., 'lowpass')
    jitter = normalized_rms(jitter) * .0025
    fm = 1. + .008 * np.sin(2 * np.pi * 16.5 * t) + jitter
    freq = f0 * fm
    phase = np.cumsum(2 * np.pi * freq / SYNTH_SR)
    out = np.zeros_like(t)
    for h in range(1, 29):
        hf = h * freq
        # Broad resonances move from open aow to closed u.
        formants = (.95 * np.exp(-.5 * ((hf-f1) / 190.) ** 2)
                    + .80 * np.exp(-.5 * ((hf-f2) / 265.) ** 2)
                    + .25 * np.exp(-.5 * ((hf-f3) / 400.) ** 2))
        tilt = h ** -.90
        amplitude = tilt * (.05 + formants) * np.exp(-(hf/7800.) ** 4)
        out += amplitude * np.sin(h * phase + .16 * np.sin(h * .91))
    out = normalized_rms(out)
    breath = shaped_noise(t, rng, 800., 4700.)
    out *= (1. + roughness * np.sin(.5 * phase))
    return out + .025 * breath


def make_hiss(rng: np.random.Generator) -> FloatArray:
    t = timeline(DURATIONS['hiss'])
    air = shaped_noise(t, rng, 1600., 8300.)
    body = shaped_noise(t, rng, 650., 2200.)
    teeth = shaped_noise(t, rng, 4300., 7600.)
    flutter = normalized_rms(filter_audio(rng.standard_normal(len(t)), 55., 'lowpass'))
    env = curve(t, [0., .004, .024, .10, .23, .32, .38],
                [0., .75, 1., .84, .52, .20, 0.])
    turbulence = np.clip(1. + .10 * flutter + .06 * np.sin(2*np.pi*36.*t), .6, 1.3)
    spit = np.exp(-t/.012) * teeth
    x = (.72 * air + .22 * body + .10 * teeth) * env * turbulence + .18 * spit
    spit_accent = 1. + .70*np.exp(-((t-.012)/.006)**2)
    return np.tanh(.9*x) * spit_accent * boundary_window(len(t), .0015, .024)


def make_laowu(rng: np.random.Generator) -> FloatArray:
    t = timeline(DURATIONS['laowu'])
    times = [0., .022, .072, .15, .235, .32]
    f0 = curve(t, times, [350., 430., 535., 488., 380., 305.])
    f1 = curve(t, times, [480., 780., 950., 715., 420., 345.])
    f2 = curve(t, times, [1250., 1600., 1770., 1220., 920., 820.])
    f3 = curve(t, times, [2750., 2900., 2900., 2600., 2500., 2450.])
    voice = vocal(t, f0, f1, f2, f3, rng, .035)
    env = curve(t, [0., .008, .04, .11, .19, .265, .32],
                [0., .50, .96, 1., .84, .34, 0.])
    syllable_accent = 1. + np.exp(-((t-.028)/.008)**2)
    return np.tanh(.8*voice) * env * syllable_accent * boundary_window(len(t), .0025, .018)


def warn_pulse(seconds: float, freq: float) -> FloatArray:
    t = timeline(seconds)
    phase = 2*np.pi * (freq*t - 75.*t*t/(2*seconds))
    # Tritone color is deliberate; a warning is not a success chime.
    reed = (np.sin(phase) + .21*np.sin(3*phase)
            + .27*np.sin(np.sqrt(2.)*phase + .12)
            + .09*np.sin(2*phase))
    return reed * soft_decay(t, .045, .0025, .020)


def make_enemy_warn(rng: np.random.Generator) -> FloatArray:
    del rng
    x = np.zeros(round(DURATIONS['enemy_warn']*SYNTH_SR))
    add_at(x, warn_pulse(.09, 885.), 0.)
    add_at(x, warn_pulse(.12, 1045.), .105, .96)
    return x


def chime(seconds: float, freq: float, tau: float) -> FloatArray:
    t = timeline(seconds)
    x = (np.sin(2*np.pi*freq*t) * np.exp(-t/tau)
         + .24*np.sin(2*np.pi*2.003*freq*t+.15)*np.exp(-t/(tau*.50))
         + .10*np.sin(2*np.pi*3.012*freq*t)*np.exp(-t/(tau*.27)))
    return x * boundary_window(len(t), .0015, .018)


def make_interrupt(rng: np.random.Generator) -> FloatArray:
    t = timeline(DURATIONS['interrupt'])
    x = np.zeros_like(t)
    add_at(x, chime(.12, 1046.50, .026), 0., .66)
    add_at(x, chime(.135, 1567.98, .032), .045, .87)
    click = shaped_noise(t, rng, 650., 3200.) * soft_decay(t, .0045, .0008, .01)
    return x + .15*click


def make_player_hurt(rng: np.random.Generator) -> FloatArray:
    t = timeline(DURATIONS['player_hurt'])
    times = [0., .01, .032, .07, .125, .18]
    f0 = curve(t, times, [530., 665., 570., 400., 270., 230.])
    f1 = curve(t, times, [1120., 1200., 1100., 850., 630., 600.])
    f2 = curve(t, times, [2300., 2370., 2100., 1700., 1450., 1390.])
    f3 = curve(t, times, [3500., 3550., 3300., 2900., 2600., 2500.])
    voice = vocal(t, f0, f1, f2, f3, rng, .085)
    env = curve(t, [0., .004, .023, .065, .12, .18], [0., .84, 1., .64, .13, 0.])
    thud_phase = 2*np.pi*(145.*t + 60.*.018*(1.-np.exp(-t/.018)))
    thud = .48*np.sin(thud_phase)*soft_decay(t, .023)
    knock = .22*shaped_noise(t, rng, 300., 2500.)*soft_decay(t, .012)
    return .68*np.tanh(.8*voice)*env + thud + knock


def make_hit(rng: np.random.Generator) -> FloatArray:
    t = timeline(DURATIONS['hit'])
    freq = 155. + 100.*np.exp(-t/.013)
    phase = np.cumsum(2*np.pi*freq/SYNTH_SR)
    body = (.75*np.sin(phase) + .21*np.sin(phase*2.03+.20)
            + .14*np.sin(phase*4.17)) * soft_decay(t, .024, .0012, .014)
    tap = .42*shaped_noise(t, rng, 550., 3600.)*soft_decay(t, .009, .0008, .012)
    surface = .16*shaped_noise(t, rng, 180., 1350.)*soft_decay(t, .026, .0015, .014)
    return np.tanh(1.1*(body+tap+surface))


MAKERS = dict(zip(ORDER, [make_hiss, make_laowu, make_enemy_warn,
                         make_interrupt, make_player_hurt, make_hit]))


def k_filter(x: FloatArray) -> FloatArray:
    """48 kHz K-weighting filters for an ungated energy proxy (NOT LUFS).

    Short one-shots are not claimed to meet a programme loudness standard.
    This only helps set provisional gain; subjective matching is pending.
    """
    b1 = [1.53512485958697, -2.69169618940638, 1.19839281085285]
    a1 = [1., -1.69065929318241, .73248077421585]
    b2 = [1., -2., 1.]
    a2 = [1., -1.99004745483398, .99007225036621]
    return signal.lfilter(b2, a2, signal.lfilter(b1, a1, x))


def active_k_rms(x: FloatArray) -> float:
    # Fixed 10 ms energy frames, retaining frames > -30 dB from max.
    y = k_filter(x)
    frame = 480
    padded = np.pad(y, (0, (-len(y)) % frame))
    powers = np.mean(padded.reshape(-1, frame)**2, axis=1)
    keep = powers > max(float(powers.max())*1e-3, 1e-16)
    return math.sqrt(float(np.mean(powers[keep]))) if np.any(keep) else 0.


def true_peak(x: FloatArray, factor: int = 8) -> float:
    # Offline oversampled estimate, not a certified true-peak meter.
    return float(np.max(np.abs(signal.resample_poly(x, factor, 1, window=('kaiser', 10.)))))


def master(x: FloatArray, name: str, seed: int) -> tuple[FloatArray, dict[str, float]]:
    x = filter_audio(x, 42., 'highpass', order=2)
    x = filter_audio(x, 8750., 'lowpass', order=4)
    x = signal.resample_poly(x, 1, 2, window=('kaiser', 9.))
    x = x[:round(DURATIONS[name]*SR)]
    x *= boundary_window(len(x), .001, .012 if name != 'hit' else .008, sr=SR)
    # Windowed mean correction preserves zero-valued boundaries.
    w = np.sin(np.linspace(0., np.pi, len(x)))**2
    x -= np.sum(x)/np.sum(w)*w
    target = -14. if name != 'hit' else -15.
    gain = 10**(target/20.)/max(active_k_rms(x), 1e-12)
    gain = min(gain, 10**(-3.22/20.)/max(true_peak(x), 1e-12))
    x *= gain
    # TPDF dither: at most one LSB; fade the dither with the boundaries.
    rng = np.random.default_rng(seed)
    dither = (rng.random(len(x))-rng.random(len(x)))/32768.
    dither *= boundary_window(len(x), .002, .006, sr=SR)
    quantized = np.rint((x+dither)*32768.)
    if np.max(np.abs(quantized)) >= 32767:
        raise ValueError(f'Unexpected clipping while mastering {name}')
    out = quantized.astype('<i2').astype(np.float64)/32768.
    out[0] = out[-1] = 0.
    return out, {'gain_db': db(gain), 'target_active_k_rms_db': target}


def write_wav(path: Path, x: FloatArray) -> None:
    if x.ndim != 1 or not np.all(np.isfinite(x)):
        raise ValueError('Expected a finite, mono signal')
    if np.max(np.abs(x)) >= 1.:
        raise ValueError(f'Refusing clipped output: {path}')
    pcm = np.rint(x*32768.).astype('<i2')
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def read_wav(path: Path) -> tuple[FloatArray, tuple[int, int, int]]:
    with wave.open(str(path), 'rb') as w:
        fmt = (w.getframerate(), w.getsampwidth()*8, w.getnchannels())
        data = w.readframes(w.getnframes())
    return np.frombuffer(data, dtype='<i2').astype(np.float64)/32768., fmt


def qa_clip(path: Path, name: str) -> dict[str, Any]:
    x, fmt = read_wav(path)
    active = np.flatnonzero(np.abs(x) > 10**(-60./20.))
    lead_ms = 1000.*active[0]/SR if len(active) else len(x)/SR*1000
    tail_ms = 1000.*(len(x)-1-active[-1])/SR if len(active) else len(x)/SR*1000
    dc = float(np.mean(x))
    peaks = true_peak(x)
    info: dict[str, Any] = {
        'file': path.name, 'description': DESCRIPTIONS[name],
        'sample_rate_hz': fmt[0], 'bit_depth': fmt[1], 'channels': fmt[2],
        'frames': len(x), 'duration_seconds': round(len(x)/SR, 6),
        'file_bytes': path.stat().st_size,
        'sample_peak_dbfs': round(db(np.max(np.abs(x))), 3),
        'true_peak_8x_estimate_dbtp': round(db(peaks), 3),
        'rms_dbfs': round(db(np.sqrt(np.mean(x*x))), 3),
        'active_k_weighted_rms_db_proxy_not_lufs': round(db(active_k_rms(x)), 3),
        'dc_offset_linear': dc, 'dc_offset_dbfs': round(db(abs(dc)), 3),
        'first_sample': float(x[0]), 'last_sample': float(x[-1]),
        'leading_below_minus60_dbfs_ms': round(float(lead_ms), 3),
        'trailing_below_minus60_dbfs_ms': round(float(tail_ms), 3),
        'tail_last_5ms_rms_dbfs': round(db(np.sqrt(np.mean(x[-240:]**2))), 3),
        'clipped_samples': int(np.sum(np.abs(x) >= 32767/32768.)),
    }
    lo, hi = LIMITS[name]
    checks = {
        'format': fmt == (48_000, 16, 1),
        'duration': lo <= len(x)/SR <= hi,
        'headroom_at_least_3_db': db(peaks) <= -3.,
        'zero_boundary_samples': x[0] == 0. and x[-1] == 0.,
        'dc_below_minus80_dbfs': db(abs(dc)) < -80.,
        'no_digital_clipping': info['clipped_samples'] == 0,
        'onset_under_10_ms': lead_ms < 10.,
        'unused_tail_under_25_ms': tail_ms < 25.,
        'tail_last_5ms_below_minus40_dbfs': info['tail_last_5ms_rms_dbfs'] < -40.,
    }
    checks = {key: bool(value) for key, value in checks.items()}
    info['checks'] = checks
    info['technical_pass'] = all(checks.values())
    return info


def build_previews(clips: dict[str, FloatArray], output: Path) -> dict[str, Any]:
    review = output/'Review'
    review.mkdir(exist_ok=True)
    # Single auditions preserve the mastered file gains; no normalisation.
    sections: list[dict[str, Any]] = []
    pos = .35
    single = np.zeros(round(7.2*SR))
    for name in ORDER:
        add_at(single, clips[name], pos, sr=SR)
        sections.append({'file': f'{name}.wav', 'start_seconds': round(pos, 3),
                         'end_seconds': round(pos+len(clips[name])/SR, 3)})
        pos += 1.1
    write_wav(review/'preview_single.wav', single)
    # Repetition is deterministic: no pitch randomisation, no compression.
    intervals = {'hiss': .55, 'laowu': 1./1.30, 'enemy_warn': .80,
                 'interrupt': .35, 'player_hurt': .50, 'hit': .06}
    repeats = {'hiss': 4, 'laowu': 6, 'enemy_warn': 4,
               'interrupt': 6, 'player_hurt': 6, 'hit': 20}
    events: list[tuple[float, str]] = []
    rep_sections = []
    pos = .35
    for name in ORDER:
        start = pos
        for i in range(repeats[name]):
            events.append((start+i*intervals[name], name))
        end = start+(repeats[name]-1)*intervals[name]+len(clips[name])/SR
        rep_sections.append({'file': f'{name}.wav', 'start_seconds': round(start, 3),
                             'end_seconds': round(end, 3), 'count': repeats[name],
                             'interval_seconds': intervals[name]})
        pos = end+.8
    repeat = np.zeros(math.ceil(pos*SR))
    for start, name in events:
        add_at(repeat, clips[name], start, sr=SR)
    repeat_scale = min(1., 10**(-3.22/20.)/max(true_peak(repeat), 1e-12))
    write_wav(review/'preview_repeat.wav', repeat*repeat_scale)
    # A deliberately small offline mix exercises prioritisation, not Unity.
    bus_gain_db = {'hiss': -10., 'laowu': -12., 'enemy_warn': -8.,
                   'interrupt': -10., 'player_hurt': -8., 'hit': -17.}
    mixed = np.zeros(round(6.5*SR))
    mix_events: list[tuple[float, str]] = []
    for start in np.arange(.3, 6., 1./1.3):
        mix_events.append((float(start), 'laowu'))
    for start in np.arange(.52, 6.12, .06):
        mix_events.append((float(start), 'hit'))
    mix_events += [(1.20, 'enemy_warn'), (1.72, 'hiss'), (1.735, 'interrupt'),
                   (3.25, 'enemy_warn'), (4.00, 'player_hurt'),
                   (5.05, 'enemy_warn'), (5.53, 'hiss'), (5.545, 'interrupt')]
    for start, name in mix_events:
        add_at(mixed, clips[name], start, gain=10**(bus_gain_db[name]/20.), sr=SR)
    mix_scale = min(1., 10**(-3.22/20.)/max(true_peak(mixed), 1e-12))
    write_wav(review/'preview_mix.wav', mixed*mix_scale)
    result = {
        'single_sections': sections, 'repeat_sections': rep_sections,
        'repeat_global_gain_db': round(db(repeat_scale), 4),
        'mix_clip_gains_db_starting_points_only': bus_gain_db,
        'mix_global_gain_db': round(db(mix_scale), 4),
        'mix_events': [{'time': round(t, 5), 'file': f'{n}.wav'} for t, n in sorted(mix_events)],
        'note': 'Offline playback material only. No Unity implementation or human listening claimed.'
    }
    (review/'preview_timeline.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parent/'rebuild')
    parser.add_argument('--overwrite', action='store_true', help='Explicitly replace generated audio and QA outputs.')
    args = parser.parse_args()
    out = args.output.resolve()
    if not args.overwrite and any((out/f'{n}.wav').exists() for n in ORDER):
        parser.error('Existing audio found. Use a new --output folder or explicitly pass --overwrite.')
    out.mkdir(parents=True, exist_ok=True)
    clips: dict[str, FloatArray] = {}
    report: dict[str, Any] = {
        'delivery': 'R01-S01 original synthetic SFX candidate v1',
        'production_date': '2026-09-14', 'seed': SEED,
        'tools': {'python': platform.python_version(), 'numpy': np.__version__, 'scipy': scipy.__version__},
        'audio_model': None, 'external_recordings': [],
        'human_listening': 'not_performed', 'unity_test': 'not_performed',
        'true_peak_method': '8x scipy.signal.resample_poly, Kaiser beta 10; estimated, not certified',
        'loudness_method': '10ms active K-weighted RMS proxy, no integrated LUFS or subjective equivalence claim',
        'clips': [],
    }
    for i, name in enumerate(ORDER):
        raw = MAKERS[name](np.random.default_rng(SEED+i))
        pcm, mastering = master(raw, name, SEED+100+i)
        path = out/f'{name}.wav'
        write_wav(path, pcm)
        clips[name] = pcm
        item = qa_clip(path, name)
        item['mastering'] = mastering
        report['clips'].append(item)
    report['preview'] = build_previews(clips, out)
    report['all_technical_checks_pass'] = all(c['technical_pass'] for c in report['clips'])
    (out/'qa.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: report[k] for k in ['all_technical_checks_pass', 'tools', 'clips']}, ensure_ascii=False, indent=2))
    if not report['all_technical_checks_pass']:
        raise SystemExit('A technical check failed; inspect qa.json before delivery.')


if __name__ == '__main__':
    main()
