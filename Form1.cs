using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using ZedGraph;
using System.Management;

namespace MotorCtl
{
    public partial class frMain : Form
    {
        private short[] reg;
        private bool[] sw ;
        private int m_nCmdNo;
        private int m_curInterface;
        private ushort m_nAddr;
        private ushort m_nLength;
        private bool IsConnect = false;
        private string strFmtByte ;
        private int nTx, nRx;
        private int nFailCounter;
        private int[]   lsNum;
        private string[] lsAlais;
        private int lsLen = 0;
        private ushort[] m_perAdr;
        private int bExtend = 0;
        private ZedGraphControl zd;
        private GraphPane gp;
        private Button btnWriteData;
        private Button btnCompress;
        private int[] m_Count;
        private bool bLoadFin = false ;
        private Size ScreenSize;    //屏幕尺寸
        private Point curProgPnt;   //程序位置
        private Size curProgSize;   //程序尺寸

        private int timeStart = 0;
        private int[] agpAdr = new int[10];
        private int[] agpStartAdr = new int[10];
        private int[] agpCmdNo = new int[10];
        private string[] agpName = new string[10];
        private int ptr_gp = 0;
        private Color[] agclr = new Color[10] { Color.Red, Color.Purple, Color.Pink,Color.Plum,Color.Peru,Color.PaleTurquoise,Color.PowderBlue,Color.RosyBrown,Color.SlateBlue,Color.SlateGray};


        public frMain()
        {
            InitializeComponent();
            m_perAdr = new ushort[4];
            m_Count = new int[4]{120, 120, 120, 120} ;
        }

