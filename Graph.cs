using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ZedGraph;


namespace MotorCtl
{
    public partial class Graph : Form
    {
        public double[] data;
        public double[] x;
        private ZedGraph.GraphPane gp;

        public Graph()
        {
            InitializeComponent();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Graph_Load(object sender, EventArgs e)
        {
            gp = zdGraph.GraphPane;
            gp.YAxis.IsShowGrid = true;
            gp.YAxis.Title = "Voltage";
            gp.YAxis.ScaleFontSpec.Size = 8;
            gp.YAxis.TitleFontSpec.Size = 8;
            gp.YAxis.Min = 0;
            gp.YAxis.Max = 4096;

            gp.XAxis.IsShowGrid = true;
            gp.XAxis.Max = data.Length ;
            gp.XAxis.Min = 0;
            gp.XAxis.Title = "Time";
            gp.XAxis.ScaleFontSpec.Size = 8;
            gp.XAxis.TitleFontSpec.Size = 8;

            zdGraph.IsShowPointValues = true;
            gp.Title = "Voltage Curve";
            zdGraph.AxisChange();

            gp.AddCurve("Data", x, data, Color.Blue, SymbolType.None);

            zdGraph.AxisChange();
            zdGraph.Refresh();
        }
    }
}
