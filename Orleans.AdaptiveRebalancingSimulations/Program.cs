using System.Windows.Forms;
using ScottPlot;

// Memory-constraint entropy

int maxSilos = 5;
int maxCycles = 30;
int maxEntropyStaleCycles = 5;
double maxEntropyDiff = 1e-4;

// Example memory usages for each silo  
double[][] memoryUsagesList = new double[][]
{
    new double[] { 1 },
    new double[] { 1, 1 },
    new double[] { 1, 1, 1 },
    new double[] { 1, 1, 1, 1 },
    new double[] { 1, 1, 1, 1, 1 }
};

var form = new Form();
var panel = new Panel
{
    Dock = DockStyle.Fill,
    AutoScroll = true
};

var tableLayoutPanel = new TableLayoutPanel
{
    RowCount = maxSilos,
    ColumnCount = 2,
    Dock = DockStyle.Top,
    AutoSize = true
};

panel.Controls.Add(tableLayoutPanel);
form.Controls.Add(panel);

for (int S = 1; S <= maxSilos; S++)
{
    var memoryUsages = memoryUsagesList[S - 1];
    var (cycles, alphas, deviations, activationsHistory, initialActivations, finalActivations, totalCycles) = SimulateCluster(S, maxCycles, maxEntropyStaleCycles, maxEntropyDiff, memoryUsages);
    double H_start = CalculateEntropy(initialActivations);
    double H_end = CalculateEntropy(finalActivations);
    double H_max = Math.Log2(S);
    double alpha_start = alphas[0];
    double alpha_end = alphas[^1];

    // Create alpha plot  
    var alphaPlot = new FormsPlot { Dock = DockStyle.Fill };
    tableLayoutPanel.Controls.Add(alphaPlot, 0, S - 1);
    alphaPlot.Plot.AddScatter(cycles, alphas, label: $"Alpha", markerShape: MarkerShape.filledCircle);
    alphaPlot.Plot.AddHorizontalLine(H_max, color: System.Drawing.Color.Red, label: $"H_max={H_max:F2}");
    alphaPlot.Plot.Title($"Silos = {S}");
    alphaPlot.Plot.XLabel("Cycles");
    alphaPlot.Plot.YLabel("Alpha / Entropy");
    alphaPlot.Plot.Legend();
    alphaPlot.Refresh();

    // Create activations plot  
    var activationPlot = new FormsPlot { Dock = DockStyle.Fill };
    tableLayoutPanel.Controls.Add(activationPlot, 1, S - 1);
    double idealActivation = initialActivations.Sum() / S;
    for (int i = 0; i < S; i++)
    {
        activationPlot.Plot.AddScatter(cycles, activationsHistory.Select(x => x[i]).ToArray(), label: $"Silo {i + 1} (Initial={initialActivations[i]}, Final={finalActivations[i]}, Memory={memoryUsages[i]})", markerShape: MarkerShape.filledCircle);
    }
    activationPlot.Plot.AddHorizontalLine(idealActivation, color: System.Drawing.Color.Black, style: LineStyle.Dash, label: $"Idealized Equilibrium={idealActivation:F0}");
    activationPlot.Plot.Title($"Silos = {S} | Total Cycles = {totalCycles} | Stale Cycles = {maxEntropyStaleCycles}");
    activationPlot.Plot.XLabel("Cycles");
    activationPlot.Plot.YLabel("Activations");
    activationPlot.Plot.SetAxisLimits(yMin: 0);
    activationPlot.Plot.Legend();
    activationPlot.Refresh();
}

Application.Run(form);

static double CalculateEntropy(double[] n)
{
    double totalActivations = n.Sum();
    if (totalActivations == 0)
        return 0;

    double[] p = n.Select(x => x / totalActivations).Where(x => x > 0).ToArray(); // filter out zero probabilities to avoid log2(0)  
    double entropy = -p.Sum(x => x * Math.Log2(x));
    return entropy;
}

static double[] GenerateInitialActivations(int S)
{
    switch (S)
    {
        case 1: return new double[] { 100 };
        case 2: return new double[] { 900, 10 };
        case 3: return new double[] { 800, 10, 100 };
        case 4: return new double[] { 70, 10, 10, 10 };
        case 5: return new double[] { 700, 120, 340, 1200, 2500 };
        default:
            double baseValue = 10;
            double[] activations = new double[S];
            activations[0] = baseValue * (S - 1);
            for (int i = 1; i < S; i++)
                activations[i] = baseValue;
            return activations;
    }
}

static (double[], double[], double[][], double[], double[], int, double[][]) SimulateCluster(int S, int maxCycles, int maxEntropyStaleCycles, double maxEntropyDiff, double[] memoryUsages)
{
    double[] activations = GenerateInitialActivations(S);
    double N = activations.Sum(); // total activations  

    // initial entropy  
    double maxEntropy = Math.Log2(S);
    double currentEntropy = CalculateEntropy(activations);

    // store data for plotting  
    double[] cycleNumbers = Enumerable.Range(0, maxCycles).Select(x => (double)x).ToArray();
    double[] alphas = new double[maxCycles];
    double[][] deviations = new double[maxCycles][];
    double[][] activationsHistory = new double[maxCycles][];

    double[] initialActivations = (double[])activations.Clone();
    int entropyStableCount = 0;
    int cycle;

    // Calculate mean memory usage  
    double meanMemoryUsage = memoryUsages.Average();

    for (cycle = 0; cycle < maxCycles; cycle++)
    {
        activationsHistory[cycle] = (double[])activations.Clone();

        // Calculate optimal activations based on memory usage  
        double[] optimalActivations = activations.Select((x, i) => (N / S) * (meanMemoryUsage / memoryUsages[i])).ToArray();

        // Calculate deviations from optimal activations  
        deviations[cycle] = activations.Select((x, i) => x - optimalActivations[i]).ToArray();

        // Calculate current entropy  
        double previousEntropy = currentEntropy;
        currentEntropy = CalculateEntropy(activations);
        double alpha = maxEntropy != 0 ? currentEntropy / maxEntropy : 0;
        alphas[cycle] = alpha;

        // has entropy stabilized  
        if (Math.Abs(currentEntropy - previousEntropy) < maxEntropyDiff)
            entropyStableCount++;
        else
            entropyStableCount = 0;

        if (entropyStableCount >= maxEntropyStaleCycles)
            break;

        // adjust activations based on deviations  
        for (int i = 0; i < S; i++)
        {
            if (i < S - 1)
            {
                double devDiff = deviations[cycle][i] - deviations[cycle][i + 1];
                // Averaging deviations ensures that no single silo makes a disproportionately large adjustment in one cycle.  
                double deltaN = alpha * (devDiff / 2);
                if (!double.IsNaN(deltaN))
                {
                    deltaN = Math.Round(deltaN);
                    activations[i] -= deltaN;
                    activations[i + 1] += deltaN;
                }
            }
        }
        activations = activations.Select(x => Math.Max(x, 0)).ToArray(); // ensure activations are non-negative  
    }

    double[] finalActivations = (double[])activations.Clone();
    return (cycleNumbers.Take(cycle + 1).ToArray(), alphas.Take(cycle + 1).ToArray(), deviations.Take(cycle + 1).ToArray(), activationsHistory.Take(cycle + 1).ToArray(), initialActivations, finalActivations, cycle + 1);
}


/*
// Non-constraint entropy


*/