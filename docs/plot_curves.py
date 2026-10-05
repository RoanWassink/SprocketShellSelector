"""Render v0.9.7 gameplay equations from ShellBalance.SpacedRetention.
Run with Python + matplotlib + numpy. No measured in-game data.
"""
from pathlib import Path
import csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.ticker import PercentFormatter

OUT = Path(__file__).resolve().parent
COLORS = ["#2563eb", "#0891b2", "#d97706", "#7c3aed"]
plt.rcParams.update({"font.family": "DejaVu Sans", "font.size": 11,
                     "axes.spines.top": False, "axes.spines.right": False,
                     "svg.fonttype": "none", "savefig.facecolor": "white"})

def retention(behavior, calibre, plate_rha, gap):
    floor, sensitivity = (.15, .35) if behavior == "heat" else (.10, 12)
    relative = np.asarray(gap) / calibre
    exponent = (sensitivity * np.maximum(0, relative - .1)
                * (.25 + .75 * (1 - np.exp(-plate_rha / (.25 * calibre))))
                if behavior == "heat" else sensitivity * relative)
    return floor + (1 - floor) * np.exp(-exponent)

def canvas(title, subtitle):
    fig, ax = plt.subplots(figsize=(11, 6.6))
    fig.subplots_adjust(left=.10, right=.96, top=.79, bottom=.23)
    fig.text(.10, .94, title, fontsize=22, weight="bold", color="#172033")
    fig.text(.10, .875, subtitle, fontsize=11, color="#526078")
    ax.set(xlim=(0, 500), ylim=(0, 1.025), xlabel="Air gap between plates (mm)",
           ylabel="Remaining penetration retained after the gap")
    ax.yaxis.set_major_formatter(PercentFormatter(1))
    ax.set_xticks(np.arange(0, 501, 100))
    ax.set_yticks(np.arange(0, 1.01, .2))
    ax.grid(alpha=.18)
    fig.text(.10, .12, "Sprocket Shell Selector · v0.9.7 model · Default settings", fontsize=10, weight="bold")
    fig.text(.10, .085, "Calculated gameplay curves, not experimental results. Native plate consumption occurs separately.\n"
             "Applied to the original penetrator once per gap; secondary fragments retain native penetration.",
             fontsize=9, color="#526078", linespacing=1.6, va="top")
    return fig, ax

gap = np.arange(0, 501, dtype=float)
rows = []
fig, ax = canvas("HEAT — gradual jet disruption",
                 "100 mm gun calibre · Effect of preceding plate thickness (LOS RHA) · Loss rate 0.35 per calibre")
for thickness, color in zip([5, 10, 25, 50], COLORS):
    values = retention("heat", 100, thickness, gap)
    ax.plot(gap, values, color=color, lw=2.5, label=f"{thickness} mm front plate")
    rows.extend(("HEAT", 100, thickness, g, v) for g, v in zip(gap, values))
ax.axhline(.15, color="#64748b", ls="--", lw=1)
ax.text(495, .17, "15% asymptotic floor per gap", ha="right", fontsize=9, color="#64748b")
ax.legend(loc="upper right", frameon=False)
ax.text(12, .035, "No extra gap loss up to 10 mm at this calibre", fontsize=9, color="#526078")
for ext in ("png", "svg"):
    fig.savefig(OUT / f"heat-air-gap.{ext}", dpi=180)
plt.close(fig)

fig, ax = canvas("HESH — rapid decoupling across an air gap",
                 "Separate HESH curve · Loss rate 12 per calibre · Front-plate thickness does not alter this loss curve")
for calibre, color in zip([75, 100, 125], COLORS):
    values = retention("hesh", calibre, 10, gap)
    ax.plot(gap, values, color=color, lw=2.5, label=f"{calibre} mm gun")
    rows.extend(("HESH", calibre, 10, g, v) for g, v in zip(gap, values))
ax.axhline(.10, color="#64748b", ls="--", lw=1)
ax.text(495, .125, "10% asymptotic floor per gap", ha="right", fontsize=9, color="#64748b")
ax.legend(loc="upper right", frameon=False)
inset = ax.inset_axes([.36, .37, .44, .52])
small_gap = np.linspace(0, 50, 251)
for calibre, color in zip([75, 100, 125], COLORS):
    inset.plot(small_gap, retention("hesh", calibre, 10, small_gap), color=color, lw=2)
inset.set(xlim=(0, 50), ylim=(.08, 1.02), title="Close spacing: 0–50 mm")
inset.set_xticks([0, 10, 25, 50]); inset.set_yticks([.1, .5, 1])
inset.yaxis.set_major_formatter(PercentFormatter(1))
inset.tick_params(labelsize=9); inset.grid(alpha=.18)
for ext in ("png", "svg"):
    fig.savefig(OUT / f"hesh-air-gap.{ext}", dpi=180)
plt.close(fig)

with (OUT / "curve-data.csv").open("w", newline="", encoding="utf-8") as stream:
    writer = csv.writer(stream, lineterminator="\n")
    writer.writerow(["behavior", "gun_calibre_mm", "front_plate_los_rha_mm", "air_gap_mm", "retention_fraction"])
    writer.writerows(rows)
assert abs(float(retention("heat", 100, 10, 100)) - .8767622974) < 1e-9
assert float(retention("heat", 100, 10, 0)) == 1
assert float(retention("hesh", 100, 10, 50)) < .103
print("Created two PNG/SVG figures and CSV data; formula reference checks passed.")
