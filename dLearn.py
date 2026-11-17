import os
import time
import math
import random
import ctypes
import win32gui
import win32api
import win32con

# Enable DPI Awareness so Unity physical pixels match Windows screen pixels 1:1
try:
    ctypes.windll.shcore.SetProcessDpiAwareness(2)
except Exception:
    ctypes.windll.user32.SetProcessDPIAware()

current_offset_x = 0.0
current_offset_y = 0.0

STORAGE_PATH = r"C:\Users\jacob\Documents\CS-530\storage.txt"
NUM_ANGLES = 48

# For relative hardware deltas, [5, 25, 80] gives short/medium/far impulses.
# For absolute Trackpad Mode around the pot, use larger pixel radii like [40, 120, 250].
RADII = [100, 800]

def find_goi_window():
    """Finds the Getting Over It window handle regardless of exact title."""
    found_hwnd = None
    def enum_handler(hwnd, _):
        nonlocal found_hwnd
        if win32gui.IsWindowVisible(hwnd):
            title = win32gui.GetWindowText(hwnd)
            if "Getting Over It" in title:
                found_hwnd = hwnd
    win32gui.EnumWindows(enum_handler, None)
    return found_hwnd

def parse_coords(content: str):
    """Parses 'x, y' string into a (float, float) tuple."""
    try:
        x_str, y_str, x_vel, y_vel, ang_vel = content.split(",")
        return float(x_str.strip()), float(y_str.strip())
    except Exception:
        return None

def get_target_offset(angle_idx: int, dist_idx: int):
    theta = angle_idx * (2.0 * math.pi / NUM_ANGLES)
    r = RADII[dist_idx]
    dx = r * math.cos(theta)
    dy = -r * math.sin(theta)  # Top-left origin: -Y is Up
    return dx, dy

def move_mouse_to_action(pot_x: float, pot_y: float, angle_idx: int, dist_idx: int, hwnd=None, trackpad_mode=False):
    global current_offset_x, current_offset_y

    # 1. Calculate desired offset (target_dx, target_dy) around the pot for this angle & radius
    target_dx, target_dy = get_target_offset(angle_idx, dist_idx)

    # 2. Compute the relative delta from the PREVIOUS hammer offset to the NEW target offset
    step_dx = int(round(target_dx - current_offset_x))
    step_dy = int(round(target_dy - current_offset_y))

    # 3. Send the hardware mouse delta so Unity moves the hammer to the new angle/radius
    win32api.mouse_event(win32con.MOUSEEVENTF_MOVE, step_dx, step_dy, 0, 0)

    # 4. Update current offset state
    current_offset_x += step_dx
    current_offset_y += step_dy

    return int(round(target_dx)), int(round(target_dy))

def run_random_agent(trigger_on_any_write=True, min_step_interval=0.05, trackpad_mode=False):
    hwnd = find_goi_window()
    if hwnd:
        print(f"Found window: '{win32gui.GetWindowText(hwnd)}' (HWND: {hwnd})")
    else:
        print("Warning: Getting Over It window not found.")

    last_content = None
    last_mtime = 0.0

    print("Watching storage.txt for changes... Press Ctrl+C to stop.")

    try:
        while True:
            if os.path.exists(STORAGE_PATH):
                try:
                    mtime = os.path.getmtime(STORAGE_PATH)
                    with open(STORAGE_PATH, "r") as f:
                        content = f.read().strip()
                except (PermissionError, OSError):
                    time.sleep(0.005)
                    continue

                if content:
                    should_trigger = (
                        (mtime != last_mtime) if trigger_on_any_write else (content != last_content)
                    )

                    if should_trigger:
                        coords = parse_coords(content)
                        if coords is not None:
                            last_content = content
                            last_mtime = mtime

                            angle_idx = random.randint(0, NUM_ANGLES - 1)
                            dist_idx = random.randint(0, len(RADII) - 1)

                            pot_x, pot_y = coords
                            out_x, out_y = move_mouse_to_action(
                                pot_x, pot_y, angle_idx, dist_idx, hwnd, trackpad_mode
                            )

                            angle_deg = angle_idx * (360.0 / NUM_ANGLES)
                            mode_str = "Target" if trackpad_mode else "Delta"
                            print(
                                f"Pot: ({pot_x:.1f}, {pot_y:.1f}) -> {mode_str}: ({out_x}, {out_y}) | "
                                f"Angle: {angle_idx:2d} ({angle_deg:5.1f} deg) | "
                                f"Radius: {RADII[dist_idx]}px"
                            )

                            time.sleep(min_step_interval)
                            continue

            time.sleep(0.005)

    except KeyboardInterrupt:
        print("\nStopped random agent.")

if __name__ == "__main__":
    # Set trackpad_mode=False for standard relative mouse impulses (works out of the box),
    # or trackpad_mode=True if Trackpad Mode is enabled in Getting Over It settings.
    run_random_agent(trigger_on_any_write=True, min_step_interval=0.05, trackpad_mode=True)