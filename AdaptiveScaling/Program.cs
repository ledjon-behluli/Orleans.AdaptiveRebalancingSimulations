using ScottPlot;
using System.Windows.Forms;

int minSilos = 2;
int maxSilos = 10;
int maxCycles = 20;
double cycleBaseRate = 0.2;
double siloBaseRate = 0.5;

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
    RowCount = 3,
    ColumnCount = 1,
    Dock = DockStyle.Top,
    AutoSize = true
};

panel.Controls.Add(tableLayoutPanel);
form.Controls.Add(panel);

double GrowthComponent(int cycle) => 1 - Math.Exp(-cycleBaseRate * cycle);
double SiloComponent(int S) => 1 / (1 + siloBaseRate * (S - 1));
double ScalingFactor(int cycle, int S) => GrowthComponent(cycle) * SiloComponent(S);

var cycles = Enumerable.Range(1, maxCycles).ToArray();
var cyclesCompontentData = cycles.Select(GrowthComponent).ToArray();

var silos = Enumerable.Range(minSilos, maxSilos - minSilos + 1).ToArray();
var silosCompontentData = silos.Select(SiloComponent).ToArray();

var scalingFactorData = new double[silos.Length, maxCycles];
for (int i = 0; i < silos.Length; i++)
{
    for (int j = 0; j < maxCycles; j++)
    {
        scalingFactorData[i, j] = ScalingFactor(cycles[j], silos[i]);
    }
}

var growthPlot = new FormsPlot { Dock = DockStyle.Fill };
tableLayoutPanel.Controls.Add(growthPlot, 0, 0);
growthPlot.Plot.AddScatter(cycles.Select(c => (double)c).ToArray(), cyclesCompontentData, markerShape: MarkerShape.filledCircle);
growthPlot.Plot.Title("Growth Rate of Cycle Component");
growthPlot.Plot.XLabel("Cycle");
growthPlot.Plot.YLabel("Value");
growthPlot.Plot.XAxis.ManualTickPositions(cycles.Select(c => (double)c).ToArray(), cycles.Select(c => c.ToString()).ToArray());
growthPlot.Plot.Legend();
growthPlot.Refresh();

var siloComponentPlot = new FormsPlot { Dock = DockStyle.Fill };
tableLayoutPanel.Controls.Add(siloComponentPlot, 0, 1);
siloComponentPlot.Plot.AddScatter(silos.Select(s => (double)s).ToArray(), silosCompontentData, markerShape: MarkerShape.filledCircle);
siloComponentPlot.Plot.Title("Growth Rate of Silo Component");
siloComponentPlot.Plot.XLabel("Number of Silos");
siloComponentPlot.Plot.YLabel("Value");
siloComponentPlot.Plot.XAxis.ManualTickPositions(silos.Select(s => (double)s).ToArray(), silos.Select(s => s.ToString()).ToArray());
siloComponentPlot.Plot.Legend();
siloComponentPlot.Refresh();

var scalingFactorPlot = new FormsPlot { Dock = DockStyle.Fill };
tableLayoutPanel.Controls.Add(scalingFactorPlot, 0, 2);
for (int i = 0; i < silos.Length; i++)
{
    scalingFactorPlot.Plot.AddScatter(cycles.Select(c => (double)c).ToArray(), scalingFactorData.GetRow(i), label: $"{silos[i]} Silos", markerShape: MarkerShape.filledCircle);
}
scalingFactorPlot.Plot.Title("Combined Growth Rate (Adaptive Scaling)");
scalingFactorPlot.Plot.XLabel("Cycle");
scalingFactorPlot.Plot.YLabel("Value");
scalingFactorPlot.Plot.XAxis.ManualTickPositions(cycles.Select(c => (double)c).ToArray(), cycles.Select(c => c.ToString()).ToArray());
scalingFactorPlot.Plot.Legend();
scalingFactorPlot.Refresh();

Application.Run(form);

public static class ArrayExtensions
{
    public static T[] GetRow<T>(this T[,] array, int row)
    {
        var rowLength = array.GetLength(1);
        var rowArray = new T[rowLength];
        for (var i = 0; i < rowLength; i++)
        {
            rowArray[i] = array[row, i];
        }
        return rowArray;
    }
}
