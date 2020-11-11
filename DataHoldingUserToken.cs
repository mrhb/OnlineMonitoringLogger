﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Text; //for testing
using System.Net;
using System.Collections.Generic;
using System.Linq;
using InfluxDB.Collector;

namespace SocketAsyncServer
{
    class DataHoldingUserToken
    {
        static readonly Dictionary<int, string> AMF25Registers = new Dictionary<int, string>();
        static readonly Dictionary<int, string> minitRegisters = new Dictionary<int, string>();
        static readonly Dictionary<int, string> ClassicRegisters = new Dictionary<int, string>();
        static readonly List<UnitData> ValidUnits = new List<UnitData>();


    internal List<ReqSection> DefinedSections {
            get
            {
                switch (Type)
                {
                    case "Classic":
                        return new List<ReqSection>() {
                        new ReqSection(){
                            startingAddress = 1 ,
                            quantity = 34,
                        },
                    };
                    case "AMF25":
                        return new List<ReqSection>() {
                        new ReqSection(){
                            startingAddress = 0 ,
                            quantity = 50,
                        },
                        new ReqSection(){
                            startingAddress =118 ,
                            quantity =1,
                        },
                            new ReqSection(){
                            startingAddress =182 ,
                            quantity =16,
                        },
                        //new ReqSection(){
                        //    startingAddress =3000 ,
                        //    quantity = 100,
                        //},
                        //new ReqSection(){
                        //    startingAddress =3000 ,
                        //    quantity = 200,
                        //}
                    };
                    case "minit":
                        return new List<ReqSection>() {
                        new ReqSection(){
                            startingAddress =1 ,
                            quantity = 20,
                        },
                        //new ReqSection(){
                        //    startingAddress =58 ,
                        //    quantity = 5,
                        //},
                        //new ReqSection(){
                        //    startingAddress =3000 ,
                        //    quantity = 10,
                        //}
                    };
                    default:
                        throw new ArgumentException("Not Type Defined");

                };
            }
        }



        static DataHoldingUserToken()
            {
                //*******InfluxDb Init*********************
                try
                {
                    Metrics.Collector = new CollectorConfiguration()

                             .Tag.With("Company", "TetaPower")

                             .Batch.AtInterval(TimeSpan.FromSeconds(5))
                             .WriteTo.InfluxDB("http://localhost:8086", "telegraf")
                             //.WriteTo.InfluxDB("udp://localhost:8089", "data")
                             .CreateCollector();
                }
                catch(Exception c)
                {
                    Console.WriteLine("Error in initializing InfluxDb:  "+c.Message);
                }


                //*************** Read Modbus Addresses***************
            try
            {
                var AMF25Lines = File.ReadLines("Resource\\ModbusAddress_AMF25.csv").Select(a => a.Split(','));
                foreach (var item in AMF25Lines)
                {
                    AMF25Registers.Add(int.Parse(item[0]), item[1].Trim());
                }
                var minitLines = File.ReadLines("Resource\\ModbusAddress_minit.csv").Select(a => a.Split(','));
                foreach (var item in minitLines)

                {
                    minitRegisters.Add(int.Parse(item[0]), item[1].Trim());
                }



                var ClassicLines = File.ReadLines("Resource\\ModbusAddress_Classic.csv").Select(a => a.Split(','));
                foreach (var item in ClassicLines)
                {
                    ClassicRegisters.Add(int.Parse(item[0]), item[1].Trim());
                }

                //*************Read Vali Units***************
                var ComApUnits = File.ReadLines("Resource\\ValidComApUnits.csv").Select(a => a.Split(','));
                foreach (var item in ComApUnits)
                {
                    ValidUnits.Add(
                        new UnitData() {
                            Type = item[0].Trim(),
                            Id = int.Parse(item[1]),
                            RemoteIp = IPAddress.Parse(item[2]),
                            LocalPort = int.Parse(item[3])
                        }
                        );
                }

            }
            catch (Exception c)
            {

            }
        }
        public  void Logg()
        {
            var starttime = DateTime.Now;
            //*******InfluxDb ********
            var tags = new Dictionary<string, string>() {
                   { "Company", "TetaPower" },
                { "UnitId",UnitId.ToString() },
            };
           Metrics.Write("ModbusLogger", datas, tags);

            lastUpdateTime = DateTime.Now;
            Thread.Sleep(3000);
            Console.WriteLine(TokenId + " data Logged at " + DateTime.Now.ToString()+ "  in "+ DateTime.Now.Subtract(starttime).Milliseconds.ToString() + " Milliseconds");

        }


