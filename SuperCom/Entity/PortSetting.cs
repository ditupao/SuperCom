﻿using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperCom.Entity
{


    public enum FlowControls
    {
        None,
        Hardware,
        Software,
        Custom
    }

    public class PortSetting
    {

        public static int DEFAULT_BAUDRATE = 115200;
        public static int DEFAULT_DATABITS = 8;
        public static StopBits DEFAULT_STOPBITS = StopBits.One;
        public static Parity DEFAULT_PARITY = Parity.None;
        public static FlowControls DEFAULT_FLOWCONTROLS = FlowControls.None;

        public int BaudRate { get; set; }
        public int DataBits { get; set; }
        public StopBits Stopbits { get; set; }

        public Parity Parity { get; set; }
        public FlowControls FlowControls { get; set; }

        public static PortSetting GetDefaultSetting()
        {
            PortSetting portSetting = new PortSetting();
            portSetting.BaudRate = DEFAULT_BAUDRATE;
            portSetting.DataBits = DEFAULT_DATABITS;
            portSetting.Stopbits = DEFAULT_STOPBITS;
            portSetting.Parity = DEFAULT_PARITY;
            portSetting.FlowControls = DEFAULT_FLOWCONTROLS;
            return portSetting;
        }
    }
}
