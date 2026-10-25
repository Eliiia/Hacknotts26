"""Plot the two headerless CSV columns as frequency and amplitude."""
import argparse
import os
from pathlib import Path

os.environ.setdefault("MPLCONFIGDIR", "/tmp/frequency-plot-matplotlib")
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

parser = argparse.ArgumentParser()
parser.add_argument("csv", nargs="?", default="/Users/me/Downloads/data.csv")
parser.add_argument("--output", default=str(Path(__file__).parent / "frequency_amplitude.png"))
parser.add_argument("--rows", type=int, help="Use only the first N CSV rows.")
parser.add_argument("--max-freq", type=float, help="Plot frequencies up to this value.")
parser.add_argument("--log-y", action="store_true", help="Use a logarithmic amplitude axis.")
args = parser.parse_args()
data = np.loadtxt(args.csv, delimiter=",", ndmin=2)
if data.shape[1] != 2 or not np.isfinite(data).all():
    raise ValueError("Expected two columns of finite numeric values.")
if args.rows is not None:
    if args.rows < 1:
        raise ValueError("Row limit must be positive.")
    data = data[:args.rows]
data = data[np.argsort(data[:, 0], kind="stable")]
data = data[data[:, 0] != 0]
if args.max_freq is not None:
    data = data[data[:, 0] <= args.max_freq]
if not len(data):
    raise ValueError("No data remains after excluding frequency 0.")
fig, ax = plt.subplots(figsize=(11, 5.5), layout="constrained")
ax.plot(data[:, 0], data[:, 1], color="#1769aa", linewidth=1.2)
ax.set(xlabel="Freq", ylabel="Amplitude", title="Amplitude vs Frequency")
if args.rows is not None:
    ax.set_title(f"Amplitude vs Frequency — First {args.rows:,} Rows")
ax.set_xlim(data[:, 0].min(), data[:, 0].max())
if args.max_freq is not None:
    ax.set_xlim(0, args.max_freq)
if args.log_y:
    if np.any(data[:, 1] <= 0):
        raise ValueError("Logarithmic amplitude requires positive values.")
    ax.set_yscale("log")
else:
    ax.ticklabel_format(axis="y", style="sci", scilimits=(0, 0), useMathText=True)
ax.grid(True, color="#dce2e8", linewidth=0.6)
ax.spines[["top", "right"]].set_visible(False)
fig.savefig(args.output, dpi=200, facecolor="white")
plt.close(fig)
print(f"Plotted {len(data):,} points; frequency range {data[0, 0]:g} to {data[-1, 0]:g}.")
print(args.output)