        private DateTime lastUpdateTime=DateTime.Now;
        //elapsed Time From Last Update Time
        public int elapsedTime
        {
            get
            {
                return DateTime.Now.Subtract(lastUpdateTime).Seconds;
            }
        }
        internal Mediator theMediator;
        internal DataHolder theDataHolder;

        internal Int32 socketHandleNumber;

        internal readonly Int32 bufferOffsetReceive;
        internal readonly Int32 permanentReceiveMessageOffset;
        internal readonly Int32 bufferOffsetSend;
        
        private Int32 idOfThisObject; //for testing only        
               
        internal Int32 lengthOfCurrentIncomingMessage;
        
        //receiveMessageOffset is used to mark the byte position where the message
        //begins in the receive buffer. This value can sometimes be out of
        //bounds for the data stream just received. But, if it is out of bounds, the 
        //code will not access it.
        internal Int32 receiveMessageOffset;        
        internal Byte[] byteArrayForPrefix;        
        internal readonly Int32 receivePrefixLength;
        internal Int32 receivedPrefixBytesDoneCount = 0;
        internal Int32 receivedMessageBytesDoneCount = 0;
        //This variable will be needed to calculate the value of the
        //receiveMessageOffset variable in one situation. Notice that the
        //name is similar but the usage is different from the variable
        //receiveSendToken.receivePrefixBytesDone.
        internal Int32 recPrefixBytesDoneThisOp = 0;

        internal Int32 sendBytesRemainingCount;
        internal readonly Int32 sendPrefixLength;
        internal Byte[] dataToSend;
        internal Int32 bytesSentAlreadyCount;

        //The session ID correlates with all the data sent in a connected session.
        //It is different from the transmission ID in the DataHolder, which relates
        //to one TCP message. A connected session could have many messages, if you
        //set up your app to allow it.
        private Int32 sessionId;

        //*****************

        public DataHoldingUserToken(SocketAsyncEventArgs e, Int32 rOffset, Int32 sOffset, Int32 receivePrefixLength, Int32 sendPrefixLength, Int32 identifier)
        {
            this.idOfThisObject = identifier;
           
            //Create a Mediator that has a reference to the SAEA object.
            this.theMediator = new Mediator(e);
            this.bufferOffsetReceive = rOffset;
            this.bufferOffsetSend = sOffset;
            this.receivePrefixLength = receivePrefixLength;
            this.sendPrefixLength = sendPrefixLength;
            this.receiveMessageOffset = rOffset + receivePrefixLength;
            this.permanentReceiveMessageOffset = this.receiveMessageOffset;            
        }

        class UnitData{
            public int Id;
            public string Type;
            public IPAddress RemoteIp;
            public int LocalPort;
        }
        internal struct ReqSection
        {
          internal  int startingAddress;
          internal int quantity;
          internal int EndAddress {
                get {
                    return startingAddress+quantity;
                }
            }
         }



        string Type="";
        public void DetectType(SocketAsyncEventArgs e)
        {
            Type = "Classic";
            unitId = null;
            this.theMediator = new Mediator(e);

           

            var matched = ValidUnits.FirstOrDefault(u => u.RemoteIp.Equals( theMediator.GetRemoteIp()) &  (u.LocalPort.Equals( theMediator.GetLocalPort())));

            if (matched != null)
            {
                Type = matched.Type;
                unitId = matched.Id;
            }


            Reset();
            }

