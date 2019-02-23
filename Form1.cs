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
        private int timeStart = 0;
        private int gpAdr;
        private Button btnWriteData;
        private Button btnCompress;
        private string gpName;
        private int[] m_Count;

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
                        MessageBox.Show("Connect failure! (" + resT.ToString() + ")","Alarm");
                }
                else
                {
                    serial.BaudRate = int.Parse(cbBaudrate.SelectedItem.ToString());
                    serial.Parity = WSMBS.Parity.None;
                    serial.DataBits = 8;
                    serial.StopBits = 1;
                    serial.PortName = cbSerial.SelectedItem.ToString();

                    resS = serial.Open();
                    if ( resS == WSMBS.Result.SUCCESS)
                        IsConnect = true;
                    else
                        MessageBox.Show("Connect failure! (" + resS.ToString() + ")", "Alarm");
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
                string str = "Send:";
                for (int i = 0; i < n; i++)
                {
                    str += string.Format("{0:X2} ", txbuf[i]);
                    if ((i + 1) % 5 == 0)
                        str += "__";
                }
                labSend.Text = str;
            }

            if (sRes != WSMBS.Result.SUCCESS || tRes != WSMBT.Result.SUCCESS)
            {
                if ( m_curInterface == 2 )
                    MessageBox.Show("Read failure!  (" + tRes.ToString() + ")");
                else
                    MessageBox.Show("Read failure!  (" + sRes.ToString() + ")");
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
                }

                if (bExtend == 1)
                {
                    bExtend = 2;
                }

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
            m_curInterface = cbInterface.SelectedIndex;
            strFmtByte = ini.ReadString("Para", "Display", "X");
            txtIPAdr.Text = ini.ReadString("Para", "IP Address", "127.0.0.1");
            numPortNo.Value = ini.ReadInt("Para", "Port No", 502);

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

            if (m_curInterface != 2)
            {
                labText1.Text = "Serial";
                labText2.Text = "Baud Rate";
                cbSerial.Visible = true;
                cbBaudrate.Visible = true;
                txtIPAdr.Visible = false;
                numPortNo.Visible = false;
                labText1.Refresh();
                labText2.Refresh();
                label5.Enabled = true;
            }
            else
            {
                label5.Enabled = false;
            }

            if (strFmtByte == "D")
            {
                rdDec.Checked = true;
                rdHex.Checked = false;
            }

            cbInterface.SelectedIndexChanged += new EventHandler(cbInterface_SelectedIndexChanged);

            foreach (string vPortName in SerialPort.GetPortNames())
            {
                cbSerial.Items.Add(vPortName);
            }

            k = ini.ReadInt("Para", "Serail", 0);
            if (cbSerial.Items.Count > k)
                cbSerial.SelectedIndex = k;

            cbBaudrate.Items.Add("9600");
            cbBaudrate.Items.Add("19200");
            cbBaudrate.Items.Add("38400");
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
                            str += "__";
                    }
                    labRecv.Text = str;
                }

                for (int i = 0; i < m_nLength; i++)
                {
                    gridData.Rows[i].Cells[1].Value = (m_nCmdNo > 1) ? reg[i].ToString(strFmtByte) : sw[i].ToString();
                }

                if ( bExtend == 1)
                {
                    CurveItem curve = gp.CurveList[0];
                    curve.AddPoint((double)(timeStart++), (double)reg[gpAdr]);
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
                                    MessageBox.Show("WriteMultipleRegisters error! (" + res.ToString() + ")");
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
                labText1.Text = "Serial";
                labText2.Text = "Baud Rate";
                cbSerial.Visible = true;
                cbBaudrate.Visible = true;
                txtIPAdr.Visible = false;
                numPortNo.Visible = false;
                label5.Enabled = true;
                numStation.Enabled = true;
            }
            else
            {
                labText1.Text = "IP Address";
                labText2.Text = "Port";
                cbSerial.Visible = false;
                cbBaudrate.Visible = false;
                txtIPAdr.Visible = true;
                numPortNo.Visible = true;
                label5.Enabled = false;
                numStation.Enabled = false;
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
                Size oldsize = this.Size;
                this.Size = new Size(1200, oldsize.Height);
                zd = new ZedGraphControl();
                zd.Location = new Point(670, 67);
                zd.Size = new Size(500, oldsize.Height - 117);

                gp = zd.GraphPane;
                gp.FontSpec.Size = 16;

                gp.XAxis.ScaleFontSpec.Size = 10;
                gp.XAxis.IsShowGrid = true;
                gp.XAxis.Title = "Time/cycle";
                gp.XAxis.Color = Color.Salmon;

                gp.YAxis.IsShowGrid = true;
                gp.YAxis.ScaleFontSpec.Size = 10;
                gp.YAxis.Title = "Value";
                gp.YAxis.Color = Color.Salmon;

                gp.Title = "Data trend diagram";
                //gp.PaneFill.Color = Color.DarkBlue;
                gp.AxisFill.Color = Color.DarkBlue;
                this.Controls.Add(zd);

                btClose.Location = new Point(1070, 22);

                btnCompress = new Button();
                btnCompress.Text = "<<";
                btnCompress.Size = new Size(100, 30);
                btnCompress.Location = new Point(810, 22);
                btnCompress.Click += btnCompress_Click;
                this.Controls.Add(btnCompress);

                btnWriteData = new Button();
                btnWriteData.Text = "Save";
                btnWriteData.Size = new Size(100,30);
                btnWriteData.Location = new Point(940,22);
                btnWriteData.Click += btnWriteData_Click;
                this.Controls.Add(btnWriteData);

            }
            else
            {
                gp.CurveList.Clear();
            }

            PointPairList ppl = new PointPairList() ;
            gpName = gridData.Rows[e.RowIndex].Cells[0].Value.ToString();
            gp.AddCurve(gpName, ppl, Color.Red, SymbolType.None);
            timeStart = 0;
            gpAdr = e.RowIndex; 
            zd.Refresh();
            bExtend = 1;
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
            sfd.FileName = gpName;//设置默认文件名
            sfd.DefaultExt = "txt";//设置默认格式（可以不设）
            sfd.AddExtension = true;//设置自动在文件名中添加扩展名
            if (sfd.ShowDialog()==DialogResult.OK)
            {
                try
                {
                    StreamWriter fil = new StreamWriter(sfd.FileName);
                    CurveItem curve = gp.CurveList[0];
                    fil.WriteLine("Cycle,Value");
                    for (int i = 0; i < curve.Points.Count; i++)
                    {
                        txtLine = string.Format("{0},{1}", curve.Points[i].X, curve.Points[i].Y);
                        fil.WriteLine(txtLine);
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
            btClose.Location = new Point(546, 22);
            this.Size = new Size(672, 860);
            bExtend = 0;
        }


        private void numCycle_ValueChanged(object sender, EventArgs e)
        {
            timer1.Interval = (int)numCycle.Value;
        }

        private void labRecv_Click(object sender, EventArgs e)
        {

        }
    }
}
