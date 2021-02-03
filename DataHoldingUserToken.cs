using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Text; //for testing
using System.Net;
using System.Collections.Generic;
using System.Linq;
using InfluxDB.Collector;
using MongoDB.Driver;
using MongoDB.Bson;
using Newtonsoft.Json;
using SocketAsyncServer;

namespace SocketAsyncServer
{
    internal class regSpec
    {
        public regSpec(string _name,short _Len,short _Dec)
        {
            name=_name;
            Len=_Len;
            Dec=_Dec;
        }
        public string name;
        public short Len;
        public short Dec;
    }
    class DataHoldingUserToken
    {
        static readonly Dictionary<int, regSpec> amf25Registers = new Dictionary<int, regSpec>();
        static readonly Dictionary<int, regSpec> mintRegisters = new Dictionary<int, regSpec>();
        static readonly Dictionary<int, regSpec> tetaRegisters = new Dictionary<int, regSpec>();
        public static readonly List<UnitData> ValidUnits = new List<UnitData>();
        readonly ReqSection AlarmListReq = new ReqSection() {
            startingAddress = 6668,

            quantity=0   
        };
        readonly ReqSection AlarmlistCountReq=new ReqSection()
                    { 
                        startingAddress=6353,
                        quantity=1
                    }; 
        readonly ReqSection COM_STATReq=new ReqSection()
                    { 
                        startingAddress=24571,
                        quantity=2
                    }; 
        //request GenSet Name
        readonly ReqSection GensetNameReq = new ReqSection() {
            startingAddress = 3013,
            quantity=8
        };
        List<ReqSection> CurrentSections = new List<ReqSection>();
    internal List<ReqSection> DefinedSections {
            get
            {
                switch (_type)
                {
                    case "teta":
                        return new List<ReqSection>() {
                        new ReqSection(){
                            startingAddress = 1 ,
                            quantity = 34,
                        },
                    };
                    case "amf25":
                        return new List<ReqSection>() {
                        new ReqSection(){
                            startingAddress = 0 ,
                            quantity = 75,
                        },
                        new ReqSection(){
                            startingAddress =118 ,
                            quantity =1,
                        },
                            new ReqSection(){
                            startingAddress =182 ,
                            quantity =16,
                        },
                        new ReqSection(){
                           startingAddress =3000 ,
                           quantity = 100,
                        },
                        new ReqSection(){
                           startingAddress =3101 ,
                           quantity = 100,
                        },
                        new ReqSection(){
                           startingAddress =3207 ,
                           quantity = 35,
                        },
                        //new ReqSection(){
                        //    startingAddress =3000 ,
                        //    quantity = 200,
                        //}
                    };
                    case "mint":
                        return new List<ReqSection>() {
                        new ReqSection(){
                            startingAddress =1 ,
                            quantity = 81,
                        },
                       new ReqSection(){
                            startingAddress =3000 ,
                            quantity = 10,
                        },
                        new ReqSection(){
                            startingAddress =6353 ,
                            quantity =1,
                        },
                    };
                    default:
                        throw new ArgumentException("Not Type:'" + _type + "' Defined");


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
                var amf25Lines = File.ReadLines("Resource\\ModbusAddress_amf25.csv").Select(a => a.Split(','));
                foreach (var item in amf25Lines)
                {
                    amf25Registers.Add(int.Parse(item[0]),
                     new regSpec(
                        item[1].Trim(),
                        Convert.ToInt16(item[2].Trim()),
                        Convert.ToInt16(item[3].Trim())
                        )
                    );
                }
                var mintLines = File.ReadLines("Resource\\ModbusAddress_mint.csv").Select(a => a.Split(','));
                foreach (var item in mintLines)

                {
                    mintRegisters.Add(int.Parse(item[0]), new regSpec(item[1].Trim(),2,0));
                }



                var TetaLines = File.ReadLines("Resource\\ModbusAddress_teta.csv").Select(a => a.Split(','));
                foreach (var item in TetaLines)
                {
                    tetaRegisters.Add(int.Parse(item[0]),  new regSpec(item[1].Trim(),2,0));
                }

                //*************Read Valid Units From TextFile ***************
                //var ComApUnits = File.ReadLines("Resource\\ValidComApUnits.csv").Select(a => a.Split(','));
                //foreach (var item in ComApUnits)
                //{
                //    ValidUnits.Add(
                //        new UnitData() {
                //            Type = item[0].Trim().ToUpper(),
                //            ModBusId = int.Parse(item[1]),
                //            RemoteIp = IPAddress.Parse(item[2]),
                //            LocalPort = int.Parse(item[3])
                //        }
                //        );
                //}


                //*************Read Valid Units From MongoDb***************
                string connectionString = "mongodb://localhost:27017";
                MongoClientSettings settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
                var client = new MongoClient(settings);
                var db = client.GetDatabase("rest-tutorial");
                var collection = db.GetCollection<BsonDocument>("units");

                var filter = new BsonDocument();
                var units = collection.Find(filter).ToListAsync();
                units.Wait();
                foreach (var u in units.Result)
                {
                    var matched = new UnitData()
                    {
                        Id = u.GetValue("_id").ToString(),
                        Name = u.GetValue("name").ToString(),
                        OwnerId= u.GetValue("userId").ToString(),
                        Type = u.GetValue("deviceType").ToString().Trim().ToUpper(),
                        RemoteIp = IPAddress.Parse(u.GetValue("ip").ToString()),
                        LocalPort = u.GetValue("port").ToInt32(),
                        ModBusId = u.GetValue("port").ToInt32() - 4510
                    };

                    ValidUnits.Add(matched);
                           Console.WriteLine(
                    "Valid Unit Added:     type:" + matched.Type + " ,Id:" + matched.ModBusId.ToString() + " ,ip:" + matched.RemoteIp.ToString() + " ,port:" + matched.LocalPort.ToString());
                }
            }
            catch (Exception c)
            {
                Console.WriteLine("*******Error in reading resources:***********\n" + c.ToString());
            }
        }

    // Enum   
        public enum GenstatusEnum
            {
                stop = 0, running= 1, loaded= 2, noData= 4
            }
        public enum COM_STAT
        {
            GenSetNameChecking,
            authenticatedByName,
            authenticatedByIp,
            NotAuthenticated,
            req_name,
            wait_name,
            req_alarmCount,
            wait_alarmCount,
            req_alarmList,
            wait_alarmList,
            req_data,
            wait_data,
            req_COM_STAT,
            wait_COM_STAT,
        }
        COM_STAT _comState;
        public COM_STAT comState
        {
            get { return _comState; }
           internal set {_comState=value;}
        }

        struct GenStateStruct
        {
            public GenstatusEnum status;
            public bool redAlarm;
            public bool yellowAlarm;
        }
         GenStateStruct readStateAlarms()
        {
            GenstatusEnum _status=GenstatusEnum.noData;

            bool _loaded = false;
            bool _running = false;
            bool _redAlarm=false;
            bool _yellowAlarm=false;

            switch (_type)
            {
                case "teta":
                    var  statusReg = datas
                    .Where(kv => kv.Key == "state")
                    .Select(kv => (short)kv.Value)   // not a problem even if no item matches
                    .DefaultIfEmpty((short)0) // or no argument -> null
                    .First(); 

                    var alarmReg = datas
                    .Where(kv => kv.Key == "alarm")
                    .Select(kv => (short)kv.Value)   // not a problem even if no item matches
                    .DefaultIfEmpty((short)0) // or no argument -> null
                    .First(); 

                    _loaded =       ((byte)statusReg & (1 << 0)) != 0;
                    _running =      ((byte)statusReg & (1 << 1)) != 0;
                    _redAlarm =     ((byte)alarmReg  & (1 << 0)) != 0;
                    _yellowAlarm =  ((byte)alarmReg  & (1 << 1)) != 0;
                    break;
                case "amf25":
                    int amf25_EnginState = datas
                    .Where(kv => kv.Key == "EnginState")
                    .Select(kv => (short)kv.Value)   // not a problem even if no item matches
                    .DefaultIfEmpty((short)0) // or no argument -> null
                    .First();  
                    _loaded =    (amf25_EnginState == 27);
                    _running = (amf25_EnginState == 26);
                    //_redAlarm =     ((byte)statusReg & (1 << 5)) != 0;
                    //_yellowAlarm =  ((byte)statusReg & (1 << 6)) != 0;
                    break;
                case "mint":
                    int mint_EnginState = datas
                    .Where(kv => kv.Key == "EnginState")
                    .Select(kv => (short)kv.Value)   // not a problem even if no item matches
                    .DefaultIfEmpty((short)0) // or no argument -> null
                    .First();  

                    _loaded = (mint_EnginState == 29);
                    _running = (mint_EnginState == 30);
                    break;
                default:
                    throw new ArgumentException("Not Type:'" + _type + "' Defined in readStateAlarms()");
            }

            _status = _loaded ? GenstatusEnum.loaded : (_running ? GenstatusEnum.running : GenstatusEnum.stop);
            return  new GenStateStruct()
            {
                redAlarm = _redAlarm,
                yellowAlarm = _yellowAlarm,
                status =_status
            };
        }
          public  void LoggData()
        {
            //*******State and Alarnms*******
            GenStateStruct Status = readStateAlarms();

            datas.Add("status", Status.status.ToString());
            datas.Add("redAlarm", Status.redAlarm);
            datas.Add("yellowAlarm", Status.yellowAlarm);
            //*******************************


            //******Combine two short datas *************
            short Run_Hours_1 = datas
            .Where(kv => kv.Key == "Run_Hours_1")
            .Select(kv => (short)kv.Value)   // not a problem even if no item matches
            .DefaultIfEmpty((short)0) // or no argument -> null
            .First(); 
            datas.Remove("Run_Hours_1");

            short Run_Hours_2 = datas
            .Where(kv => kv.Key == "Run_Hours_2")
            .Select(kv => (short)kv.Value)   // not a problem even if no item matches
            .DefaultIfEmpty((short)0) // or no argument -> null
            .First();   
            datas.Remove("Run_Hours_2");

            byte[] Run_HoursBytes=new byte[4];
            Run_HoursBytes[0]=(byte) (Run_Hours_2 >> 0);//byte1
            Run_HoursBytes[1]= (byte) (Run_Hours_2 >> 8);//byte2

            Run_HoursBytes[2]=(byte) (Run_Hours_1 >> 0);//byte1
            Run_HoursBytes[3]= (byte) (Run_Hours_1 >> 8);//byte2


            UInt32 Run_Hours=BitConverter.ToUInt32(Run_HoursBytes);

            datas.Add("Run_Hours",Run_Hours/10); //Dec=1
            //***********************************

            //******Combine two short datas *************
            short Genset_kWh_1 = datas
            .Where(kv => kv.Key == "Genset_kWh_1")
            .Select(kv => (short)kv.Value)   // not a problem even if no item matches
            .DefaultIfEmpty((short)0) // or no argument -> null
            .First(); 
            datas.Remove("Genset_kWh_1");

            short Genset_kWh_2 = datas
            .Where(kv => kv.Key == "Genset_kWh_2")
            .Select(kv => (short)kv.Value)   // not a problem even if no item matches
            .DefaultIfEmpty((short)0) // or no argument -> null
            .First();   
            datas.Remove("Genset_kWh_2");


            byte[] Genset_kWhBytes=new byte[4];
            Genset_kWhBytes[0]=(byte) (Genset_kWh_2 >> 0);//byte1
            Genset_kWhBytes[1]= (byte) (Genset_kWh_2 >> 8);//byte2

            Genset_kWhBytes[2]=(byte) (Genset_kWh_1 >> 0);//byte1
            Genset_kWhBytes[3]= (byte) (Genset_kWh_1 >> 8);//byte2


            UInt32 Genset_kWh=BitConverter.ToUInt32(Genset_kWhBytes);

            datas.Add("Genset_kWh",Genset_kWh);
            //***********************************

            var starttime = DateTime.Now;
            //*******InfluxDb ********
            var tags = new Dictionary<string, string>() {
                   { "Company", "TetaPower" },
                { "UnitId",ModbusId.ToString() },
                { "OwnerId",OwnerId.ToString() },
                { "Id",unitId },
            };
           Metrics.Write("ModbusLogger", datas, tags);

            lastUpdateTime = DateTime.Now;
            Thread.Sleep(4000);
            Console.WriteLine(name.Substring(0, Math.Min(12,name.Length)).PadRight(12, ' ')
             + "data Logged at " + DateTime.Now.ToString()+ "  in "+ DateTime.Now.Subtract(starttime).Milliseconds.ToString() + " Milliseconds");

        }



        public  void LoggAlarms(String alarms)
        {      
            Dictionary<string, object> alarmsDic = new Dictionary<string, object>();
            alarmsDic.Add("AlarmList",alarms);
            var starttime = DateTime.Now;
            //*******InfluxDb ********
            var tags = new Dictionary<string, string>() {
                { "Company", "TetaPower" },
                { "UnitId",ModbusId.ToString() },
                { "OwnerId",OwnerId.ToString() },
                { "Id",unitId },
            };
           Metrics.Write("ModbusAlarms", alarmsDic, tags);

            lastUpdateTime = DateTime.Now;
            Console.WriteLine(TokenId + " AlarmList Logged at " + DateTime.Now.ToString()+ "  in "+ DateTime.Now.Subtract(starttime).Milliseconds.ToString() + " Milliseconds");

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

       public class UnitData{
            public string Id;
            public string Name;
            public string OwnerId;
            public int ModBusId;
            public string Type;
            public IPAddress RemoteIp;
            public int LocalPort;
        }
        internal class ReqSection
        {
          internal  int startingAddress {get;set;}
          internal int quantity{get;set;}

          internal int EndAddress {
                get {
                    return startingAddress+quantity;
                }
            }
         }



        private string _type="";
        public string type { get { return _type; } }
        private Int32 _modbusId = 0;
        public Int32 ModbusId
        {
            get
            {
                return _modbusId;
            }
        }
        private string _ownerId ="";
        public string OwnerId
        {
            get
            {
                return _ownerId;
            }
        }

        private string _unitId="";
        public string unitId
        {
            get
            {
                return _unitId;
            }
          
        }
        private string _name = "";
        public string name { get { return _name; } }

        public bool AuthenticationByIp(SocketAsyncEventArgs e)
        {
            _type = "teta";            
            this.theMediator = new Mediator(e);
            Console.WriteLine("Finding Match...");
            var matched = ValidUnits.FirstOrDefault(u => u.RemoteIp.Equals( theMediator.GetRemoteIp()) & u.LocalPort.Equals(theMediator.GetLocalPort()));

            Console.WriteLine("Match Checking...");
            if (matched != null)
            {
                _type = matched.Type.ToLower();
                _modbusId = matched.ModBusId;
                _unitId = matched.Id;
                _name = matched.Name;
                _ownerId = matched.OwnerId;
                Console.WriteLine("Match Fined");
               Reset();
                Console.WriteLine(
                    "authenticated By Ip:     type:" + matched.Type+ " ,Id:" + matched.ModBusId.ToString() + " ,ip:" + matched.RemoteIp.ToString() + " ,port:" + matched.LocalPort.ToString());
                _comState = COM_STAT.authenticatedByIp;
                return true;
            }

            _modbusId =theMediator.GetLocalPort() - 4510;
            _comState = COM_STAT.GenSetNameChecking;
            Console.WriteLine(
                 "GenSetName Checking with " + " ip:" + theMediator.GetRemoteIp().ToString() + " ,port:" + theMediator.GetLocalPort().ToString());
                 comState=COM_STAT.req_name;
            return false;

        }


        public bool AuthenticationByName(string genSetName)
        {
            _type = "teta";
            Console.WriteLine("Finding Match By Name...");
            var matched = ValidUnits.FirstOrDefault(u => u.Id.Substring(0,16)==genSetName.ToLower());

            Console.WriteLine("Match Checking...");
            if (matched != null)
            {
                _type = matched.Type.ToLower();
                _modbusId = matched.ModBusId;
                _unitId = matched.Id;
                _name = matched.Name;
                _ownerId = matched.OwnerId;
                Console.WriteLine("Match Fined");
                Reset();
                Console.WriteLine(
                    "authenticated By Name:     type:" + matched.Type + " ,Id:" + matched.ModBusId.ToString() + " ,ip:" + matched.RemoteIp.ToString() + " ,port:" + matched.LocalPort.ToString());
                _comState = COM_STAT.authenticatedByName;
                return true;
            }

            _modbusId = theMediator.GetLocalPort() - 4510;
            _comState = COM_STAT.NotAuthenticated;
            Console.WriteLine(
                 "'"+genSetName+"' Not Approved with " + " ip:" + theMediator.GetRemoteIp().ToString() + " ,port:" + theMediator.GetLocalPort().ToString());
            return false;

        }

        public string GetInfo(SocketAsyncEventArgs e)
        {
            theMediator = new Mediator(e);

            //var matched = ValidUnits.FirstOrDefault(u => u.RemoteIp.Equals(theMediator.GetRemoteIp()) & u.LocalPort.Equals(theMediator.GetLocalPort()));

            //if (matched != null)
            //{
                StringBuilder sb = new StringBuilder();
            sb.Append(" │");
            sb.Append(unitId.PadRight(26, ' '));
            sb.Append(name.Substring(0, Math.Min(12,name.Length)).PadRight(12, ' '));
            sb.Append(type.PadRight(8, ' '));
            sb.Append(theMediator.GetRemoteIp().ToString().PadRight(17, ' '));
            sb.Append(theMediator.GetLocalPort().ToString().PadRight(3, ' '));
            sb.Append("│");

                return sb.ToString();

        }

       

        public byte[] prepareRequest()
        {
            transactionIdentifierInternal++;
            byte[] request;
            switch (comState)
            {
                case COM_STAT.req_name:
                    request= modbusRTUoverTCP_Request(GensetNameReq);
                    comState=COM_STAT.wait_name;
                    break;
                case COM_STAT.req_alarmCount:
                    
                    request= modbusRTUoverTCP_Request(AlarmlistCountReq);
                    comState=COM_STAT.wait_alarmCount;
                    break;
                case COM_STAT.req_alarmList:
                    request= modbusRTUoverTCP_Request(AlarmListReq);
                    comState=COM_STAT.wait_alarmList;
                    break;
                case COM_STAT.req_COM_STAT:
                    request= modbusRTUoverTCP_Request(COM_STATReq);
                    comState=COM_STAT.wait_COM_STAT;
                    break;
                case COM_STAT.req_data:
                    request= RequestData();
                    comState=COM_STAT.wait_data;
                    break;
                default:
                request=new byte[0];
                    Console.WriteLine("unExpected State:'"+comState+ "' reached");
                break;           
            }
            return request;
            
        }
        public Byte[] RequestData()
        {
            ReqSection currentSection;
            currentSection = CurrentSections.First();
            switch (_type)
            {
                case "teta":
                    return modbusTCP_Request(currentSection);
                case "amf25":
                    return modbusRTUoverTCP_Request(currentSection);
                case "mint":
                    return modbusRTUoverTCP_Request(currentSection);
                default:
                    throw new ArgumentException("Not Type:'"+_type+ "' Defined");
            }
        }

        Byte[] modbusRTUoverTCP_Request(ReqSection currentSection)
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
                                     (byte)_modbusId,
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
        Byte[] modbusTCP_Request(ReqSection currentSection)
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
                            (byte)_modbusId,
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


 StringBuilder sb = new StringBuilder();
             sb.Append(name.Substring(0, Math.Min(12,name.Length)).PadRight(12, ' '));
            sb.Append("recieved " );
            sb.Append(ResponseData.Length.ToString().PadRight(6, ' '));
            sb.Append("   in ");
            sb.Append(comState.ToString().PadRight(15, ' '));

            
            Console.WriteLine(sb);
         
            switch (comState)
            {
                case COM_STAT.wait_name:
                    var GenSetName = ModbusRTUoverTCP_ExtractGenSetNameFromHoldingRegister(ResponseData);
                    if(AuthenticationByName(GenSetName))
                        comState=COM_STAT.req_alarmCount;
                    else
                        comState=COM_STAT.NotAuthenticated;
                break;
                case COM_STAT.wait_COM_STAT:
                    var commStat= ModbusRTUoverTCP_ExtractComState(ResponseData);
                    if(commStat.alarmListChanged && commStat.alarmListIsNotEmpty)
                        comState= COM_STAT.req_alarmCount;
                    else if(commStat.alarmListChanged && !commStat.alarmListIsNotEmpty)
                        {
                            LoggAlarms("");
                            comState=COM_STAT.req_data;
                        }
                    else
                        comState=COM_STAT.req_data;
                break;
                case COM_STAT.wait_alarmCount:
                    ModbusRTUoverTCP_ExtractIntArray(ResponseData,AlarmlistCountReq);  // set AlarmlistCountReq

                    AlarmListReq.quantity =25* datas
                    .Where(kv => kv.Key == "AlarmlistCount")
                    .Select(kv => (short)kv.Value)   // not a problem even if no item matches
                    .DefaultIfEmpty((short)0) // or no argument -> null
                    .First(); 
                    if(AlarmListReq.quantity==0)
                    comState=COM_STAT.req_data;
                    else
                    comState=COM_STAT.req_alarmList;
                break;
                case COM_STAT.wait_alarmList:
                    var alarmlist=ModbusRTUoverTCP_ExtractAlarmList(ResponseData);
                    LoggAlarms(JsonConvert.SerializeObject(alarmlist));

                    comState=COM_STAT.req_data;
                break;  

                case COM_STAT.wait_data:
                    ExtractData(ResponseData);
                    
                    ReqSection currentSection;
                    currentSection = CurrentSections.First();
                    CurrentSections.Remove(currentSection);

                    comState=COM_STAT.req_data;
                    if (CurrentSections.Count == 0)
                    {
                        LoggData();
                        datas = new Dictionary<string, object>();
                        CurrentSections = new List<ReqSection>(DefinedSections);

                        comState=COM_STAT.req_COM_STAT;
                    }
                break;                 
                default:
                    Console.WriteLine("unExpected State:'"+comState+ "' reached");
                break;

            }

        }
        void ExtractData(byte[] ResponseData)
        {
             switch (_type)
                {
                    case "teta":
                        ModbusTCP_ExtractIntArray(ResponseData);
                        break;
                    case "amf25":
                        ModbusRTUoverTCP_ExtractIntArray(ResponseData);
                        break;
                    case "mint":
                        ModbusRTUoverTCP_ExtractIntArray(ResponseData);
                        break;
                    default:
                        throw new ArgumentException("Not Type:'" + _type + "' Defined in ProcessResponseData()");

                }
        }
        string ModbusRTUoverTCP_ExtractGenSetNameFromHoldingRegister(byte[] ResponseData)
        {
            ReqSection currentSection = GensetNameReq;

            if ((2 * currentSection.quantity + 5) != ResponseData.Count())
            {
                Console.WriteLine("Error reading name" + ModbusId.ToString());
                Reset();
                return "";
            }
           return  System.Text.Encoding.UTF8.GetString(ResponseData, 3, 16);
        }

        string[] ModbusRTUoverTCP_ExtractAlarmList(byte[] ResponseData)
        {
            ReqSection currentSection = AlarmListReq;
            string[] alarmlist=new string[currentSection.quantity/25];

            if ((2 * currentSection.quantity + 5) != ResponseData.Count())
            {
                Console.WriteLine("Error in resived AlarmList length of '" + name.Substring(0, Math.Min(12,name.Length))+"'");
                Reset();
                return new string[0];
            }
            var alarmSection=ResponseData.Skip(3).Take(currentSection.quantity).ToArray();
            for(int i=0;i<currentSection.quantity/25;i++)
            alarmlist[i]=System.Text.Encoding.Default.GetString(alarmSection,i*25,25);           

           return alarmlist;
        }
        Dictionary<string, object> datas = new Dictionary<string, object>();
        void ModbusRTUoverTCP_ExtractIntArray(byte[] ResponseData,ReqSection currentSection)
        {
            if ((2 * currentSection.quantity + 5) != ResponseData.Count())
            {
                Console.WriteLine("Error in ExtractIntArray of '" + name.Substring(0, Math.Min(12,name.Length))+"'");
                Reset();
                return;
            }

            var quantity = currentSection.quantity;
            var response_int = new short[quantity];
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
        }
        struct commStat{
           public bool alarmListIsNotEmpty;
           public bool alarmListChanged;
        }
         commStat ModbusRTUoverTCP_ExtractComState(byte[] ResponseData)
        {
            if ((2* COM_STATReq.quantity + 5) != ResponseData.Count())
            {
                Console.WriteLine("Error in ExtractComState of '" + name.Substring(0, Math.Min(12,name.Length))+"'");
                Reset();
                return new commStat();
            }
            
            commStat commStat=new commStat(){
            alarmListChanged =      (ResponseData[5] & (1 << 4)) != 0,
            alarmListIsNotEmpty =      (ResponseData[5] & (1 << 3)) != 0
            };



            StringBuilder sb = new StringBuilder();
            sb.Append(name.Substring(0, Math.Min(12,name.Length)).PadRight(12, ' '));
            var msg=commStat.alarmListChanged?"Alarmlist changed and ":"Alarmlist not changed" ;
            if(commStat.alarmListChanged)
            msg=msg+ (commStat.alarmListIsNotEmpty?"is not  empty ":"is empty" );
            sb.Append(msg);

            
            Console.WriteLine(sb);
            return commStat;
        }
        
        void ModbusRTUoverTCP_ExtractIntArray(byte[] ResponseData)
        {
            ReqSection currentSection;
            currentSection = CurrentSections.First();

            if ((2 * currentSection.quantity + 5) != ResponseData.Count())
            {
                Console.WriteLine("Error in ExtractIntArray of '" + name.Substring(0, Math.Min(12,name.Length))+"'");
                Reset();
                return;
            }

            var quantity = currentSection.quantity;
            var response_int = new short[quantity];
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
        }
        
        void ModbusTCP_ExtractIntArray(byte[] ResponseData)
        {
            ReqSection currentSection;
            
            currentSection = CurrentSections.First();
            
            if ((2 * currentSection.quantity + 10) != ResponseData.Count())
            {
                Console.WriteLine("Error in ExtractIntArray of '" + name.Substring(0, Math.Min(12,name.Length))+"'");
                Reset();
                return;
            }



            var quantity = currentSection.quantity;
            var response_int = new short[quantity];

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
        }


        private void addToDatas(short val, int register)
        {

            string item = getRegisterName(register);
            if (item != "" && val!=Int16.MinValue) //item.RecievedData(val, DateTime.Now))
            {
                datas.Add(item, val);
            }
        }

        private string getRegisterName(int register)
        {
            string name = "";

            switch (_type)
            {
                case "teta":
                    if (tetaRegisters.ContainsKey(register))
                            name = tetaRegisters[register].name;
                    break;
                case "amf25":
                    if (amf25Registers.ContainsKey(register))
                            name = amf25Registers[register].name;
                    break;
                case "mint":
                    if (mintRegisters.ContainsKey(register))
                        name = mintRegisters[register].name;
                    break;
                default:
                    throw new ArgumentException("Not Type:'" + _type + "' Defined in getRegisterName()");
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