        List<ReqSection> CurrentSections = new List<ReqSection>();
        byte[] request;
        public byte[] prepareRequest()
        {
            transactionIdentifierInternal++;
            if (UnitId == null)
            {
                request = RequestId(transactionIdentifierInternal);
            }
            else
            {
                request = RequestData((byte)UnitId, transactionIdentifierInternal);
            }
            return request;
        }

        public Byte[] RequestId(uint transactionIdentifierInternal)
        {
            int int_startingAddress = 0;
            int int_quantity = 0;

            byte[] transactionIdentifier = new byte[2];
            byte[] protocolIdentifier = new byte[2];
            byte[] crc = new byte[2];
            byte[] length = new byte[2];
            byte[] startingAddress = new byte[2];
            byte[] quantity = new byte[2];

           
            transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            protocolIdentifier = BitConverter.GetBytes((int)0x0000);
            length = BitConverter.GetBytes((int)0x0002);
            byte functionCode = 0x11;
            startingAddress = BitConverter.GetBytes(int_startingAddress);
            quantity = BitConverter.GetBytes(int_quantity);

            Byte[] data = new byte[]{
                                    transactionIdentifier[1],
                                    transactionIdentifier[0],
                                    protocolIdentifier[1],
                                    protocolIdentifier[0],
                                    length[1],
                                    length[0],
                                    0,//this.unitIdentifier,
                                    functionCode,
                                    //this.startingAddress[1],
                                    //this.startingAddress[0],
                                    //this.quantity[1],
                                    //this.quantity[0],
                                    //this.crc[0],
                                    //this.crc[1]
                    };



            return data;
        }

        public Byte[] RequestData(byte unitIdentifier, uint transactionIdentifierInternal)
        {
            ReqSection currentSection;
            currentSection = CurrentSections.First();
            switch (Type)
            {
                case "Classic":
                    return modbusRTUoverTCP_ReadHoldingRegister(currentSection);
                case "AMF25":
                    return modbusTCP_ReadHoldingRegister(currentSection);
                case "minit":
                    return modbusTCP_ReadHoldingRegister(currentSection);
                default:
                    throw new ArgumentException("Not Type Defined");
            }
        }

