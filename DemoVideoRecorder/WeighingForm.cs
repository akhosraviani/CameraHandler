﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Text;
using System.IO.Ports;
using System.Data.SqlClient;
using System.Globalization;
using System.Configuration;
using System.Threading;
using System.Net;
using System.IO;
using AshaWeighing.Properties;

namespace _03_Onvif_Network_Video_Recorder
{
    public partial class WeighingForm : Form
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont,
            IntPtr pdv, [System.Runtime.InteropServices.In] ref uint pcFonts);

        private PrivateFontCollection fonts = new PrivateFontCollection();

        private Font myFont, myFontBig;
        private List<string> _connectionStringList;
        private List<Label> _indicatorList;
        private SerialPort _serialPort;
        private SqlConnection _dbConnection;
        private string _shipmentState = "Shp_FirstWeighing";
        private DataTable _shipmentTable;
        private bool _negativeWeight = false;
        private Bitmap loadedBitmap;
        private Dictionary<string, string> configs;
        public WeighingForm()
        {
            InitializeComponent();
            this.Load += WeighingForm_Load;
            this.FormClosing += WeighingForm_FormClosing;

            InitializeConfigurations();
            InitializeFontAndCamera();
        }

        private void requestFrame(int requestNumber)
        {
            string cameraUrl = Globals.CameraAddress[requestNumber];
            try
            {
                var request = System.Net.HttpWebRequest.Create(cameraUrl);
                request.Credentials = new NetworkCredential(Globals.CameraUsername[requestNumber], Globals.CameraPassword[requestNumber]);
                request.Proxy = null;
                request.BeginGetResponse(new AsyncCallback(finishRequestFrame), request);
            }
            catch(UriFormatException exp)
            {
                MessageBox.Show("تنظیمات دوربین " + (requestNumber+1) + " صحیح نمیباشد. لطفا با مدیر سیستم تماس بگیرید");
            }
        }

        void finishRequestFrame(IAsyncResult result)
        {
            try
            {
                HttpWebResponse response = (result.AsyncState as HttpWebRequest).EndGetResponse(result) as HttpWebResponse;
                Stream responseStream = response.GetResponseStream();

                using (Bitmap frame = new Bitmap(responseStream))
                {
                    if (frame != null)
                    {
                        if (response.ResponseUri.OriginalString == Globals.CameraAddress[0])
                        {
                            imgCamera1.Image = (Bitmap)frame.Clone();
                            imgCamera1.Image.Tag = "Camera1-" + DateTime.Now.Year + "y-" + DateTime.Now.Month + "m-" + DateTime.Now.Day + "d-" +
                                DateTime.Now.Hour + "h-" + DateTime.Now.Minute + "m-" + DateTime.Now.Second + "s.jpg";
                            InvokeGuiThread(() =>
                            {
                                _indicatorList[0].Text = "فعال";
                                _indicatorList[0].ForeColor = Color.Green;
                            });
                        }
                        else if (response.ResponseUri.OriginalString == Globals.CameraAddress[1])
                        {
                            imgCamera2.Image = (Bitmap)frame.Clone();
                            imgCamera2.Image.Tag = "Camera2-" + DateTime.Now.Year + "y-" + DateTime.Now.Month + "m-" + DateTime.Now.Day + "d-" +
                                DateTime.Now.Hour + "h-" + DateTime.Now.Minute + "m-" + DateTime.Now.Second + "s.jpg";
                            InvokeGuiThread(() =>
                            {
                                _indicatorList[1].Text = "فعال";
                                _indicatorList[1].ForeColor = Color.Green;
                            });
                        }
                        else if (response.ResponseUri.OriginalString == Globals.CameraAddress[2])
                        {
                            imgCamera3.Image = (Bitmap)frame.Clone();
                            imgCamera3.Image.Tag = "Camera3-" + DateTime.Now.Year + "y-" + DateTime.Now.Month + "m-" + DateTime.Now.Day + "d-" +
                                DateTime.Now.Hour + "h-" + DateTime.Now.Minute + "m-" + DateTime.Now.Second + "s.jpg";
                            InvokeGuiThread(() =>
                            {
                                _indicatorList[2].Text = "فعال";
                                _indicatorList[2].ForeColor = Color.Green;
                            });
                        }
                        else if (response.ResponseUri.OriginalString == Globals.CameraAddress[3])
                        {
                            imgCamera4.Image = (Bitmap)frame.Clone();
                            imgCamera4.Image.Tag = "Camera4-" + DateTime.Now.Year + "y-" + DateTime.Now.Month + "m-" + DateTime.Now.Day + "d-" +
                                DateTime.Now.Hour + "h-" + DateTime.Now.Minute + "m-" + DateTime.Now.Second + "s.jpg";
                            InvokeGuiThread(() =>
                            {
                                _indicatorList[3].Text = "فعال";
                                _indicatorList[3].ForeColor = Color.Green;
                            });
                        }
                    }
                }
            }
            catch(WebException exp)
            {

            }
        }
        void WeighingForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            DisconnectWeighingMachine();
            DisconnectDatabase();
        }

        private void InitializeConfigurations()
        {
            var connection =
                System.Configuration.ConfigurationManager.ConnectionStrings["AshaDbContext"].ConnectionString;
            if (_dbConnection == null)
                _dbConnection = new SqlConnection(connection);
            if (_dbConnection.State != ConnectionState.Open)
            {
                try
                {
                    _dbConnection.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT Code, Title FROM WMLog_Configuration "
                                                            , _dbConnection))
                    {
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable conf = new DataTable();
                        da.Fill(conf);
                        configs = new Dictionary<string, string>();
                        ToolStripMenuItem[] items = new ToolStripMenuItem[conf.Rows.Count];

                        for (int i = 0; i < conf.Rows.Count; i++)
                        {
                            configs.Add(conf.Rows[i].Field<string>("Code"), conf.Rows[i].Field<string>("Title"));

                            items[i] = new ToolStripMenuItem();
                            items[i].Name = "dynamicItem" + i.ToString();
                            items[i].Tag = conf.Rows[i].Field<string>("Code");
                            items[i].Text = conf.Rows[i].Field<string>("Title");
                            items[i].Click += new EventHandler(contextMenu_Click);
                        }

                        ctmConfig.DropDownItems.AddRange(items);
                    }

                }
                catch (Exception)
                {

                }
                Globals.GetConfigurationDetails(Settings.Default.SelectedConfiguration);
            }
        }

        private void InitializeFontAndCamera()
        {
            cameraIndicator1.Parent = groupBox8;
            cameraIndicator1.Location = new Point(6, 1);
            imgCamera1.MouseDoubleClick += new MouseEventHandler(this.PicBox_DoubleClick);

            cameraIndicator2.Parent = groupBox7;
            cameraIndicator2.Location = new Point(6, 1);
            imgCamera2.MouseDoubleClick += new MouseEventHandler(this.PicBox_DoubleClick);

            cameraIndicator3.Parent = groupBox6;
            cameraIndicator3.Location = new Point(6, 1);
            imgCamera3.MouseDoubleClick += new MouseEventHandler(this.PicBox_DoubleClick);

            cameraIndicator4.Parent = groupBox5;
            cameraIndicator4.Location = new Point(6, 1);
            imgCamera4.MouseDoubleClick += new MouseEventHandler(this.PicBox_DoubleClick);

            byte[] fontData = AshaWeighing.Properties.Resources.IRANSans_FaNum_;
            IntPtr fontPtr = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(fontData.Length);
            System.Runtime.InteropServices.Marshal.Copy(fontData, 0, fontPtr, fontData.Length);
            uint dummy = 0;
            fonts.AddMemoryFont(fontPtr, AshaWeighing.Properties.Resources.IRANSans_FaNum_.Length);
            AddFontMemResourceEx(fontPtr, (uint)AshaWeighing.Properties.Resources.IRANSans_FaNum_.Length, IntPtr.Zero, ref dummy);
            System.Runtime.InteropServices.Marshal.FreeCoTaskMem(fontPtr);

            myFont = new Font(fonts.Families[0], 8.5F);
            myFontBig = new Font(fonts.Families[0], 14F);

        }

        private void PicBox_DoubleClick(object sender, MouseEventArgs e)
        {
            CameraViewer viewer = new CameraViewer();
            viewer.PreviewImage = ((PictureBox)sender).Image;
            viewer.Show();
        }

        void WeighingForm_Load(object sender, EventArgs e)
        {
            this.Font = myFont;
            lblDiscrepency.Font = myFontBig;
            lblNetWeightLoad.Font = myFontBig;
            lblLoadedBranches.Font = myFontBig;

            foreach (ToolStripMenuItem item in ctmConfig.DropDownItems)
            {
                if (((string)item.Tag) != Settings.Default.SelectedConfiguration)
                    item.Checked = false;
                else
                    item.Checked = true;
            }

            _indicatorList = new List<Label>();
            _shipmentTable = new DataTable();
            CreateIndicators();
            CreateSerialPort();
        }

        private void CreateSerialPort()
        {
            _serialPort = new SerialPort();
            _serialPort.PortName = Globals.WeighingMachineSerialPort;  
            _serialPort.BaudRate = Globals.SerialPorBaudRate;
            _serialPort.DataBits = Globals.SerialPortDataBits;
            _serialPort.Parity = Globals.SerialPortParity;
            _serialPort.Handshake = Globals.SerialPortHandshake;
            _serialPort.StopBits = Globals.SerialPortStopBits;
            _serialPort.RtsEnable = true;
            _serialPort.Encoding = Encoding.ASCII;
            _serialPort.DataReceived +=
                new SerialDataReceivedEventHandler(_serialPort_DataReceived);        
        }

        public void ShowNegativeWeightMessageBox()
        {
          var thread = new Thread(
            () =>
            {
              if(MessageBox.Show("وزن باسکول منفی می باشد! لطفا دستگاه را بررسی نمایید") == System.Windows.Forms.DialogResult.OK);
              {
                  _negativeWeight = false;
                  btnSaveData.Enabled = true;
              }
            });
          thread.Start();
        }
        private void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            byte[] v = new byte[8];
            int intResult = 0;
            int tryCount = 0;

            if (_serialPort.BytesToRead <= 0)
            {
                
            }
            else
            {
                while (_serialPort.BytesToRead > 0 && tryCount < 10)
                {
                    var output = _serialPort.Read(v, 0, 7);
                    
                    if (output == 7)
                    {
                        try
                        {
                            StringBuilder hex = new StringBuilder(2);
                            hex.AppendFormat("{0:x2}", v[0]);

                            if (hex.ToString().ToLower().Equals("bb"))
                            {
                                hex.Clear();
                                hex.AppendFormat("{0:x2}", v[1]);

                                if (hex.ToString().ToLower().Equals("e0"))
                                {
                                    intResult = -10 * Int32.Parse(System.Text.Encoding.ASCII.GetString(v, 2, 6));
                                    tryCount = 10;
                                }
                                else
                                {
                                    intResult = Int32.Parse(System.Text.Encoding.ASCII.GetString(v, 1, 6));
                                    tryCount = 10;
                                }
                            }
                            
                            
                        }
                        catch (FormatException)
                        {
                            tryCount++;
                        }
                    }
                    else
                        tryCount++;
                }
            }

            try
            {

                if (_shipmentState == "Shp_FirstWeighing")
                {
                    if (intResult < -10)
                    {

                        txtWeight1.Text = intResult.ToString();
                        txtDate1.Text = GetDate();
                        txtTime1.Text = GetTime();
                        txtMachine1.Text = GetMachine();

                        if (txtWeight1.BackColor != Color.Red)
                        {
                            txtWeight1.BackColor = Color.Red;
                            txtDate1.BackColor = Color.Red;
                            txtTime1.BackColor = Color.Red;
                            txtMachine1.BackColor = Color.Red;
                        }

                        if(_negativeWeight == false)
                        {
                            _negativeWeight = true;
                            btnSaveData.Enabled = false;
                            ShowNegativeWeightMessageBox();
                        }

                    }
                    else if (intResult > 0)
                    {
                        txtWeight1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(248)))), ((int)(((byte)(225)))));
                        txtDate1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(248)))), ((int)(((byte)(225)))));
                        txtTime1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(248)))), ((int)(((byte)(225)))));
                        txtMachine1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(248)))), ((int)(((byte)(225)))));

                        txtWeight1.Text = intResult.ToString();
                        txtDate1.Text = GetDate();
                        txtTime1.Text = GetTime();
                        txtMachine1.Text = GetMachine();
                    }


                    if (txtWeight2.Text != "")
                    {
                        txtWeight2.Text = "";
                        txtDate2.Text = "";
                        txtTime2.Text = "";
                        txtMachine2.Text = "";
                    }
                }
                else if (_shipmentState == "Shp_SecondWeighing" || _shipmentState == "Shp_Loading")
                {

                    if (intResult < -10)
                    {
                        if (txtWeight2.BackColor != Color.Red)
                        {
                            txtWeight2.BackColor = Color.Red;
                            txtDate2.BackColor = Color.Red;
                            txtTime2.BackColor = Color.Red;
                            txtMachine2.BackColor = Color.Red;
                        }

                        txtWeight2.Text = intResult.ToString();
                        txtDate2.Text = GetDate();
                        txtTime2.Text = GetTime();
                        txtMachine2.Text = GetMachine();

                        if (_negativeWeight == false)
                        {
                            _negativeWeight = true;
                            btnSaveData.Enabled = false;
                            ShowNegativeWeightMessageBox();
                        }
                    }
                    else if (intResult > 0)
                    {
                        if (txtWeight2.BackColor != System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(248)))), ((int)(((byte)(225))))))
                        {
                            txtWeight2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(248)))), ((int)(((byte)(225)))));
                            txtDate2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(248)))), ((int)(((byte)(225)))));
                            txtTime2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(248)))), ((int)(((byte)(225)))));
                            txtMachine2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(248)))), ((int)(((byte)(225)))));
                        }

                        txtWeight2.Text = intResult.ToString();
                        txtDate2.Text = GetDate();
                        txtTime2.Text = GetTime();
                        txtMachine2.Text = GetMachine();

                        double weight1, weight2;
                        if (double.TryParse(txtWeight1.Text, out weight1) && double.TryParse(txtWeight2.Text, out weight2))
                        {
                            lblNetWeightLoad.Text = string.Format("{0:0.###}", Math.Abs(weight2 - weight1));

                            double netWeight, estimatedWeight;
                            if (double.TryParse(lblNetWeightLoad.Text, out netWeight) && double.TryParse(_shipmentTable.Rows[0].ItemArray[21].ToString(), out estimatedWeight))
                            {
                                lblDiscrepency.Text = string.Format("{0:0.###}", Math.Abs((estimatedWeight - netWeight) / estimatedWeight * 100));
                            }
                        }
                    }


                    if (_shipmentTable.Rows.Count > 0)
                    {
                        txtWeight1.Text = string.Format("{0:0.###}", _shipmentTable.Rows[0].ItemArray[10]);
                        txtDate1.Text = _shipmentTable.Rows[0].ItemArray[15].ToString();
                        txtTime1.Text = _shipmentTable.Rows[0].ItemArray[17].ToString();
                        txtMachine1.Text = _shipmentTable.Rows[0].ItemArray[18].ToString();
                    }
                }
            }
            catch (Exception)
            { }
        }

        private string GetMachine()
        {
            return Globals.WeighingMachineCode;
        }

        private void CreateIndicators()
        {
            _indicatorList.Clear();
            _indicatorList.Add(cameraIndicator1);
            _indicatorList.Add(cameraIndicator2);
            _indicatorList.Add(cameraIndicator3);
            _indicatorList.Add(cameraIndicator4);
        }
        private void InvokeGuiThread(Action action)
        {
            if (IsHandleCreated)
            {
                BeginInvoke(action);
            }
        }

        private string GetTime()
        {
            return string.Format("{0}:{1}:{2}", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
        }

        private string GetDate()
        {
            string GregorianDate = DateTime.Now.ToString();
            DateTime d = DateTime.Parse(GregorianDate);
            PersianCalendar pc = new PersianCalendar();
            return string.Format("{0:0000}/{1:00}/{2:00}", pc.GetYear(d), pc.GetMonth(d), pc.GetDayOfMonth(d));
        }

        private void ConnectDatabase()
        {
            var connection =
                System.Configuration.ConfigurationManager.ConnectionStrings["AshaDbContext"].ConnectionString;
            if (_dbConnection == null)
                _dbConnection = new SqlConnection(connection);
            if (_dbConnection.State != ConnectionState.Open)
            {
                try
                {
                    _dbConnection.Open();
                    InvokeGuiThread(() =>
                        {
                            DatabaseIndicator.Text = "فعال";
                            DatabaseIndicator.ForeColor = Color.Green;
                        });
                }
                catch (Exception)
                {
                    InvokeGuiThread(() =>
                        {
                            DatabaseIndicator.Text = "غیرفعال";
                            DatabaseIndicator.ForeColor = Color.Red;
                        });
                }
            }
        }

        private void ConnectWeighingMachine()
        {
            if (!_serialPort.IsOpen)
            {
                try
                {
                    _serialPort.Open();
                    InvokeGuiThread(() =>
                    {
                        WeighingMachineIndicator.Text = "فعال";
                        WeighingMachineIndicator.ForeColor = Color.Green;
                    });
                }
                catch (Exception)
                {
                    InvokeGuiThread(() =>
                    {
                        WeighingMachineIndicator.Text = "غیرفعال";
                        WeighingMachineIndicator.ForeColor = Color.Red;
                    });
                }
            }
        }

        private void DisconnectDatabase()
        {
            if (_dbConnection != null && _dbConnection.State != ConnectionState.Closed)
            {
                _dbConnection.Close();
                DatabaseIndicator.Text = "غیرفعال";
                DatabaseIndicator.ForeColor = Color.Red;
            }
        }

        private void DisconnectWeighingMachine()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
                WeighingMachineIndicator.Text = "غیرفعال";
                WeighingMachineIndicator.ForeColor = Color.Red;
            }
        }

        public byte[] imageToByteArray(System.Drawing.Image imageIn)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }

        private void btnGetData_Click(object sender, EventArgs e)
        {

            for (int i = 0; i < 4; i++)
            {
                requestFrame(i);
            }
           
            calcWaitingCars();
        }
        private void btnSaveData_Click(object sender, EventArgs e)
        {
            if (_shipmentTable.Rows.Count <= 0)
            {
                MessageBox.Show("این محموله در وضعیت توزین قرار ندارد", "خطا", MessageBoxButtons.OK,
                MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                return;
            }
            Image[] images = { imgCamera1.Image, imgCamera2.Image, imgCamera3.Image, imgCamera4.Image };
            SqlCommand sqlCommand;

            try
            {
                if (_shipmentState == "Shp_FirstWeighing" &&
                    MessageBox.Show("اطلاعات به بارگیری ارسال خواهد شد. آیا مطمئن هستید؟", "تکمیل توزین", MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button1)
                    == System.Windows.Forms.DialogResult.OK)
                {
                    sqlCommand = new SqlCommand("UPDATE SDSO_Shipment SET TruckWeight=@TruckWeight, FirstWeighingMachineCode=@FirstMachine " +
                                "WHERE Code = @ShipmentCode", _dbConnection);
                    sqlCommand.Parameters.AddWithValue("@ShipmentCode", _shipmentTable.Rows[0].ItemArray[20].ToString());
                    sqlCommand.Parameters.AddWithValue("@TruckWeight", txtWeight1.Text);
                    sqlCommand.Parameters.AddWithValue("@FirstMachine", txtMachine1.Text);
                    sqlCommand.ExecuteNonQuery();
                    sqlCommand.Dispose();

                    sqlCommand = new SqlCommand("SDSO_001_ShipmentStatus", _dbConnection);
                    sqlCommand.CommandType = CommandType.StoredProcedure;

                    // set up the parameters
                    sqlCommand.Parameters.Add("@ShipmentCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@StatusCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@NewStatusCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@PositionCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@CreatorCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@ReturnMessage", SqlDbType.NVarChar, 1024).Direction = ParameterDirection.Output;
                    sqlCommand.Parameters.Add("@ReturnValue", SqlDbType.Int).Direction = ParameterDirection.Output;

                    // set parameter values
                    sqlCommand.Parameters["@shipmentCode"].Value = _shipmentTable.Rows[0].ItemArray[20].ToString();
                    sqlCommand.Parameters["@StatusCode"].Value = "Shp_FirstWeighing";
                    sqlCommand.Parameters["@NewStatusCode"].Value = "Shp_Loading";
                    sqlCommand.Parameters["@PositionCode"].Value = "Pos_999";
                    sqlCommand.Parameters["@CreatorCode"].Value = Globals.UserCode;
                    sqlCommand.Parameters["@ReturnMessage"].Value = "";
                    sqlCommand.Parameters["@ReturnValue"].Value = 1;

                    sqlCommand.ExecuteNonQuery();
                    string returnMessage = Convert.ToString(sqlCommand.Parameters["@ReturnMessage"].Value);
                    MessageBox.Show(returnMessage, "پیغام", MessageBoxButtons.OK,
                        MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                    sqlCommand.Dispose();

                    foreach (var item in images)
                    {
                        if (item == null) continue;
                        var image = imageToByteArray(item);
                        var date = item.Tag;

                        if (image != null)
                        {
                            sqlCommand = new SqlCommand("INSERT INTO SIDev_Binary (BinaryTitle, BinaryPath, BinaryData, BinaryExt, BinarySize, CreatorID, AttachDate, Embedded, Guid)" +
                                                                       "VALUES (@date, @date, @Image, '.jpg', @ImageSize, 1, GETDATE(), 1, NEWID())", _dbConnection);
                            sqlCommand.Parameters.AddWithValue("@date", date);
                            sqlCommand.Parameters.AddWithValue("@Image", image);
                            sqlCommand.Parameters.AddWithValue("@ImageSize", image.Length);
                            sqlCommand.ExecuteNonQuery();
                            sqlCommand.Dispose();

                            sqlCommand = new SqlCommand("SELECT ID, Guid FROM SIDev_Binary WHERE BinaryTitle = '" + date + "'", _dbConnection);
                            SqlDataAdapter sqlAdapter = new SqlDataAdapter(sqlCommand);
                            DataTable BinaryTable = new DataTable();
                            sqlAdapter.Fill(BinaryTable);
                            sqlCommand.Dispose();

                            sqlCommand = new SqlCommand("INSERT INTO SIDev_Attachment (MainSysEntityID, RelatedSysEntityID, MainItemGuid, RelatedItemGuid, AttachmentType)" +
                                                                "VALUES (2631, 2822, @MainGuid, @RelatedGuid, 2)", _dbConnection);
                            sqlCommand.Parameters.AddWithValue("@MainGuid", _shipmentTable.Rows[0].ItemArray[6].ToString());
                            sqlCommand.Parameters.AddWithValue("@RelatedGuid", BinaryTable.Rows[0].ItemArray[1].ToString());
                            sqlCommand.Parameters.AddWithValue("@ImageSize", image.Length);
                            sqlCommand.ExecuteNonQuery();
                            sqlCommand.Dispose();
                        }
                    }

                    ClearFields();
                }
                else if ((_shipmentState == "Shp_SecondWeighing" || _shipmentState == "Shp_Loading") &&
                    MessageBox.Show("اطلاعات به دیسپچینگ ارسال خواهد شد. آیا مطمئن هستید؟", "تکمیل توزین", MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button1)
                    == System.Windows.Forms.DialogResult.OK)
                {
                    double weight1, weight2;
                    if (double.TryParse(txtWeight1.Text, out weight1) && double.TryParse(txtWeight2.Text, out weight2))
                    {
                        lblNetWeightLoad.Text = string.Format("{0:0.###}", Math.Abs(weight2 - weight1));
                    }

                    sqlCommand = new SqlCommand("UPDATE SDSO_Shipment SET LoadedTruckWeight=@LoadedTruckWeight, NetWeight=@NetWeight, SecondWeighingMachineCode=@SecondMachine " +
                                "WHERE Code = @ShipmentCode", _dbConnection);
                    sqlCommand.Parameters.AddWithValue("@ShipmentCode", _shipmentTable.Rows[0].ItemArray[20].ToString());
                    sqlCommand.Parameters.AddWithValue("@NetWeight", lblNetWeightLoad.Text);
                    sqlCommand.Parameters.AddWithValue("@LoadedTruckWeight", txtWeight2.Text);
                    sqlCommand.Parameters.AddWithValue("@SecondMachine", txtMachine2.Text);
                    sqlCommand.ExecuteNonQuery();
                    sqlCommand.Dispose();

                    sqlCommand = new SqlCommand("SDSO_001_ShipmentStatus", _dbConnection);
                    sqlCommand.CommandType = CommandType.StoredProcedure;

                    // set up the parameters
                    sqlCommand.Parameters.Add("@ShipmentCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@StatusCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@NewStatusCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@PositionCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@CreatorCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@ReturnMessage", SqlDbType.NVarChar, 1024).Direction = ParameterDirection.Output;
                    sqlCommand.Parameters.Add("@ReturnValue", SqlDbType.Int).Direction = ParameterDirection.Output;

                    // set parameter values
                    sqlCommand.Parameters["@shipmentCode"].Value = _shipmentTable.Rows[0].ItemArray[20].ToString();
                    sqlCommand.Parameters["@StatusCode"].Value = "Shp_SecondWeighing";
                    sqlCommand.Parameters["@NewStatusCode"].Value = "Shp_Issue";
                    sqlCommand.Parameters["@PositionCode"].Value = "Pos_999";
                    sqlCommand.Parameters["@CreatorCode"].Value = Globals.UserCode;
                    sqlCommand.Parameters["@ReturnMessage"].Value = "";
                    sqlCommand.Parameters["@ReturnValue"].Value = 1;

                    sqlCommand.ExecuteNonQuery();
                    string returnMessage = Convert.ToString(sqlCommand.Parameters["@ReturnMessage"].Value);
                    int returnValue = Convert.ToInt32(sqlCommand.Parameters["@ReturnValue"].Value);



                    if (returnValue == 1)
                    {
                        MessageBox.Show(returnMessage, "پیغام", MessageBoxButtons.OK,
                            MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                        sqlCommand.Dispose();

                        foreach (var item in images)
                        {
                            if (item == null) continue;
                            var image = imageToByteArray(item);
                            var date = item.Tag;

                            if (image != null)
                            {
                                sqlCommand = new SqlCommand("INSERT INTO SIDev_Binary (BinaryTitle, BinaryPath, BinaryData, BinaryExt, BinarySize, CreatorID, AttachDate, Embedded, Guid)" +
                                                                           "VALUES (@date, @date, @Image, '.jpg', @ImageSize, 1, GETDATE(), 1, NEWID())", _dbConnection);
                                sqlCommand.Parameters.AddWithValue("@date", date);
                                sqlCommand.Parameters.AddWithValue("@Image", image);
                                sqlCommand.Parameters.AddWithValue("@ImageSize", image.Length);
                                sqlCommand.ExecuteNonQuery();
                                sqlCommand.Dispose();

                                sqlCommand = new SqlCommand("SELECT ID, Guid FROM SIDev_Binary WHERE BinaryTitle = '" + date + "'", _dbConnection);
                                SqlDataAdapter sqlAdapter = new SqlDataAdapter(sqlCommand);
                                DataTable BinaryTable = new DataTable();
                                sqlAdapter.Fill(BinaryTable);
                                sqlCommand.Dispose();

                                sqlCommand = new SqlCommand("INSERT INTO SIDev_Attachment (MainSysEntityID, RelatedSysEntityID, MainItemGuid, RelatedItemGuid, AttachmentType)" +
                                                                    "VALUES (2631, 2822, @MainGuid, @RelatedGuid, 2)", _dbConnection);
                                sqlCommand.Parameters.AddWithValue("@MainGuid", _shipmentTable.Rows[0].ItemArray[6].ToString());
                                sqlCommand.Parameters.AddWithValue("@RelatedGuid", BinaryTable.Rows[0].ItemArray[1].ToString());
                                sqlCommand.Parameters.AddWithValue("@ImageSize", image.Length);
                                sqlCommand.ExecuteNonQuery();
                                sqlCommand.Dispose();
                            }
                        }
                        ClearFields();
                    }
                    else if (returnValue == 0)
                    {
                        if (MessageBox.Show(returnMessage, "اخطار", MessageBoxButtons.OK,
                            MessageBoxIcon.Error, MessageBoxDefaultButton.Button1) == System.Windows.Forms.DialogResult.OK)
                        {
                            sqlCommand.Dispose();

                            if (MessageBox.Show("آیا مغایرت وزنی تایید می شود؟", "پیغام", MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == System.Windows.Forms.DialogResult.Yes)
                            {
                                sqlCommand = new SqlCommand("SDSO_001_ShipmentWeightApprove", _dbConnection);
                                sqlCommand.CommandType = CommandType.StoredProcedure;

                                // set up the parameters
                                sqlCommand.Parameters.Add("@ShipmentCode", SqlDbType.NVarChar, 64);
                                sqlCommand.Parameters.Add("@PositionCode", SqlDbType.NVarChar, 64);
                                sqlCommand.Parameters.Add("@CreatorCode", SqlDbType.NVarChar, 64);
                                sqlCommand.Parameters.Add("@ReturnMessage", SqlDbType.NVarChar, 1024).Direction = ParameterDirection.Output;
                                sqlCommand.Parameters.Add("@ReturnValue", SqlDbType.Int).Direction = ParameterDirection.Output;

                                // set parameter values
                                sqlCommand.Parameters["@shipmentCode"].Value = _shipmentTable.Rows[0].ItemArray[20].ToString();
                                sqlCommand.Parameters["@PositionCode"].Value = "Pos_999";
                                sqlCommand.Parameters["@CreatorCode"].Value = Globals.UserCode;
                                sqlCommand.Parameters["@ReturnMessage"].Value = "";
                                sqlCommand.Parameters["@ReturnValue"].Value = 1;

                                sqlCommand.ExecuteNonQuery();
                                sqlCommand.Dispose();

                                foreach (var item in images)
                                {
                                    if (item == null) continue;
                                    var image = imageToByteArray(item);
                                    var date = item.Tag;

                                    if (image != null)
                                    {
                                        sqlCommand = new SqlCommand("INSERT INTO SIDev_Binary (BinaryTitle, BinaryPath, BinaryData, BinaryExt, BinarySize, CreatorID, AttachDate, Embedded, Guid)" +
                                                                                   "VALUES (@date, @date, @Image, '.jpg', @ImageSize, 1, GETDATE(), 1, NEWID())", _dbConnection);
                                        sqlCommand.Parameters.AddWithValue("@date", date);
                                        sqlCommand.Parameters.AddWithValue("@Image", image);
                                        sqlCommand.Parameters.AddWithValue("@ImageSize", image.Length);
                                        sqlCommand.ExecuteNonQuery();
                                        sqlCommand.Dispose();

                                        sqlCommand = new SqlCommand("SELECT ID, Guid FROM SIDev_Binary WHERE BinaryTitle = '" + date + "'", _dbConnection);
                                        SqlDataAdapter sqlAdapter = new SqlDataAdapter(sqlCommand);
                                        DataTable BinaryTable = new DataTable();
                                        sqlAdapter.Fill(BinaryTable);
                                        sqlCommand.Dispose();

                                        sqlCommand = new SqlCommand("INSERT INTO SIDev_Attachment (MainSysEntityID, RelatedSysEntityID, MainItemGuid, RelatedItemGuid, AttachmentType)" +
                                                                            "VALUES (2631, 2822, @MainGuid, @RelatedGuid, 2)", _dbConnection);
                                        sqlCommand.Parameters.AddWithValue("@MainGuid", _shipmentTable.Rows[0].ItemArray[6].ToString());
                                        sqlCommand.Parameters.AddWithValue("@RelatedGuid", BinaryTable.Rows[0].ItemArray[1].ToString());
                                        sqlCommand.Parameters.AddWithValue("@ImageSize", image.Length);
                                        sqlCommand.ExecuteNonQuery();
                                        sqlCommand.Dispose();
                                    }
                                }
                                ClearFields();
                            }
                        }
                    }
                    sqlCommand.Dispose(); 
                    ClearFields();
                }
            }
            catch (InvalidOperationException ex)
            {
                InvokeGuiThread(() =>
                {
                    DatabaseIndicator.Text = "غیرفعال";
                    DatabaseIndicator.ForeColor = Color.Red;
                });
                MessageBox.Show("لطفا به پایگاد داده متصل شوید", "Database Connection Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch(SqlException exp)
            {
                MessageBox.Show(exp.Message, "SQL Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                calcWaitingCars();
            }
        }

        private void ClearFields()
        {
            _shipmentState = "Shp_FirstWeighing";
            lblBillOfLading.Text = "---";
            lblCar.Text = "---";
            lblAddress.Text = "---";
            lblDriver.Text = "---";
            lblDriverLicence.Text = "---";
            lblDestination.Text = "---";
            lblSaler.Text = "---";
            lblSender.Text = "---";
            lblSaler.Text = "---";
            txtDate1.Text = "";
            txtDate2.Text = "";
            txtMachine1.Text = "";
            txtMachine2.Text = "";
            txtShipmentCode.Text = "";
            txtTime1.Text = "";
            txtTime2.Text = "";
            txtWeight1.Text = "";
            txtWeight2.Text = "";
            lblLoadedBranches.Text = "0";
            lblNetWeightLoad.Text = "0";
            lblDiscrepency.Text = "0";
            _shipmentTable.Clear();
            dgShipmentDetail.DataSource = null;
            imgCamera1.Image = null;
            imgCamera2.Image = null;
            imgCamera3.Image = null;
            imgCamera4.Image = null;
        }

        private void btnShipmentSearch_Click(object sender, EventArgs e)
        {
            if (_dbConnection == null || _dbConnection.State != ConnectionState.Open)
                ConnectDatabase();

            try
            {
                if (_dbConnection.State == ConnectionState.Open)
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT        SDSO_Shipment.Title AS ShipmentTitle, SDSO_Shipment.TransportCode, SDSO_Customer.Title AS Destination, WMLog_Vehicle.CarrierNumber, " +
                                                           "                     WMLog_Driver.Title AS DriverTitle, WMLog_Driver.LicenseNumber, SDSO_Shipment.Guid, SDSO_Shipment.FormStatusCode AS ShipmentStatus, " +
                                                           "                     contact1.Title AS TransportCompany, SDSO_Shipment.DestinationPoint AS City, SDSO_Shipment.TruckWeight, SDSO_Shipment.LoadedTruckWeight, " +
                                                           "                     SDSO_Shipment.NetWeight, SDSO_Shipment.EstimatedWeight, LEFT(dbo.MiladiToShamsi(SDSO_Shipment.EndTime), 10) AS EndDate, LEFT(dbo.MiladiToShamsi(SDSO_Shipment.StartTime), 10) AS StartDate, " +
                                                           "                     RIGHT(dbo.MiladiToShamsi(SDSO_Shipment.EndTime), 8) AS EndTime, RIGHT(dbo.MiladiToShamsi(SDSO_Shipment.StartTime), 8) AS StartTime, " +
                                                           "                     FirstWeighingMachineCode, SecondWeighingMachineCode, SDSO_Shipment.Code, SDSO_Shipment.EstimatedWeight, sdso_customer.CustomerCode, SDSO_Shipment.DeliverToAddress " +
                                                           " FROM		  SDSO_Shipment LEFT OUTER JOIN SDSO_Customer " +
		                                                   "         ON	SDSO_Shipment.CustomerCode = SDSO_Customer.CustomerCode LEFT OUTER JOIN WMLog_Vehicle " +
		                                                   "         ON	SDSO_Shipment.VehicleCode = WMLog_Vehicle.Code LEFT OUTER JOIN WMLog_Driver " +
		                                                   "         ON	SDSO_Shipment.DriverCode = WMLog_Driver.DriverCode LEFT OUTER JOIN WFFC_Contact AS contact1 " +
		                                                   "         ON	SDSO_Shipment.TransportCompanyCode = contact1.Code LEFT OUTER JOIN WFFC_Contact AS contact2 " +
		                                                   "        ON	SDSO_Customer.CustomerCode = contact2.code LEFT OUTER JOIN SISys_Location " +
                                                           "         ON	contact2.GeograghyLocationCode = SISys_Location.Code " +
                                                           " WHERE        (SDSO_Shipment.FormStatusCode IN ('Shp_FirstWeighing', 'Shp_SecondWeighing')) AND SDSO_Shipment.Code = '" + txtShipmentCode.Text.PadLeft(8, '0') + "'"
                                                            , _dbConnection))
                    {
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        _shipmentTable.Clear();
                        da.Fill(_shipmentTable);
                        if (_shipmentTable.Rows.Count > 0)
                        {
                            lblSender.Text = _shipmentTable.Rows[0].ItemArray[8].ToString();
                            lblSaler.Text = _shipmentTable.Rows[0].ItemArray[2].ToString();
                            lblBillOfLading.Text = _shipmentTable.Rows[0].ItemArray[1].ToString();
                            lblDestination.Text = _shipmentTable.Rows[0].ItemArray[9].ToString();
                            lblCar.Text = _shipmentTable.Rows[0].ItemArray[3].ToString();
                            lblDriver.Text = _shipmentTable.Rows[0].ItemArray[4].ToString();
                            lblDriverLicence.Text = _shipmentTable.Rows[0].ItemArray[5].ToString();
                            lblAddress.Text = _shipmentTable.Rows[0].ItemArray[23].ToString();
                            txtWeight1.Text = string.Format("{0:0.###}", _shipmentTable.Rows[0].ItemArray[10]);
                            txtWeight2.Text = string.Format("{0:0.###}", _shipmentTable.Rows[0].ItemArray[11]);
                            txtDate1.Text = _shipmentTable.Rows[0].ItemArray[15].ToString();
                            txtDate2.Text = _shipmentTable.Rows[0].ItemArray[14].ToString();
                            txtTime1.Text = _shipmentTable.Rows[0].ItemArray[17].ToString();
                            txtTime2.Text = _shipmentTable.Rows[0].ItemArray[16].ToString();
                            txtMachine1.Text = _shipmentTable.Rows[0].ItemArray[18].ToString();
                            txtMachine2.Text = _shipmentTable.Rows[0].ItemArray[19].ToString();
                            _shipmentState = _shipmentTable.Rows[0].ItemArray[7].ToString();
                        }
                        else
                        {
                            lblDriver.Text = "---";
                            lblBillOfLading.Text = "---";
                            lblAddress.Text = "---";
                            lblCar.Text = "---";
                            lblDriverLicence.Text = "---";
                        }
                    }

                    calcWaitingCars();

                    using (SqlCommand cmd = new SqlCommand("SELECT  Sequence as ردیف, PartSerialCode as [بارکد شمش], SDSO_ShipmentDetail.ProductCode as [کد کالا], WMInv_Part.Title as [نام کالا], ShipmentAuthorizeCode as [مجوز حمل], CONVERT(DECIMAL(10,0), RemainedQuantity) as [باقیمانده مجوز] FROM SDSO_Shipment " +
                        "INNER JOIN SDSO_ShipmentDetail ON SDSO_Shipment.Code = SDSO_ShipmentDetail.ShipmentCode " +
                        "INNER JOIN SDSO_ShipmentAuthorize ON SDSO_ShipmentDetail.ShipmentAuthorizeCode = SDSO_ShipmentAuthorize.Code " +
                        "INNER JOIN WMInv_Part ON SDSO_ShipmentDetail.ProductCode = WMInv_Part.Code WHERE (SDSO_Shipment.FormStatusCode IN ('Shp_FirstWeighing', 'Shp_SecondWeighing')) AND SDSO_Shipment.Code = '" + txtShipmentCode.Text.PadLeft(8, '0') + "'"
                                                            , _dbConnection))
                    {
                        DataTable shipmentDetailTable = new DataTable();
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        da.Fill(shipmentDetailTable);
                        if (shipmentDetailTable.Rows.Count > 0)
                        {
                            dgShipmentDetail.DataSource = shipmentDetailTable;

                            var results = shipmentDetailTable.AsEnumerable().Count();
                            lblLoadedBranches.Text = string.Format("{0:0.###}", results);
                        }
                        else
                        {
                            dgShipmentDetail.DataSource = null;
                        }
                    }
                }
            }
            catch (Exception)
            {
                InvokeGuiThread(() =>
                {
                    DatabaseIndicator.Text = "غیرفعال";
                    DatabaseIndicator.ForeColor = Color.Red;
                });
            }
        }

        private void calcWaitingCars()
        {
             if (_dbConnection == null || _dbConnection.State != ConnectionState.Open)
                ConnectDatabase();

             try
             {
                 if (_dbConnection.State == ConnectionState.Open)
                 {
                     using (SqlCommand cmd = new SqlCommand("SELECT        COUNT(*) FROM SDSO_Shipment " +
                                                            " WHERE        (FormStatusCode LIKE '%Loading%') AND ReceptionDate > DATEADD(dd, -1, GETDATE())"
                                                                     , _dbConnection))
                     {
                         DataTable CarCount = new DataTable();
                         SqlDataAdapter da = new SqlDataAdapter(cmd);
                         CarCount.Clear();
                         da.Fill(CarCount);
                         if (CarCount.Rows.Count > 0)
                         {
                             lblWaitingMachines2.Text = CarCount.Rows[0].ItemArray[0].ToString();
                         }
                         else
                         {
                             lblWaitingMachines2.Text = "---";
                         }
                     }
                 }
             }
            catch(Exception)
             {
                 lblWaitingMachines2.Text = "---";
             }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            System.Threading.Thread thread1 = new System.Threading.Thread(ConnectDatabase);
            System.Threading.Thread thread2 = new System.Threading.Thread(ConnectWeighingMachine);
            thread1.Start();
            thread2.Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DisconnectWeighingMachine();
            DisconnectDatabase();
        }

        private void txtShipmentCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnShipmentSearch_Click(sender, e);
            }
        }

        private void بزرگنماییToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CameraViewer viewer = new CameraViewer();
            viewer.PreviewImage = ((PictureBox)((ContextMenuStrip)(((ToolStripMenuItem)sender).Owner)).SourceControl).Image;
            viewer.Show();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            ShipmentListForm shipments = new ShipmentListForm();
            if(shipments.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.txtShipmentCode.Text = shipments.shipmentCode;
            }
        }

        private void contextMenu_Click(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem != null)
            {
                menuItem.Checked = true;
                Settings.Default.SelectedConfiguration = (string)menuItem.Tag;
                Settings.Default.Save();

                var parentMenu = ((ToolStripDropDownMenu)(((ToolStripMenuItem)sender).Owner));
                foreach (ToolStripMenuItem item in parentMenu.Items)
                {
                    if (item != menuItem)
                    {
                        item.Checked = false;
                    }
                }
            }

            Globals.GetConfigurationDetails(Settings.Default.SelectedConfiguration);

            DisconnectWeighingMachine();
            CreateSerialPort();

            System.Threading.Thread thread2 = new System.Threading.Thread(ConnectWeighingMachine);
            thread2.Start();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            ClearFields();
        }
    }
}
