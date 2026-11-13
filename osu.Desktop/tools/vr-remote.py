#!/usr/bin/env python3
# Torii: drives Monado's "remote" simulated headset and Index controllers (src/xrt/drivers/remote in Monado, protocol "mndrmt3"),
# so VR input can be scripted without a headset: controller poses, trigger, grip (squeeze) and thumbsticks.
#
# Usage: vr-remote.py <scenario.json> [--hold SECONDS] [--port 4242]
#
# The scenario is a list of keyframes, interpolated linearly (rotations as yaw/pitch/roll in degrees, applied in that order):
#   [{"t": 0.0, "right": {"pos": [0.2, 1.3, -0.4], "rot": [0, 0, 0], "squeeze": 0, "trigger": 0, "stick": [0, 0]}},
#    {"t": 1.0, "right": {"pos": [0.3, 1.3, -0.4], "rot": [20, 0, 0], "squeeze": 1}}, ...]
# Values left out of a keyframe keep the previous keyframe's value. "head" takes "pos" and "rot" too. Positions are in metres in the
# runtime's tracking space (y up, -z forward; the head starts at y = 1.6). After the last keyframe the final state is held for --hold
# seconds; Monado keeps it after disconnecting.
#
# Monado has to run with the remote driver: osu.Desktop/tools/vr-monado.sh does that when REMOTE=1.
import json
import math
import socket
import struct
import sys
import time

HEADER = b"mndrmt3\0"
HEAD_FORMAT = "<" + "4f7fI" * 2 + "7f" + "4?"
CONTROLLER_FORMAT = "<7f3f3f5f" + "f" * 3 + "2f" + "f" + "2f" + "16?"
HEAD_SIZE = struct.calcsize(HEAD_FORMAT)
CONTROLLER_SIZE = struct.calcsize(CONTROLLER_FORMAT)
PACKET_SIZE = 8 + HEAD_SIZE + 2 * CONTROLLER_SIZE
assert PACKET_SIZE == 376, PACKET_SIZE


def quat_from_euler(yaw, pitch, roll):
    """Yaw about +y (positive turns left), then pitch about +x (positive looks up), then roll about -z; degrees. Returns (x, y, z, w)."""

    def axis(ax, ay, az, deg):
        h = math.radians(deg) / 2
        s = math.sin(h)
        return (ax * s, ay * s, az * s, math.cos(h))

    def mul(a, b):
        ax, ay, az, aw = a
        bx, by, bz, bw = b
        return (aw * bx + ax * bw + ay * bz - az * by,
                aw * by - ax * bz + ay * bw + az * bx,
                aw * bz + ax * by - ay * bx + az * bw,
                aw * bw - ax * bx - ay * by - az * bz)

    return mul(mul(axis(0, 1, 0, yaw), axis(1, 0, 0, pitch)), axis(0, 0, -1, roll))


def lerp(a, b, f):
    if isinstance(a, list):
        return [lerp(x, y, f) for x, y in zip(a, b)]
    return a + (b - a) * f


DEFAULTS = {
    "head": {"pos": [0, 1.6, 0], "rot": [0, 0, 0]},
    "left": {"pos": [-0.2, 1.3, -0.4], "rot": [0, 0, 0], "squeeze": 0, "trigger": 0, "stick": [0, 0]},
    "right": {"pos": [0.2, 1.3, -0.4], "rot": [0, 0, 0], "squeeze": 0, "trigger": 0, "stick": [0, 0]},
}


def complete(frames):
    """Fills in each keyframe's missing values from the previous one."""
    state = json.loads(json.dumps(DEFAULTS))
    out = []
    for frame in frames:
        for device, values in frame.items():
            if device != "t":
                state[device].update(values)
        out.append({"t": frame["t"], **json.loads(json.dumps(state))})
    return out


def state_at(frames, t):
    if t <= frames[0]["t"]:
        return frames[0]
    for a, b in zip(frames, frames[1:]):
        if t < b["t"]:
            f = (t - a["t"]) / (b["t"] - a["t"])
            return {d: {k: lerp(a[d][k], b[d][k], f) for k in a[d]} for d in DEFAULTS}
    return frames[-1]


def pack_head(head):
    pose = (*quat_from_euler(*head["rot"]), *head["pos"])
    view = (0.0,) * 4 + (0, 0, 0, 1) + (0, 0, 0) + (0,)
    return struct.pack(HEAD_FORMAT, *view, *view, *pose, False, False, False, False)


def pack_controller(c):
    pose = (*quat_from_euler(*c["rot"]), *c["pos"])
    trigger = float(c["trigger"])
    squeeze = float(c["squeeze"])
    bools = [False, True] + [False] * 6 + [trigger > 0.9, trigger > 0.05] + [False] * 6
    return struct.pack(CONTROLLER_FORMAT, *pose, 0, 0, 0, 0, 0, 0, *([0.0] * 5), trigger, squeeze, squeeze, *c["stick"], 0, 0, 0, *bools)


def main():
    args = sys.argv[1:]
    hold = 1.0
    port = 4242
    if "--hold" in args:
        i = args.index("--hold")
        hold = float(args[i + 1])
        del args[i:i + 2]
    if "--port" in args:
        i = args.index("--port")
        port = int(args[i + 1])
        del args[i:i + 2]

    with open(args[0]) as f:
        frames = complete(json.load(f))

    sock = socket.create_connection(("127.0.0.1", port))
    # The hub first sends its reset and latest state.
    received = b""
    while len(received) < 2 * PACKET_SIZE:
        chunk = sock.recv(2 * PACKET_SIZE - len(received))
        if not chunk:
            break
        received += chunk

    start = time.monotonic()
    end = frames[-1]["t"] + hold
    while True:
        t = time.monotonic() - start
        s = state_at(frames, min(t, frames[-1]["t"]))
        sock.sendall(HEADER + pack_head(s["head"]) + pack_controller(s["left"]) + pack_controller(s["right"]))
        if t >= end:
            break
        time.sleep(1 / 120)
    sock.close()


if __name__ == "__main__":
    main()
