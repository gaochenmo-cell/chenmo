using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.Windows.Forms;

namespace ConfigAppNS
{
    class SerialCom
    {
        private     SerialPort  mySerialPort;
        public      bool        SerialStatus = false;
        public      bool        bNewRxFlag;
        public      int         RxLength = 0;
        //public    string      SerialRxStr;
        public      byte[]      RxdFIFObuffer = new byte[1024];
        public      byte[]      SerialTempbuffer = new byte[1024];
        public      byte[]      Serialbuffer  = new byte[1024];
        public      byte[]      Serialbuffer1 = new byte[1024];
        public      byte[]      Serialbuffer2 = new byte[1024];
        public      byte[]      Serialbuffer3 = new byte[1024];
        public      bool        RxFlag1 = false;
        public      bool        RxFlag2 = false;
        public      bool        RxFlag3 = false;
        public      int         RxLen1 = 0;
        public      int         RxLen2 = 0;
        public      int         RxLen3 = 0;
        public      int         RxdNose = 0, RxdTail = 0;
        /// =============================================
        /// ====  Open Serial Port  ====
        /// =============================================
        public bool OpenSerialPort(string ComNumber,string BaudRateStr)
        {
            try
            {
                //---------------------------------------------------------------------------------------
                if (SerialStatus == false)
                {
                    string[] S = BaudRateStr.Split('-');
                    //----------------------------------------------------- Set Com Number
                    mySerialPort = new SerialPort(ComNumber);
                    //----------------------------------------------------- Set BuadRate
                    UInt32 BaudRate = Convert.ToUInt32(S[0], 10);
                    mySerialPort.BaudRate = (int)BaudRate;
                    //----------------------------------------------------- Set Parity Bit
                    switch (S[1])
                    {
                        case "E": mySerialPort.Parity = Parity.Even; break;
                        case "M": mySerialPort.Parity = Parity.Mark; break;
                        case "N": mySerialPort.Parity = Parity.None; break;
                        case "O": mySerialPort.Parity = Parity.Odd; break;
                        case "S": mySerialPort.Parity = Parity.Space; break;
                        default: mySerialPort.Parity = Parity.None; break;
                    }
                    //----------------------------------------------------- Set Data Bit
                    mySerialPort.DataBits = Convert.ToByte(S[2], 10);
                    //----------------------------------------------------- Set Stop Bit
                    switch (S[3])
                    {
                        case "0": mySerialPort.StopBits = StopBits.None; break;
                        case "1": mySerialPort.StopBits = StopBits.One; break;
                        case "1.5": mySerialPort.StopBits = StopBits.OnePointFive; break;
                        case "2": mySerialPort.StopBits = StopBits.Two; break;
                        default: mySerialPort.StopBits = StopBits.One; break;
                    }
                    //-----------------------------------------------------
                    mySerialPort.Handshake = Handshake.None;

                    mySerialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceviedHandler);

                    mySerialPort.Open();
                    SerialStatus = true;

                    //Console.WriteLine("Press any key to continue...");
                    //Console.WriteLine();
                    //Console.ReadKey();
                    //mySerialPort.Close();
                }
                //---------------------------------------------------------------------------------------     
       
            }
            catch (Exception ex)
            {
                MessageBox.Show("Open ComPort Failed \r\n"+ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }
        /// =============================================
        /// ====  Close Serial Port  ====
        /// =============================================
        public void CloseSerialPort()
        {
            try
            {
                if (SerialStatus == true)
                {
                    mySerialPort.Close();
                    SerialStatus = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Close ComPort Failed \r\n" + ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        /// =============================================
        /// ====  Recevie Handler  ====
        /// =============================================
        public void DataReceviedHandler(object sender,SerialDataReceivedEventArgs e)
        {
            try
            {
                //SerialRxStr = mySerialPort.ReadExisting();
                //bNewRxFlag = true;
                //Thread.Sleep(10);
                int len = mySerialPort.Read(RxdFIFObuffer, 0, 1024);

                if (RxFlag1 == false)
                {
                    for (int i = 0; i < len; i++)
                    {
                        Serialbuffer1[i] = RxdFIFObuffer[i];
                    }
                    RxLen1  = len;
                    RxFlag1 = true;
                }
                else if (RxFlag2 == false)
                {
                    for (int i = 0; i < len; i++)
                    {
                        Serialbuffer2[i] = RxdFIFObuffer[i];
                    }
                    RxLen2 = len;
                    RxFlag2 = true;
                }
                else if (RxFlag3 == false)
                {
                    for (int i = 0; i < len; i++)
                    {
                        Serialbuffer3[i] = RxdFIFObuffer[i];
                    }
                    RxLen3 = len;
                    RxFlag3 = true;
                }
                //RxLength   = len;
                bNewRxFlag = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("ComPort DataReceviedHandler Error \r\n" + ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            /*if ((RxdFIFObuffer[i] == (byte)'A')&&(RxdFIFObuffer[i+1] == (byte)'A'))
{
    _RxdNose = 0;
    _RxdTail = 0;
}
else if ((RxdFIFObuffer[i] == (byte)'&') && (RxdFIFObuffer[i + 1] == (byte)'&'))
{
    int L = 0;
    byte[] _RxdBuffer = new byte[512];
    for (L = 0; _RxdNose != _RxdTail; L++)
    {
        Serialbuffer[L] = SerialTempbuffer[_RxdTail++];
    }
    //ProcessRxdConfigCommand(Serialbuffer, L);
    bNewRxFlag = true;
    RxLength = L;
    _RxdNose = 0; _RxdTail = 0;
    break;
}
else
{
    SerialTempbuffer[_RxdNose++] = RxdFIFObuffer[i];
}*/

            /*SerialRxStr = mySerialPort.ReadExisting();
            //bNewRxFlag = true;
            Thread.Sleep(10);
            int len = mySerialPort.Read(RxdFIFObuffer, 0, 1024);

            for (int i = 0; i < len; i++)
            {
                Serialbuffer[RxdNose++] = RxdFIFObuffer[i];

                /*if ((RxdFIFObuffer[i] == (byte)'A')&&(RxdFIFObuffer[i+1] == (byte)'A'))
                {
                    _RxdNose = 0;
                    _RxdTail = 0;
                }
                else if ((RxdFIFObuffer[i] == (byte)'&') && (RxdFIFObuffer[i + 1] == (byte)'&'))
                {
                    int L = 0;
                    byte[] _RxdBuffer = new byte[512];
                    for (L = 0; _RxdNose != _RxdTail; L++)
                    {
                        Serialbuffer[L] = SerialTempbuffer[_RxdTail++];
                    }
                    //ProcessRxdConfigCommand(Serialbuffer, L);
                    bNewRxFlag = true;
                    RxLength = L;
                    _RxdNose = 0; _RxdTail = 0;
                    break;
                }
                else
                {
                    SerialTempbuffer[_RxdNose++] = RxdFIFObuffer[i];
                }
            }
            RxLength = len;
            bNewRxFlag  = true;
                */


            

            /*
            try
            {
                //StringBuilder currentline = new StringBuilder();
                int point=0;
                while (mySerialPort.BytesToRead > 0)   //循环接收数据
                {
                    //char ch = (char)mySerialPort.ReadByte();
                    //currentline.Append(ch);

                    Serialbuffer[point++] = (byte)mySerialPort.ReadByte();

                }
                //在这里对接收到的数据进行处理
                //
                //SerialRxStr = currentline.ToString();
                RxLength   = point;
                bNewRxFlag = true;
                //currentline = new StringBuilder();
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }*/



        }
        /// =============================================
        /// ==== Write Serial Port Char   ====
        /// =============================================
        public void WriteSerialPort(string WData)
        {
            try
            {
                mySerialPort.Write(WData);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Write ComPort Failed \r\n" + ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// =============================================
        /// ====  Write Serial Port Hex  ====
        /// =============================================
        public void WriteSerialPortHex(byte[] buf,int Offset,int Len)
        {
            try
            {
                mySerialPort.Write(buf,Offset,Len);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Write ComPort Failed \r\n" + ex.Message, "Error Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}