        Byte[] modbusTCP_ReadHoldingRegister(ReqSection currentSection)
        {


            int int_startingAddress = currentSection.startingAddress;
            int int_quantity = currentSection.quantity;

            byte[] transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            byte[] protocolIdentifier = new byte[2];
            byte[] crc = new byte[2];
            byte[] length = new byte[2];
            byte[] startingAddress = BitConverter.GetBytes(int_startingAddress);
            byte[] quantity = BitConverter.GetBytes(int_quantity);


            transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            protocolIdentifier = BitConverter.GetBytes((int)0x0000);
            length = BitConverter.GetBytes((int)0x0002);
            byte functionCode = 0x03;
            startingAddress = BitConverter.GetBytes(int_startingAddress);
            quantity = BitConverter.GetBytes(int_quantity);

            Byte[] data = new byte[]{
                    //              transactionIdentifier[1],
                    //              transactionIdentifier[0],
                    //              protocolIdentifier[1],
                    //              protocolIdentifier[0],
                    //              length[1],
                    //              length[0],
                                     (byte)unitId,
                                    functionCode,
                                    startingAddress[1],
                                    startingAddress[0],
                                    quantity[1],
                                    quantity[0],
                                    crc[0],
                                    crc[1]
                    };
            crc = BitConverter.GetBytes(calculateCRC(data, 6, 0));
            data[6] = crc[0];
            data[7] = crc[1];




            return data;
        }
        Byte[] modbusRTUoverTCP_ReadHoldingRegister(ReqSection currentSection)
        {
            int int_startingAddress = currentSection.startingAddress;
            int int_quantity = currentSection.quantity;

            byte[] transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            byte[] protocolIdentifier = new byte[2];
            byte[] crc = new byte[2];
            byte[] length = new byte[2];
            byte[] startingAddress = BitConverter.GetBytes(int_startingAddress);
            byte[] quantity = BitConverter.GetBytes(int_quantity);


            transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
            protocolIdentifier = BitConverter.GetBytes((int)0x0000);
            length = BitConverter.GetBytes((int)0x0002);
            byte functionCode = 0x03;
            startingAddress = BitConverter.GetBytes(int_startingAddress);
            quantity = BitConverter.GetBytes(int_quantity);

            Byte[] data = new byte[]{
                transactionIdentifier[1],
                            transactionIdentifier[0],
                            protocolIdentifier[1],
                            protocolIdentifier[0],
                            length[1],
                            length[0],
                            (byte)unitId,
                            functionCode,
                            startingAddress[1],
                            startingAddress[0],
                            quantity[1],
                            quantity[0],
                            crc[0],
                            crc[1]
            };
            crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
            data[12] = crc[0];
            data[13] = crc[1];






            return data;
        }
        public static UInt16 calculateCRC(byte[] data, UInt16 numberOfBytes, int startByte)
        {
            byte[] auchCRCHi = {
                    0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
                    0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                    0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01,
                    0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
                    0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81,
                    0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
                    0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01,
                    0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
                    0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
                    0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                    0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01,
                    0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                    0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
                    0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                    0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01,
                    0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                    0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
                    0x40
                    };

            byte[] auchCRCLo = {
                    0x00, 0xC0, 0xC1, 0x01, 0xC3, 0x03, 0x02, 0xC2, 0xC6, 0x06, 0x07, 0xC7, 0x05, 0xC5, 0xC4,
                    0x04, 0xCC, 0x0C, 0x0D, 0xCD, 0x0F, 0xCF, 0xCE, 0x0E, 0x0A, 0xCA, 0xCB, 0x0B, 0xC9, 0x09,
                    0x08, 0xC8, 0xD8, 0x18, 0x19, 0xD9, 0x1B, 0xDB, 0xDA, 0x1A, 0x1E, 0xDE, 0xDF, 0x1F, 0xDD,
                    0x1D, 0x1C, 0xDC, 0x14, 0xD4, 0xD5, 0x15, 0xD7, 0x17, 0x16, 0xD6, 0xD2, 0x12, 0x13, 0xD3,
                    0x11, 0xD1, 0xD0, 0x10, 0xF0, 0x30, 0x31, 0xF1, 0x33, 0xF3, 0xF2, 0x32, 0x36, 0xF6, 0xF7,
                    0x37, 0xF5, 0x35, 0x34, 0xF4, 0x3C, 0xFC, 0xFD, 0x3D, 0xFF, 0x3F, 0x3E, 0xFE, 0xFA, 0x3A,
                    0x3B, 0xFB, 0x39, 0xF9, 0xF8, 0x38, 0x28, 0xE8, 0xE9, 0x29, 0xEB, 0x2B, 0x2A, 0xEA, 0xEE,
                    0x2E, 0x2F, 0xEF, 0x2D, 0xED, 0xEC, 0x2C, 0xE4, 0x24, 0x25, 0xE5, 0x27, 0xE7, 0xE6, 0x26,
                    0x22, 0xE2, 0xE3, 0x23, 0xE1, 0x21, 0x20, 0xE0, 0xA0, 0x60, 0x61, 0xA1, 0x63, 0xA3, 0xA2,
                    0x62, 0x66, 0xA6, 0xA7, 0x67, 0xA5, 0x65, 0x64, 0xA4, 0x6C, 0xAC, 0xAD, 0x6D, 0xAF, 0x6F,
                    0x6E, 0xAE, 0xAA, 0x6A, 0x6B, 0xAB, 0x69, 0xA9, 0xA8, 0x68, 0x78, 0xB8, 0xB9, 0x79, 0xBB,
                    0x7B, 0x7A, 0xBA, 0xBE, 0x7E, 0x7F, 0xBF, 0x7D, 0xBD, 0xBC, 0x7C, 0xB4, 0x74, 0x75, 0xB5,
                    0x77, 0xB7, 0xB6, 0x76, 0x72, 0xB2, 0xB3, 0x73, 0xB1, 0x71, 0x70, 0xB0, 0x50, 0x90, 0x91,
                    0x51, 0x93, 0x53, 0x52, 0x92, 0x96, 0x56, 0x57, 0x97, 0x55, 0x95, 0x94, 0x54, 0x9C, 0x5C,
                    0x5D, 0x9D, 0x5F, 0x9F, 0x9E, 0x5E, 0x5A, 0x9A, 0x9B, 0x5B, 0x99, 0x59, 0x58, 0x98, 0x88,
                    0x48, 0x49, 0x89, 0x4B, 0x8B, 0x8A, 0x4A, 0x4E, 0x8E, 0x8F, 0x4F, 0x8D, 0x4D, 0x4C, 0x8C,
                    0x44, 0x84, 0x85, 0x45, 0x87, 0x47, 0x46, 0x86, 0x82, 0x42, 0x43, 0x83, 0x41, 0x81, 0x80,
                    0x40
                    };
            UInt16 usDataLen = numberOfBytes;
            byte uchCRCHi = 0xFF;
            byte uchCRCLo = 0xFF;
            int i = 0;
            int uIndex;
            while (usDataLen > 0)
            {
                usDataLen--;
                if ((i + startByte) < data.Length)
                {
                    uIndex = uchCRCLo ^ data[i + startByte];
                    uchCRCLo = (byte)(uchCRCHi ^ auchCRCHi[uIndex]);
                    uchCRCHi = auchCRCLo[uIndex];
                }
                i++;
            }
            return (UInt16)((UInt16)uchCRCHi << 8 | uchCRCLo);
        }
        public void ProcessResponseData(byte[] ResponseData) {

            if (UnitId == null)
            {
                ReqSection currentSection;

                currentSection = CurrentSections.First();

                if (ResponseData.Count()!=8)
                {
                    Console.WriteLine("Error in resived data length of Id Request");
                    Reset();
                    return;
                }
                int[] bytesAsInts = Array.ConvertAll(ResponseData, c => (int)c);
                UnitId = bytesAsInts[6];
            }
            else
            {
                switch (Type)
                {
                    case "Classic":
                        ModbusRTUoverTCP_ExtractHoldingRegister(ResponseData);
                        break;
                    case "AMF25":
                        ModbusTCP_ExtractHoldingRegister(ResponseData);
                        break;
                    case "minit":
                        ModbusTCP_ExtractHoldingRegister(ResponseData);
                        break;
                    default:
                        throw new ArgumentException("Not Type Defined");
                }
            }
            string bytedata = "";
            foreach (var b in ResponseData)
                bytedata = bytedata + b.ToString() + " ";
            Console.WriteLine("recived " + ResponseData.Length.ToString());// + "bytes:   " + bytedata);


            //*****************
            //var result = string.Join(",", bytesAsInts.Select(x => x.ToString()).ToArray());

            //if (Program.watchData == true)
            //{
            //    Program.testWriter.WriteLine(TokenId + " data received at " + DateTime.Now.ToString());
            //}

        }
        Dictionary<string, object> datas = new Dictionary<string, object>();
        void ModbusTCP_ExtractHoldingRegister(byte[] ResponseData)
        {
            ReqSection currentSection;
            currentSection = CurrentSections.First();

            if ((2 * currentSection.quantity + 5) != ResponseData.Count())
            {
                Console.WriteLine("Error in resived data length of UniotId="+UnitId.ToString());
                Reset();
                return;
            }

            CurrentSections.Remove(currentSection);

            var quantity = currentSection.quantity;
            var response_int = new int[quantity];
            for (int i = 0; i < quantity; i++)
            {
                byte lowByte;
                byte highByte;
                highByte = ResponseData[3 + i * 2];
                lowByte = ResponseData[3 + i * 2 + 1];

                ResponseData[3 + i * 2] = lowByte;
                ResponseData[3 + i * 2 + 1] = highByte;

                response_int[i] = BitConverter.ToInt16(ResponseData, (3 + i * 2));


                addToDatas(response_int[i], currentSection.startingAddress + i);
            }

            if (CurrentSections.Count == 0)
            {
                Logg();
                datas = new Dictionary<string, object>();
                CurrentSections = new List<ReqSection>(DefinedSections);
            }

          
        }
        
