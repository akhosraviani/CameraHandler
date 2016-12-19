﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Text;
using System.IO.Ports;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading;
using System.Net;
using System.IO;
using AshaWeighing.Properties;

namespace AshaWeighing
{
    public partial class WeighingForm : Form
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont,
            IntPtr pdv, [System.Runtime.InteropServices.In] ref uint pcFonts);

        private PrivateFontCollection fonts = new PrivateFontCollection();

        private bool _isStable = false;
        private Font myFont, myFontBig;
        private List<Label> _indicatorList;
        private SerialPort _serialPort;
        private SqlConnection _dbConnection;
        private string _weighingOrderCode = string.Empty;
        private DataTable _WeighingDetail = new DataTable();
        private string _shipmentState = "Shp_FirstWeighing";
        private string _weighingDetailCriteria = string.Empty;
        private string _weighingMasterCriteria = string.Empty;
        private string _weighingStatusCriteria = string.Empty;
        private DataTable _WeighingOrderTable;
        private bool _negativeWeight = false;
        private Dictionary<string, string> _configs;
        private List<WeighingOrderType> _weighingTypes;
        private System.Windows.Forms.Timer tmr = new System.Windows.Forms.Timer();
        public WeighingForm()
        {
            InitializeComponent();
            Load += WeighingForm_Load;
            FormClosing += WeighingForm_FormClosing;
        }

        private void requestFrame(int requestNumber)
        {
            string cameraUrl = Globals.CameraAddress[requestNumber];
            try
            {
                var request = HttpWebRequest.Create(cameraUrl);
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
            try
            {
                using (SqlCommand cmd = new SqlCommand("SELECT Code, Title FROM WMLog_Configuration WHERE FormStatusCode='Cfg_Active' "
                                                        , _dbConnection))
                {
                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    DataTable conf = new DataTable();
                    da.Fill(conf);
                    _configs = new Dictionary<string, string>();
                    ToolStripMenuItem[] items = new ToolStripMenuItem[conf.Rows.Count];

                    for (int i = 0; i < conf.Rows.Count; i++)
                    {
                        _configs.Add(conf.Rows[i].Field<string>("Code"), conf.Rows[i].Field<string>("Title"));

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

            this.Font = myFont;
            lblDiscrepency.Font = myFontBig;
            lblNetWeight.Font = myFontBig;
            lblLoadedBranches.Font = myFontBig;
            lblCurrentTime.Font = myFontBig;
            lblCurrentDate.Font = myFontBig;
            lblWeighingBridgeCode.Font = myFontBig;
            weighingBridgeIndicator.Font = myFontBig;
            lblWeighingResponsible.Font = myFontBig;
        }

        private void PicBox_DoubleClick(object sender, MouseEventArgs e)
        {
            CameraViewer viewer = new CameraViewer();
            viewer.PreviewImage = ((PictureBox)sender).Image;
            viewer.Show();
        }

        void WeighingForm_Load(object sender, EventArgs e)
        {
            InitializeFontAndCamera();

            _indicatorList = new List<Label>();
            _WeighingOrderTable = new DataTable();
            CreateIndicators();
            ConnectDatabase();
            InitializeConfigurations();
            CreateSerialPort();
            ConnectWeighingMachine();
            ConnectCameras();
            GetWeighingTypes();
            foreach (ToolStripMenuItem item in ctmConfig.DropDownItems)
            {
                if (((string)item.Tag) != Settings.Default.SelectedConfiguration)
                    item.Checked = false;
                else
                {
                    item.Checked = true;
                    lblWeighingBridgeCode.Text = item.Text;
                }
            }

            lblWeighingResponsible.Text = Globals.UserName;
            tmr.Interval = 1000;//ticks every 1 second
            tmr.Tick += new EventHandler(tmr_Tick);
            tmr.Start();
        }

        private void GetWeighingTypes()
        {
            if (_dbConnection == null || _dbConnection.State != ConnectionState.Open)
                ConnectDatabase();
            try
            {
                if (_dbConnection.State == ConnectionState.Open)
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT Code, Title FROM WMLog_WeighingType"
                                                            , _dbConnection))
                    {
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable wieghingTypeTable = new DataTable();
                        da.Fill(wieghingTypeTable);
                        _weighingTypes = new List<WeighingOrderType>();
                        for (int i = 0; i < wieghingTypeTable.Rows.Count; i++)
                        {
                            _weighingTypes.Add(new WeighingOrderType() { Name = wieghingTypeTable.Rows[i].Field<string>("Title"),
                                                                        Value = wieghingTypeTable.Rows[i].Field<string>("Code") });
                        }

                        cmbWeighingTypes.DataSource = _weighingTypes;
                        cmbWeighingTypes.DisplayMember = "Name";
                        cmbWeighingTypes.ValueMember = "Value";
                        cmbWeighingTypes.DropDownStyle = ComboBoxStyle.DropDownList;
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

        //change the label text inside the tick event
        private void tmr_Tick(object sender, EventArgs e)
        {
            lblCurrentDate.Text = GetDate();
            lblCurrentTime.Text = GetTime();
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
              if(MessageBox.Show("وزن باسکول منفی می باشد! لطفا دستگاه را بررسی نمایید") == System.Windows.Forms.DialogResult.OK)
              {
                    _negativeWeight = false;
                    btnSaveData.Enabled = true;
                    btnGetStableData.Enabled = true;
              }
            });
          thread.Start();
        }
        private void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!_isStable)
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
                    if (intResult < -10)
                    {
                        sevenSegmentWeight.Value = intResult.ToString();

                        if (sevenSegmentWeight.ColorBackground != Color.Red || sevenSegmentWeight.ColorLight != Color.Yellow)
                        {
                            sevenSegmentWeight.ColorBackground = Color.Red;
                            sevenSegmentWeight.ColorLight = Color.Yellow;
                        }

                        if (_negativeWeight == false)
                        {
                            _negativeWeight = true;
                            btnSaveData.Enabled = false;
                            btnGetStableData.Enabled = false;
                            ShowNegativeWeightMessageBox();
                        }

                    }
                    else if (intResult >= 0)
                    {
                        sevenSegmentWeight.ColorBackground = Color.Black;
                        sevenSegmentWeight.ColorLight = Color.Red;
                        sevenSegmentWeight.Value = intResult.ToString();
                    }

                    //if (_shipmentState == "Shp_SecondWeighing")
                    //{
                    //    double weight1, weight2;
                    //    if (double.TryParse(txtWeight1.Text, out weight1) && double.TryParse(txtWeight2.Text, out weight2))
                    //    {
                    //        lblNetWeightLoad.Text = string.Format("{0:0.###}", Math.Abs(weight2 - weight1));

                    //        double netWeight, estimatedWeight;
                    //        if (double.TryParse(lblNetWeightLoad.Text, out netWeight) && double.TryParse(_shipmentTable.Rows[0].ItemArray[21].ToString(), out estimatedWeight))
                    //        {
                    //            lblDiscrepency.Text = string.Format("{0:0.###}", Math.Abs((estimatedWeight - netWeight) / estimatedWeight * 100));
                    //        }
                    //    }
                    //}


                    //if (_shipmentTable.Rows.Count > 0)
                    //{
                    //    sevenSegmentWeight.Value = string.Format("{0:0.###}", _shipmentTable.Rows[0].ItemArray[10]);
                    //    lblWeighingResponsible.Text = _shipmentTable.Rows[0].ItemArray[18].ToString();
                    //}
                }
                catch (Exception)
                { }
            }
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
            return string.Format("{0:00}:{1:00}:{2:00}", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
        }

        private string GetDate()
        {
            string GregorianDate = DateTime.Now.ToString();
            DateTime d = DateTime.Parse(GregorianDate);
            PersianCalendar pc = new PersianCalendar();
            return string.Format("{0:0000}/{1:00}/{2:00}", pc.GetYear(d), pc.GetMonth(d), pc.GetDayOfMonth(d));
        }
        private void ConnectCameras()
        {
            for (int i = 0; i < 4; i++)
            {
                string cameraUrl = Globals.CameraAddress[i];
                HttpWebResponse response = null;
                var request = (HttpWebRequest)WebRequest.Create(cameraUrl);
                request.Credentials = new NetworkCredential(Globals.CameraUsername[i], Globals.CameraPassword[i]);
                request.Proxy = null;
                request.Method = "HEAD";

                try
                {
                    response = (HttpWebResponse)request.GetResponse();
                    var counter = i;
                    InvokeGuiThread(() =>
                    {
                        _indicatorList[counter].Text = "فعال";
                        _indicatorList[counter].ForeColor = Color.Green;
                    });
                }
                catch (WebException ex)
                {
                    var counter = i;
                    /* A WebException will be thrown if the status of the response is not `200 OK` */
                    InvokeGuiThread(() =>
                    {
                        _indicatorList[counter].Text = "غیرفعال";
                        _indicatorList[counter].ForeColor = Color.Red;
                    });
                }
                finally
                {
                    // Don't forget to close your response.
                    if (response != null)
                    {
                        response.Close();
                    }
                }
            }
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
            if (_serialPort != null && !_serialPort.IsOpen)
            {
                try
                {
                    _serialPort.Open();
                    InvokeGuiThread(() =>
                    {
                        weighingBridgeIndicator.Text = "فعال";
                        weighingBridgeIndicator.ForeColor = Color.Green;
                    });
                }
                catch (Exception)
                {
                    InvokeGuiThread(() =>
                    {
                        weighingBridgeIndicator.Text = "غیرفعال";
                        weighingBridgeIndicator.ForeColor = Color.Red;
                    });
                }
            }
            else
            {
                InvokeGuiThread(() =>
                {
                    weighingBridgeIndicator.Text = "غیرفعال";
                    weighingBridgeIndicator.ForeColor = Color.Red;
                });
            }
        }

        private void DisconnectDatabase()
        {
            if (_dbConnection != null && _dbConnection.State != ConnectionState.Closed)
            {
                _dbConnection.Close();
            }
        }

        private void DisconnectWeighingMachine()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
                weighingBridgeIndicator.Text = "غیرفعال";
                weighingBridgeIndicator.ForeColor = Color.Red;
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

        private void btnGetStableData_Click(object sender, EventArgs e)
        {
            if (!_isStable)
            {
                for (int i = 0; i < 4; i++)
                {
                    requestFrame(i);
                }
                btnGetStableData.Text = "فعال سازی دریافت اطلاعات";
                sevenSegmentWeight.ColorLight = Color.LightGreen;
                btnSaveData.Enabled = true;
                tmr.Tick -= tmr_Tick;
            }
            else
            {
                imgCamera1.Image = null;
                imgCamera2.Image = null;
                imgCamera3.Image = null;
                imgCamera4.Image = null;
                btnGetStableData.Text = "تثبیت وزن و دریافت تصاویر";
                sevenSegmentWeight.ColorLight = Color.Red;
                btnSaveData.Enabled = false;
                tmr.Tick += tmr_Tick;
            }
            _isStable = !_isStable;
        }
        private void btnSaveData_Click(object sender, EventArgs e)
        {
            if (_weighingOrderCode == null)
            {
                MessageBox.Show("شناسه توزین مشخص نشده است. لطفا شناسه توزین را جستجو نمایید.", "خطا", MessageBoxButtons.OK,
                MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                return;
            }
            Image[] images = { imgCamera1.Image, imgCamera2.Image, imgCamera3.Image, imgCamera4.Image };
            SqlCommand sqlCommand;

            try
            {
                if (MessageBox.Show("اطلاعات دخیره خواهد شد. آیا مطمئن هستید؟", "تکمیل توزین", MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button1)
                    == DialogResult.OK)
                {
                //    sqlCommand = new SqlCommand("UPDATE SDSO_Shipment SET TruckWeight=@TruckWeight, FirstWeighingMachineCode=@FirstMachine " +
                //                "WHERE Code = @ShipmentCode", _dbConnection);
                //    sqlCommand.Parameters.AddWithValue("@ShipmentCode", _WeighingOrderTable.Rows[0].ItemArray[20].ToString());
                //    sqlCommand.Parameters.AddWithValue("@TruckWeight", sevenSegmentWeight.Text);
                //    sqlCommand.Parameters.AddWithValue("@FirstMachine", Globals.WeighingMachineCode);
                //    sqlCommand.ExecuteNonQuery();
                //    sqlCommand.Dispose();

                //    sqlCommand = new SqlCommand("SDSO_001_ShipmentStatus", _dbConnection);
                //    sqlCommand.CommandType = CommandType.StoredProcedure;

                //    // set up the parameters
                //    sqlCommand.Parameters.Add("@ShipmentCode", SqlDbType.NVarChar, 64);
                //    sqlCommand.Parameters.Add("@StatusCode", SqlDbType.NVarChar, 64);
                //    sqlCommand.Parameters.Add("@NewStatusCode", SqlDbType.NVarChar, 64);
                //    sqlCommand.Parameters.Add("@PositionCode", SqlDbType.NVarChar, 64);
                //    sqlCommand.Parameters.Add("@CreatorCode", SqlDbType.NVarChar, 64);
                //    sqlCommand.Parameters.Add("@ReturnMessage", SqlDbType.NVarChar, 1024).Direction = ParameterDirection.Output;
                //    sqlCommand.Parameters.Add("@ReturnValue", SqlDbType.Int).Direction = ParameterDirection.Output;

                //    // set parameter values
                //    sqlCommand.Parameters["@shipmentCode"].Value = _WeighingOrderTable.Rows[0].ItemArray[20].ToString();
                //    sqlCommand.Parameters["@StatusCode"].Value = "Shp_FirstWeighing";
                //    sqlCommand.Parameters["@NewStatusCode"].Value = "Shp_Loading";
                //    sqlCommand.Parameters["@PositionCode"].Value = "Pos_999";
                //    sqlCommand.Parameters["@CreatorCode"].Value = Globals.UserCode;
                //    sqlCommand.Parameters["@ReturnMessage"].Value = "";
                //    sqlCommand.Parameters["@ReturnValue"].Value = 1;

                //    sqlCommand.ExecuteNonQuery();
                //    string returnMessage = Convert.ToString(sqlCommand.Parameters["@ReturnMessage"].Value);
                //    MessageBox.Show(returnMessage, "پیغام", MessageBoxButtons.OK,
                //        MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                //    sqlCommand.Dispose();

                //    foreach (var item in images)
                //    {
                //        if (item == null) continue;
                //        var image = imageToByteArray(item);
                //        var date = item.Tag;

                //        if (image != null)
                //        {
                //            sqlCommand = new SqlCommand("INSERT INTO SIDev_Binary (BinaryTitle, BinaryPath, BinaryData, BinaryExt, BinarySize, CreatorID, AttachDate, Embedded, Guid)" +
                //                                                       "VALUES (@date, @date, @Image, '.jpg', @ImageSize, 1, GETDATE(), 1, NEWID())", _dbConnection);
                //            sqlCommand.Parameters.AddWithValue("@date", date);
                //            sqlCommand.Parameters.AddWithValue("@Image", image);
                //            sqlCommand.Parameters.AddWithValue("@ImageSize", image.Length);
                //            sqlCommand.ExecuteNonQuery();
                //            sqlCommand.Dispose();

                //            sqlCommand = new SqlCommand("SELECT ID, Guid FROM SIDev_Binary WHERE BinaryTitle = '" + date + "'", _dbConnection);
                //            SqlDataAdapter sqlAdapter = new SqlDataAdapter(sqlCommand);
                //            DataTable BinaryTable = new DataTable();
                //            sqlAdapter.Fill(BinaryTable);
                //            sqlCommand.Dispose();

                //            sqlCommand = new SqlCommand("INSERT INTO SIDev_Attachment (MainSysEntityID, RelatedSysEntityID, MainItemGuid, RelatedItemGuid, AttachmentType)" +
                //                                                "VALUES (2631, 2822, @MainGuid, @RelatedGuid, 2)", _dbConnection);
                //            sqlCommand.Parameters.AddWithValue("@MainGuid", _WeighingOrderTable.Rows[0].ItemArray[6].ToString());
                //            sqlCommand.Parameters.AddWithValue("@RelatedGuid", BinaryTable.Rows[0].ItemArray[1].ToString());
                //            sqlCommand.Parameters.AddWithValue("@ImageSize", image.Length);
                //            sqlCommand.ExecuteNonQuery();
                //            sqlCommand.Dispose();
                //        }
                //    }

                //    ClearFields();
                //}
                //else if ((_shipmentState == "Shp_SecondWeighing" || _shipmentState == "Shp_Loading") &&
                //    MessageBox.Show("اطلاعات به دیسپچینگ ارسال خواهد شد. آیا مطمئن هستید؟", "تکمیل توزین", MessageBoxButtons.OKCancel,
                //    MessageBoxIcon.Question, MessageBoxDefaultButton.Button1)
                //    == DialogResult.OK)
                //{
                //    double weight1, weight2;
                //    //if (double.TryParse(txtWeight1.Text, out weight1) && double.TryParse(txtWeight2.Text, out weight2))
                //    //{
                //    //    lblNetWeightLoad.Text = string.Format("{0:0.###}", Math.Abs(weight2 - weight1));
                //    //}

                //    sqlCommand = new SqlCommand("UPDATE SDSO_Shipment SET LoadedTruckWeight=@LoadedTruckWeight, NetWeight=@NetWeight, SecondWeighingMachineCode=@SecondMachine " +
                //                "WHERE Code = @ShipmentCode", _dbConnection);
                //    sqlCommand.Parameters.AddWithValue("@ShipmentCode", _WeighingOrderTable.Rows[0].ItemArray[20].ToString());
                //    sqlCommand.Parameters.AddWithValue("@NetWeight", lblNetWeight.Text);
                //    //sqlCommand.Parameters.AddWithValue("@LoadedTruckWeight", txtWeight2.Text);
                //    sqlCommand.Parameters.AddWithValue("@SecondMachine", Globals.WeighingMachineCode);
                //    sqlCommand.ExecuteNonQuery();
                //    sqlCommand.Dispose();

                    sqlCommand = new SqlCommand("WMLog_000_InsertWeighing", _dbConnection);
                    sqlCommand.CommandType = CommandType.StoredProcedure;

                    // set up the parameters
                    sqlCommand.Parameters.Add("@WeighingOrderCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@WeighingTypeCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@Weight", SqlDbType.Int);
                    sqlCommand.Parameters.Add("@Image1", SqlDbType.Binary);
                    sqlCommand.Parameters.Add("@Image2", SqlDbType.Binary);
                    sqlCommand.Parameters.Add("@Image3", SqlDbType.Binary);
                    sqlCommand.Parameters.Add("@Image4", SqlDbType.Binary);
                    sqlCommand.Parameters.Add("@MachineCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@ResponsibleCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@CreatorCode", SqlDbType.NVarChar, 64);
                    sqlCommand.Parameters.Add("@ReturnMessage", SqlDbType.NVarChar, 1024).Direction = ParameterDirection.Output;
                    sqlCommand.Parameters.Add("@ReturnValue", SqlDbType.Int).Direction = ParameterDirection.Output;

                    // set parameter values
                    sqlCommand.Parameters["@WeighingOrderCode"].Value = _weighingOrderCode;
                    sqlCommand.Parameters["@WeighingTypeCode"].Value = cmbWeighingTypes.SelectedValue;
                    sqlCommand.Parameters["@Weight"].Value = Int32.Parse(sevenSegmentWeight.Value);
                    sqlCommand.Parameters["@Image1"].Value = imageToByteArray(images[0]);
                    sqlCommand.Parameters["@Image2"].Value = imageToByteArray(images[1]);
                    sqlCommand.Parameters["@Image3"].Value = imageToByteArray(images[2]);
                    sqlCommand.Parameters["@Image4"].Value = imageToByteArray(images[3]);
                    sqlCommand.Parameters["@MachineCode"].Value = Globals.WeighingMachineCode;
                    sqlCommand.Parameters["@ResponsibleCode"].Value = Globals.PersonnelCode;
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
                    }
                    else if (returnValue == 0)
                    {
                        MessageBox.Show(returnMessage, "اخطار", MessageBoxButtons.OK,
                            MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                        sqlCommand.Dispose();
                    }
                    ClearFields();

                    //    foreach (var item in images)
                    //    {
                    //        if (item == null) continue;
                    //        var image = imageToByteArray(item);
                    //        var date = item.Tag;

                    //        if (image != null)
                    //        {
                    //            sqlCommand = new SqlCommand("INSERT INTO SIDev_Binary (BinaryTitle, BinaryPath, BinaryData, BinaryExt, BinarySize, CreatorID, AttachDate, Embedded, Guid)" +
                    //                                                       "VALUES (@date, @date, @Image, '.jpg', @ImageSize, 1, GETDATE(), 1, NEWID())", _dbConnection);
                    //            sqlCommand.Parameters.AddWithValue("@date", date);
                    //            sqlCommand.Parameters.AddWithValue("@Image", image);
                    //            sqlCommand.Parameters.AddWithValue("@ImageSize", image.Length);
                    //            sqlCommand.ExecuteNonQuery();
                    //            sqlCommand.Dispose();

                    //            sqlCommand = new SqlCommand("SELECT ID, Guid FROM SIDev_Binary WHERE BinaryTitle = '" + date + "'", _dbConnection);
                    //            SqlDataAdapter sqlAdapter = new SqlDataAdapter(sqlCommand);
                    //            DataTable BinaryTable = new DataTable();
                    //            sqlAdapter.Fill(BinaryTable);
                    //            sqlCommand.Dispose();

                    //            sqlCommand = new SqlCommand("INSERT INTO SIDev_Attachment (MainSysEntityID, RelatedSysEntityID, MainItemGuid, RelatedItemGuid, AttachmentType)" +
                    //                                                "VALUES (2631, 2822, @MainGuid, @RelatedGuid, 2)", _dbConnection);
                    //            sqlCommand.Parameters.AddWithValue("@MainGuid", _WeighingOrderTable.Rows[0].ItemArray[6].ToString());
                    //            sqlCommand.Parameters.AddWithValue("@RelatedGuid", BinaryTable.Rows[0].ItemArray[1].ToString());
                    //            sqlCommand.Parameters.AddWithValue("@ImageSize", image.Length);
                    //            sqlCommand.ExecuteNonQuery();
                    //            sqlCommand.Dispose();
                    //        }
                    //    }
                    //    ClearFields();
                    //}
                    //else if (returnValue == 0)
                    //{
                    //    if (MessageBox.Show(returnMessage, "اخطار", MessageBoxButtons.OK,
                    //        MessageBoxIcon.Error, MessageBoxDefaultButton.Button1) == DialogResult.OK)
                    //    {
                    //        sqlCommand.Dispose();

                    //        if (MessageBox.Show("آیا مغایرت وزنی تایید می شود؟", "پیغام", MessageBoxButtons.YesNo,
                    //        MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
                    //        {
                    //            sqlCommand = new SqlCommand("SDSO_001_ShipmentWeightApprove", _dbConnection);
                    //            sqlCommand.CommandType = CommandType.StoredProcedure;

                    //            // set up the parameters
                    //            sqlCommand.Parameters.Add("@ShipmentCode", SqlDbType.NVarChar, 64);
                    //            sqlCommand.Parameters.Add("@PositionCode", SqlDbType.NVarChar, 64);
                    //            sqlCommand.Parameters.Add("@CreatorCode", SqlDbType.NVarChar, 64);
                    //            sqlCommand.Parameters.Add("@ReturnMessage", SqlDbType.NVarChar, 1024).Direction = ParameterDirection.Output;
                    //            sqlCommand.Parameters.Add("@ReturnValue", SqlDbType.Int).Direction = ParameterDirection.Output;

                    //            // set parameter values
                    //            sqlCommand.Parameters["@shipmentCode"].Value = _WeighingOrderTable.Rows[0].ItemArray[20].ToString();
                    //            sqlCommand.Parameters["@PositionCode"].Value = "Pos_999";
                    //            sqlCommand.Parameters["@CreatorCode"].Value = Globals.UserCode;
                    //            sqlCommand.Parameters["@ReturnMessage"].Value = "";
                    //            sqlCommand.Parameters["@ReturnValue"].Value = 1;

                    //            sqlCommand.ExecuteNonQuery();
                    //            sqlCommand.Dispose();

                    //            foreach (var item in images)
                    //            {
                    //                if (item == null) continue;
                    //                var image = imageToByteArray(item);
                    //                var date = item.Tag;

                    //                if (image != null)
                    //                {
                    //                    sqlCommand = new SqlCommand("INSERT INTO SIDev_Binary (BinaryTitle, BinaryPath, BinaryData, BinaryExt, BinarySize, CreatorID, AttachDate, Embedded, Guid)" +
                    //                                                               "VALUES (@date, @date, @Image, '.jpg', @ImageSize, 1, GETDATE(), 1, NEWID())", _dbConnection);
                    //                    sqlCommand.Parameters.AddWithValue("@date", date);
                    //                    sqlCommand.Parameters.AddWithValue("@Image", image);
                    //                    sqlCommand.Parameters.AddWithValue("@ImageSize", image.Length);
                    //                    sqlCommand.ExecuteNonQuery();
                    //                    sqlCommand.Dispose();

                    //                    sqlCommand = new SqlCommand("SELECT ID, Guid FROM SIDev_Binary WHERE BinaryTitle = '" + date + "'", _dbConnection);
                    //                    SqlDataAdapter sqlAdapter = new SqlDataAdapter(sqlCommand);
                    //                    DataTable BinaryTable = new DataTable();
                    //                    sqlAdapter.Fill(BinaryTable);
                    //                    sqlCommand.Dispose();

                    //                    sqlCommand = new SqlCommand("INSERT INTO SIDev_Attachment (MainSysEntityID, RelatedSysEntityID, MainItemGuid, RelatedItemGuid, AttachmentType)" +
                    //                                                        "VALUES (2631, 2822, @MainGuid, @RelatedGuid, 2)", _dbConnection);
                    //                    sqlCommand.Parameters.AddWithValue("@MainGuid", _WeighingOrderTable.Rows[0].ItemArray[6].ToString());
                    //                    sqlCommand.Parameters.AddWithValue("@RelatedGuid", BinaryTable.Rows[0].ItemArray[1].ToString());
                    //                    sqlCommand.Parameters.AddWithValue("@ImageSize", image.Length);
                    //                    sqlCommand.ExecuteNonQuery();
                    //                    sqlCommand.Dispose();
                    //                }
                    //            }
                    //            ClearFields();
                    //        }
                    //    }
                    //}
                    //sqlCommand.Dispose(); 
                    //ClearFields();
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
        }

        private void ClearFields()
        {
            _weighingOrderCode = null;
            _shipmentState = "Shp_FirstWeighing";
            
            //txtWeighingOrderCode.Text = "";
            //sevenSegmentWeight.Value = "0";
            lblLoadedBranches.Text = "0";
            lblNetWeight.Text = "0";
            lblDiscrepency.Text = "0";
            _WeighingOrderTable.Clear();
            dgWeighingOrderDetail.DataSource = null;
            dgWeighingData.DataSource = null;
            dgShipmentDetail.DataSource = null;
            imgCamera1.Image = null;
            imgCamera2.Image = null;
            imgCamera3.Image = null;
            imgCamera4.Image = null;
        }

        private void btnWeighingOrderSearch_Click(object sender, EventArgs e)
        {
            if (_dbConnection == null || _dbConnection.State != ConnectionState.Open)
                ConnectDatabase();

            try
            {
                if (_dbConnection.State == ConnectionState.Open)
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT Code, Title, WeighingTypeCode FROM WMLog_WeighingOrder where "
                                    + "FormStatusCode='Wgh_Weighing' and "
                                    + "(POShipmentCode='" + txtWeighingOrderCode.Text 
                                    + "' OR SOShipmentCode='" + txtWeighingOrderCode.Text + "' "
                                    + "OR InvTransactionCode='" + txtWeighingOrderCode.Text 
                                    + "' OR Reference='" + txtWeighingOrderCode.Text
                                    + "' OR Code='" + txtWeighingOrderCode.Text + "')"
                                    , _dbConnection))
                    {
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        _WeighingOrderTable.Clear();
                        da.Fill(_WeighingOrderTable);
                        if (_WeighingOrderTable.Rows.Count > 1)
                        {
                            DataRow[] results = _WeighingOrderTable.Select("WeighingTypeCode='" +cmbWeighingTypes.SelectedValue+"'");
                            if (results.Count() > 1)
                                MessageBox.Show("بیش از یک شناسه توزین باز برای این کد وجود دارد. لطفاً با مدیر سیستم تماس بگیرید.", "خطا در سیستم توزین");
                            else
                                _weighingOrderCode = results[0].Field<string>("Code");
                        }
                        if (_WeighingOrderTable.Rows.Count == 1)
                        {
                            _weighingOrderCode = _WeighingOrderTable.Rows[0].Field<string>("Code");
                            cmbWeighingTypes.SelectedValue = _WeighingOrderTable.Rows[0].Field<string>("WeighingTypeCode");
                            cmbWeighingTypes_SelectedValueChanged(cmbWeighingTypes, new EventArgs());
                        }
                        else
                        {
                            ClearFields();
                            MessageBox.Show("شناسه توزین باز با کد مذکور در سیستم وجود ندارد. لطفاً با مدیر سیستم تماس بگیرید.", "خطا در سیستم توزین");
                            return;
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand(_weighingMasterCriteria.Replace("@Code", _weighingOrderCode)
                                    , _dbConnection))
                    {
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable weighingFieldsTable = new DataTable();
                        weighingFieldsTable.Clear();
                        da.Fill(weighingFieldsTable);
                        if (weighingFieldsTable.Rows.Count > 0)
                        {
                            dgWeighingData.DataSource = weighingFieldsTable;
                            dgWeighingData.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                            dgWeighingData.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        }
                        else
                        {
                            MessageBox.Show("دریافت اطلاعات تکمیلی توزین با خطا مواجه شد. لطفا با مدیر سیستم تماس بگیرید.", "خطا در سیستم توزین");
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand(_weighingDetailCriteria.Replace("@Code", _weighingOrderCode)
                                    , _dbConnection))
                    {
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        _WeighingDetail.Clear();
                        da.Fill(_WeighingDetail);
                        if (_WeighingDetail.Rows.Count > 0)
                        {
                            dgShipmentDetail.DataSource = _WeighingDetail;
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand(_weighingStatusCriteria.Replace("@Code", _weighingOrderCode)
                                    , _dbConnection))
                    {
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable weighingStatusTable = new DataTable();
                        weighingStatusTable.Clear();
                        da.Fill(weighingStatusTable);
                        if (weighingStatusTable.Rows.Count > 0)
                        {
                            dgWeighingStatus.DataSource = weighingStatusTable;
                            dgWeighingStatus.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                            dgWeighingStatus.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand("SELECT WMLog_WeighingOrderDetail.OperationSequence AS [ردیف], WMLog_WeighingOperation.Title AS [توزین], "
                                    + "SISys_FormStatus.Title AS[وضعیت], CONVERT(Decimal(10, 2), WMLog_WeighingOrderDetail.Weight) AS[وزن], dbo.MiladiTOShamsi(WeighingDateTime) AS[تاریخ توزین], "
                                    + "HREA_Personnel.Title AS[توزینکار], MRMA_Machine.Title AS[باسکول] "
                                    + "FROM WMLog_WeighingOrderDetail LEFT OUTER JOIN WMLog_WeighingOperation "
                                    + "ON WMLog_WeighingOrderDetail.OperationCode = WMLog_WeighingOperation.Code LEFT OUTER JOIN SISys_FormStatus "
                                    + "ON WMLog_WeighingOrderDetail.OperationStatusCode = SISys_FormStatus.Code LEFT OUTER JOIN MRMA_Machine "
                                    + "ON WMLog_WeighingOrderDetail.MachineCode = MRMA_Machine.Code LEFT OUTER JOIN HREA_Personnel "
                                    + "ON WMLog_WeighingOrderDetail.ResponsibleCode = HREA_Personnel.PersonnelCode "
                                    + "WHERE WeighingOrderCode='" + _weighingOrderCode + "'"
                                                            , _dbConnection))
                    {
                        DataTable weighingOrderDetailTable = new DataTable();
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        da.Fill(weighingOrderDetailTable);
                        if (weighingOrderDetailTable.Rows.Count > 0)
                        {
                            dgWeighingOrderDetail.DataSource = weighingOrderDetailTable;

                            var results = weighingOrderDetailTable.AsEnumerable().Count();
                        }
                        else
                        {
                            dgWeighingOrderDetail.DataSource = null;
                        }
                    }
                }
            }
            catch (Exception exp)
            {
                InvokeGuiThread(() =>
                {
                    DatabaseIndicator.Text = "غیرفعال";
                    DatabaseIndicator.ForeColor = Color.Red;
                });
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            Thread thread1 = new Thread(ConnectDatabase);
            Thread thread2 = new Thread(ConnectWeighingMachine);
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
                btnWeighingOrderSearch_Click(sender, e);
            }
        }

        private void بزرگنماییToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CameraViewer viewer = new CameraViewer();
            viewer.PreviewImage = ((PictureBox)((ContextMenuStrip)(((ToolStripMenuItem)sender).Owner)).SourceControl).Image;
            viewer.Show();
        }

        private void btnOpenWeighingList_Click(object sender, EventArgs e)
        {
            ShipmentListForm shipments = new ShipmentListForm();
            if(shipments.ShowDialog() == DialogResult.OK)
            {
                this.txtWeighingOrderCode.Text = shipments.shipmentCode;
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
                    else
                    {
                        lblWeighingBridgeCode.Text = item.Text;
                    }
                }
            }

            Globals.GetConfigurationDetails(Settings.Default.SelectedConfiguration);

            DisconnectWeighingMachine();
            CreateSerialPort();

            System.Threading.Thread thread2 = new System.Threading.Thread(ConnectWeighingMachine);
            thread2.Start();
        }

        private void cmbWeighingTypes_SelectedValueChanged(object sender, EventArgs e)
        {
            if (_dbConnection == null || _dbConnection.State != ConnectionState.Open)
                ConnectDatabase();

            try
            {
                if (_dbConnection.State == ConnectionState.Open)
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT MasterCriteria, DetailCriteria, StatusCriteria "
                            + "FROM WMLog_WeighingType WHERE Code='" + cmbWeighingTypes.SelectedValue + "'"
                                                            , _dbConnection))
                    {
                        DataTable weighingTypeDetailTable = new DataTable();
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        da.Fill(weighingTypeDetailTable);
                        if (weighingTypeDetailTable.Rows.Count > 0)
                        {
                            _weighingDetailCriteria = weighingTypeDetailTable.Rows[0].Field<string>("DetailCriteria");
                            _weighingMasterCriteria = weighingTypeDetailTable.Rows[0].Field<string>("MasterCriteria");
                            _weighingStatusCriteria = weighingTypeDetailTable.Rows[0].Field<string>("StatusCriteria");
                        }
                        else
                        {

                        }
                    }
                }
            }
            catch (Exception exp)
            {
                InvokeGuiThread(() =>
                {
                    DatabaseIndicator.Text = "غیرفعال";
                    DatabaseIndicator.ForeColor = Color.Red;
                });
            }
        }

        private void dgWeighingData_SelectionChanged(object sender, EventArgs e)
        {
            dgWeighingData.ClearSelection();
        }

        private void dgWeighingStatus_SelectionChanged(object sender, EventArgs e)
        {
            dgWeighingStatus.ClearSelection();
        }

        private void dgWeighingOrderDetail_SelectionChanged(object sender, EventArgs e)
        {
            dgWeighingOrderDetail.ClearSelection();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            ClearFields();
        }
    }
}