        private void btClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btConnect_Click(object sender, EventArgs e)
        {
            WSMBT.Result resT;
            WSMBS.Result resS;
            try
            {
                m_curInterface = cbInterface.SelectedIndex;
                if (m_curInterface == 2)
                {
                    resT = tcpip.Connect(txtIPAdr.Text, (int)numPortNo.Value);
                    if (resT == WSMBT.Result.SUCCESS)
                        IsConnect = true;
                    else
                        MessageBox.Show("连接错误! (" + resT.ToString() + ")","Alarm");
                }
                else
                {
                    serial.BaudRate = int.Parse(cbBaudrate.SelectedItem.ToString());
                    serial.Parity = WSMBS.Parity.None;
                    serial.DataBits = 8;
                    serial.StopBits = 1;

                    string comx = cbSerial.SelectedItem.ToString();
                    int s1 = comx.IndexOf('(') ;
                    int s2 = comx.IndexOf(')') ;
                    comx = comx.Substring(s1 + 1, s2 - s1 - 1);
                    serial.PortName = comx;

                    resS = serial.Open();
                    if ( resS == WSMBS.Result.SUCCESS)
                        IsConnect = true;
                    else
                        MessageBox.Show("连接错误! (" + resS.ToString() + ")", "Alarm");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }

            if (IsConnect)
            {
                nTx = 0;
                nRx = 0;
                nFailCounter = 0;
                btConnect.Enabled = false;
                btnDisconnect.Enabled = true;
                btExec.Enabled = true;
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            picConn.Enabled = false;
            serial.Close();
            tcpip.Close();
            btnDisconnect.Enabled = false;
            btConnect.Enabled = true;
            btExec.Enabled = false;
            btnSupend.Enabled = false;
        }

        private void btExec_Click(object sender, EventArgs e)
        {
            WSMBS.Result sRes = WSMBS.Result.SUCCESS ;
            WSMBT.Result tRes = WSMBT.Result.SUCCESS ;

            m_nAddr = (ushort)numStartAdr.Value;
            m_nLength = (ushort)numCount.Value;
            m_nCmdNo = lstCmd.SelectedIndex;
            m_perAdr[m_nCmdNo] = m_nAddr;
            m_Count[m_nCmdNo] = m_nLength;

            reg = new short[m_nLength];
            sw = new bool[m_nLength];

            switch (m_nCmdNo)
            {
                case 0:
                    if (m_curInterface == 2)
                        tRes = tcpip.ReadDiscreteInputs((byte)numStation.Value, m_nAddr, m_nLength, sw);
                    else
                        sRes = serial.ReadDiscreteInputs((byte)numStation.Value, m_nAddr, m_nLength, sw);
                    break;
                case 1:
                    if (m_curInterface == 2)
                        tRes = tcpip.ReadCoils((byte)numStation.Value, m_nAddr, m_nLength, sw);
                    else
                        sRes = serial.ReadCoils((byte)numStation.Value, m_nAddr, m_nLength, sw);
                    break;
                case 2:
                    if (m_curInterface == 2)
                        tRes = tcpip.ReadHoldingRegisters((byte)numStation.Value, m_nAddr, m_nLength, reg);
                    else
                        sRes = serial.ReadHoldingRegisters((byte)numStation.Value, m_nAddr, m_nLength, reg);
                    break;
                case 3:
                    if (m_curInterface == 2)
                        tRes = tcpip.ReadInputRegisters((byte)numStation.Value, m_nAddr, m_nLength, reg);
                    else
                        sRes = serial.ReadInputRegisters((byte)numStation.Value, m_nAddr, m_nLength, reg);
                    break;
                default:
                    break;
            }

            byte[] txbuf = new byte[256] ;
            int n ;
            if (m_curInterface == 2)
                n = tcpip.GetTxBuffer(txbuf);
            else
                n = serial.GetTxBuffer(txbuf);

            if (n != 0)
            {
                string str = "发送帧:";
                for (int i = 0; i < n; i++)
                {
                    str += string.Format("{0:X2} ", txbuf[i]);
                    if ((i + 1) % 5 == 0)
                        str += "_ ";
                }
                labSend.Text = str;
            }

            if (sRes != WSMBS.Result.SUCCESS || tRes != WSMBT.Result.SUCCESS)
            {
                if ( m_curInterface == 2 )
                    MessageBox.Show("读控制站错误!  (" + tRes.ToString() + ")");
                else
                    MessageBox.Show("读控制站错误!  (" + sRes.ToString() + ")");
                picConn.Enabled = false;
            }
            else
            {
                int index = this.gridData.Rows.Add(); //得到当前控件的行数
                for (int i = 0; i < index + 1; i++)
                {
                    gridData.Rows.RemoveAt(0);
                }

                for (int i = 0; i < m_nLength; i++)
                {
                    int no = this.gridData.Rows.Add();
                    int nFind = -1 ;
                    for (int k = 0; k < lsLen; k++)
                    {
                        if (lsNum[k] == (m_nAddr + i))
                        {
                            nFind = k;
                            break;
                        }
                    }

                    if (ckDispAlais.Checked && nFind != -1)
                        this.gridData.Rows[no].Cells[0].Value = (m_nAddr + i).ToString() + " " + lsAlais[nFind];
                    else
                        this.gridData.Rows[no].Cells[0].Value = (m_nAddr + i).ToString() + " " + (m_nAddr + i).ToString("X") + "H ";
                    this.gridData.Rows[no].Cells[1].Value = (m_nCmdNo > 1) ? reg[i].ToString(strFmtByte) : sw[i].ToString();
                    if ((i/10) % 2 == 0)
                    {
                        this.gridData.Rows[no].Cells[0].Style.BackColor = Color.LightGray;
                        this.gridData.Rows[no].Cells[1].Style.BackColor = Color.LightBlue;
                    }
                }

                if (bExtend == 1)
                {
                    bool bLast = false ;
                    int address ;
                    for(int i=0 ; i < ptr_gp ; i++)
                    {
                        address = agpStartAdr[i] + agpAdr[i] ;
                        if (agpCmdNo[i] == m_nCmdNo && address >= m_nAddr && address < (m_nAddr + m_nLength))
                        {                            
                            agpAdr[i] = address - m_nAddr;
                            agpStartAdr[i] = m_nAddr;
                        }
                        else
                            bLast = true ;
                    }
                    if (bLast)
                    {
                        bExtend = 2;
                        gp.CurveList.Clear();
                        zd.Refresh();
                        ptr_gp = 0;
                        timeStart = 0;
                    }
                    
                }

                btnSupend.Enabled = true;
                timer1.Interval = (int)numCycle.Value;
                timer1.Enabled = true;
                picConn.Enabled = true;
            }
        }

        private void frMain_Load(object sender, EventArgs e)
        {
            //read inifile
            int k;
            IniFile ini = new IniFile(Environment.CurrentDirectory + "\\Setting.ini");

            m_perAdr[0] = (ushort)ini.ReadInt("Para", "DI Adr", 0);
            m_perAdr[1] = (ushort)ini.ReadInt("Para", "Coil Adr", 0);
            m_perAdr[2] = (ushort)ini.ReadInt("Para", "Holding Adr", 0);
            m_perAdr[3] = (ushort)ini.ReadInt("Para", "Input Adr", 0);

            lstCmd.SelectedIndex = ini.ReadInt("Para", "Command", 2);
            cbInterface.SelectedIndex = ini.ReadInt("Para","Interface",2) ;

            if (cbInterface.SelectedIndex != 2)
            {
                labText1.Text = "串口号:";
                labText2.Text = "波特率:";
                cbSerial.Visible = true;
                cbBaudrate.Visible = true;
                txtIPAdr.Visible = false;
                numPortNo.Visible = false;
            }
            else
            {
                labText1.Text = "IP地址:";
                labText2.Text = "端口号:";
                cbSerial.Visible = false;
                cbBaudrate.Visible = false;
                txtIPAdr.Visible = true;
                numPortNo.Visible = true;
            }

            m_curInterface = cbInterface.SelectedIndex;
            strFmtByte = ini.ReadString("Para", "Display", "X");
            txtIPAdr.Text = ini.ReadString("Para", "IP Address", "127.0.0.1");
            numPortNo.Value = ini.ReadInt("Para", "Port No", 502);

            for (int i = 0; i < 4; i++)
                m_Count[i] = ini.ReadInt("Para", "LengthN" + i.ToString(), 100);

            numStartAdr.Value = m_perAdr[lstCmd.SelectedIndex];         //ini.ReadInt("Para", "Start Address", 0);

            m_perAdr[lstCmd.SelectedIndex] = (ushort)numStartAdr.Value;
            numCount.Value = ini.ReadInt("Para", "Length", 10);
            m_Count[lstCmd.SelectedIndex] = (int)numCount.Value;
            numCycle.Value = ini.ReadInt("Para", "Cycle", 1000);
            numStation.Value = ini.ReadInt("Para", "Station", 1);
            k = ini.ReadInt("Para", "Display Alais", 0);
            if (k == 0)
                ckDispAlais.Checked = false;
            else
                ckDispAlais.Checked = true;


            if (strFmtByte == "D")
            {
                rdDec.Checked = true;
                rdHex.Checked = false;
            }

            cbInterface.SelectedIndexChanged += new EventHandler(cbInterface_SelectedIndexChanged);


            string[] astr = MulGetHardwareInfo(HardwareEnum.Win32_PnPEntity, "Name");
            foreach (string vPortName in astr)
            {
                cbSerial.Items.Add(vPortName);
            }

            k = ini.ReadInt("Para", "Serail", 0);
            if (cbSerial.Items.Count > k)
                cbSerial.SelectedIndex = k;

            cbBaudrate.Items.Add("9600");
            cbBaudrate.Items.Add("19200");
            cbBaudrate.Items.Add("38400");
            cbBaudrate.Items.Add("57600");
            cbBaudrate.Items.Add("115200");
            cbBaudrate.SelectedIndex = ini.ReadInt("Para","Baudrate",0) ;            

            serial.LicenseKey("2222222222222222222222222F3AA");
            tcpip.LicenseKey("2222222222222222222222222AAF2");


            //-------------------------------------------
            DirectoryInfo folder = new DirectoryInfo(".");
            foreach( FileInfo fil in folder.GetFiles("*.csv") )
            {
                cbCSVFile.Items.Add(fil.Name) ;
            }
            cbCSVFile.Text = ini.ReadString("Para", "CSV File", "Alais.csv");
            ReadCSVFile(cbCSVFile.Text);

            ScreenSize = new Size(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height);
            curProgPnt = this.Location;
            curProgSize = this.Size;
            bLoadFin = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            WSMBS.Result sRes = WSMBS.Result.SUCCESS;
            WSMBT.Result tRes = WSMBT.Result.SUCCESS;

            switch (m_nCmdNo)
            {
                case 0:
                    if (m_curInterface == 2)
                        tRes = tcpip.ReadDiscreteInputs((byte)numStation.Value, m_nAddr, m_nLength, sw);
                    else
                        sRes = serial.ReadDiscreteInputs((byte)numStation.Value, m_nAddr, m_nLength, sw);
                    break;
                case 1:
                    if (m_curInterface == 2)
                        tRes = tcpip.ReadCoils((byte)numStation.Value, m_nAddr, m_nLength, sw);
                    else
                        sRes = serial.ReadCoils((byte)numStation.Value, m_nAddr, m_nLength, sw);
                    break;
                case 2:
                    if (m_curInterface == 2)
                        tRes = tcpip.ReadHoldingRegisters((byte)numStation.Value, m_nAddr, m_nLength, reg);
                    else
                        sRes = serial.ReadHoldingRegisters((byte)numStation.Value, m_nAddr, m_nLength, reg);
                    break;
                case 3:
                    if (m_curInterface == 2)
                        tRes = tcpip.ReadInputRegisters((byte)numStation.Value, m_nAddr, m_nLength, reg);
                    else
                        sRes = serial.ReadInputRegisters((byte)numStation.Value, m_nAddr, m_nLength, reg);
                    break;
                default:
                    break;
            }

            nTx++;
            if (sRes != WSMBS.Result.SUCCESS || tRes != WSMBT.Result.SUCCESS)
            {          
                picConn.Enabled = false;
                nFailCounter++;
            }
            else
            {
                byte[] rxbuf = new byte[256];
                int n;
                if (m_curInterface == 2)
                    n = tcpip.GetRxBuffer(rxbuf);
                else
                    n = serial.GetRxBuffer(rxbuf);

                if (n != 0)
                {
                    string str = "";
                    for (int i = 0; i < n; i++)
                    {
                        str += string.Format("{0:X2} ", rxbuf[i]);
                        if (((i+1) % 5) == 0)
                            str += "_ ";
                    }
                    labRecv.Text = str;
                }

                for (int i = 0; i < m_nLength; i++)
                {
                    gridData.Rows[i].Cells[1].Value = (m_nCmdNo > 1) ? reg[i].ToString(strFmtByte) : sw[i].ToString();
                }

                if ( bExtend == 1)
                {
                    for (int i = 0; i < ptr_gp; i++)
                    {
                        CurveItem curve = gp.CurveList[i];
                        curve.AddPoint((double)(timeStart++), (double)reg[agpAdr[i]]);
                    }
                    zd.AxisChange();
                    zd.Refresh();
                }

                nRx++;
                nFailCounter = 0;
                picConn.Enabled = true ;
            }

            if (nFailCounter >= 3)
            {
                picConn.Enabled = false;
                timer1.Enabled = false;         
                if (m_curInterface == 2)
                    MessageBox.Show(tRes.ToString(), "Alarm", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                    MessageBox.Show(sRes.ToString(), "Alarm", MessageBoxButtons.OK, MessageBoxIcon.Error);
                nFailCounter = 0;
            }

            labTx.Text = nTx.ToString();
            labRx.Text = nRx.ToString();
        }

        private void gridData_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            WSMBS.Result res;

            try
            {
                short val;
                if (  strFmtByte == "D" )
                    val = short.Parse(gridData.Rows[e.RowIndex].Cells[1].Value.ToString(), System.Globalization.NumberStyles.Integer);
                else
                    val = short.Parse(gridData.Rows[e.RowIndex].Cells[1].Value.ToString(), System.Globalization.NumberStyles.HexNumber);

                short[] regs = new short[1];
                regs[0] = val;
                ushort adr = (ushort)(m_nAddr + e.RowIndex);
                 bool coil = (val > 0) ? true : false;
                if ( (val != reg[e.RowIndex] && m_nCmdNo == 2) || ( coil != sw[e.RowIndex] && m_nCmdNo == 0) )
                {
                    if (m_curInterface == 2)
                    {
                        switch (m_nCmdNo)
                        {
                            case 0:
                                tcpip.WriteSingleCoil((byte)numStation.Value, adr, coil);
                                break;
                            case 2:
                                tcpip.WriteSingleRegister((byte)numStation.Value, adr, (short)val);
                                break;
                        }
                    }
                    else
                    {
                        switch (m_nCmdNo)
                        {
                            case 0:
                                serial.WriteSingleCoil((byte)numStation.Value, adr, coil);
                                break;
                            case 2:
                                res = serial.WriteMultipleRegisters((byte)numStation.Value, adr, 1, regs);
                                if ( res != WSMBS.Result.SUCCESS)
                                    MessageBox.Show("写寄存器错误! (" + res.ToString() + ")");
                                //serial.WriteSingleRegister((byte)numStation.Value, adr, (short)val);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message,"Alarm");
            }
        }

        private void cbInterface_SelectedIndexChanged(object sender, EventArgs e)
        {

            if (cbInterface.SelectedIndex != 2)
            {
                labText1.Text = "串口号:";
                labText2.Text = "波特率:";
                cbSerial.Visible = true;
                cbBaudrate.Visible = true;
                txtIPAdr.Visible = false;
                numPortNo.Visible = false;
            }
            else
            {
                labText1.Text = "IP地址:";
                labText2.Text = "端口号:";
                cbSerial.Visible = false;
                cbBaudrate.Visible = false;
                txtIPAdr.Visible = true;
                numPortNo.Visible = true;
            }
            labText1.Refresh();
            labText2.Refresh();            
        }

        private void rdDec_CheckedChanged(object sender, EventArgs e)
        {
            strFmtByte = "D";
        }

        private void rdHex_CheckedChanged(object sender, EventArgs e)
        {
            strFmtByte = "X";
        }

        private void frMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            IniFile ini = new IniFile(Environment.CurrentDirectory + "\\Setting.ini");
            ini.WriteInt("Para", "Command", m_nCmdNo);
            ini.WriteInt("Para", "Interface", m_curInterface);
            ini.WriteString("Para", "Display", strFmtByte);
            ini.WriteString("Para", "IP Address", txtIPAdr.Text);
            ini.WriteInt("Para", "Port No", (int)numPortNo.Value);
            ini.WriteInt("Para", "Start Address", (int)numStartAdr.Value);
            for(int i=0 ; i < 4 ; i++)
                ini.WriteInt("Para", "LengthN" + i.ToString(), (int)m_Count[i]);
            ini.WriteInt("Para", "Length", (int)numCount.Value);
            ini.WriteInt("Para", "Cycle", (int)numCycle.Value);
            ini.WriteInt("Para", "Serail", cbSerial.SelectedIndex);
            ini.WriteInt("Para", "Baudrate", cbBaudrate.SelectedIndex);
            ini.WriteInt("Para", "Station", (int)numStation.Value);
            ini.WriteString("Para", "CSV File", cbCSVFile.Text);

            ini.WriteInt("Para", "DI Adr", (int)m_perAdr[0]);
            ini.WriteInt("Para", "Coil Adr", (int)m_perAdr[1]);
            ini.WriteInt("Para", "Holding Adr", (int)m_perAdr[2]);
            ini.WriteInt("Para", "Input Adr", (int)m_perAdr[3]);

            if (ckDispAlais.Checked )
                ini.WriteInt("Para", "Display Alais", 1);
            else
                 ini.WriteInt("Para", "Display Alais", 0);

            serial.Close();
            tcpip.Close();
        }

        private void ReadCSVFile(string strName)
        {
            string line = "";
            string[] lsStr;
            try
            {
                StreamReader fil = new StreamReader(strName, System.Text.Encoding.Default);

                lsNum = new int[1024];
                lsAlais = new string[1024];
                lsLen = 0;
                while (line != null)
                {
                    line = fil.ReadLine();
                    if (line != null && line.Length > 0)
                    {
                        lsStr = line.Split(',');
                        if (lsStr.Length >= 2)
                        {
                            lsNum[lsLen] = int.Parse(lsStr[0]);
                            lsAlais[lsLen] = lsStr[1];
                            lsLen++;
                        }
                    }
                }

                fil.Close();
            }
            catch (Exception ex)
            {               
                MessageBox.Show(ex.ToString(), "Alarm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void lstCmd_SelectedIndexChanged(object sender, EventArgs e)
        {
            numStartAdr.Value = m_perAdr[lstCmd.SelectedIndex];
            numCount.Value = m_Count[lstCmd.SelectedIndex];

        }

        private void gridData_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (!picConn.Enabled)
                return;

            if (bExtend == 0)
            {
                this.Size = new Size(ScreenSize.Width, curProgSize.Height);
                this.Location = new Point(0, curProgPnt.Y);

                zd = new ZedGraphControl();
                zd.Location = new Point(670, 67);
                zd.Size = new Size(ScreenSize.Width - 700, curProgSize.Height - 117);

                GraphPane pan = zd.GraphPane;
 
                pan.XAxis.Title = "Counter";
                pan.XAxis.IsShowGrid = true;
                pan.XAxis.TitleFontSpec.Size = 5;
                pan.XAxis.ScaleFontSpec.Size = 5;
                pan.XAxis.Color = Color.WhiteSmoke;
                pan.XAxis.GridColor = Color.WhiteSmoke;
                
                pan.YAxis.IsShowGrid = true;
                pan.YAxis.TitleFontSpec.Size = 5;
                pan.YAxis.ScaleFontSpec.Size = 5;
                pan.YAxis.IsShowTitle = false;
                pan.YAxis.Color = Color.WhiteSmoke;
                pan.YAxis.GridColor = Color.WhiteSmoke;

                // Fill the axis area with a gradient                
                pan.AxisFill = new Fill(Color.Black, Color.Black, 90F);
                // Fill the pane area with a solid color
                pan.PaneFill = new Fill(Color.White);
              

                gp = zd.GraphPane;
                gp.IsShowTitle = false;
                gp.Legend.FontSpec.Size = 5;

                gp.AxisFill.Color = Color.White;
                this.Controls.Add(zd);

                btClose.Location = new Point(ScreenSize.Width - 120, 22);

                btnCompress = new Button();
                btnCompress.Text = "<<";
                btnCompress.Size = new Size(100, 30);
                btnCompress.Location = new Point(ScreenSize.Width - 320, 22);
                btnCompress.Click += btnCompress_Click;
                this.Controls.Add(btnCompress);

                btnWriteData = new Button();
                btnWriteData.Text = "保存[&S]";
                btnWriteData.Size = new Size(100,30);
                btnWriteData.Location = new Point(ScreenSize.Width - 220, 22);
                btnWriteData.Click += btnWriteData_Click;
                this.Controls.Add(btnWriteData);

            }

            if (ptr_gp > 9)
                return ;

            for(int i=0 ; i < ptr_gp ;i++)
                if ( e.RowIndex == agpAdr[i])
                    return ;

            PointPairList ppl = new PointPairList() ;
            agpName[ptr_gp] = gridData.Rows[e.RowIndex].Cells[0].Value.ToString();
            agpAdr[ptr_gp] = e.RowIndex;
            agpStartAdr[ptr_gp] = m_nAddr;
            agpCmdNo[ptr_gp] = m_nCmdNo;
            gp.AddCurve(agpName[ptr_gp], ppl,agclr[ptr_gp], SymbolType.None);
            zd.Refresh();

            bExtend = 1;
            ptr_gp++;
       }

        private void lstCmd_DoubleClick(object sender, EventArgs e)
        {
            btExec_Click(sender, e);
        }

        private void cbCSVFile_SelectedIndexChanged(object sender, EventArgs e)
        {
            ReadCSVFile(cbCSVFile.Text);
        }

        private void btnWriteData_Click(object sender, EventArgs e)
        {
            string txtLine ;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Text file(*.txt)|*.txt|All file|*.*";      //设置文件类型
            sfd.FileName = agpName[0] ;//设置默认文件名
            sfd.DefaultExt = "txt";//设置默认格式（可以不设）
            sfd.AddExtension = true;//设置自动在文件名中添加扩展名
            if (sfd.ShowDialog()==DialogResult.OK)
            {
                try
                {
                    StreamWriter fil = new StreamWriter(sfd.FileName);
                    for (int k = 0; k < ptr_gp; k++)
                    {
                        CurveItem curve = gp.CurveList[k];
                        fil.WriteLine("Cycle,Value----------------" + agpName[k]);
                        for (int i = 0; i < curve.Points.Count; i++)
                        {
                            txtLine = string.Format("{0},{1}", curve.Points[i].X, curve.Points[i].Y);
                            fil.WriteLine(txtLine);
                        }
                    }
                    fil.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("A error happend when saving data!", "Alarm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
             }
        }

        private void btnCompress_Click(object sender, EventArgs e)
        {
            Controls.Remove(btnCompress);
            Controls.Remove(btnWriteData);
            Controls.Remove(zd);
            btClose.Location = new Point(557, 22) ;
            this.Location = curProgPnt;
            this.Size = curProgSize;
            bExtend = 0;
            ptr_gp = 0;
            timeStart = 0;
        }


        private void numCycle_ValueChanged(object sender, EventArgs e)
        {
            timer1.Interval = (int)numCycle.Value;
        }

        public enum HardwareEnum
        {
            // 硬件
            Win32_Processor, // CPU 处理器
            Win32_PhysicalMemory, // 物理内存条
            Win32_Keyboard, // 键盘
            Win32_PointingDevice, // 点输入设备，包括鼠标。
            Win32_FloppyDrive, // 软盘驱动器
            Win32_DiskDrive, // 硬盘驱动器
            Win32_CDROMDrive, // 光盘驱动器
            Win32_BaseBoard, // 主板
            Win32_BIOS, // BIOS 芯片
            Win32_ParallelPort, // 并口
            Win32_SerialPort, // 串口
            Win32_SerialPortConfiguration, // 串口配置
            Win32_SoundDevice, // 多媒体设置，一般指声卡。
            Win32_SystemSlot, // 主板插槽 (ISA & PCI & AGP)
            Win32_USBController, // USB 控制器
            Win32_NetworkAdapter, // 网络适配器
            Win32_NetworkAdapterConfiguration, // 网络适配器设置
            Win32_Printer, // 打印机
            Win32_PrinterConfiguration, // 打印机设置
            Win32_PrintJob, // 打印机任务
            Win32_TCPIPPrinterPort, // 打印机端口
            Win32_POTSModem, // MODEM
            Win32_POTSModemToSerialPort, // MODEM 端口
            Win32_DesktopMonitor, // 显示器
            Win32_DisplayConfiguration, // 显卡
            Win32_DisplayControllerConfiguration, // 显卡设置
            Win32_VideoController, // 显卡细节。
            Win32_VideoSettings, // 显卡支持的显示模式。

            // 操作系统
            Win32_TimeZone, // 时区
            Win32_SystemDriver, // 驱动程序
            Win32_DiskPartition, // 磁盘分区
            Win32_LogicalDisk, // 逻辑磁盘
            Win32_LogicalDiskToPartition, // 逻辑磁盘所在分区及始末位置。
            Win32_LogicalMemoryConfiguration, // 逻辑内存配置
            Win32_PageFile, // 系统页文件信息
            Win32_PageFileSetting, // 页文件设置
            Win32_BootConfiguration, // 系统启动配置
            Win32_ComputerSystem, // 计算机信息简要
            Win32_OperatingSystem, // 操作系统信息
            Win32_StartupCommand, // 系统自动启动程序
            Win32_Service, // 系统安装的服务
            Win32_Group, // 系统管理组
            Win32_GroupUser, // 系统组帐号
            Win32_UserAccount, // 用户帐号
            Win32_Process, // 系统进程
            Win32_Thread, // 系统线程
            Win32_Share, // 共享
            Win32_NetworkClient, // 已安装的网络客户端
            Win32_NetworkProtocol, // 已安装的网络协议
            Win32_PnPEntity,//all device
        }

        public static string[] MulGetHardwareInfo(HardwareEnum hardType, string propKey)
        {
            List<string> strs = new List<string>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from  " + hardType))
                {
                    var hardInfos = searcher.Get();
                    foreach (var hardInfo in hardInfos)
                    {
                        if (hardInfo.Properties[propKey].Value != null )
                        {
                            string str= hardInfo.Properties[propKey].Value.ToString() ;
                            if ( str.Contains("(COM") )
                                strs.Add(str) ;
                        }

                    }
                    searcher.Dispose();
                }
                return strs.ToArray();
            }
            catch
            {
                return null;
            }
            finally
            { strs = null; }
        }

        private void btnSupend_Click(object sender, EventArgs e)
        {
             btnSupend.Enabled = false;
             timer1.Enabled = false;
        }

        private void labText1_Click(object sender, EventArgs e)
        {
            string[] astr = MulGetHardwareInfo(HardwareEnum.Win32_PnPEntity, "Name");
            for (int i = cbSerial.Items.Count - 1; i > 0; i--)
            {
                if (!astr.Contains(cbSerial.Items[i].ToString()))
                    cbSerial.Items.RemoveAt(i);
            }

            foreach (string vPortName in astr)
            {
                if (!cbSerial.Items.Contains(vPortName))
                    cbSerial.Items.Add(vPortName);
            }
            cbSerial.Refresh();
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            if (timer1.Enabled)
                return;
            labText1_Click(sender, e);
        }
    }
}