        void ModbusRTUoverTCP_ExtractHoldingRegister(byte[] ResponseData)
        {
            ReqSection currentSection;
            
            currentSection = CurrentSections.First();
            
            if ((2 * currentSection.quantity + 10) != ResponseData.Count())
            {
                Console.WriteLine("Error in resived data length of UniotId=" + UnitId.ToString());
                Reset();
                return;
            }


            CurrentSections.Remove(currentSection);

            var quantity = currentSection.quantity;
            var response_int = new int[quantity];

            for (int i = 0; i < quantity; i++)
            {
                byte lowByte;
                byte highByte;
                highByte = ResponseData[9 + i * 2];
                lowByte = ResponseData[9 + i * 2 + 1];

                ResponseData[9 + i * 2] = lowByte;
                ResponseData[9 + i * 2 + 1] = highByte;

                response_int[i] = BitConverter.ToInt16(ResponseData, (9 + i * 2));

                addToDatas(response_int[i], currentSection.startingAddress + i);
            }


            if (CurrentSections.Count == 0)
            {
                Logg();
                datas = new Dictionary<string, object>();
                CurrentSections = new List<ReqSection>(DefinedSections);
            }


        }


        private void addToDatas(int val, int register)
        {
  
                string item = getRegisterName(register);
                if (item != "")//item.RecievedData(val, DateTime.Now))
                {
                    datas.Add(item, val);
                }
        }

