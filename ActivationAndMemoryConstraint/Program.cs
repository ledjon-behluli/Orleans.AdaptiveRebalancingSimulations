﻿using ScottPlot;
using System.Drawing;
using System.Windows.Forms;

const bool UseAdaptiveScaling = true;

int silos = 5;
int maxCycles = 140;
int maxEntropyStaleCycles = 10;
double maxEntropyDiff = 1e-4;

double[][] relativeMemoryUsagesList =
[
    [1], 
    [1, 1], // [1, 1.5]
    [1, 1, 1], // [1, 1.5, 2]
    [1, 1, 1, 1], // [1, 1.5, 2, 1.3],
    [1, 1, 1, 1, 1] // [1, 1.5, 2, 1.3, 2.2]
];

var form = new Form()
{
    WindowState = FormWindowState.Maximized
};

var panel = new Panel
{
    Dock = DockStyle.Fill,
    AutoScroll = true
};

var tableLayoutPanel = new TableLayoutPanel
{
    RowCount = silos,
    ColumnCount = 2,
    Dock = DockStyle.Top,
    AutoSize = true
};

panel.Controls.Add(tableLayoutPanel);
form.Controls.Add(panel);

for (int S = 1; S <= silos; S++)
{
    var relativeMemoryUsages = relativeMemoryUsagesList[S - 1];
    var (cycles, alphas, activationsHistory, initialActivations, finalActivations, totalCycles, H_current) = 
        SimulateCluster(S, maxCycles, maxEntropyStaleCycles, maxEntropyDiff, relativeMemoryUsages);

    double H_start = CalculateEntropy(initialActivations, relativeMemoryUsages, relativeMemoryUsages.HarmonicMean());
    double H_end = CalculateEntropy(finalActivations, relativeMemoryUsages, relativeMemoryUsages.HarmonicMean());
    double H_max = Math.Log(S);
    double alpha_start = alphas[0];
    double alpha_end = alphas[^1];

    var alphaPlot = new FormsPlot { Dock = DockStyle.Fill };
    tableLayoutPanel.Controls.Add(alphaPlot, 0, S - 1);
    alphaPlot.Plot.AddScatter(cycles, alphas, label: $"Alpha", markerShape: MarkerShape.filledCircle);
    alphaPlot.Plot.AddScatter(cycles, H_current, label: $"H_current", markerShape: MarkerShape.filledCircle);
    alphaPlot.Plot.AddHorizontalLine(H_max, color: Color.Red, label: $"H_max={H_max:F2}");
    alphaPlot.Plot.Title($"Silos = {S}");
    alphaPlot.Plot.XLabel("Cycles");
    alphaPlot.Plot.YLabel("Alpha / Entropy");
    alphaPlot.Plot.XAxis.ManualTickPositions(cycles.Select(c => (double)c).ToArray(), cycles.Select(c => c.ToString()).ToArray());
    alphaPlot.Plot.Legend();
    alphaPlot.Refresh();

    var activationPlot = new FormsPlot { Dock = DockStyle.Fill };
    tableLayoutPanel.Controls.Add(activationPlot, 1, S - 1);
    double idealActivation = initialActivations.Sum() / S;
    for (int i = 0; i < S; i++)
    {
        activationPlot.Plot.AddScatter(cycles, activationsHistory.Select(x => x[i]).ToArray(), label: $"Silo {i + 1} (Initial={initialActivations[i]}, Final={finalActivations[i]}, Rel. Mem. Usage={relativeMemoryUsages[i]})", markerShape: MarkerShape.filledCircle);
    }
    activationPlot.Plot.AddHorizontalLine(idealActivation, color: Color.Black, style: LineStyle.Dash, label: $"Idealized Equilibrium={idealActivation:F0}");
    activationPlot.Plot.Title($"Silos = {S} | Total Cycles = {totalCycles} | Stale Cycles = {maxEntropyStaleCycles}");
    activationPlot.Plot.XLabel("Cycles");
    activationPlot.Plot.YLabel("Activations");
    activationPlot.Plot.XAxis.ManualTickPositions(cycles.Select(c => (double)c).ToArray(), cycles.Select(c => c.ToString()).ToArray());
    activationPlot.Plot.SetAxisLimits(yMin: 0);
    activationPlot.Plot.Legend();
    activationPlot.Refresh();
}

Application.Run(form);

