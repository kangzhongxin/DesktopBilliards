"""Build the two scattered-table combo GIFs from the game's own off-screen frames.

The game's --demo mode:
  1. breaks the rack with real physics;
  2. waits for the balls to settle (the scattered table);
  3. records its own frames at ~20fps (RenderScene -> PNG, no screen capture);
  4. performs three scripted pots with a visible cue-stick pullback/strike.

This avoids other desktop windows appearing in the GIFs.
"""

import re
import subprocess
import tempfile
import time
from pathlib import Path

from PIL import Image

ROOT = Path(r"E:\OtherProject\DesktopBilliards")
EXE = ROOT / "bin" / "Debug" / "net10.0-windows" / "DesktopBilliards.exe"
ASSETS = ROOT / "blog" / "assets"
TEMP = Path(tempfile.gettempdir())
LOG = TEMP / "DesktopBilliards.log"
FRAME_DIR = TEMP / "DesktopBilliards-frames"


def wait_for_log(text: str, timeout: float = 60.0) -> bool:
    end = time.monotonic() + timeout
    while time.monotonic() < end:
        try:
            if text in LOG.read_text(encoding="utf-8", errors="ignore"):
                return True
        except FileNotFoundError:
            pass
        time.sleep(0.05)
    return False


def parse_marks() -> dict[str, float]:
    marks: dict[str, float] = {}
    try:
        lines = LOG.read_text(encoding="utf-8", errors="ignore").splitlines()
    except FileNotFoundError:
        return marks

    for line in lines:
        if "Demo: settled" in line:
            m = re.search(r"t=([\d.]+)", line)
            if m:
                marks["settled"] = float(m.group(1))
        elif "Demo: pot " in line:
            m = re.search(r"Demo: pot (\d+).*?t=([\d.]+)", line)
            if m:
                marks[f"pot{m.group(1)}"] = float(m.group(2))
        elif "Demo: done" in line:
            m = re.search(r"t=([\d.]+)", line)
            if m:
                marks["done"] = float(m.group(1))

    return marks


def load_frames() -> list[tuple[Path, float]]:
    manifest = FRAME_DIR / "frames.txt"
    result: list[tuple[Path, float]] = []
    if not manifest.exists():
        return result

    for line in manifest.read_text(encoding="utf-8", errors="ignore").splitlines():
        if "|" not in line:
            continue
        name, value = line.split("|", 1)
        try:
            result.append((FRAME_DIR / name, float(value)))
        except ValueError:
            pass

    result.sort(key=lambda item: item[1])
    return result


def make_gif(frames: list[tuple[Path, float]], out: Path, colors: int = 160) -> None:
    if len(frames) < 3:
        print("Not enough frames for", out)
        return

    images = [Image.open(path).convert("RGB") for path, _ in frames]
    durations = []
    for i in range(len(frames) - 1):
        durations.append(max(30, min(160, int((frames[i + 1][1] - frames[i][1]) * 1000))))
    durations.append(durations[-1] if durations else 60)

    quantized = [
        image.quantize(colors=colors, method=Image.Quantize.MEDIANCUT)
        for image in images
    ]

    raw = out.with_name(out.stem + "-raw.gif")
    quantized[0].save(
        raw,
        save_all=True,
        append_images=quantized[1:],
        duration=durations,
        loop=0,
        optimize=True,
        disposal=2,
    )
    print(f"Raw {raw.name}: {len(images)} frames, {raw.stat().st_size / 1024 / 1024:.1f} MB")

    # Re-encode with a shared ffmpeg palette for a smaller, smoother GIF.
    cmd = [
        "ffmpeg", "-y", "-i", str(raw),
        "-vf",
        "fps=16,scale=900:-1:flags=lanczos,split[s0][s1];"
        "[s0]palettegen=max_colors=160:stats_mode=diff[p];"
        "[s1][p]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle",
        "-loop", "0", str(out),
    ]
    subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    raw.unlink(missing_ok=True)
    print(f"Saved {out.name}: {out.stat().st_size / 1024 / 1024:.1f} MB")


def main() -> None:
    try:
        LOG.unlink(missing_ok=True)
    except Exception:
        pass

    proc = subprocess.Popen([str(EXE), "--demo"])
    if not wait_for_log("first paint"):
        print("Game did not reach first paint")
        proc.kill()
        return

    if not wait_for_log("Demo: record done", timeout=70):
        print("Demo recording did not finish")
        proc.kill()
        return

    time.sleep(0.4)
    proc.kill()
    time.sleep(0.2)

    marks = parse_marks()
    frames = load_frames()
    print("Demo marks:", marks)
    print("Frames:", len(frames), "first:", frames[0][1] if frames else None,
          "last:", frames[-1][1] if frames else None)

    if not frames:
        print("No frames found")
        return

    pot2 = marks.get("pot2", frames[-1][1] - 2.0)
    pot3 = marks.get("pot3", frames[-1][1])

    # Three consecutive pots: the whole recorded scattered-table sequence.
    make_gif([f for f in frames if f[1] <= pot3 + 1.05], ASSETS / "hero.gif", 160)

    # Two consecutive pots: cut before the third pot.
    make_gif([f for f in frames if f[1] <= pot2 + 1.05], ASSETS / "combo.gif", 160)

    print("Done.")


if __name__ == "__main__":
    main()