        private string getRegisterName(int register)
        {
            string name = "";

            switch (Type)
            {
                case "Classic":
                    if (ClassicRegisters.ContainsKey(register))
                            name = ClassicRegisters[register];
                    break;
                case "AMF25":
                    if (AMF25Registers.ContainsKey(register))
                            name = AMF25Registers[register];
                    break;
                case "minit":
                    if (minitRegisters.ContainsKey(register))
                        name = minitRegisters[register];
                    break;
                default:
                    throw new ArgumentException("Not Type Defined in 'getRegisterName'");
            }

            //return Regs[register];
            return name;
        }

        //Let's use an ID for this object during testing, just so we can see what
        //is happening better if we want to.
        public Int32 TokenId
        {
            get
            {
                return this.idOfThisObject;
            }
        }

        private Int32? unitId = null;
        public Int32? UnitId
        {
            get
            {
                return unitId;
            }
            set
            {
                unitId = value;
            }
        }
        public uint transactionIdentifierInternal = 0;
        internal void CreateNewDataHolder()
        {
            theDataHolder = new DataHolder();
        }
                
        //Used to create sessionId variable in DataHoldingUserToken.
        //Called in ProcessAccept().
        internal void CreateSessionId()
        {
            sessionId = Interlocked.Increment(ref Program.mainSessionId);                        
        }

        public Int32 SessionId
        {
            get
            {
                return this.sessionId;
            }
        }

        public void Reset()
        {
            this.receivedPrefixBytesDoneCount = 0;
            this.receivedMessageBytesDoneCount = 0;
            this.recPrefixBytesDoneThisOp = 0;
            this.receiveMessageOffset = this.permanentReceiveMessageOffset;

            
            CurrentSections = new List<ReqSection>(DefinedSections);
            datas = new Dictionary<string, object>();
        }
    }
}