static double CalculateEntropy(double[] n, double[] m, double M)
{
    double totalActivations = n.Sum();
    if (totalActivations == 0)
        return 0;

    double[] p = n.Select((x, i) => x / totalActivations * (m[i] / M)).Where(x => x > 0).ToArray(); // filter out zero probabilities to avoid Log(0)  
    double entropy = -p.Sum(x => x * Math.Log(x));
    return entropy;
}

static double[] GenerateInitialActivations(int S)
{
    switch (S)
    {
        case 1: return [100];
        case 2: return [900, 10]; // increase from 900 to 9000, and turn on adaptive-scaling to see difference
        case 3: return [800, 10, 100];
        case 4: return [70, 10, 10, 10];
        case 5: return [700, 120, 340, 1200, 2500];
        default:
            double baseValue = 10;
            double[] activations = new double[S];

            activations[0] = baseValue * (S - 1);
            for (int i = 1; i < S; i++)
                activations[i] = baseValue;

            return activations;
    }
}

static double AdaptiveScaling(int cycle, int S, double baseGrowthRate = 0.1, double siloFactor = 0.5)
{
    double growthComponent = 1 - Math.Exp(-baseGrowthRate * cycle);
    double siloComponent = 1 / (1 + siloFactor * (S - 1));

    return growthComponent * siloComponent;
}

static (double[], double[], double[][], double[], double[], int, double[]) SimulateCluster(
    int S, int maxCycles, int maxEntropyStaleCycles, double maxEntropyDiff, double[] memoryUsages)
{
    double[] activations = GenerateInitialActivations(S);
    double N = activations.Sum();

    double maxEntropy = Math.Log(S);
    double currentEntropy = CalculateEntropy(activations, memoryUsages, memoryUsages.HarmonicMean());

    double[] cycleNumbers = Enumerable.Range(0, maxCycles).Select(x => (double)x).ToArray();
    double[] alphas = new double[maxCycles];
    double[] H_current = new double[maxCycles];
    double[][] deviations = new double[maxCycles][];
    double[][] activationsHistory = new double[maxCycles][];

    double[] initialActivations = (double[])activations.Clone();
    int entropyStableCount = 0;
    int cycle;

    double meanMemoryUsage = memoryUsages.HarmonicMean();

    for (cycle = 0; cycle < maxCycles; cycle++)
    {
        activationsHistory[cycle] = (double[])activations.Clone();

        double[] optimalActivations = activations.Select((x, i) => (N / S) * (meanMemoryUsage / memoryUsages[i])).ToArray();

        deviations[cycle] = activations.Select((x, i) => x - optimalActivations[i]).ToArray();

        double previousEntropy = currentEntropy;
        currentEntropy = CalculateEntropy(activations, memoryUsages, meanMemoryUsage);
        double alpha = maxEntropy != 0 ? currentEntropy / maxEntropy : 0;
        alphas[cycle] = alpha;
        H_current[cycle] = currentEntropy;

        if (Math.Abs(currentEntropy - previousEntropy) < maxEntropyDiff)
            entropyStableCount++;
        else
            entropyStableCount = 0;

        if (entropyStableCount >= maxEntropyStaleCycles)
            break;

        for (int i = 0; i < S; i++)
        {
            if (i < S - 1)
            {
                double devDiff = deviations[cycle][i] - deviations[cycle][i + 1];
                double scalingFactor = UseAdaptiveScaling ? AdaptiveScaling(cycle, S) : 1.0d;
                double deltaN = alpha * scalingFactor * (devDiff / 2);

                if (!double.IsNaN(deltaN))
                {
                    deltaN = Math.Round(deltaN);
                    activations[i] -= deltaN;
                    activations[i + 1] += deltaN;
                }
            }
        }
        activations = activations.Select(x => Math.Max(x, 0)).ToArray();
    }

    double[] finalActivations = (double[])activations.Clone();

    return (
        cycleNumbers.Take(cycle + 1).ToArray(),
        alphas.Take(cycle + 1).ToArray(),
        activationsHistory.Take(cycle + 1).ToArray(),
        initialActivations,
        finalActivations,
        cycle + 1,
        H_current.Take(cycle + 1).ToArray());
}

public static class MeanEx
{
    public static double HarmonicMean(this double[] values)
    {
        double sumReciprocal = 0.0;
        foreach (double value in values)
        {
            sumReciprocal += 1.0 / value;
        }

        return values.Length / sumReciprocal;
    }
}