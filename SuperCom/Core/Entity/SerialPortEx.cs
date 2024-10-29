﻿
using ITLDG.DataCheck;
using Microsoft.Win32;
using SuperCom.Core.Utils;
using SuperControls.Style;
using SuperUtils.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using static SuperCom.App;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace SuperCom.Entity
{
    public class SerialPortEx : SerialPort, INotifyPropertyChanged
    {
        private static readonly string DEFAULT_STOPBITS = StopBits.One.ToString();
        private static readonly string DEFAULT_PARITY = Parity.None.ToString();
        private const string COM_PATTERN = @"COM[0-9]+";
        private const string PORT_INFO_SELECT_SQL = "SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'";

        bool showEscCommand = false;
        bool hideDuplicateCRLF = true;
        bool useTXDAsDTR = false;

        [Browsable(true)]
        [DefaultValue(false)]
        [MonitoringDescription("ShowEscCommand")]
        public bool ShowEscCommand
        {
            get
            {
                return showEscCommand;
            }
            set
            {
                showEscCommand = value;
            }
        }

        [Browsable(true)]
        [DefaultValue(false)]
        [MonitoringDescription("HideDuplicateCRLF")]
        public bool HideDuplicateCRLF
        {
            get
            {
                return hideDuplicateCRLF;
            }
            set
            {
                hideDuplicateCRLF = value;
            }
        }

        [Browsable(false)]
        [DefaultValue(false)]
        [MonitoringDescription("UseTXDAsDTR")]
        public bool UseTXDAsDTR
        {
            get
            {
                return useTXDAsDTR;
            }
            set
            {
                useTXDAsDTR = value;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        #region "属性"

        private PortSetting _Setting = PortSetting.GetDefaultSetting();
        public PortSetting Setting {
            get { return _Setting; }
            set { _Setting = value; }
        }


        public string Remark { get; set; } = "";
        public bool Pinned { get; set; }
        public bool Hide { get; set; }

        public string SettingJson { get; set; } = "";

        private string _PortEncoding = PortSetting.DEFAULT_ENCODING_STRING;
        public string PortEncoding {
            get { return _PortEncoding; }
            set {
                _PortEncoding = value;
                RaisePropertyChanged();
                RefreshSetting();
            }
        }

        private string _StopBitsString = DEFAULT_STOPBITS;
        public string StopBitsString {
            get { return _StopBitsString; }
            set {
                _StopBitsString = value;
                RaisePropertyChanged();
                RefreshSetting();
            }
        }

        private string _ParityString = DEFAULT_PARITY;
        public string ParityString {
            get { return _ParityString; }
            set {
                _ParityString = value;
                RaisePropertyChanged();
                RefreshSetting();
            }
        }

        private double _TextFontSize = PortSetting.DEFAULT_FONTSIZE;
        public double TextFontSize {
            get { return _TextFontSize; }
            set { _TextFontSize = value; RaisePropertyChanged(); }
        }

        private long _HighLightIndex = 0;
        public long HighLightIndex {
            get { return _HighLightIndex; }
            set { _HighLightIndex = value; RaisePropertyChanged(); }
        }


        private DataCheck _DataCheck;
        public DataCheck DataCheck {
            get { return _DataCheck; }
            set {
                _DataCheck = value;
                RaisePropertyChanged();
            }
        }

        private long _FilterSelectedIndex = 0;
        public long FilterSelectedIndex {
            get { return _FilterSelectedIndex; }
            set { _FilterSelectedIndex = value; RaisePropertyChanged(); }
        }

        private long _ReadTimeoutValue = PortSetting.DEFAULT_READ_TIME_OUT;
        public long ReadTimeoutValue {
            get { return _ReadTimeoutValue; }
            set {
                _ReadTimeoutValue = value;
                RaisePropertyChanged();
                if (value >= PortSetting.MIN_TIME_OUT && value <= PortSetting.MAX_TIME_OUT)
                    this.ReadTimeout = (int)value;

            }
        }

        private long _WriteTimeoutValue = PortSetting.DEFAULT_WRITE_TIME_OUT;
        public long WriteTimeoutValue {
            get { return _WriteTimeoutValue; }
            set {
                _WriteTimeoutValue = value;
                RaisePropertyChanged();
                if (value >= PortSetting.MIN_TIME_OUT && value <= PortSetting.MAX_TIME_OUT)
                    this.WriteTimeout = (int)value;
            }
        }

        private int _SubcontractingTimeoutValue = PortSetting.DEFAULT_SUBCONTRACTING_TIME_OUT;
        public int SubcontractingTimeoutValue {
            get { return _SubcontractingTimeoutValue; }
            set {
                _SubcontractingTimeoutValue = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        public SerialPortEx()
        {
            Init();
        }

        public SerialPortEx(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
            : base(portName, baudRate, parity, dataBits, stopBits)
        {
            Init();
        }

        public SerialPortEx(string portName) : this()
        {
            this.PortName = portName;
            DataCheck = new DataCheck();
            RefreshSetting();
        }

        public void Init()
        {
            Remark = "";
        }

        /// <summary>
        /// 仅有 COM[NUMBER]
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> GetAllPorts()
        {
            Logger.Info($"get all ports:");
            string[] portNames = new string[0];
            Dictionary<string, string> dict = new Dictionary<string, string>();
            try {
                portNames = GetPortNames();
                using (var searcher = new ManagementObjectSearcher(PORT_INFO_SELECT_SQL)) {
                    var ports = searcher.Get().Cast<ManagementBaseObject>().ToList().Select(p => p["Caption"].ToString());
                    var portList = portNames.Select(n => n + " - " + ports.FirstOrDefault(s => s.EndsWith(n+")"))).ToList();
                    foreach (string detail in portList) {
                        Logger.Info(detail);
                        if (Regex.Match(detail, COM_PATTERN) is Match match && match.Success && match.Groups != null &&
                            match.Value is string portName && !string.IsNullOrEmpty(portName) &&
                            !dict.ContainsKey(portName)) {
                            dict.Add(portName, detail);
                        }
                    }
                }
            } catch (Exception ex) {
                Logger.Error(ex);
                // 使用默认的
                try {
                    foreach (var item in portNames) {
                        if (int.TryParse(item.ToUpper().Replace("COM", ""), out _) && !dict.ContainsKey(item)) {
                            Logger.Info(item);
                            dict.Add(item, item);
                        }
                    }
                } catch (Exception e) {
                    Logger.Error(e);
                }
            }
            return dict;
        }

        public static string[] GetUsbSerDevices()
        {
            // HKLM\SYSTEM\CurrentControlSet\services\usbser\Enum -> Device IDs of what is plugged in
            // HKLM\SYSTEM\CurrentControlSet\Enum\{Device_ID}\Device Parameters\PortName -> COM port name.

            List<string> ports = new List<string>();

            RegistryKey k1 = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\services\usbser\Enum");
            if (k1 == null) {
                Debug.Fail("Unable to open Enum key");
            } else {
                int count = (int)k1.GetValue("Count");
                for (int i = 0; i < count; i++) {
                    object deviceID = k1.GetValue(i.ToString("D", CultureInfo.InvariantCulture));
                    Debug.Assert(deviceID != null && deviceID is string);
                    RegistryKey k2 = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + (string)deviceID + @"\Device Parameters");
                    if (k2 == null) {
                        continue;
                    }
                    object portName = k2.GetValue("PortName");
                    Debug.Assert(portName != null && portName is string);
                    ports.Add((string)portName);
                }
            }
            return ports.ToArray();
        }


        public void SaveRemark(string remark)
        {
            this.Remark = remark;
            SettingJson = PortSettingToJson(); // 保存
        }
        public void SavePinned(bool pinned)
        {
            this.Pinned = pinned;
            SettingJson = PortSettingToJson(); // 保存
        }

        public void SaveDataCheck()
        {
            SettingJson = PortSettingToJson(); // 保存
        }


        public void SaveProperties()
        {
            this.Encoding = GetEncoding();
            this.StopBits = GetStopBits();
            this.Parity = GetParity();
        }

        public void RefreshSetting()
        {
            try {
                SaveProperties();
                SettingJson = PortSettingToJson(); // 保存
            } catch (Exception ex) {
                MessageCard.Error(ex.Message);
            }
        }


        public string PortSettingToJson()
        {
            Dictionary<string, object> dic =
                new Dictionary<string, object>
            {
                { "BaudRate", this.BaudRate },
                { "DataBits", this.DataBits },
                { "Encoding", this.Encoding.HeaderName },
                { "StopBits", this.StopBitsString },
                { "Parity", this.ParityString },
                { "DTR", this.DtrEnable },
                { "Handshake", this.Handshake },
                { "ReadTimeout", this.ReadTimeoutValue },
                { "WriteTimeout", this.WriteTimeoutValue },
                { "SubcontractingTimeout", this.SubcontractingTimeoutValue },
                { "DiscardNull", this.DiscardNull },
                { "Remark", this.Remark },
                { "Pinned", this.Pinned },
                { "Hide", this.Hide },
                { "TextFontSize", this.TextFontSize },
                { "HighLightIndex", this.HighLightIndex },
                { "DataCheck", JsonUtils.TrySerializeObject(this.DataCheck) },
                { "ShowEscCommand", this.ShowEscCommand },
                { "HideDuplicateCRLF", this.HideDuplicateCRLF },
                { "UseTXDAsDTR" , this.UseTXDAsDTR }
            };
            if (this.Handshake == Handshake.RequestToSend || this.Handshake == Handshake.RequestToSendXOnXOff) {
                // Handshake 设置为 RequestToSend 或 RequestToSendXOnXOff，则无法访问 RtsEnable
            } else {
                dic.Add("RTS", this.RtsEnable);
            }
            //dic.Add("BreakState", this.BreakState);
            return JsonUtils.TrySerializeObject(dic);
        }

        public void SetPortSettingByJson(string json)
        {
            Dictionary<string, object> dict = JsonUtils.TryDeserializeObject<Dictionary<string, object>>(json);
            if (dict != null) {
                this.BaudRate = dict.GetInt("BaudRate", PortSetting.DEFAULT_BAUDRATE);
                this.DataBits = dict.GetInt("DataBits", PortSetting.DEFAULT_DATABITS);
                this.TextFontSize = dict.GetDouble("TextFontSize", PortSetting.DEFAULT_FONTSIZE);
                this.HighLightIndex = dict.GetLong("HighLightIndex", 0);
                this.PortEncoding = dict.GetString("Encoding", PortSetting.DEFAULT_ENCODING.ToString());
                this.ParityString = dict.GetString("Parity", PortSetting.DEFAULT_PARITY.ToString());
                this.StopBitsString = dict.GetString("StopBits", PortSetting.DEFAULT_STOPBITS.ToString());
                this.DtrEnable = dict.GetBool("DTR", PortSetting.DEFAULT_DTR);
                this.RtsEnable = dict.GetBool("RTS", PortSetting.DEFAULT_RTS);
                // this.BreakState = dict.GetBool("BreakState", false);
                this.DiscardNull = dict.GetBool("DiscardNull", false);

                this.ReadTimeoutValue = dict.GetInt("ReadTimeout", PortSetting.DEFAULT_READ_TIME_OUT);
                this.WriteTimeoutValue = dict.GetInt("WriteTimeout", PortSetting.DEFAULT_WRITE_TIME_OUT);
                this.SubcontractingTimeoutValue = dict.GetInt("SubcontractingTimeout", PortSetting.DEFAULT_SUBCONTRACTING_TIME_OUT);

                if (dict.ContainsKey("Handshake") && Enum.TryParse(dict["Handshake"].ToString(), out Handshake Handshake))
                    this.Handshake = Handshake;

                this.Remark = dict.GetString("Remark", "");
                this.Pinned = dict.GetBool("Pinned", false);
                this.Hide = dict.GetBool("Hide", false);
                this.DataCheck = DataCheck.FromJson(dict.GetString("DataCheck", ""));

                this.ShowEscCommand = dict.GetBool("ShowEscCommand", false);
                this.HideDuplicateCRLF = dict.GetBool("HideDuplicateCRLF", true);
                this.UseTXDAsDTR = dict.GetBool("UseTXDAsDTR", false);
            }
        }

        public static string GetRemark(string json)
        {
            Dictionary<string, object> dict = JsonUtils.TryDeserializeObject<Dictionary<string, object>>(json);
            if (dict != null) {
                if (dict.ContainsKey("Remark") && dict.Get("Remark", "") is object remark)
                    return remark.ToString();
            }
            return "";
        }

        public static bool GetHide(string json)
        {
            Dictionary<string, object> dict = JsonUtils.TryDeserializeObject<Dictionary<string, object>>(json);
            string status = "";
            if (dict != null) {
                if (dict.ContainsKey("Hide"))
                    status = dict["Hide"].ToString();
            }
            return status.ToLower().Equals("true") ? true : false;
        }

        public static bool GetPinned(string json)
        {
            Dictionary<string, object> dict = JsonUtils.TryDeserializeObject<Dictionary<string, object>>(json);
            string status = "";
            if (dict != null) {
                if (dict.ContainsKey("Pinned"))
                    status = dict["Pinned"].ToString();
            }
            return status.ToLower().Equals("true");
        }

        public Encoding GetEncoding()
        {
            try {
                if (PortEncoding.ToUpper().Equals("UTF8"))
                    return System.Text.Encoding.UTF8;
                Encoding encoding = System.Text.Encoding.GetEncoding(PortEncoding);
                return encoding;
            } catch (Exception ex) {
                MessageCard.Error(ex.Message);
                return System.Text.Encoding.UTF8;
            }
        }

        public StopBits GetStopBits()
        {
            Enum.TryParse<StopBits>(StopBitsString, out StopBits result);
            return result;
        }
        public Parity GetParity()
        {
            Enum.TryParse<Parity>(ParityString, out Parity result);
            return result;
        }

        public void PrintSetting()
        {
            string data = PortSettingToJson();
            Logger.Info(data);
        }

        public override bool Equals(object obj)
        {
            if (obj != null && obj is SerialPortEx serialPort) {
                return serialPort.PortName.Equals(PortName);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return PortName.GetHashCode();
        }

        public void RestoreDefault()
        {
            this.DtrEnable = PortSetting.DEFAULT_DTR;
            this.RtsEnable = PortSetting.DEFAULT_RTS;
            this.DiscardNull = PortSetting.DEFAULT_DISCARD_NULL;
            this.ReadTimeout = PortSetting.DEFAULT_READ_TIME_OUT;
            this.WriteTimeout = PortSetting.DEFAULT_WRITE_TIME_OUT;
            this.Handshake = PortSetting.DEFAULT_HANDSHAKE;
            this.StopBits = PortSetting.DEFAULT_STOPBITS;
            this.Parity = PortSetting.DEFAULT_PARITY;
            this.Encoding = PortSetting.DEFAULT_ENCODING;
            this.ShowEscCommand = false;
            this.HideDuplicateCRLF = true;
            this.UseTXDAsDTR = false;
        }
    }
}
